using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.ResponseModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    // Mirror STD_XNT_HRC2Repository.cs, đổi Id_HeaderKey -> PhuLieuID, Header_Key -> HRC1_PhuLieuNM,
    // DLNM_HRC2/PhuLieu_HRC2 -> HRC1_TieuHao/HRC1_PhuLieu, bỏ nhánh RH (HRC1 chỉ có BOF/LF).
    // Xem .claude/hrc1_xnt.md mục 3-7 cho thiết kế đầy đủ.
    public class STD_XNT_HRC1Repository : ISTD_NXT_HRC1Repository
    {
        private readonly ProductFormContext _context;

        public STD_XNT_HRC1Repository(ProductFormContext context)
        {
            _context = context;
        }

        /// <summary>Scope trên STD_XUAT_NHAP_TON_HRC1 lưu giá trị enum ToHopSTDNXT_HRC1 (1-10):
        /// 1-5 = Lò thổi 1-5 (BOF), 6-10 = Tinh luyện 1-5 (LF).</summary>
        private static string ResolveBieuMauFromScope(int scope) => scope >= 1 && scope <= 5 ? "BOF" : "LF";

        /// <summary>
        /// Tính lại "Tổng thực tế" (tongThucTe) HIỆN TẠI của 1 PhuLieuID trong đúng Ngày/Ca, thẳng từ
        /// nguồn Hrc1PhuLieu (chỉ dòng đo thật IsPhanBo=false) — mirror đúng công thức
        /// DLNMHRC1Repository.GetHRC1GroupedByMaterialAsync (dùng khi bấm "Làm mới"), KHÔNG cộng
        /// KLPhanBo (khác ComputeEffectiveTotalHrc1 dùng cho báo cáo thống kê KLTK). Dùng để phát hiện
        /// mẻ tiêu hao đã đổi (VD: phiếu Tiêu Hao vừa được "Đề nghị hiệu chỉnh") sau lần "Làm mới" +
        /// "Lưu" gần nhất trên Sổ Xuất-Nhập-Tồn, tránh phân bổ dựa trên ChênhLệch đã lỗi thời.
        /// </summary>
        private async Task<decimal> ComputeTongThucTeHienTaiAsync(DateOnly ngaySX, int ca, int phuLieuID)
        {
            var caByte = (byte)ca;
            var raw = await (
                from pl in _context.Hrc1PhuLieus
                join tieuHao in _context.Hrc1TieuHaos on pl.MeID equals tieuHao.ID
                where tieuHao.NgaySanXuat == ngaySX
                      && tieuHao.Ca == caByte
                      && tieuHao.IsDeleted == false
                      && pl.IsDeleted == false
                      && pl.IsPhanBo == false
                      && pl.PhuLieuID == phuLieuID
                      && (tieuHao.IDPhieu == null ||
                          _context.BmPhieus.Any(p => p.Idphieu == tieuHao.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                select new { pl.IsManual, pl.KLPhuGia, pl.KLPhuGia_Manual }
            ).ToListAsync();

            return raw.Sum(x => x.IsManual ? (x.KLPhuGia_Manual ?? 0) : (x.KLPhuGia ?? 0));
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

        /// <summary>Port đúng công thức DLNMHRC1Repository.ComputeEffectiveTotal (api/DLNMHRC1/search-thongke)
        /// sang decimal cho 1 dòng HRC1_PhuLieu.</summary>
        private static decimal? ComputeEffectiveTotalHrc1(Hrc1PhuLieu p)
        {
            var effective = p.IsManual ? p.KLPhuGia_Manual : p.KLPhuGia;
            if (!effective.HasValue && !p.KLPhanBo.HasValue) return null;
            return (effective ?? 0) + (p.KLPhanBo ?? 0);
        }

        /// <summary>
        /// Tính KLTK_BOF/KLTK_LF cho 1 PhuLieuID trong đúng Ngày/Ca — mirror công thức
        /// DLNMHRC1Repository.GetThongKeSumAsync (api/DLNMHRC1/search-thongke): gộp TẤT CẢ Scope của từng
        /// BieuMau (không lọc riêng 1 Scope), cộng cả dòng phân bổ (IsPhanBo=true, cùng PhuLieuID nên tự
        /// khớp), loại dòng thuộc phiếu "Đề nghị hiệu chỉnh" cha đang khóa (BM_Phieu.IsLock=1) — mirror
        /// BuildThongKeQuery. Trả về NULL cho vế nào không có dòng dữ liệu (khớp hành vi "không xuất hiện
        /// trong kết quả" của search-thongke), khác 0.
        /// </summary>
        private async Task<(decimal? Bof, decimal? Lf)> ComputeKLTKAsync(DateOnly ngaySX, int ca, int phuLieuID)
        {
            var caByte = (byte)ca;
            var meRows = await _context.Hrc1TieuHaos
                .Where(x => x.NgaySanXuat == ngaySX && x.Ca == caByte && !x.IsDeleted)
                .Where(x => x.IDPhieu == null ||
                    _context.BmPhieus.Any(p => p.Idphieu == x.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                .Select(x => new { x.ID, x.BieuMau })
                .ToListAsync();

            if (meRows.Count == 0) return (null, null);

            var bieuMauByMeId = meRows.ToDictionary(x => x.ID, x => x.BieuMau);
            var meIds = meRows.Select(x => x.ID).ToList();

            var plRows = await _context.Hrc1PhuLieus
                .Where(pl => pl.PhuLieuID == phuLieuID && !pl.IsDeleted && meIds.Contains(pl.MeID))
                .ToListAsync();

            decimal? bofTotal = null, lfTotal = null;
            foreach (var pl in plRows)
            {
                var effective = ComputeEffectiveTotalHrc1(pl);
                if (!effective.HasValue) continue;
                if (!bieuMauByMeId.TryGetValue(pl.MeID, out var bieuMau) || bieuMau == null) continue;

                if (string.Equals(bieuMau, "BOF", StringComparison.OrdinalIgnoreCase))
                    bofTotal = (bofTotal ?? 0) + effective.Value;
                else if (string.Equals(bieuMau, "LF", StringComparison.OrdinalIgnoreCase))
                    lfTotal = (lfTotal ?? 0) + effective.Value;
            }

            return (bofTotal, lfTotal);
        }

        public async Task<STD_NXT_HRC1_UpsertResponse> UpsertAsync(STD_NXT_HRC1_UpsertDto entity)
        {
            try
            {
                var existingRecord = await _context.BmPhieus
                    .FirstOrDefaultAsync(e => e.Idphieu == entity.IdPhieu);
                if (existingRecord == null)
                    throw new Exception("Phiếu không tồn tại");

                // ========== XỬ LÝ STD_XUAT_NHAP_TON_HRC1 (Details) ==========
                // Key: (Scope, PhuLieuID, Id_Phieu)
                var existingDetails = await _context.STD_XUAT_NHAP_TON_HRC1s
                    .Where(x => x.Id_Phieu == entity.IdPhieu)
                    .ToListAsync();

                if (entity.Details != null && entity.Details.Any())
                {
                    foreach (var detailDto in entity.Details)
                    {
                        var existingDetail = existingDetails.FirstOrDefault(x =>
                            x.Scope == detailDto.Scope &&
                            x.PhuLieuID == detailDto.PhuLieuID &&
                            x.Id_Phieu == entity.IdPhieu);

                        if (existingDetail != null)
                        {
                            existingDetail.NgaySX = entity.NgaySX;
                            existingDetail.Ca = entity.Ca;
                            existingDetail.BieuMau = ResolveBieuMauFromScope(detailDto.Scope);
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
                            existingDetail.ThuTu = detailDto.ThuTu;

                            _context.STD_XUAT_NHAP_TON_HRC1s.Update(existingDetail);
                        }
                        else
                        {
                            var newDetail = new STD_XUAT_NHAP_TON_HRC1
                            {
                                Id_Phieu = entity.IdPhieu,
                                NgaySX = entity.NgaySX,
                                Ca = entity.Ca,
                                ViTri = detailDto.ViTri,
                                Scope = detailDto.Scope,
                                BieuMau = ResolveBieuMauFromScope(detailDto.Scope),
                                PhuLieuID = detailDto.PhuLieuID,
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
                                ThuTu = detailDto.ThuTu,
                            };

                            await _context.STD_XUAT_NHAP_TON_HRC1s.AddAsync(newDetail);
                        }
                    }
                }

                var detailKeysInDto = entity.Details?
                    .Select(d => (Scope: d.Scope, PhuLieuID: d.PhuLieuID))
                    .ToHashSet() ?? new HashSet<(int Scope, int PhuLieuID)>();

                var detailsToDelete = existingDetails.Where(existing =>
                    !detailKeysInDto.Contains((existing.Scope, existing.PhuLieuID)))
                    .ToList();

                if (detailsToDelete.Any())
                {
                    _context.STD_XUAT_NHAP_TON_HRC1s.RemoveRange(detailsToDelete);
                }

                // ========== XỬ LÝ STD_NXT_TOTAL_HRC1 (Summary) ==========
                // Key: (PhuLieuID, Id_Phieu)
                var existingSummary = await _context.STD_NXT_TOTAL_HRC1s
                    .Where(x => x.Id_Phieu == entity.IdPhieu)
                    .ToListAsync();

                if (entity.Summary != null && entity.Summary.Any())
                {
                    foreach (var summaryDto in entity.Summary)
                    {
                        var existingSum = existingSummary.FirstOrDefault(x =>
                            x.PhuLieuID == summaryDto.PhuLieuID &&
                            x.Id_Phieu == entity.IdPhieu);

                        if (existingSum != null)
                        {
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
                            if (summaryDto.TyLeLF != null) existingSum.TyLeLF = summaryDto.TyLeLF;
                            _context.STD_NXT_TOTAL_HRC1s.Update(existingSum);
                        }
                        else
                        {
                            var newSummary = new STD_NXT_TOTAL_HRC1
                            {
                                Id_Phieu = entity.IdPhieu,
                                NgaySX = entity.NgaySX,
                                Ca = entity.Ca,
                                PhuLieuID = summaryDto.PhuLieuID,
                                TenNguyenLieu = summaryDto.TenNguyenLieu,
                                TongTonDauCa = summaryDto.TongTonDauCa,
                                TongTonNhapTrongCa = summaryDto.TongNhapTrongCa,
                                TongTonCuoiCa = summaryDto.TongTonCuoiCa,
                                TongSuDung = summaryDto.TongSuDung,
                                TongSDTrenSoSach = summaryDto.TongSDTrenSoSach,
                                ChenhLech = summaryDto.ChenhLech,
                                TyLeBOF = summaryDto.TyLeBOF,
                                TyLeLF = summaryDto.TyLeLF,
                            };

                            await _context.STD_NXT_TOTAL_HRC1s.AddAsync(newSummary);
                        }
                    }
                }

                var summaryKeysInDto = entity.Summary?
                    .Select(s => s.PhuLieuID)
                    .ToList() ?? new List<int>();

                var summaryToDelete = existingSummary.Where(existing =>
                    !summaryKeysInDto.Contains(existing.PhuLieuID))
                    .ToList();

                if (summaryToDelete.Any())
                {
                    _context.STD_NXT_TOTAL_HRC1s.RemoveRange(summaryToDelete);
                }

                await _context.SaveChangesAsync();

                return new STD_NXT_HRC1_UpsertResponse
                {
                    Id_Phieu = entity.IdPhieu
                };
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task InitializeHRC1_STD_NXTAsync(BmPhieu phieu)
        {
            var ngaySx = phieu.NgaySX?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now;
            var ca = phieu.Ca.Value;
            var ngaySxOnly = DateOnly.FromDateTime(ngaySx);

            List<int> allPhuLieuIds = new();
            Dictionary<int, List<(int PhuLieuID, string? TenNguyenLieu, decimal? TyTrong, int? IDSilo, int? ThuTu)>> sourceByScope = new();
            Dictionary<int, (decimal? TyLeBOF, decimal? TyLeLF)> prevTotalTyLeLookup = new();

            // -------------------------------------------------------
            // Lấy danh sách nguồn
            // -------------------------------------------------------
            var prevPhieu = await _context.BmPhieus
                .Where(x => x.MaBm == "HRC1_STD_NXT"
                         && (x.NgaySX < ngaySxOnly
                             || (x.NgaySX == ngaySxOnly && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .Select(x => x.Idphieu)
                .FirstOrDefaultAsync();

            if (prevPhieu == default)
            {
                // Không có phiếu trước đó → dùng danh sách HRC1_PhuLieuNM mặc định (IsUsedNXT = true),
                // theo ThuTu_Excel_BOF khai báo trong danh mục (cột ThuTu đơn cũ đã bỏ; nhánh này áp dụng
                // chung cho cả 10 tổ hợp BOF/LF nên không tách riêng theo BieuMau ở bước init này).
                var defaults = await _context.Hrc1PhuLieuNms
                    .Where(x => x.IsUsedNXT == true)
                    .OrderBy(x => x.ThuTu_Excel_BOF ?? int.MaxValue)
                    .ThenBy(x => x.ID)
                    .Select(x => new { x.ID, x.TenPhuLieu })
                    .ToListAsync();

                if (!defaults.Any())
                    throw new Exception("Không có Phụ liệu mặc định nào (IsUsedNXT = true) trong HRC1_PhuLieuNM");

                var defaultItems = defaults
                    .Select((x, idx) => (x.ID, (string?)x.TenPhuLieu, (decimal?)null, (int?)null, (int?)idx))
                    .ToList();

                foreach (var tohop in Enum.GetValues<ToHopSTDNXT_HRC1>())
                {
                    sourceByScope[(int)tohop] = defaultItems;
                }

                allPhuLieuIds = defaults.Select(x => x.ID).ToList();
            }
            else
            {
                var prevRecords = await _context.STD_XUAT_NHAP_TON_HRC1s
                    .Where(x => x.Id_Phieu == prevPhieu)
                    .Select(x => new
                    {
                        x.PhuLieuID,
                        x.TenNguyenLieu,
                        x.TyTrong,
                        x.Scope,
                        x.IDSilo,
                        x.ThuTu,
                    })
                    .ToListAsync();

                var prevTotalRecords = await _context.STD_NXT_TOTAL_HRC1s
                    .Where(x => x.Id_Phieu == prevPhieu)
                    .Select(x => new { x.PhuLieuID, x.TyLeBOF, x.TyLeLF })
                    .ToListAsync();
                prevTotalTyLeLookup = prevTotalRecords
                    .ToDictionary(x => x.PhuLieuID, x => (x.TyLeBOF, x.TyLeLF));

                if (!prevRecords.Any())
                    throw new Exception($"Phiếu ca trước không có dữ liệu phụ liệu (IdPhieu: {prevPhieu})");

                // Group theo Scope → dedup theo PhuLieuID trong mỗi Scope, giữ nguyên thứ tự
                // hiển thị (ThuTu) mà người dùng đã sắp xếp ở ca trước
                sourceByScope = prevRecords
                    .GroupBy(x => x.Scope)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderBy(x => x.ThuTu ?? int.MaxValue)
                            .GroupBy(x => x.PhuLieuID)
                            .Select(hg => hg.First())
                            .Select((x, idx) => (x.PhuLieuID, x.TenNguyenLieu, x.TyTrong, x.IDSilo, (int?)idx))
                            .ToList()
                    );

                allPhuLieuIds = prevRecords
                    .Select(x => x.PhuLieuID)
                    .Distinct()
                    .ToList();
            }

            // -------------------------------------------------------
            // MERGE STD_XUAT_NHAP_TON_HRC1 theo từng Scope độc lập (10 tổ hợp)
            // -------------------------------------------------------
            var existingRecords = await _context.STD_XUAT_NHAP_TON_HRC1s
                .Where(x => x.Id_Phieu == phieu.Idphieu)
                .Select(x => new { x.PhuLieuID, x.Scope })
                .ToListAsync();

            foreach (var (scope, sourceItems) in sourceByScope)
            {
                var sourceKeys = sourceItems.Select(x => x.PhuLieuID).ToList();
                var existingKeysInScope = existingRecords
                    .Where(x => x.Scope == scope)
                    .Select(x => x.PhuLieuID)
                    .ToList();

                var newKeys = sourceKeys.Except(existingKeysInScope).ToList();
                var removedKeys = existingKeysInScope.Except(sourceKeys).ToList();

                if (removedKeys.Any())
                {
                    var toRemove = await _context.STD_XUAT_NHAP_TON_HRC1s
                        .Where(x => x.Id_Phieu == phieu.Idphieu
                                 && x.Scope == scope
                                 && removedKeys.Contains(x.PhuLieuID))
                        .ToListAsync();
                    _context.STD_XUAT_NHAP_TON_HRC1s.RemoveRange(toRemove);
                }

                foreach (var item in sourceItems.Where(x => newKeys.Contains(x.PhuLieuID)))
                {
                    await _context.STD_XUAT_NHAP_TON_HRC1s.AddAsync(new STD_XUAT_NHAP_TON_HRC1
                    {
                        Id_Phieu = phieu.Idphieu,
                        NgaySX = ngaySx,
                        Ca = ca,
                        Scope = scope,
                        BieuMau = ResolveBieuMauFromScope(scope),
                        PhuLieuID = item.PhuLieuID,
                        TenNguyenLieu = item.TenNguyenLieu,
                        TyTrong = item.TyTrong,
                        IDSilo = item.IDSilo,
                        ViTri = 1,
                        ThuTu = item.ThuTu
                    });
                }
            }

            // -------------------------------------------------------
            // MERGE STD_NXT_TOTAL_HRC1
            // -------------------------------------------------------
            var existingTotalKeys = await _context.STD_NXT_TOTAL_HRC1s
                .Where(x => x.Id_Phieu == phieu.Idphieu)
                .Select(x => x.PhuLieuID)
                .ToListAsync();

            var newTotalKeys = allPhuLieuIds.Except(existingTotalKeys).ToList();
            var removedTotalKeys = existingTotalKeys.Except(allPhuLieuIds).ToList();

            if (removedTotalKeys.Any())
            {
                var toRemove = await _context.STD_NXT_TOTAL_HRC1s
                    .Where(x => x.Id_Phieu == phieu.Idphieu
                             && removedTotalKeys.Contains(x.PhuLieuID))
                    .ToListAsync();
                _context.STD_NXT_TOTAL_HRC1s.RemoveRange(toRemove);
            }

            var allSourceItems = sourceByScope.Values
                .SelectMany(x => x)
                .GroupBy(x => x.PhuLieuID)
                .Select(g => g.First())
                .ToList();

            foreach (var item in allSourceItems.Where(x => newTotalKeys.Contains(x.PhuLieuID)))
            {
                var prevTyLe = prevTotalTyLeLookup.TryGetValue(item.PhuLieuID, out var tl) ? tl : default;
                await _context.STD_NXT_TOTAL_HRC1s.AddAsync(new STD_NXT_TOTAL_HRC1
                {
                    Id_Phieu = phieu.Idphieu,
                    NgaySX = ngaySx,
                    Ca = ca,
                    PhuLieuID = item.PhuLieuID,
                    TenNguyenLieu = item.TenNguyenLieu,
                    TyLeBOF = prevTyLe.TyLeBOF,
                    TyLeLF = prevTyLe.TyLeLF,
                });
            }

            await _context.SaveChangesAsync();

            // -------------------------------------------------------
            // Gọi SP: init TonDauCa mặc định
            // -------------------------------------------------------
            await GetHRC1FilterInitAsync(new InitXuatNhapTonHRC1Request
            {
                NgaySX = ngaySx,
                Ca = ca,
                IdPhieu = phieu.Idphieu,
                PhuLieus = allPhuLieuIds
                    .Select(id => new IdPhuLieuModel { Id_PhuLieu = id })
                    .ToList()
            });
        }

        private static DataTable ToPhuLieuDataTable(List<IdPhuLieuModel> data)
        {
            var table = new DataTable();
            table.Columns.Add("Id_PhuLieu", typeof(int));

            foreach (var item in data)
            {
                table.Rows.Add(item.Id_PhuLieu);
            }

            return table;
        }

        public async Task GetHRC1FilterInitAsync(InitXuatNhapTonHRC1Request request)
        {
            var parameters = new[]
            {
                new SqlParameter("@NgaySX", SqlDbType.Date) { Value = request.NgaySX.Date },
                new SqlParameter("@Ca", SqlDbType.Int) { Value = request.Ca },
                new SqlParameter("@Id_Phieu", SqlDbType.UniqueIdentifier) { Value = request.IdPhieu },
                new SqlParameter("@ListPhuLieuID", SqlDbType.Structured)
                {
                    TypeName = "dbo.TT_IdPhuLieuID",
                    Value = ToPhuLieuDataTable(request.PhuLieus)
                }
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_Init_XuatNhapTon_HRC1 @NgaySX, @Ca, @Id_Phieu, @ListPhuLieuID",
                parameters
            );
        }

        public async Task<STD_NXT_HRC1_GetDetailResponse> GetByPhieuIdAsync(Guid phieuId)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == phieuId);
            if (phieu == null)
            {
                throw new Exception("Phiếu không tồn tại");
            }
            var details = await _context.STD_XUAT_NHAP_TON_HRC1s
                .Where(x => x.Id_Phieu == phieuId)
                .OrderBy(x => x.Scope)
                .ThenBy(x => x.ThuTu ?? int.MaxValue)
                .ThenBy(x => x.Id)
                .ToListAsync();
            var summary = await _context.STD_NXT_TOTAL_HRC1s.Where(x => x.Id_Phieu == phieuId).ToListAsync();

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

            return new STD_NXT_HRC1_GetDetailResponse
            {
                Id_Phieu = phieuId,
                BieuMau = phieu.MaBm,
                NgaySX = phieu.NgaySX?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Now,
                Ca = phieu.Ca.Value,
                Details = details.Select(x => new NXTDetailResponseModel_HRC1
                {
                    Scope = x.Scope,
                    BieuMau = x.BieuMau,
                    PhuLieuID = x.PhuLieuID,
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
                Summary = summary.Select(x => new NXTSummaryResponseModel_HRC1
                {
                    PhuLieuID = x.PhuLieuID,
                    TenNguyenLieu = x.TenNguyenLieu,
                    TongTonDauCa = x.TongTonDauCa,
                    TongTonNhapTrongCa = x.TongTonNhapTrongCa,
                    TongTonCuoiCa = x.TongTonCuoiCa,
                    TongSuDung = x.TongSuDung,
                    TongSDTrenSoSach = x.TongSDTrenSoSach,
                    ChenhLech = x.ChenhLech,
                    HasPhanBo = x.HasPhanBo,
                    TyLeBOF = x.TyLeBOF,
                    TyLeLF = x.TyLeLF,
                    KLPB_BOF = x.KLPB_BOF,
                    KLPB_LF = x.KLPB_LF,
                    KLTK_BOF = x.KLTK_BOF,
                    KLTK_LF = x.KLTK_LF,
                }).ToList()
            };
        }

        private static string? NormalizeTieuHaoMaBm(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var value = input.Trim();

            if (value.Equals("BOF", StringComparison.OrdinalIgnoreCase)) return "HRC1_BB_TieuHao_BOF";
            if (value.Equals("LF", StringComparison.OrdinalIgnoreCase)) return "HRC1_BB_TieuHao_LF";

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
                    MaBm = NormalizeTieuHaoMaBm(t.BieuMau),
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

            // Đếm cả tab "Chưa có phiếu" (TinhTrang null, không match được phiếu nào) là chưa hoàn thành —
            // trước đây dùng x.TinhTrang.HasValue && ... nên tab chưa tạo phiếu bị bỏ qua khỏi
            // IncompleteCount, khiến CanPhanBo tính true dù còn nhiều lò/TL chưa có phiếu Hoàn thành.
            response.IncompleteCount = response.Items.Count(x => x.TinhTrang != 2);
            response.CanPhanBo = response.IncompleteCount == 0;
            return response;
        }

        private async Task CheckPhieuChotAsync(DateTime ngaySX, int ca)
        {
            var tieuHaoMaBms = new[] { "HRC1_BB_TieuHao_BOF", "HRC1_BB_TieuHao_LF" };
            var ngaySXDate = DateOnly.FromDateTime(ngaySX);

            var hasChotPhieu = await _context.BmPhieus
                .AnyAsync(p =>
                    tieuHaoMaBms.Contains(p.MaBm) &&
                    p.NgaySX == ngaySXDate &&
                    p.Ca == ca &&
                    p.IsDelete != 1 &&
                    p.TinhTrang == 5);

            if (hasChotPhieu)
            {
                throw new Exception("Đã có phiếu trong ca này được chốt. Không thể thực hiện thao tác này.");
            }
        }

        public async Task<bool> PhanBoAsync(STD_NXT_HRC1_PhanBoDto entity)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await CheckPhieuChotAsync(entity.NgaySX, entity.Ca);

                // ========== BƯỚC 0.5: Kiểm tra phiếu Tiêu Hao còn đang lưu ==========
                var tieuHaoMaBms = new[] { "HRC1_BB_TieuHao_BOF", "HRC1_BB_TieuHao_LF" };
                var ngaySXDate = DateOnly.FromDateTime(entity.NgaySX);
                var hasPhieuDangLuu = await _context.BmPhieus
                    .AnyAsync(p =>
                        tieuHaoMaBms.Contains(p.MaBm) &&
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
                var totalRow = await _context.STD_NXT_TOTAL_HRC1s
                    .Where(x =>
                        x.PhuLieuID == entity.PhuLieuID &&
                        x.NgaySX == entity.NgaySX &&
                        x.Ca == entity.Ca &&
                        x.Id_Phieu == entity.IdPhieu)
                    .FirstOrDefaultAsync();

                if (totalRow == null)
                {
                    throw new Exception("Không tìm thấy dữ liệu tổng hợp. Vui lòng lưu trước khi phân bổ.");
                }

                var chenhLechDiff = Math.Abs((totalRow.ChenhLech ?? 0) - entity.ChenhLech);
                if (chenhLechDiff > 0.001m)
                {
                    throw new Exception("Chênh lệch không khớp với dữ liệu đã lưu. Vui lòng lưu lại trước khi phân bổ.");
                }

                // ========== BƯỚC 1.5: Kiểm tra mẻ tiêu hao có bị đổi (VD: do "Đề nghị hiệu chỉnh")
                // sau lần "Làm mới" + "Lưu" gần nhất chưa — tránh phân bổ dựa trên ChênhLệch lỗi thời. ==========
                var tongThucTeHienTai = await ComputeTongThucTeHienTaiAsync(ngaySXDate, entity.Ca, entity.PhuLieuID);
                var tongThucTeDiff = Math.Abs((totalRow.TongSDTrenSoSach ?? 0) - tongThucTeHienTai);
                if (tongThucTeDiff > 0.001m)
                {
                    throw new Exception("Số liệu tiêu hao mẻ đã thay đổi so với lần lưu gần nhất trên Sổ Xuất-Nhập-Tồn (có thể do phiếu Tiêu Hao vừa được hiệu chỉnh). Vui lòng bấm \"Làm mới\" rồi \"Lưu\" lại Sổ Xuất-Nhập-Tồn trước khi phân bổ.");
                }

                // ========== BƯỚC 2: Lấy tất cả mẻ trong HRC1_TieuHao theo ngày/ca ==========
                // Loại dòng thuộc phiếu đã khóa (IsLock=1 — clone "Đề nghị hiệu chỉnh" đang mở), mirror
                // DLNMHRC1Repository.GetAllAsync — tránh đếm đôi mẻ khi tính số mẻ để phân bổ chênh lệch.
                var caByte = (byte)entity.Ca;
                var meTrongCa = await _context.Hrc1TieuHaos
                    .Where(x => x.NgaySanXuat == ngaySXDate && x.Ca == caByte && x.IsDeleted == false)
                    .Where(x => x.IDPhieu == null ||
                        _context.BmPhieus.Any(p => p.Idphieu == x.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                    .ToListAsync();

                if (!meTrongCa.Any())
                {
                    throw new Exception($"Không tìm thấy mẻ nào trong ngày {entity.NgaySX:dd/MM/yyyy} ca {entity.Ca}.");
                }

                var allMeIds = meTrongCa.Select(x => x.ID).ToList();

                // ========== BƯỚC 3: Tìm mẻ thực sự có dùng PhuLieuID này (dòng đo thật, IsPhanBo=false) ==========
                var meIdsWithPhuLieu = await _context.Hrc1PhuLieus
                    .Where(pl =>
                        allMeIds.Contains(pl.MeID) &&
                        pl.IsPhanBo == false &&
                        pl.IsDeleted == false &&
                        pl.PhuLieuID == entity.PhuLieuID)
                    .Select(pl => pl.MeID)
                    .Distinct()
                    .ToListAsync();

                if (!meIdsWithPhuLieu.Any())
                {
                    throw new Exception($"Không tìm thấy mẻ nào trong ngày {entity.NgaySX:dd/MM/yyyy} ca {entity.Ca} có sử dụng phụ liệu này.");
                }

                var meIds = meIdsWithPhuLieu;
                var meLookupAll = meTrongCa.ToDictionary(x => x.ID);

                // ========== Lưu tỷ lệ phân bổ vào DB nếu được truyền vào ==========
                if (entity.TyLeBOF != null || entity.TyLeLF != null)
                {
                    if (entity.TyLeBOF != null) totalRow.TyLeBOF = entity.TyLeBOF;
                    if (entity.TyLeLF != null) totalRow.TyLeLF = entity.TyLeLF;
                }

                // ========== BƯỚC 5: Tính khối lượng phân bổ theo tỷ lệ BOF / LF ==========
                decimal? tyLeBOF = entity.TyLeBOF;
                decimal? tyLeLF = entity.TyLeLF;
                if (tyLeBOF == null || tyLeLF == null)
                {
                    var tyLeRecord = await _context.STD_NXT_TOTAL_HRC1s
                        .Where(x => x.Id_Phieu == entity.IdPhieu && x.PhuLieuID == entity.PhuLieuID)
                        .Select(x => new { x.TyLeBOF, x.TyLeLF })
                        .FirstOrDefaultAsync();

                    if (tyLeBOF == null) tyLeBOF = tyLeRecord?.TyLeBOF;
                    if (tyLeLF == null) tyLeLF = tyLeRecord?.TyLeLF;
                }

                Dictionary<int, decimal> klPhanBoByMe;
                decimal? klPerBofToPersist = null;
                decimal? klPerLFToPersist = null;

                if (tyLeBOF != null || tyLeLF != null)
                {
                    var bofMeIds = meIds
                        .Where(id => meLookupAll.TryGetValue(id, out var d) &&
                                     d.BieuMau != null &&
                                     d.BieuMau.Equals("BOF", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(id => id)
                        .ToList();
                    var lfMeIds = meIds
                        .Where(id => meLookupAll.TryGetValue(id, out var d) &&
                                     d.BieuMau != null &&
                                     d.BieuMau.Equals("LF", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(id => id)
                        .ToList();

                    if (bofMeIds.Count == 0 && tyLeBOF is decimal tBof && tBof != 0)
                        throw new Exception($"Không có mẻ BOF trong ca này nhưng tỷ lệ BOF đang là {tBof}%. Vui lòng điều chỉnh lại tỷ lệ.");
                    if (lfMeIds.Count == 0 && tyLeLF is decimal tLF && tLF != 0)
                        throw new Exception($"Không có mẻ LF trong ca này nhưng tỷ lệ LF đang là {tLF}%. Vui lòng điều chỉnh lại tỷ lệ.");

                    klPhanBoByMe = new Dictionary<int, decimal>();

                    if (bofMeIds.Any() && tyLeBOF is decimal tyLeBOFValue && tyLeBOFValue > 0)
                    {
                        var bofAmount = entity.ChenhLech * tyLeBOFValue / 100;
                        klPerBofToPersist = PhanBoKhongLechLamTron(klPhanBoByMe, bofMeIds, bofAmount);
                    }
                    else
                    {
                        foreach (var id in bofMeIds) klPhanBoByMe[id] = 0;
                        if (bofMeIds.Any()) klPerBofToPersist = 0;
                    }

                    if (lfMeIds.Any() && tyLeLF is decimal tyLeLFValue && tyLeLFValue > 0)
                    {
                        var lfAmount = entity.ChenhLech * tyLeLFValue / 100;
                        klPerLFToPersist = PhanBoKhongLechLamTron(klPhanBoByMe, lfMeIds, lfAmount);
                    }
                    else
                    {
                        foreach (var id in lfMeIds) klPhanBoByMe[id] = 0;
                        if (lfMeIds.Any()) klPerLFToPersist = 0;
                    }
                }
                else
                {
                    // Fallback: chia đều cho tất cả mẻ (hành vi cũ, không truyền tỷ lệ nào)
                    var klEqual = entity.ChenhLech / meIds.Count;
                    klPhanBoByMe = meIds.ToDictionary(id => id, _ => klEqual);
                }

                // ========== BƯỚC 6: Tên hiển thị phụ liệu ==========
                var phuLieuNm = await _context.Hrc1PhuLieuNms
                    .Where(k => k.ID == entity.PhuLieuID)
                    .Select(k => new { k.TenPhuLieu })
                    .FirstOrDefaultAsync();

                var tenPhuLieu = phuLieuNm?.TenPhuLieu ?? "Phân bổ";

                // ========== BƯỚC 7: Lấy record phân bổ cũ để upsert (theo PhuLieuID + IsPhanBo=1) ==========
                var oldPhanBoRecords = await _context.Hrc1PhuLieus
                    .Where(pl =>
                        pl.PhuLieuID == entity.PhuLieuID &&
                        pl.IsPhanBo == true &&
                        allMeIds.Contains(pl.MeID) &&
                        pl.IsDeleted == false)
                    .ToListAsync();

                var oldPhanBoLookup = oldPhanBoRecords.ToDictionary(x => x.MeID);

                // ========== BƯỚC 8: Upsert record phân bổ cho mỗi mẻ ==========
                foreach (var meId in meIds)
                {
                    if (!meLookupAll.TryGetValue(meId, out var me)) continue;

                    var klPhanBo = klPhanBoByMe.TryGetValue(meId, out var kl) ? kl : 0;

                    if (oldPhanBoLookup.TryGetValue(meId, out var existingPhanBo))
                    {
                        existingPhanBo.KLPhuGia = klPhanBo;
                        existingPhanBo.TenPhuLieu = tenPhuLieu;
                        _context.Hrc1PhuLieus.Update(existingPhanBo);
                    }
                    else
                    {
                        var newPhanBo = new Hrc1PhuLieu
                        {
                            MeID = meId,
                            PhuLieuID = entity.PhuLieuID,
                            TenPhuLieu = tenPhuLieu,
                            KLPhuGia = klPhanBo,
                            IsPhanBo = true,
                            IsManual = false,
                            IsAddManual = false,
                            IsNM = false,
                        };
                        _context.Hrc1PhuLieus.Add(newPhanBo);
                    }
                }

                // ========== BƯỚC 9: Xóa các record phân bổ cũ không còn trong danh sách mới ==========
                var meIdsSet = meIds.ToHashSet();
                var phanBoToDelete = oldPhanBoRecords
                    .Where(x => !meIdsSet.Contains(x.MeID))
                    .ToList();

                if (phanBoToDelete.Any())
                {
                    _context.Hrc1PhuLieus.RemoveRange(phanBoToDelete);
                }

                totalRow.HasPhanBo = true;
                totalRow.KLPB_BOF = klPerBofToPersist;
                totalRow.KLPB_LF = klPerLFToPersist;
                await _context.SaveChangesAsync();

                // Tính KLTK_BOF/KLTK_LF SAU khi dòng phân bổ đã được lưu (BƯỚC 8/9 ở trên) — phải đọc lại DB
                // sau SaveChangesAsync để công thức ComputeKLTKAsync gộp đúng cả phần phân bổ vừa ghi.
                var (klTkBof, klTkLf) = await ComputeKLTKAsync(ngaySXDate, entity.Ca, entity.PhuLieuID);
                totalRow.KLTK_BOF = klTkBof;
                totalRow.KLTK_LF = klTkLf;
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task<bool> ThuHoiPhanBoAsync(STD_NXT_HRC1_PhanBoDto entity)
        {
            try
            {
                await CheckPhieuChotAsync(entity.NgaySX, entity.Ca);

                var totalRow = await _context.STD_NXT_TOTAL_HRC1s
                    .FirstOrDefaultAsync(x =>
                        x.PhuLieuID == entity.PhuLieuID &&
                        x.NgaySX == entity.NgaySX &&
                        x.Ca == entity.Ca &&
                        x.Id_Phieu == entity.IdPhieu);

                if (totalRow == null)
                {
                    throw new Exception("Không tìm thấy dữ liệu tổng hợp để thu hồi phân bổ. Vui lòng lưu trước.");
                }

                if (totalRow.HasPhanBo != true)
                {
                    throw new Exception("Dòng này chưa được phân bổ hoặc đã thu hồi trước đó.");
                }

                var ngaySXDate = DateOnly.FromDateTime(entity.NgaySX);
                var caByte = (byte)entity.Ca;

                // Loại dòng thuộc phiếu đã khóa (mirror GetAllAsync) để chỉ thu hồi đúng dòng đang hoạt động.
                var phanBoRecords = await (
                    from pl in _context.Hrc1PhuLieus
                    join tieuHao in _context.Hrc1TieuHaos on pl.MeID equals tieuHao.ID
                    where pl.PhuLieuID == entity.PhuLieuID &&
                          pl.IsPhanBo == true &&
                          tieuHao.NgaySanXuat == ngaySXDate &&
                          tieuHao.Ca == caByte &&
                          tieuHao.IsDeleted == false &&
                          (tieuHao.IDPhieu == null ||
                              _context.BmPhieus.Any(p => p.Idphieu == tieuHao.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                    select pl
                ).ToListAsync();

                if (phanBoRecords.Any())
                {
                    _context.Hrc1PhuLieus.RemoveRange(phanBoRecords);
                }

                totalRow.HasPhanBo = null;
                totalRow.TyLeBOF = null;
                totalRow.TyLeLF = null;
                totalRow.KLPB_BOF = null;
                totalRow.KLPB_LF = null;
                // Reset chờ bấm Phân bổ/Không phân bổ lại — không tính toán lại ở đây (theo yêu cầu người dùng).
                totalRow.KLTK_BOF = null;
                totalRow.KLTK_LF = null;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>
        /// Đối chiếu tính liên tục của sổ Xuất-Nhập-Tồn: với mỗi Scope (tổ hợp Lò/TL), ghép 2 PHIẾU
        /// liên tiếp theo thời gian (NgaySX, Ca) trong khoảng lọc, rồi full-outer-join theo PhuLieuID
        /// giữa 2 phiếu đó — bên trái lấy TonCuoiCa (phiếu ca trước), bên phải lấy TonDauCa (phiếu ca
        /// sau). Phụ liệu chỉ có ở 1 trong 2 phiếu (mới phát sinh, hoặc không còn dùng nữa) thì bên
        /// còn lại để trống thay vì bị bỏ qua khỏi kết quả.
        /// </summary>
        public async Task<List<STD_NXT_HRC1_NhapXuatTonRow>> GetNhapXuatTonAsync(STD_NXT_HRC1_NhapXuatTonSearchRequest request)
        {
            var tuNgay = request.TuNgay.Date;
            var denNgayExclusive = request.DenNgay.Date.AddDays(1);

            var rows = await (
                from d in _context.STD_XUAT_NHAP_TON_HRC1s
                join p in _context.BmPhieus on d.Id_Phieu equals p.Idphieu
                where p.IsDelete != 1
                      && d.NgaySX >= tuNgay
                      && d.NgaySX < denNgayExclusive
                select d
            ).ToListAsync();

            var buffer = new List<(STD_NXT_HRC1_NhapXuatTonRow Row, DateTime SortDate, int SortCa)>();

            foreach (var scopeGroup in rows.GroupBy(x => x.Scope))
            {
                var phieuList = scopeGroup
                    .GroupBy(x => x.Id_Phieu)
                    .Select(g => new
                    {
                        NgaySX = g.First().NgaySX,
                        Ca = g.First().Ca,
                        ByPhuLieu = g.ToDictionary(x => x.PhuLieuID),
                    })
                    .OrderBy(x => x.NgaySX)
                    .ThenBy(x => x.Ca)
                    .ToList();

                for (var i = 1; i < phieuList.Count; i++)
                {
                    var prevPhieu = phieuList[i - 1];
                    var currPhieu = phieuList[i];

                    var allPhuLieuIds = prevPhieu.ByPhuLieu.Keys
                        .Union(currPhieu.ByPhuLieu.Keys)
                        .OrderBy(id =>
                        {
                            var thuTu = (prevPhieu.ByPhuLieu.TryGetValue(id, out var pv) ? pv.ThuTu : null)
                                        ?? (currPhieu.ByPhuLieu.TryGetValue(id, out var cv) ? cv.ThuTu : null);
                            return thuTu ?? int.MaxValue;
                        })
                        .ThenBy(id => id);

                    foreach (var phuLieuId in allPhuLieuIds)
                    {
                        prevPhieu.ByPhuLieu.TryGetValue(phuLieuId, out var prevDetail);
                        currPhieu.ByPhuLieu.TryGetValue(phuLieuId, out var currDetail);

                        var row = new STD_NXT_HRC1_NhapXuatTonRow
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

        public async Task<bool> KhongPhanBoAsync(STD_NXT_HRC1_KhongPhanBoDto entity)
        {
            try
            {
                await CheckPhieuChotAsync(entity.NgaySX, entity.Ca);

                var totalRow = await _context.STD_NXT_TOTAL_HRC1s
                    .FirstOrDefaultAsync(x =>
                        x.PhuLieuID == entity.PhuLieuID &&
                        x.Id_Phieu == entity.IdPhieu);

                if (totalRow == null)
                {
                    throw new Exception("Không tìm thấy dữ liệu tổng hợp. Vui lòng lưu trước.");
                }
                if (totalRow.HasPhanBo == false)
                {
                    // Bấm lại lần 2 = hủy quyết định "Không phân bổ" — reset chờ quyết định lại, mirror
                    // ThuHoiPhanBoAsync.
                    totalRow.HasPhanBo = null;
                    totalRow.KLTK_BOF = null;
                    totalRow.KLTK_LF = null;
                }
                else
                {
                    totalRow.HasPhanBo = false;
                    var (klTkBof, klTkLf) = await ComputeKLTKAsync(
                        DateOnly.FromDateTime(entity.NgaySX), entity.Ca, entity.PhuLieuID);
                    totalRow.KLTK_BOF = klTkBof;
                    totalRow.KLTK_LF = klTkLf;
                }
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
    }
}
