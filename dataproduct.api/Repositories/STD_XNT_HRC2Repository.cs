using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.ResponseModels;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class STD_XNT_HRC2Repository : ISTD_NXT_HRC2Repository
    {
        private readonly ProductFormContext _context;

        public STD_XNT_HRC2Repository(ProductFormContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Chia totalAmount cho các phần tử trong ids: (N-1) phần tử đầu nhận giá trị làm tròn
        /// Math.Round(totalAmount / ids.Count), phần tử CUỐI nhận phần dư (totalAmount - tổng đã gán).
        /// Đảm bảo tổng các phần luôn khớp CHÍNH XÁC totalAmount, tránh lệch tích lũy khi làm tròn
        /// riêng lẻ từng mẻ (VD: 10 chia 3 mẻ → 3.3333 làm tròn 3 cho cả 3 mẻ → tổng chỉ còn 9).
        /// Trả về giá trị làm tròn mỗi phần (perUnit) để lưu vào cột thống kê KLPB_*.
        /// </summary>
        private static decimal PhanBoKhongLechLamTron<TKey>(
            IDictionary<TKey, decimal> target, IReadOnlyList<TKey> ids, decimal totalAmount)
        {
            if (ids.Count == 0) return 0;

            var perUnit = Math.Round(totalAmount / ids.Count);
            decimal sumAssigned = 0;
            for (var i = 0; i < ids.Count - 1; i++)
            {
                target[ids[i]] = perUnit;
                sumAssigned += perUnit;
            }
            target[ids[ids.Count - 1]] = totalAmount - sumAssigned;
            return perUnit;
        }

        /// <summary>
        /// Tính KLTK_BOF/KLTK_LF/KLTK_RH cho 1 Id_HeaderKey trong đúng Ngày/Ca — mirror
        /// STD_XNT_HRC1Repository.ComputeKLTKAsync: gộp TẤT CẢ mẻ đang dùng HeaderKey này (kể cả dòng
        /// phân bổ IsPhanBo=true, cùng cách nhận diện qua Header_Mappings/ID_HeaderKey trực tiếp như
        /// BƯỚC 3 của PhanBoAsync), nhóm theo BieuMau (BOF/LF/RH) của mẻ. Không áp dụng quy đổi đơn vị
        /// đặc biệt cho ID_HeaderKey==5 (khác GetThongKeSumAsync) vì PhanBoAsync cũng không áp dụng.
        /// </summary>
        private async Task<(decimal? Bof, decimal? Lf, decimal? Rh)> ComputeKLTKAsync(DateTime ngaySX, int ca, int idHeaderKey)
        {
            var dlnmRows = await _context.DLNM_HRC2s
                .Where(x => x.Ngay == ngaySX && x.Ca == ca && x.IsDelete != true)
                .Select(x => new { x.ID, x.BieuMau })
                .ToListAsync();

            if (dlnmRows.Count == 0) return (null, null, null);

            var bieuMauByMeId = dlnmRows.ToDictionary(x => x.ID, x => x.BieuMau);
            var meIds = dlnmRows.Select(x => x.ID).ToList();

            var mappedPhuLieuIds = await _context.Header_Mappings
                .Where(hm => hm.ID_HeaderKey == idHeaderKey)
                .Select(hm => hm.ID_PhuLieu)
                .ToListAsync();

            var plRows = await _context.PhuLieu_HRC2s
                .Where(pl => meIds.Contains(pl.ID_MeThoi) &&
                    ((pl.ID_PhuLieu.HasValue && mappedPhuLieuIds.Contains(pl.ID_PhuLieu.Value)) ||
                     pl.ID_HeaderKey == idHeaderKey))
                .ToListAsync();

            decimal? bofTotal = null, lfTotal = null, rhTotal = null;
            foreach (var pl in plRows)
            {
                var effective = pl.IsManual == true ? pl.KLPhuGia_Manual : pl.KLPhuGia;
                if (!effective.HasValue) continue;
                if (!bieuMauByMeId.TryGetValue(pl.ID_MeThoi, out var bieuMau) || bieuMau == null) continue;

                var value = (decimal)effective.Value;
                if (bieuMau.StartsWith("BOF", StringComparison.OrdinalIgnoreCase))
                    bofTotal = (bofTotal ?? 0) + value;
                else if (bieuMau.StartsWith("LF", StringComparison.OrdinalIgnoreCase))
                    lfTotal = (lfTotal ?? 0) + value;
                else if (bieuMau.StartsWith("RH", StringComparison.OrdinalIgnoreCase))
                    rhTotal = (rhTotal ?? 0) + value;
            }

            return (bofTotal, lfTotal, rhTotal);
        }

        /// <summary>
        /// Tính lại "Tổng thực tế" (tongThucTe) HIỆN TẠI của 1 Id_HeaderKey trong đúng Ngày/Ca, thẳng từ
        /// nguồn PhuLieu_HRC2 (chỉ dòng đo thật IsPhanBo != true) — dùng cùng kỹ thuật nhận diện mẻ
        /// đang dùng HeaderKey này như BƯỚC 3 của PhanBoAsync (Header_Mappings/ID_HeaderKey trực tiếp),
        /// đảm bảo nhất quán với chính quyết định phân bổ. Dùng để phát hiện mẻ nấu luyện đã đổi (VD:
        /// phiếu Nấu Luyện vừa được "Đề nghị hiệu chỉnh") sau lần "Làm mới" + "Lưu" gần nhất trên Sổ
        /// Xuất-Nhập-Tồn, tránh phân bổ dựa trên ChênhLệch đã lỗi thời.
        /// </summary>
        private async Task<decimal> ComputeTongThucTeHienTaiAsync(DateTime ngaySX, int ca, int idHeaderKey)
        {
            var dlnmIds = await _context.DLNM_HRC2s
                .Where(x => x.Ngay == ngaySX && x.Ca == ca && x.IsDelete != true)
                .Select(x => x.ID)
                .ToListAsync();

            if (dlnmIds.Count == 0) return 0;

            var mappedPhuLieuIds = await _context.Header_Mappings
                .Where(hm => hm.ID_HeaderKey == idHeaderKey)
                .Select(hm => hm.ID_PhuLieu)
                .ToListAsync();

            var plRows = await _context.PhuLieu_HRC2s
                .Where(pl =>
                    dlnmIds.Contains(pl.ID_MeThoi) &&
                    (pl.IsPhanBo != true) &&
                    (
                        (pl.ID_PhuLieu.HasValue && mappedPhuLieuIds.Contains(pl.ID_PhuLieu.Value)) ||
                        pl.ID_HeaderKey == idHeaderKey
                    ))
                .Select(pl => new { pl.IsManual, pl.KLPhuGia, pl.KLPhuGia_Manual })
                .ToListAsync();

            return (decimal)plRows.Sum(x => x.IsManual == true ? (x.KLPhuGia_Manual ?? 0) : (x.KLPhuGia ?? 0));
        }

        public async Task<STD_NXT_HRC2_UpsertResponse> UpsertAsync(STD_NXT_HRC2_UpsertDto entity)
        {
           try
           {
               var existingRecord = await _context.BmPhieus
                   .FirstOrDefaultAsync(e => e.Idphieu == entity.IdPhieu);
               if (existingRecord == null)
                   throw new Exception("Phiếu không tồn tại");

               // ========== XỬ LÝ STD_XUAT_NHAP_TON_HRC2 (Details) ==========
               // Key: (Scope, Id_HeaderKey, Id_Phieu)
               var existingDetails = await _context.STD_XUAT_NHAP_TON_HRC2s
                   .Where(x => x.Id_Phieu == entity.IdPhieu)
                   .ToListAsync();

               if (entity.Details != null && entity.Details.Any())
               {
                   foreach (var detailDto in entity.Details)
                   {
                       // Tìm record hiện có theo key (Scope, Id_HeaderKey, Id_Phieu)
                       var existingDetail = existingDetails.FirstOrDefault(x =>
                           x.Scope == detailDto.Scope &&
                           x.Id_HeaderKey == detailDto.Id_HeaderKey &&
                           x.Id_Phieu == entity.IdPhieu);

                       if (existingDetail != null)
                       {
                           // Update record hiện có
                           existingDetail.NgaySX = entity.NgaySX;
                           existingDetail.Ca = entity.Ca;
                           existingDetail.BieuMau = entity.BieuMau;
                           existingDetail.ViTri = detailDto.ViTri;
                           existingDetail.TenNguyenLieu = detailDto.TenNguyenLieu;
                           existingDetail.TonDauCa = detailDto.TonDauCa;
                           existingDetail.TuongQuanDauCa = detailDto.TuongQuanDauCa;
                           existingDetail.NhapVaoTrongCa = detailDto.NhapVaoTrongCa;
                           existingDetail.MucLieu = detailDto.MucLieu;
                           existingDetail.TheTich = detailDto.TheTich;
                           existingDetail.TyTrong = detailDto.TyTrong;
                           existingDetail.TonCuoiCa = detailDto.TonCuoiCa;
                           existingDetail.TuongQuanCuoiCa = detailDto.TuongQuanCuoiCa;
                           existingDetail.TongThucTe = detailDto.TongThucTe;
                           existingDetail.IDSilo = detailDto.IDSilo;
                           existingDetail.LuongSuDungKiemKe = detailDto.LuongSuDungKiemKe;

                           _context.STD_XUAT_NHAP_TON_HRC2s.Update(existingDetail);
                       }
                       else
                       {
                           // Insert record mới
                           var newDetail = new STD_XUAT_NHAP_TON_HRC2
                           {
                               Id_Phieu = entity.IdPhieu,
                               NgaySX = entity.NgaySX,
                               Ca = entity.Ca,
                               ViTri = detailDto.ViTri,
                               Scope = detailDto.Scope,
                               BieuMau = entity.BieuMau,
                               Id_HeaderKey = detailDto.Id_HeaderKey,
                               TenNguyenLieu = detailDto.TenNguyenLieu,
                               TonDauCa = detailDto.TonDauCa,
                               TuongQuanDauCa = detailDto.TuongQuanDauCa,
                               NhapVaoTrongCa = detailDto.NhapVaoTrongCa,
                               MucLieu = detailDto.MucLieu,
                               TheTich = detailDto.TheTich,
                               TyTrong = detailDto.TyTrong,
                               TonCuoiCa = detailDto.TonCuoiCa,
                               TuongQuanCuoiCa = detailDto.TuongQuanCuoiCa,
                               TongThucTe = detailDto.TongThucTe,
                               IDSilo = detailDto.IDSilo,
                               LuongSuDungKiemKe = detailDto.LuongSuDungKiemKe,
                           };
                           
                           await _context.STD_XUAT_NHAP_TON_HRC2s.AddAsync(newDetail);
                       }
                   }
               }

               // Xóa các record dư thừa trong Details (có trong DB nhưng không có trong DTO)
               var detailKeysInDto = entity.Details?
                   .Select(d => (Scope: d.Scope, Id_HeaderKey: d.Id_HeaderKey))
                   .ToHashSet() ?? new HashSet<(int Scope, int Id_HeaderKey)>();

               var detailsToDelete = existingDetails.Where(existing =>
                   !detailKeysInDto.Contains((existing.Scope, existing.Id_HeaderKey)))
                   .ToList();

               if (detailsToDelete.Any())
               {
                   _context.STD_XUAT_NHAP_TON_HRC2s.RemoveRange(detailsToDelete);
               }

               // ========== XỬ LÝ STD_NXT_TOTAL_HRC2 (Summary) ==========
               // Key: (Id_HeaderKey, Id_Phieu)
               var existingSummary = await _context.STD_NXT_TOTAL_HRC2s
                   .Where(x => x.Id_Phieu == entity.IdPhieu)
                   .ToListAsync();

               if (entity.Summary != null && entity.Summary.Any())
               {
                   foreach (var summaryDto in entity.Summary)
                   {
                       // Tìm record hiện có theo key (Id_HeaderKey, Id_Phieu)
                       var existingSum = existingSummary.FirstOrDefault(x =>
                           x.Id_HeaderKey == summaryDto.Id_HeaderKey &&
                           x.Id_Phieu == entity.IdPhieu);

                       if (existingSum != null)
                       {
                           // Update record hiện có
                           existingSum.NgaySX = entity.NgaySX;
                           existingSum.Ca = entity.Ca;
                           existingSum.TenNguyenLieu = summaryDto.TenNguyenLieu;
                           existingSum.TongTonDauCa = summaryDto.TongTonDauCa;
                           existingSum.TongTonNhapTrongCa = summaryDto.TongNhapTrongCa;
                           existingSum.TongTonCuoiCa = summaryDto.TongTonCuoiCa;
                           existingSum.TongSuDung = summaryDto.TongSuDung;
                           existingSum.TongSDTrenSoSach = summaryDto.TongSDTrenSoSach;
                           existingSum.ChenhLech = summaryDto.ChenhLech;
                           if (summaryDto.TyLeBOF != null) existingSum.TyLeBOF = summaryDto.TyLeBOF;
                           if (summaryDto.TyLeTinhLuyen != null) existingSum.TyLeTinhLuyen = summaryDto.TyLeTinhLuyen;
                           if (summaryDto.TyLeRH != null) existingSum.TyLeRH = summaryDto.TyLeRH;
                           _context.STD_NXT_TOTAL_HRC2s.Update(existingSum);
                       }
                       else
                       {
                           // Insert record mới
                           var newSummary = new STD_NXT_TOTAL_HRC2
                           {
                               Id_Phieu = entity.IdPhieu,
                               NgaySX = entity.NgaySX,
                               Ca = entity.Ca,
                               Id_HeaderKey = summaryDto.Id_HeaderKey,
                               TenNguyenLieu = summaryDto.TenNguyenLieu,
                               TongTonDauCa = summaryDto.TongTonDauCa,
                               TongTonNhapTrongCa = summaryDto.TongNhapTrongCa,
                               TongTonCuoiCa = summaryDto.TongTonCuoiCa,
                               TongSuDung = summaryDto.TongSuDung,
                               TongSDTrenSoSach = summaryDto.TongSDTrenSoSach,
                               ChenhLech = summaryDto.ChenhLech,
                               TyLeBOF = summaryDto.TyLeBOF,
                               TyLeTinhLuyen = summaryDto.TyLeTinhLuyen,
                               TyLeRH = summaryDto.TyLeRH
                           };
                           
                           await _context.STD_NXT_TOTAL_HRC2s.AddAsync(newSummary);
                       }
                   }
               }

               // Xóa các record dư thừa trong Summary (có trong DB nhưng không có trong DTO)
               var summaryKeysInDto = entity.Summary?
                   .Select(s => s.Id_HeaderKey)
                   .ToList() ?? new List<int>();

               var summaryToDelete = existingSummary.Where(existing =>
                   !summaryKeysInDto.Contains(existing.Id_HeaderKey))
                   .ToList();

               if (summaryToDelete.Any())
               {
                   _context.STD_NXT_TOTAL_HRC2s.RemoveRange(summaryToDelete);
               }

               // ========== XỬ LÝ BmKiemKePhuLieu (Kiểm kê - Snapshot) ==========
               if (entity.KiemKe != null && entity.KiemKe.Any())
               {
                   var siloRepo = new SiloRepository(_context);
                   var siloService = new SiloService(siloRepo);

                   foreach (var kiemKeDto in entity.KiemKe)
                   {
                       // ValidateBeforeSaveAsync - kiểm tra silo có chứa phụ liệu NM và mapping còn hiệu lực
                       await siloService.ValidateBeforeSaveAsync(
                           kiemKeDto.SiloId, 
                           kiemKeDto.PhuLieuNMId, 
                           kiemKeDto.NgaySX
                       );

                       // Check trùng (Key: NgaySX, Ca, HeaderKeyId, SiloId, PhuLieuNMId, Scope)
                       var exists = await _context.BmKiemKePhuLieus
                           .AnyAsync(k => 
                               k.NgaySX.Date == kiemKeDto.NgaySX.Date &&
                               k.Ca == kiemKeDto.Ca &&
                               k.ID_HeaderKey == kiemKeDto.HeaderKeyId &&
                               k.ID_Silo == kiemKeDto.SiloId &&
                               k.ID_PhuLieuNM == kiemKeDto.PhuLieuNMId &&
                               k.Scope == kiemKeDto.Scope
                           );

                       if (exists)
                       {
                           throw new InvalidOperationException(
                               $"Đã tồn tại bản ghi kiểm kê với cùng Ngày SX, Ca, HeaderKey, Silo, Phụ liệu NM và Scope."
                           );
                       }

                       // Tạo entity BmKiemKePhuLieu
                       var kiemKe = new BmKiemKePhuLieu
                       {
                           NgaySX = kiemKeDto.NgaySX,
                           Ca = kiemKeDto.Ca,
                           Scope = kiemKeDto.Scope,
                           ID_HeaderKey = kiemKeDto.HeaderKeyId,
                           ID_Silo = kiemKeDto.SiloId,
                           ID_PhuLieuNM = kiemKeDto.PhuLieuNMId,
                           TheTich = kiemKeDto.TheTich,
                           TyTrong = kiemKeDto.TyTrong,
                           NgayTao = DateTime.Now
                       };

                       // Lưu snapshot
                       await _context.BmKiemKePhuLieus.AddAsync(kiemKe);
                   }
               }

               // Lưu thay đổi
               await _context.SaveChangesAsync();

               return new STD_NXT_HRC2_UpsertResponse
               {
                   Id_Phieu = entity.IdPhieu
               };
           }
           catch(Exception ex)
           {
               throw new Exception(ex.Message);
           }
        }

        private class HeaderKeyInfo
        {
            public int Id { get; set; }
            public string TenHienThi { get; set; }
            public decimal? TyTrong { get; set; }
        }


        public async Task InitializeHRC2_STD_NXTAsync(BmPhieu phieu)
        {
            var ngaySx = phieu.NgaySX?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now;
            var ca = phieu.Ca.Value;
            var bieuMau = phieu.MaBm;
            var ngaySxOnly = DateOnly.FromDateTime(ngaySx);

            List<int> allHeaderKeyIds = new();
            Dictionary<int, List<(int Id_HeaderKey, string TenNguyenLieu, decimal? TyTrong, int? IDSilo)>> sourceByScope = new();
            Dictionary<int, (decimal? TyLeBOF, decimal? TyLeTinhLuyen, decimal? TyLeRH)> prevTotalTyLeLookup = new();

            // -------------------------------------------------------
            // Lấy danh sách nguồn
            // -------------------------------------------------------
            // Tìm phiếu gần nhất trước ca hiện tại (không giới hạn theo tháng)
            var prevPhieu = await _context.BmPhieus
                .Where(x => x.MaBm == "HRC2_STD_NXT"
                         && (x.NgaySX < ngaySxOnly
                             || (x.NgaySX == ngaySxOnly && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .Select(x => x.Idphieu)
                .FirstOrDefaultAsync();

            if (prevPhieu == default)
            {
                // Không có phiếu trước đó trong tháng → dùng danh sách Header_Key mặc định
                var defaults = await _context.Header_Keys
                    .Where(x => x.IsUsedNXT == true)
                    .Select(x => new { x.Id, x.TenHienThi, x.TyTrong})
                    .ToListAsync();

                if (!defaults.Any())
                    throw new Exception("Không có Header Key mặc định nào (IsUsedNXT = true)");

                var defaultItems = defaults
                    .Select(x => (x.Id, x.TenHienThi, x.TyTrong, (int?)null))
                    .ToList();

                foreach (var tohop in Enum.GetValues<ToHopSTDNXT>())
                {
                    sourceByScope[(int)tohop] = defaultItems;
                }

                allHeaderKeyIds = defaults.Select(x => x.Id).ToList();
            }
            else
            {
                // Lấy danh sách phụ liệu từ phiếu gần nhất
                var prevRecords = await _context.STD_XUAT_NHAP_TON_HRC2s
                    .Where(x => x.Id_Phieu == prevPhieu)
                    .Select(x => new
                    {
                        x.Id_HeaderKey,
                        x.TenNguyenLieu,
                        x.TyTrong,
                        x.Scope,
                        x.IDSilo,
                    })
                    .ToListAsync();

                var prevTotalRecords = await _context.STD_NXT_TOTAL_HRC2s
                    .Where(x => x.Id_Phieu == prevPhieu)
                    .Select(x => new { x.Id_HeaderKey, x.TyLeBOF, x.TyLeTinhLuyen, x.TyLeRH })
                    .ToListAsync();
                prevTotalTyLeLookup = prevTotalRecords
                    .ToDictionary(x => x.Id_HeaderKey, x => (x.TyLeBOF, x.TyLeTinhLuyen, x.TyLeRH));

                if (!prevRecords.Any())
                    throw new Exception($"Phiếu ca trước không có dữ liệu phụ liệu (IdPhieu: {prevPhieu})");

                // Group theo Scope → dedup theo Id_HeaderKey trong mỗi Scope
                sourceByScope = prevRecords
                    .GroupBy(x => x.Scope)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .GroupBy(x => x.Id_HeaderKey)
                            .Select(hg => hg.First())
                            .Select(x => (x.Id_HeaderKey, x.TenNguyenLieu, x.TyTrong, x.IDSilo))
                            .ToList()
                    );

                allHeaderKeyIds = prevRecords
                    .Select(x => x.Id_HeaderKey)
                    .Distinct()
                    .ToList();
            }

            // -------------------------------------------------------
            // MERGE STD_XUAT_NHAP_TON_HRC2 theo từng Scope độc lập
            // -------------------------------------------------------
            var existingRecords = await _context.STD_XUAT_NHAP_TON_HRC2s
                .Where(x => x.Id_Phieu == phieu.Idphieu)
                .Select(x => new { x.Id_HeaderKey, x.Scope })
                .ToListAsync();

            foreach (var (scope, sourceItems) in sourceByScope)
            {
                var sourceKeys = sourceItems.Select(x => x.Id_HeaderKey).ToList();
                var existingKeysInScope = existingRecords
                    .Where(x => x.Scope == scope)
                    .Select(x => x.Id_HeaderKey)
                    .ToList();

                var newKeys = sourceKeys.Except(existingKeysInScope).ToList();
                var removedKeys = existingKeysInScope.Except(sourceKeys).ToList();

                // Xóa dư
                if (removedKeys.Any())
                {
                    var toRemove = await _context.STD_XUAT_NHAP_TON_HRC2s
                        .Where(x => x.Id_Phieu == phieu.Idphieu
                                 && x.Scope == scope
                                 && removedKeys.Contains(x.Id_HeaderKey))
                        .ToListAsync();
                    _context.STD_XUAT_NHAP_TON_HRC2s.RemoveRange(toRemove);
                }

                // Thêm mới
                foreach (var item in sourceItems.Where(x => newKeys.Contains(x.Id_HeaderKey)))
                {
                    await _context.STD_XUAT_NHAP_TON_HRC2s.AddAsync(new STD_XUAT_NHAP_TON_HRC2
                    {
                        Id_Phieu = phieu.Idphieu,
                        NgaySX = ngaySx,
                        Ca = ca,
                        Scope = scope,
                        BieuMau = bieuMau,
                        Id_HeaderKey = item.Id_HeaderKey,
                        TenNguyenLieu = item.TenNguyenLieu,
                        TyTrong = item.TyTrong,
                        IDSilo = item.IDSilo,
                        ViTri = 1
                    });
                }
            }

            // -------------------------------------------------------
            // MERGE STD_NXT_TOTAL_HRC2
            // -------------------------------------------------------
            var existingTotalKeys = await _context.STD_NXT_TOTAL_HRC2s
                .Where(x => x.Id_Phieu == phieu.Idphieu)
                .Select(x => x.Id_HeaderKey)
                .ToListAsync();

            var newTotalKeys = allHeaderKeyIds.Except(existingTotalKeys).ToList();
            var removedTotalKeys = existingTotalKeys.Except(allHeaderKeyIds).ToList();

            if (removedTotalKeys.Any())
            {
                var toRemove = await _context.STD_NXT_TOTAL_HRC2s
                    .Where(x => x.Id_Phieu == phieu.Idphieu
                             && removedTotalKeys.Contains(x.Id_HeaderKey))
                    .ToListAsync();
                _context.STD_NXT_TOTAL_HRC2s.RemoveRange(toRemove);
            }

            var allSourceItems = sourceByScope.Values
                .SelectMany(x => x)
                .GroupBy(x => x.Id_HeaderKey)
                .Select(g => g.First())
                .ToList();

            foreach (var item in allSourceItems.Where(x => newTotalKeys.Contains(x.Id_HeaderKey)))
            {
                var prevTyLe = prevTotalTyLeLookup.TryGetValue(item.Id_HeaderKey, out var tl) ? tl : default;
                await _context.STD_NXT_TOTAL_HRC2s.AddAsync(new STD_NXT_TOTAL_HRC2
                {
                    Id_Phieu = phieu.Idphieu,
                    NgaySX = ngaySx,
                    Ca = ca,
                    Id_HeaderKey = item.Id_HeaderKey,
                    TenNguyenLieu = item.TenNguyenLieu,
                    TyLeBOF = prevTyLe.TyLeBOF,
                    TyLeTinhLuyen = prevTyLe.TyLeTinhLuyen,
                    TyLeRH = prevTyLe.TyLeRH
                });
            }

            await _context.SaveChangesAsync();

            // -------------------------------------------------------
            // Gọi SP: init TonDauCa + TonCuoiCa default
            // -------------------------------------------------------
            await GetHRC2FilterInitAsync(new InitXuatNhapTonHRC2Request
            {
                NgaySX = ngaySx,
                Ca = ca,
                IdPhieu = phieu.Idphieu,
                HeaderKeys = allHeaderKeyIds
                             .Select(id => new IdHeaderKeyModel { Id_HeaderKey = id })
                             .ToList()
            });
        }
        private static DataTable ToHeaderKeyDataTable(List<IdHeaderKeyModel> data)
        {
            var table = new DataTable();
            table.Columns.Add("Id_HeaderKey", typeof(int));

            foreach (var item in data)
            {
                table.Rows.Add(item.Id_HeaderKey);
            }

             return table;
        }

        public async Task GetHRC2FilterInitAsync(InitXuatNhapTonHRC2Request request)
        {
            var parameters = new[]
            {
                new SqlParameter("@NgaySX", SqlDbType.Date) { Value = request.NgaySX.Date },
                new SqlParameter("@Ca", SqlDbType.Int) { Value = request.Ca },
                new SqlParameter("@Id_Phieu", SqlDbType.UniqueIdentifier) { Value = request.IdPhieu },
                new SqlParameter("@ListHeaderKey", SqlDbType.Structured)
                {
                    TypeName = "dbo.TT_IdHeaderKey",
                    Value = ToHeaderKeyDataTable(request.HeaderKeys)
                }
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_Init_XuatNhapTon_HRC2 @NgaySX, @Ca, @Id_Phieu, @ListHeaderKey",
                parameters
            );
        }

        public async Task<STD_NXT_HRC2_GetDetailResponse> GetByPhieuIdAsync(Guid phieuId)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == phieuId);
            if (phieu == null)
            {
                throw new Exception("Phiếu không tồn tại");
            }
            var details = await _context.STD_XUAT_NHAP_TON_HRC2s.Where(x => x.Id_Phieu == phieuId).ToListAsync();
            var summary = await _context.STD_NXT_TOTAL_HRC2s.Where(x => x.Id_Phieu == phieuId).ToListAsync();

            var siloIds = details
                .Where(x => x.IDSilo.HasValue)
                .Select(x => x.IDSilo!.Value)
                .Distinct()
                .ToList();

            var siloNameById = siloIds.Count > 0
                ? await _context.Silos
                    .Where(s => siloIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.TenSilo)
                : new Dictionary<int, string>();
            return new STD_NXT_HRC2_GetDetailResponse
            {
                Id_Phieu = phieuId,
                BieuMau = phieu.MaBm,
                NgaySX = phieu.NgaySX?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now,
                Ca = phieu.Ca.Value,
                Details = details.Select(x => new NXTDetailResponseModel
                {
                    Scope = x.Scope,
                    Id_HeaderKey = x.Id_HeaderKey,
                    TenNguyenLieu = x.TenNguyenLieu,
                    ViTri = x.ViTri,
                    IDSilo = x.IDSilo,
                    TenSilo = x.IDSilo.HasValue && siloNameById.TryGetValue(x.IDSilo.Value, out var name) ? name : null,
                    TonDauCa = x.TonDauCa,
                    TuongQuanDauCa = x.TuongQuanDauCa,
                    NhapVaoTrongCa = x.NhapVaoTrongCa,
                    MucLieu = x.MucLieu,
                    TheTich = x.TheTich,
                    TyTrong = x.TyTrong,
                    TonCuoiCa = x.TonCuoiCa,
                    TuongQuanCuoiCa = x.TuongQuanCuoiCa,
                    TongThucTe = x.TongThucTe,
                    LuongSuDungKiemKe = x.LuongSuDungKiemKe,
                }).ToList(),
                Summary = summary.Select(x => new NXTSummaryResponseModel
                {
                    Id_HeaderKey = x.Id_HeaderKey,
                    TenNguyenLieu = x.TenNguyenLieu,
                    TongTonDauCa = x.TongTonDauCa,
                    TongTonNhapTrongCa = x.TongTonNhapTrongCa,
                    TongTonCuoiCa = x.TongTonCuoiCa,
                    TongSuDung = x.TongSuDung,
                    TongSDTrenSoSach = x.TongSDTrenSoSach,
                    ChenhLech = x.ChenhLech,
                    HasPhanBo = x.HasPhanBo,
                    TyLeBOF = x.TyLeBOF,
                    TyLeTinhLuyen = x.TyLeTinhLuyen,
                    TyLeRH = x.TyLeRH,
                    KLPB_BOF = x.KLPB_BOF,
                    KLPB_TL = x.KLPB_TL,
                    KLPB_RH = x.KLPB_RH,
                    KLTK_BOF = x.KLTK_BOF,
                    KLTK_LF = x.KLTK_LF,
                    KLTK_RH = x.KLTK_RH
                }).ToList()
            };
        }

        private static string? NormalizeNauLuyenMaBm(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var value = input.Trim();

            if (value.Equals("BOF", StringComparison.OrdinalIgnoreCase)) return "HRC2_BB_NauLuyen_BOF";
            if (value.Equals("LF", StringComparison.OrdinalIgnoreCase)) return "HRC2_BB_NauLuyen_LF";
            if (value.Equals("RH", StringComparison.OrdinalIgnoreCase)) return "HRC2_BB_NauLuyen_RH";

            return value;
        }

        public async Task<STD_NXT_RelatedPhieuStatusResponse> GetRelatedPhieuStatusesAsync(STD_NXT_RelatedPhieuStatusRequest request)
        {
            var targets = request.Targets ?? new List<STD_NXT_RelatedPhieuTarget>();
            var response = new STD_NXT_RelatedPhieuStatusResponse();

            if (!targets.Any())
            {
                response.CanPhanBo = true;
                response.IncompleteCount = 0;
                return response;
            }

            var normalizedTargets = targets
                .Select(t => new
                {
                    Target = t,
                    MaBm = NormalizeNauLuyenMaBm(t.BieuMau),
                    Scope = t.Scope
                })
                .ToList();

            var maBms = normalizedTargets
                .Select(x => x.MaBm)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct()
                .ToList();

            var scopes = normalizedTargets
                .Where(x => x.Scope.HasValue)
                .Select(x => x.Scope!.Value)
                .Distinct()
                .ToList();

            var ngaySX = DateOnly.FromDateTime(request.NgaySX);
            var candidates = await _context.BmPhieus
                .Where(p =>
                    p.IsDelete != 1 &&
                    p.IsLock != 1 &&
                    p.NgaySX == ngaySX &&
                    p.Ca == request.Ca &&
                    maBms.Contains(p.MaBm) &&
                    (!p.Scope.HasValue || scopes.Contains(p.Scope.Value)))
                .OrderByDescending(p => p.NgayTao)
                .ThenByDescending(p => p.Idphieu)
                .ToListAsync();

            foreach (var item in normalizedTargets)
            {
                var matched = candidates.FirstOrDefault(p =>
                    p.MaBm == item.MaBm &&
                    p.Scope == item.Scope);

                response.Items.Add(new STD_NXT_RelatedPhieuStatusItem
                {
                    TabKey = item.Target.TabKey,
                    Label = item.Target.Label,
                    BieuMau = item.Target.BieuMau,
                    Scope = item.Target.Scope,
                    IdPhieu = matched?.Idphieu,
                    TinhTrang = matched?.TinhTrang
                });
            }

            response.IncompleteCount = response.Items.Count(x => x.TinhTrang.HasValue && x.TinhTrang.Value != 2);
            response.CanPhanBo = response.IncompleteCount == 0;
            return response;
        }

        private async Task CheckPhieuChotAsync(DateTime ngaySX, int ca)
        {
            var nauLuyenMaBms = new[] { "HRC2_BB_NauLuyen_BOF", "HRC2_BB_NauLuyen_LF", "HRC2_BB_NauLuyen_RH" };
            var ngaySXDate = DateOnly.FromDateTime(ngaySX);

            var hasChotPhieu = await _context.BmPhieus
                .AnyAsync(p =>
                    nauLuyenMaBms.Contains(p.MaBm) &&
                    p.NgaySX == ngaySXDate &&
                    p.Ca == ca &&
                    p.IsDelete != 1 &&
                    p.TinhTrang == 5);

            if (hasChotPhieu)
            {
                throw new Exception("Đã có phiếu trong ca này được chốt. Không thể thực hiện thao tác này.");
            }
        }

        public async Task<bool> PhanBoAsync(STD_NXT_HRC2_PhanBoDto entity)
        {
            try
            {
                await CheckPhieuChotAsync(entity.NgaySX, entity.Ca);

                // ========== BƯỚC 0.5: Kiểm tra phiếu NauLuyen còn đang lưu ==========
                var maBmNauLuyen = new[] { "HRC2_BB_NauLuyen_RH", "HRC2_BB_NauLuyen_LF", "HRC2_BB_NauLuyen_BOF" };
                var ngaySXDate = DateOnly.FromDateTime(entity.NgaySX);
                var hasPhieuDangLuu = await _context.BmPhieus
                    .AnyAsync(p =>
                        maBmNauLuyen.Contains(p.MaBm) &&
                        p.NgaySX == ngaySXDate &&
                        p.Ca == entity.Ca &&
                        p.IsLock != 1 &&
                        p.IsDelete != 1 &&
                        p.TinhTrang != 2);

                if (hasPhieuDangLuu)
                {
                    throw new Exception("Có phiếu Chưa hoàn thành hoặc đã chốt. Vui lòng kiểm tra lại trước khi phân bổ.");
                }

                // ========== BƯỚC 1: Kiểm tra dữ liệu đã được lưu chưa ==========
                var details = await _context.STD_NXT_TOTAL_HRC2s
                    .Where(x =>
                        x.Id_HeaderKey == entity.Id_HeaderKey &&
                        x.NgaySX == entity.NgaySX &&
                        x.Ca == entity.Ca &&
                        x.Id_Phieu == entity.IdPhieu)
                    .FirstOrDefaultAsync();

                if (details == null)
                {
                    throw new Exception("Không tìm thấy dữ liệu tổng hợp. Vui lòng lưu trước khi phân bổ.");
                }

                // So sánh ChenhLech (cho phép sai số nhỏ do decimal)
                var chenhLechDiff = Math.Abs((details.ChenhLech ?? 0) - entity.ChenhLech);
                if (chenhLechDiff > 0.001m) // Cho phép sai số 0.001
                {
                    throw new Exception("Chênh lệch không khớp với dữ liệu đã lưu. Vui lòng lưu lại trước khi phân bổ.");
                }

                // ========== BƯỚC 1.5: Kiểm tra mẻ nấu luyện có bị đổi (VD: do "Đề nghị hiệu chỉnh")
                // sau lần "Làm mới" + "Lưu" gần nhất chưa — tránh phân bổ dựa trên ChênhLệch lỗi thời. ==========
                var tongThucTeHienTai = await ComputeTongThucTeHienTaiAsync(entity.NgaySX, entity.Ca, entity.Id_HeaderKey);
                var tongThucTeDiff = Math.Abs((details.TongSDTrenSoSach ?? 0) - tongThucTeHienTai);
                if (tongThucTeDiff > 0.001m)
                {
                    throw new Exception("Số liệu tiêu hao mẻ đã thay đổi so với lần lưu gần nhất trên Sổ Xuất-Nhập-Tồn (có thể do phiếu Nấu Luyện vừa được hiệu chỉnh). Vui lòng bấm \"Làm mới\" rồi \"Lưu\" lại Sổ Xuất-Nhập-Tồn trước khi phân bổ.");
                }

                // ========== BƯỚC 2: Lấy tất cả mẻ trong DLNM_HRC2 theo ngày/ca ==========
                var dlnmInCa = await _context.DLNM_HRC2s
                    .Where(x => x.Ngay == entity.NgaySX && x.Ca == entity.Ca && x.IsDelete != true)
                    .ToListAsync();

                if (!dlnmInCa.Any())
                {
                    throw new Exception($"Không tìm thấy mẻ nào trong ngày {entity.NgaySX:dd/MM/yyyy} ca {entity.Ca}.");
                }

                var allDlnmIds = dlnmInCa.Select(x => x.ID).ToList();

                // ========== BƯỚC 3: Tìm mẻ thực sự có sử dụng phụ liệu thuộc HeaderKey này ==========
                // Lấy ID_PhuLieu mapped vào HeaderKey
                var mappedPhuLieuIds = await _context.Header_Mappings
                    .Where(hm => hm.ID_HeaderKey == entity.Id_HeaderKey)
                    .Select(hm => hm.ID_PhuLieu)
                    .ToListAsync();

                // Tìm mẻ có PhuLieu_HRC2 (không phải phanBo) cho HeaderKey này
                // — khớp qua ID_PhuLieu → mapping hoặc ID_HeaderKey trực tiếp
                var meThoiIdsWithPhuLieu = await _context.PhuLieu_HRC2s
                    .Where(pl =>
                        allDlnmIds.Contains(pl.ID_MeThoi) &&
                        (pl.IsPhanBo != true) &&
                        (
                            (pl.ID_PhuLieu.HasValue && mappedPhuLieuIds.Contains(pl.ID_PhuLieu.Value)) ||
                            pl.ID_HeaderKey == entity.Id_HeaderKey
                        ))
                    .Select(pl => pl.ID_MeThoi)
                    .Distinct()
                    .ToListAsync();

                if (!meThoiIdsWithPhuLieu.Any())
                {
                    throw new Exception($"Không tìm thấy mẻ nào trong ngày {entity.NgaySX:dd/MM/yyyy} ca {entity.Ca} có sử dụng phụ liệu này.");
                }

                // Chỉ phân bổ cho các mẻ thực sự có dùng phụ liệu đó
                var meThoiIds = meThoiIdsWithPhuLieu;
                var dlnmLookupAll = dlnmInCa.ToDictionary(x => x.ID);

                // ========== BƯỚC 0 (sau validation): Lưu tỷ lệ phân bổ vào DB nếu được truyền vào ==========
                if (entity.TyLeBOF != null || entity.TyLeTinhLuyen != null || entity.TyLeRH != null)
                {
                    var tyLeRow = await _context.STD_NXT_TOTAL_HRC2s
                        .FirstOrDefaultAsync(x => x.Id_Phieu == entity.IdPhieu && x.Id_HeaderKey == entity.Id_HeaderKey);

                    if (tyLeRow != null)
                    {
                        if (entity.TyLeBOF != null) tyLeRow.TyLeBOF = entity.TyLeBOF;
                        if (entity.TyLeTinhLuyen != null) tyLeRow.TyLeTinhLuyen = entity.TyLeTinhLuyen;
                        if (entity.TyLeRH != null) tyLeRow.TyLeRH = entity.TyLeRH;
                    }
                }

                // ========== BƯỚC 5: Tính khối lượng phân bổ theo tỷ lệ BOF / Tinh luyện ==========
                // Dùng trực tiếp tỷ lệ người dùng gửi (entity.TyLeBOF/tyLeTinhLuyen) để tính phân bổ.
                // Chỉ truy vấn DB để bù khi thiếu một (null) để tránh lấy lại giá trị cũ chưa SaveChanges.
                decimal? tyLeBOF = entity.TyLeBOF;
                decimal? tyLeTinhLuyen = entity.TyLeTinhLuyen;
                decimal? tyLeRH = entity.TyLeRH;
                if (tyLeBOF == null || tyLeTinhLuyen == null || tyLeRH == null)
                {
                    var tyLeRecord = await _context.STD_NXT_TOTAL_HRC2s
                        .Where(x => x.Id_Phieu == entity.IdPhieu && x.Id_HeaderKey == entity.Id_HeaderKey)
                        .Select(x => new { x.TyLeBOF, x.TyLeTinhLuyen, x.TyLeRH })
                        .FirstOrDefaultAsync();

                    if (tyLeBOF == null) tyLeBOF = tyLeRecord?.TyLeBOF;
                    if (tyLeTinhLuyen == null) tyLeTinhLuyen = tyLeRecord?.TyLeTinhLuyen;
                    if (tyLeRH == null) tyLeRH = tyLeRecord?.TyLeRH;
                }

                Dictionary<long, decimal> klPhanBoByMeThoi;
                decimal? klPerBofToPersist = null;
                decimal? klPerTinhLuyenToPersist = null;
                decimal? klPerRHToPersist = null;

                if (tyLeBOF != null || tyLeTinhLuyen != null || tyLeRH != null)
                {
                    // Phân nhóm mẻ theo BieuMau: BOF / LF / RH
                    var bofMeIds = meThoiIds
                        .Where(id => dlnmLookupAll.TryGetValue(id, out var d) &&
                                     d.BieuMau != null &&
                                     d.BieuMau.StartsWith("BOF", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(id => id)
                        .ToList();
                    var lfMeIds = meThoiIds
                        .Where(id => dlnmLookupAll.TryGetValue(id, out var d) &&
                                     d.BieuMau != null &&
                                     d.BieuMau.StartsWith("LF", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(id => id)
                        .ToList();
                    var rhMeIds = meThoiIds
                        .Where(id => dlnmLookupAll.TryGetValue(id, out var d) &&
                                     d.BieuMau != null &&
                                     d.BieuMau.StartsWith("RH", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(id => id)
                        .ToList();

                    // Kiểm tra: không được phân bổ tỷ lệ > 0 cho nhóm không có mẻ
                    if (bofMeIds.Count == 0 && tyLeBOF is decimal tBof && tBof != 0)
                        throw new Exception($"Không có mẻ BOF trong ca này nhưng tỷ lệ BOF đang là {tBof}%. Vui lòng điều chỉnh lại tỷ lệ.");
                    // Validation LF/RH được xử lý bên trong nhánh tách/gộp bên dưới

                    klPhanBoByMeThoi = new Dictionary<long, decimal>();

                    if (bofMeIds.Any() && tyLeBOF is decimal tyLeBOFValue && tyLeBOFValue > 0)
                    {
                        var bofAmount = entity.ChenhLech * tyLeBOFValue / 100;
                        klPerBofToPersist = PhanBoKhongLechLamTron(klPhanBoByMeThoi, bofMeIds, bofAmount);
                    }
                    else
                    {
                        foreach (var id in bofMeIds) klPhanBoByMeThoi[id] = 0;
                        if (bofMeIds.Any()) klPerBofToPersist = 0;
                    }

                    // TyLeRH = null → dữ liệu cũ chưa tách LF/RH: dùng TyLeTinhLuyen cho cả LF+RH gộp lại (backward compat)
                    // TyLeRH có giá trị → dữ liệu mới: tách riêng LF dùng TyLeTinhLuyen, RH dùng TyLeRH
                    if (tyLeRH == null)
                    {
                        var tinhLuyenMeIds = lfMeIds.Concat(rhMeIds).ToList();
                        if (tinhLuyenMeIds.Count == 0 && tyLeTinhLuyen is decimal tTL && tTL != 0)
                            throw new Exception($"Không có mẻ tinh luyện trong ca này nhưng tỷ lệ tinh luyện đang là {tTL}%. Vui lòng điều chỉnh lại tỷ lệ.");

                        if (tinhLuyenMeIds.Any() && tyLeTinhLuyen is decimal tyLeTLValue && tyLeTLValue > 0)
                        {
                            var tlAmount = entity.ChenhLech * tyLeTLValue / 100;
                            klPerTinhLuyenToPersist = PhanBoKhongLechLamTron(klPhanBoByMeThoi, tinhLuyenMeIds, tlAmount);
                        }
                        else
                        {
                            foreach (var id in tinhLuyenMeIds) klPhanBoByMeThoi[id] = 0;
                            if (tinhLuyenMeIds.Any()) klPerTinhLuyenToPersist = 0;
                        }
                    }
                    else
                    {
                        if (lfMeIds.Any() && tyLeTinhLuyen is decimal tyLeLFValue && tyLeLFValue > 0)
                        {
                            var lfAmount = entity.ChenhLech * tyLeLFValue / 100;
                            klPerTinhLuyenToPersist = PhanBoKhongLechLamTron(klPhanBoByMeThoi, lfMeIds, lfAmount);
                        }
                        else
                        {
                            foreach (var id in lfMeIds) klPhanBoByMeThoi[id] = 0;
                            if (lfMeIds.Any()) klPerTinhLuyenToPersist = 0;
                        }

                        if (rhMeIds.Any() && tyLeRH is decimal tyLeRHValue && tyLeRHValue > 0)
                        {
                            var rhAmount = entity.ChenhLech * tyLeRHValue / 100;
                            klPerRHToPersist = PhanBoKhongLechLamTron(klPhanBoByMeThoi, rhMeIds, rhAmount);
                        }
                        else
                        {
                            foreach (var id in rhMeIds) klPhanBoByMeThoi[id] = 0;
                            if (rhMeIds.Any()) klPerRHToPersist = 0;
                        }
                    }
                }
                else
                {
                    // Fallback: chia đều cho tất cả mẻ (hành vi cũ)
                    var klEqual = entity.ChenhLech / meThoiIds.Count;
                    klPhanBoByMeThoi = meThoiIds.ToDictionary(id => id, _ => klEqual);
                }

                // ========== BƯỚC 6: Lấy thông tin Header_Key để lấy TenHienThi ==========
                var headerKey = await _context.Header_Keys
                    .Where(k => k.Id == entity.Id_HeaderKey)
                    .Select(k => new { k.TenHienThi })
                    .FirstOrDefaultAsync();

                var tenHienThi = headerKey?.TenHienThi ?? "Phân bổ";

                // ========== BƯỚC 7: Lấy các record phân bổ cũ để upsert (theo HeaderKey + ngày + ca) ==========
                var oldPhanBoRecords = await (
                    from pl in _context.PhuLieu_HRC2s
                    join dlnm in _context.DLNM_HRC2s on pl.ID_MeThoi equals dlnm.ID
                    where pl.ID_HeaderKey == entity.Id_HeaderKey &&
                          pl.IsPhanBo == true &&
                          dlnm.Ngay == entity.NgaySX &&
                          dlnm.Ca == entity.Ca &&
                          dlnm.IsDelete != true
                    select new { PhuLieu = pl, MeThoiId = dlnm.ID }
                ).ToListAsync();

                // Tạo dictionary để lookup nhanh: key = ID_MeThoi
                var oldPhanBoLookup = oldPhanBoRecords
                    .ToDictionary(x => x.MeThoiId, x => x.PhuLieu);

                // ========== BƯỚC 8: Upsert record phân bổ cho mỗi mẻ ==========
                foreach (var meThoiId in meThoiIds)
                {
                    if (!dlnmLookupAll.TryGetValue(meThoiId, out var dlnm)) continue;

                    var klPhanBo = klPhanBoByMeThoi.TryGetValue(meThoiId, out var kl) ? kl : 0;

                    // Kiểm tra xem đã có record phân bổ cho mẻ này chưa
                    if (oldPhanBoLookup.TryGetValue(meThoiId, out var existingPhanBo))
                    {
                        existingPhanBo.KLPhuGia = (double)klPhanBo;
                        existingPhanBo.TenHienThi = tenHienThi;
                        existingPhanBo.REPORT_NO = dlnm.REPORT_NO;
                        existingPhanBo.BieuMau = dlnm.BieuMau;
                        existingPhanBo.MeThoi = dlnm.MeThoi;
                        _context.PhuLieu_HRC2s.Update(existingPhanBo);
                    }
                    else
                    {
                        // INSERT: Tạo record phân bổ mới
                        var phuLieuPhanBo = new PhuLieu_HRC2
                        {
                            REPORT_NO = dlnm.REPORT_NO,
                            BieuMau = dlnm.BieuMau,
                            MeThoi = dlnm.MeThoi,
                            ID_PhuLieu = null,
                            TenPhuLieu = null,
                            KLPhuGia = (double)klPhanBo,
                            ID_HeaderKey = entity.Id_HeaderKey,
                            TenHienThi = tenHienThi,
                            ID_MeThoi = meThoiId,
                            IsPhanBo = true
                        };
                        _context.PhuLieu_HRC2s.Add(phuLieuPhanBo);
                    }
                }

                // ========== BƯỚC 9: Xóa các record phân bổ cũ không còn trong danh sách mới ==========
                // (Các mẻ đã bị xóa hoặc không còn sử dụng HeaderKey này)
                var meThoiIdsSet = meThoiIds.ToHashSet();
                var phanBoToDelete = oldPhanBoRecords
                    .Where(x => !meThoiIdsSet.Contains(x.MeThoiId))
                    .Select(x => x.PhuLieu)
                    .ToList();

                if (phanBoToDelete.Any())
                {
                    _context.PhuLieu_HRC2s.RemoveRange(phanBoToDelete);
                }

                details.HasPhanBo = true;
                details.KLPB_BOF = klPerBofToPersist;
                details.KLPB_TL = klPerTinhLuyenToPersist;
                details.KLPB_RH = klPerRHToPersist;
                await _context.SaveChangesAsync();

                // Tính KLTK_BOF/LF/RH SAU khi dòng phân bổ đã được lưu (BƯỚC 8/9 ở trên) — phải đọc lại
                // DB sau SaveChangesAsync để ComputeKLTKAsync gộp đúng cả phần phân bổ vừa ghi.
                var (klTkBof, klTkLf, klTkRh) = await ComputeKLTKAsync(entity.NgaySX, entity.Ca, entity.Id_HeaderKey);
                details.KLTK_BOF = klTkBof;
                details.KLTK_LF = klTkLf;
                details.KLTK_RH = klTkRh;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<bool> ThuHoiPhanBoAsync(STD_NXT_HRC2_PhanBoDto entity)
        {
            try
            {
                await CheckPhieuChotAsync(entity.NgaySX, entity.Ca);

                // B1: Kiểm tra row tổng hợp tồn tại (đúng phiếu đang thao tác)
                var summary = await _context.STD_NXT_TOTAL_HRC2s
                    .FirstOrDefaultAsync(x =>
                        x.Id_HeaderKey == entity.Id_HeaderKey &&
                        x.NgaySX == entity.NgaySX &&
                        x.Ca == entity.Ca &&
                        x.Id_Phieu == entity.IdPhieu);

                if (summary == null)
                {
                    throw new Exception("Không tìm thấy dữ liệu tổng hợp để thu hồi phân bổ. Vui lòng lưu trước.");
                }

                if (summary.HasPhanBo != true)
                {
                    throw new Exception("Dòng này chưa được phân bổ hoặc đã thu hồi trước đó.");
                }

                // B2: Xóa record PhuLieu_HRC2 phân bổ (IsPhanBo = true) cho HeaderKey + ngày + ca (join DLNM để lọc đúng ngày/ca)
                var phanBoRecords = await (
                    from pl in _context.PhuLieu_HRC2s
                    join dlnm in _context.DLNM_HRC2s on pl.ID_MeThoi equals dlnm.ID
                    where pl.ID_HeaderKey == entity.Id_HeaderKey &&
                          pl.IsPhanBo == true &&
                          dlnm.Ngay == entity.NgaySX &&
                          dlnm.Ca == entity.Ca &&
                          dlnm.IsDelete != true
                    select pl
                ).ToListAsync();

                if (phanBoRecords.Any())
                {
                    _context.PhuLieu_HRC2s.RemoveRange(phanBoRecords);
                }

                // B3: Reset cờ HasPhanBo
                summary.HasPhanBo = null;
                summary.TyLeBOF = null;
                summary.TyLeTinhLuyen = null;
                summary.TyLeRH = null;
                summary.KLPB_BOF = null;
                summary.KLPB_TL = null;
                summary.KLPB_RH = null;
                // Reset chờ bấm Phân bổ/Không phân bổ lại — không tính toán lại ở đây (mirror HRC1).
                summary.KLTK_BOF = null;
                summary.KLTK_LF = null;
                summary.KLTK_RH = null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Đối chiếu tính liên tục của sổ Xuất-Nhập-Tồn: với mỗi Scope (tổ hợp BOF/LF/RH), ghép 2 PHIẾU
        /// liên tiếp theo thời gian (NgaySX, Ca) trong khoảng lọc, rồi full-outer-join theo Id_HeaderKey
        /// giữa 2 phiếu đó — bên trái lấy TonCuoiCa (phiếu ca trước), bên phải lấy TonDauCa (phiếu ca
        /// sau). Phụ liệu chỉ có ở 1 trong 2 phiếu (mới phát sinh, hoặc không còn dùng nữa) thì bên
        /// còn lại để trống thay vì bị bỏ qua khỏi kết quả. Mirror STD_XNT_HRC1Repository.GetNhapXuatTonAsync.
        /// </summary>
        public async Task<List<STD_NXT_HRC2_NhapXuatTonRow>> GetNhapXuatTonAsync(STD_NXT_HRC2_NhapXuatTonSearchRequest request)
        {
            var tuNgay = request.TuNgay.Date;
            var denNgayExclusive = request.DenNgay.Date.AddDays(1);

            var rows = await (
                from d in _context.STD_XUAT_NHAP_TON_HRC2s
                join p in _context.BmPhieus on d.Id_Phieu equals p.Idphieu
                where p.IsDelete != 1
                      && d.NgaySX >= tuNgay
                      && d.NgaySX < denNgayExclusive
                select d
            ).ToListAsync();

            var buffer = new List<(STD_NXT_HRC2_NhapXuatTonRow Row, DateTime SortDate, int SortCa)>();

            foreach (var scopeGroup in rows.GroupBy(x => x.Scope))
            {
                var phieuList = scopeGroup
                    .GroupBy(x => x.Id_Phieu)
                    .Select(g => new
                    {
                        NgaySX = g.First().NgaySX,
                        Ca = g.First().Ca,
                        ByHeaderKey = g.ToDictionary(x => x.Id_HeaderKey),
                    })
                    .OrderBy(x => x.NgaySX)
                    .ThenBy(x => x.Ca)
                    .ToList();

                for (var i = 1; i < phieuList.Count; i++)
                {
                    var prevPhieu = phieuList[i - 1];
                    var currPhieu = phieuList[i];

                    var allHeaderKeyIds = prevPhieu.ByHeaderKey.Keys
                        .Union(currPhieu.ByHeaderKey.Keys)
                        .OrderBy(id => id);

                    foreach (var headerKeyId in allHeaderKeyIds)
                    {
                        prevPhieu.ByHeaderKey.TryGetValue(headerKeyId, out var prevDetail);
                        currPhieu.ByHeaderKey.TryGetValue(headerKeyId, out var currDetail);

                        var row = new STD_NXT_HRC2_NhapXuatTonRow
                        {
                            NgaySXTruoc = prevDetail != null ? prevPhieu.NgaySX : null,
                            CaTruoc = prevDetail != null ? prevPhieu.Ca : null,
                            PhuLieuTruoc = prevDetail?.TenNguyenLieu,
                            TonCuoiTruoc = prevDetail?.TonCuoiCa,

                            NgaySXSau = currDetail != null ? currPhieu.NgaySX : null,
                            CaSau = currDetail != null ? currPhieu.Ca : null,
                            PhuLieuSau = currDetail?.TenNguyenLieu,
                            TonDauSau = currDetail?.TonDauCa,
                        };

                        buffer.Add((row, currPhieu.NgaySX, currPhieu.Ca));
                    }
                }
            }

            return buffer
                .OrderBy(x => x.SortDate)
                .ThenBy(x => x.SortCa)
                .Select(x => x.Row)
                .ToList();
        }

        public async Task<bool> KhongPhanBoAsync(STD_NXT_HRC2_KhongPhanBoDto entity)
        {
            try
            {
                await CheckPhieuChotAsync(entity.NgaySX, entity.Ca);

                var summary = await _context.STD_NXT_TOTAL_HRC2s
                    .FirstOrDefaultAsync(x =>
                        x.Id_HeaderKey == entity.Id_HeaderKey &&
                        x.Id_Phieu == entity.IdPhieu);

                if (summary == null)
                {
                    throw new Exception("Không tìm thấy dữ liệu tổng hợp. Vui lòng lưu trước.");
                }
                if(summary.HasPhanBo == false){
                    // Bấm lại lần 2 = hủy quyết định "Không phân bổ" — reset chờ quyết định lại, mirror
                    // ThuHoiPhanBoAsync/HRC1.
                    summary.HasPhanBo = null;
                    summary.KLTK_BOF = null;
                    summary.KLTK_LF = null;
                    summary.KLTK_RH = null;
                } else{
                    summary.HasPhanBo = false;
                    var (klTkBof, klTkLf, klTkRh) = await ComputeKLTKAsync(entity.NgaySX, entity.Ca, entity.Id_HeaderKey);
                    summary.KLTK_BOF = klTkBof;
                    summary.KLTK_LF = klTkLf;
                    summary.KLTK_RH = klTkRh;
                }
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task SaveKiemKeAsync(SaveKiemKeRequest request)
        {
            try
            {
                // ValidateBeforeSaveAsync - sử dụng SiloService
                var siloRepo = new SiloRepository(_context);
                var siloService = new SiloService(siloRepo);
                await siloService.ValidateBeforeSaveAsync(request.SiloId, request.PhuLieuNMId, request.NgaySX);

                // Check trùng (BmKiemKePhuLieuRepository.Exists)
                // Key: (NgaySX, Ca, HeaderKeyId, SiloId, PhuLieuNMId, Scope)
                var exists = await _context.BmKiemKePhuLieus
                    .AnyAsync(k => 
                        k.NgaySX.Date == request.NgaySX.Date &&
                        k.Ca == request.Ca &&
                        k.ID_HeaderKey == request.HeaderKeyId &&
                        k.ID_Silo == request.SiloId &&
                        k.ID_PhuLieuNM == request.PhuLieuNMId &&
                        k.Scope == request.Scope
                    );

                if (exists)
                {
                    throw new InvalidOperationException(
                        $"Đã tồn tại bản ghi kiểm kê với cùng Ngày SX, Ca, HeaderKey, Silo, Phụ liệu NM và Scope."
                    );
                }

                // Tạo entity BmKiemKePhuLieu
                var kiemKe = new BmKiemKePhuLieu
                {
                    NgaySX = request.NgaySX,
                    Ca = request.Ca,
                    Scope = request.Scope,
                    ID_HeaderKey = request.HeaderKeyId,
                    ID_Silo = request.SiloId,
                    ID_PhuLieuNM = request.PhuLieuNMId,
                    TheTich = request.TheTich,
                    TyTrong = request.TyTrong,
                    NgayTao = DateTime.Now
                };

                // Lưu snapshot
                await _context.BmKiemKePhuLieus.AddAsync(kiemKe);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
