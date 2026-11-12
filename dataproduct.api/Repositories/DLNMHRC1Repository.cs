using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.ResponseModels;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class DLNMHRC1Repository : IDLNMHRC1Repository
    {
        private readonly ProductFormContext _context;

        public DLNMHRC1Repository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Hrc1TieuHao>> GetAllAsync(DateOnly? ngaySanXuat, int? ca, int? scope, string? bieuMau = "BOF")
        {
            // Loại dòng thuộc phiếu đã bị khóa (IsLock=1 — phiếu cha có clone "Đề nghị hiệu chỉnh" đang mở,
            // xem PhieuService.CloneAsync) khỏi lưới nhập liệu, để không hiện song song dòng của phiếu cha
            // (đã đông cứng) lẫn dòng nhân bản riêng của clone (DLNMHRC1Service.DuplicateHrc1RowsForCloneAsync)
            // cho cùng 1 mẻ. IDPhieu==null (dòng NM dùng chung/dữ liệu cũ chưa gắn nhãn) luôn được tính.
            var query = _context.Hrc1TieuHaos
                .Where(x => x.IsDeleted == false)
                .Where(x => x.IDPhieu == null ||
                    _context.BmPhieus.Any(p => p.Idphieu == x.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                .AsQueryable();

            if (ngaySanXuat.HasValue)
                query = query.Where(x => x.NgaySanXuat == ngaySanXuat.Value);

            if (ca.HasValue)
                query = query.Where(x => x.Ca == ca.Value);

            if (scope.HasValue)
                query = query.Where(x => x.Scope == scope.Value);

            if (!string.IsNullOrEmpty(bieuMau))
                query = query.Where(x => x.BieuMau == bieuMau);

            return await query.OrderBy(x => x.MeThoi).ToListAsync();
        }

        public async Task<List<Hrc1GroupedByMeThoiModel>> GetAllGroupedBatchAsync(IEnumerable<Hrc1TieuHao> baseList)
        {
            var bases = baseList.ToList();
            if (bases.Count == 0) return new List<Hrc1GroupedByMeThoiModel>();

            var ids = bases.Select(x => x.ID).ToList();

            // Toàn bộ dòng phụ liệu (13 phụ liệu cố định + manual_col_*) cho các mẻ này
            var allPhuLieuRaw = await _context.Hrc1PhuLieus
                .Where(x => ids.Contains(x.MeID) && x.IsDeleted == false)
                .ToListAsync();

            // Danh mục 13 phụ liệu cố định — lấy tên hiển thị nếu dòng chưa denormalize TenPhuLieu
            var phuLieuNmIds = allPhuLieuRaw
                .Where(x => x.PhuLieuID.HasValue)
                .Select(x => x.PhuLieuID!.Value)
                .Distinct()
                .ToList();
            var phuLieuNmMap = phuLieuNmIds.Count > 0
                ? await _context.Hrc1PhuLieuNms.Where(n => phuLieuNmIds.Contains(n.ID)).ToDictionaryAsync(n => n.ID)
                : new Dictionary<int, Hrc1PhuLieuNm>();

            // Header_Key (bảng dùng chung với HRC2) cho các cột điều chỉnh tự do (manual_col_*)
            var headerKeyIds = allPhuLieuRaw
                .Where(x => x.ID_HeaderKey.HasValue)
                .Select(x => x.ID_HeaderKey!.Value)
                .Distinct()
                .ToList();
            var headerKeyMap = headerKeyIds.Count > 0
                ? await _context.Header_Keys.Where(k => headerKeyIds.Contains(k.Id)).ToDictionaryAsync(k => k.Id)
                : new Dictionary<int, Header_Key>();

            var byMeId = allPhuLieuRaw.GroupBy(x => x.MeID).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<Hrc1GroupedByMeThoiModel>(bases.Count);
            foreach (var b in bases)
            {
                var model = new Hrc1GroupedByMeThoiModel { data = MapData(b) };

                if (byMeId.TryGetValue(b.ID, out var rows))
                {
                    foreach (var r in rows)
                    {
                        if (r.PhuLieuID.HasValue)
                        {
                            phuLieuNmMap.TryGetValue(r.PhuLieuID.Value, out var nm);
                            var tenPhuLieu = r.TenPhuLieu ?? nm?.TenPhuLieu;

                            // Thứ tự cột phụ liệu trên form Tạo/Chi tiết theo đúng biểu mẫu của mẻ (BOF/LF) —
                            // xem Models/Hrc1PhuLieuNm.cs. Cột ThuTu đơn cũ đã bỏ.
                            var thuTuExcel = string.Equals(b.BieuMau, "LF", StringComparison.OrdinalIgnoreCase)
                                ? nm?.ThuTu_Excel_LF
                                : nm?.ThuTu_Excel_BOF;

                            // Record phân bổ chênh lệch (IsPhanBo=true, ghi bởi STD_XNT_HRC1Repository.PhanBoAsync)
                            // dùng CHUNG PhuLieuID với record đo thật (IsPhanBo=false) nhưng KHÁC dòng — phải tách
                            // riêng ra model.phanBoPhulieus (giống HRC2), KHÔNG được gộp chung vào model.phuLieus
                            // kẻo FE ghi đè giá trị đo thật bằng giá trị phân bổ khi 2 record trùng PhuLieuID.
                            // TRƯỚC ĐÂY tách theo cột KLPhanBo (thiết kế "1 cột trên cùng dòng") nhưng PhanBoAsync
                            // không bao giờ ghi cột đó (ghi KLPhuGia trên record IsPhanBo=true riêng) — khiến
                            // phanBoPhulieus luôn rỗng còn phuLieus lẫn cả 2 loại record.
                            if (r.IsPhanBo)
                            {
                                model.phanBoPhulieus.Add(new HeaderKeyGroupedByReportNoModel
                                {
                                    ID_PhuLieu = r.PhuLieuID.Value,
                                    TenPhuLieu = tenPhuLieu,
                                    KLPhuGia = (double?)r.KLPhuGia,
                                    KLPhuGiaTotal = (double?)r.KLPhuGia,
                                });
                            }
                            else
                            {
                                model.phuLieus.Add(new HeaderKeyGroupedByReportNoModel
                                {
                                    ID_PhuLieu = r.PhuLieuID.Value,
                                    TenPhuLieu = tenPhuLieu,
                                    KLPhuGia = (double?)r.KLPhuGia,
                                    KLPhuGiaTotal = (double?)r.KLPhuGia,
                                    IsManual = r.IsManual,
                                    KLPhuGia_Manual = (double?)r.KLPhuGia_Manual,
                                    ThuTu = thuTuExcel,
                                });
                            }
                        }
                        else if (r.IsManual && r.IsAddManual && r.ID_HeaderKey.HasValue)
                        {
                            headerKeyMap.TryGetValue(r.ID_HeaderKey.Value, out var hk);
                            model.manualAdjustPhulieus.Add(new HeaderKeyGroupedByReportNoModel
                            {
                                ID_HeaderKey = r.ID_HeaderKey.Value,
                                TenHienThi = hk?.TenHienThi,
                                KLPhuGia_Manual = (double?)r.KLPhuGia_Manual,
                                IsManual = true,
                            });
                        }
                    }
                }

                result.Add(model);
            }

            return result;
        }

        // =========================================================
        // Thống kê tiêu hao BOF/LF (search / sum) — dùng cho ThongKeTieuHaoHRC1.tsx
        // =========================================================

        private IQueryable<Hrc1TieuHao> BuildThongKeQuery(SearchThongKeHrc1 dto)
        {
            var bieuMau = string.IsNullOrWhiteSpace(dto.BieuMau) ? "BOF" : dto.BieuMau;
            // Loại dòng thuộc phiếu đã khóa (IsLock=1 — phiếu cha có clone "Đề nghị hiệu chỉnh" đang mở)
            // khỏi Thống kê, mirror GetAllAsync — tránh đếm đôi dòng gốc + dòng nhân bản riêng của clone.
            var query = _context.Hrc1TieuHaos
                .Where(x => x.BieuMau == bieuMau)
                .Where(x => x.IDPhieu == null ||
                    _context.BmPhieus.Any(p => p.Idphieu == x.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                .AsQueryable();

            if (dto.TuNgay.HasValue)
                query = query.Where(x => x.NgaySanXuat >= DateOnly.FromDateTime(dto.TuNgay.Value));
            if (dto.DenNgay.HasValue)
                query = query.Where(x => x.NgaySanXuat <= DateOnly.FromDateTime(dto.DenNgay.Value));
            if (dto.Ca.HasValue)
                query = query.Where(x => x.Ca == dto.Ca.Value);
            if (dto.Scope.HasValue)
                query = query.Where(x => x.Scope == dto.Scope.Value);
            if (!string.IsNullOrWhiteSpace(dto.SearchText))
                query = query.Where(x => x.MeThoi != null && x.MeThoi.Contains(dto.SearchText));
            if (dto.IsTrungMeThoi == true)
                query = query.Where(x => x.IsTrungMeThoi == true);

            query = dto.IsDelete == true ? query.Where(x => x.IsDeleted) : query.Where(x => !x.IsDeleted);

            return query;
        }

        // Gộp 1 dòng "đo thực"/chỉnh tay (IsPhanBo=false) + 1 dòng "phân bổ" (IsPhanBo=true, cùng
        // PhuLieuID nhưng LÀ BẢN GHI RIÊNG — xem STD_XNT_HRC1Repository.PhanBoAsync, PhanBo ghi lượng
        // phân bổ vào KLPhuGia của record IsPhanBo=true, KHÔNG ghi cột KLPhanBo) thành 1 giá trị hiển thị
        // duy nhất cho FE. Trước đây 2 record này bị đưa thẳng thành 2 phần tử Values cùng PhuLieuID
        // khiến FE (Map theo PhuLieuID) ghi đè, giá trị đo thực/chỉnh tay bị mất — chỉ còn thấy lượng
        // phân bổ. Mirror cách HRC2 tách 2 query rồi merge (DLNMHRC2Repository).
        private static Hrc1ThongKeValue MergePhuLieuValue(int phuLieuId, Hrc1PhuLieu? thucTe, Hrc1PhuLieu? phanBo)
        {
            double? klPhuGia = (double?)thucTe?.KLPhuGia;
            double? klPhuGiaManual = (double?)thucTe?.KLPhuGia_Manual;
            bool isManual = thucTe?.IsManual ?? false;
            double? klPhanBo = (double?)phanBo?.KLPhuGia;

            double? effective = isManual ? klPhuGiaManual : klPhuGia;
            double? total = (!effective.HasValue && !klPhanBo.HasValue) ? null : (effective ?? 0) + (klPhanBo ?? 0);

            return new Hrc1ThongKeValue
            {
                PhuLieuID = phuLieuId,
                KLPhuGia = klPhuGia,
                KLPhuGia_Manual = klPhuGiaManual,
                IsManual = isManual,
                KLPhanBo = klPhanBo,
                TotalKLPhuGia = total,
            };
        }

        public async Task<SearchThongKeHrc1ApiResponse> SearchThongKeApiAsync(SearchThongKeHrc1 dto)
        {
            var bieuMau = string.IsNullOrWhiteSpace(dto.BieuMau) ? "BOF" : dto.BieuMau;

            var query = BuildThongKeQuery(dto)
                .OrderByDescending(x => x.NgaySanXuat).ThenBy(x => x.Ca).ThenBy(x => x.Scope).ThenBy(x => x.MeThoi);

            var totalRecords = await query.CountAsync();

            var page = dto.Page is > 0 ? dto.Page.Value : 1;
            var pageSize = dto.PageSize is > 0 ? dto.PageSize.Value : 20;
            var pageItems = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Thứ tự cột phụ liệu trên bảng Thống kê tách riêng theo BOF/LF (ThuTu_TK_BOF/ThuTu_TK_LF)
            // vì 2 biểu mẫu dùng bộ phụ liệu khác nhau — xem Models/Hrc1PhuLieuNm.cs.
            var headerQueryBase = _context.Hrc1PhuLieuNms.Where(x => x.DangSuDung);
            var headerQuery = bieuMau == "LF"
                ? headerQueryBase.OrderBy(x => x.ThuTu_TK_LF ?? int.MaxValue).ThenBy(x => x.ID)
                : headerQueryBase.OrderBy(x => x.ThuTu_TK_BOF ?? int.MaxValue).ThenBy(x => x.ID);

            var headerTables = await headerQuery
                .Select(x => new Hrc1PhuLieuHeaderTable { PhuLieuID = x.ID, TenPhuLieu = x.TenPhuLieu })
                .ToListAsync();

            var meIds = pageItems.Select(x => x.ID).ToList();
            var plByMeId = meIds.Count > 0
                ? (await _context.Hrc1PhuLieus
                    .Where(x => meIds.Contains(x.MeID) && !x.IsDeleted && x.PhuLieuID.HasValue)
                    .ToListAsync())
                    .GroupBy(x => x.MeID)
                    .ToDictionary(g => g.Key, g => g.ToList())
                : new Dictionary<int, List<Hrc1PhuLieu>>();

            var rows = pageItems.Select(b =>
            {
                var row = new Hrc1ThongKeRow { Data = MapData(b) };
                if (plByMeId.TryGetValue(b.ID, out var pls))
                {
                    row.Values = pls
                        .GroupBy(p => p.PhuLieuID!.Value)
                        .Select(g => MergePhuLieuValue(
                            g.Key,
                            g.FirstOrDefault(x => !x.IsPhanBo),
                            g.FirstOrDefault(x => x.IsPhanBo)))
                        .ToList();
                }
                return row;
            }).ToList();

            return new SearchThongKeHrc1ApiResponse
            {
                PhuLieuHeaderTables = headerTables,
                Data = rows,
                TotalRecords = totalRecords,
                Page = page,
                PageSize = pageSize,
                TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalRecords / pageSize) : 0,
            };
        }

        public async Task<List<ThongKeSumItemHrc1>> GetThongKeSumAsync(SearchThongKeHrc1 dto)
        {
            var meIds = await BuildThongKeQuery(dto).Select(x => x.ID).ToListAsync();
            if (meIds.Count == 0) return new List<ThongKeSumItemHrc1>();

            var plRows = await _context.Hrc1PhuLieus
                .Where(x => meIds.Contains(x.MeID) && !x.IsDeleted && x.PhuLieuID.HasValue)
                .ToListAsync();
            if (plRows.Count == 0) return new List<ThongKeSumItemHrc1>();

            var nmNames = await _context.Hrc1PhuLieuNms.ToDictionaryAsync(x => x.ID, x => x.TenPhuLieu);

            // Gộp đo thực + phân bổ theo (MeID, PhuLieuID) trước khi cộng dồn theo PhuLieuID — nếu không,
            // mỗi mẻ có phân bổ sẽ bị CỘNG THÊM giá trị phân bổ (record IsPhanBo=true) như 1 dòng riêng
            // biệt trong tổng, chưa kể lượng đo thực đã tính; hoặc bỏ lỡ phân bổ nếu chỉ Sum theo record
            // đo thực. Mirror MergePhuLieuValue dùng ở SearchThongKeApiAsync.
            return plRows
                .GroupBy(x => new { x.MeID, PhuLieuID = x.PhuLieuID!.Value })
                .Select(g => MergePhuLieuValue(g.Key.PhuLieuID, g.FirstOrDefault(x => !x.IsPhanBo), g.FirstOrDefault(x => x.IsPhanBo)))
                .GroupBy(v => v.PhuLieuID)
                .Select(g => new ThongKeSumItemHrc1
                {
                    PhuLieuID = g.Key,
                    TenPhuLieu = nmNames.TryGetValue(g.Key, out var n) ? n : null,
                    TotalKLPhuGia = g.Sum(v => v.TotalKLPhuGia),
                })
                .ToList();
        }

        private static Hrc1TieuHao_ResponseModel MapData(Hrc1TieuHao b) => new Hrc1TieuHao_ResponseModel
        {
            ID = b.ID,
            BieuMau = b.BieuMau,
            Scope = b.Scope,
            MeThoi = b.MeThoi,
            MacThep = b.MacThep,
            MacThepOrig = b.MacThepOrig,
            MacThepIsManual = b.MacThepIsManual,
            IsNM = b.IsNM,
            IsEdited = b.IsEdited,
            IsChuyenCa = b.IsChuyenCa,
            IsTrungMeThoi = b.IsTrungMeThoi,
            KLGang = b.KLGang,
            KLGangLongCCT = b.KLGangLongCCT,
            KLGangLongTong = b.KLGangLongTong,
            KLThepPhe = b.KLThepPhe,
            KLThepPheOrig = b.KLThepPheOrig,
            KLThepPheIsManual = b.KLThepPheIsManual,
            KLThepPheGang = b.KLThepPheGang,
            KLThepLong = b.KLThepLong,
            O2 = b.O2,
            N2 = b.N2,
            AR = b.AR,
            QueLayMau = b.QueLayMau,
            QueDoNhiet = b.QueDoNhiet,
            GhiChu = b.GhiChu,
            NgaySanXuat = b.NgaySanXuat,
            Ca = b.Ca,
            ThoiDiemBatDau = b.ThoiDiemBatDau,
            ThoiDiemKetThuc = b.ThoiDiemKetThuc,
            ThoiGianLF = b.ThoiGianLF,
        };

        // =========================================================
        // Chuyển mẻ sang ca khác (trước/sau) — mirror DLNMHRC2Repository.ChuyenMeThoiAsync,
        // đổi target từ DLNM_HRC2 sang Hrc1TieuHao (đã đổi tên từ HRC1_TieuHao_BOF, dùng chung BOF/LF).
        // Không đụng Hrc1PhuLieu: khác PhuLieu_HRC2 (khớp theo MeThoi/BieuMau, không có Ngay/Ca riêng),
        // Hrc1PhuLieu khớp qua MeID (FK tới đúng dòng Hrc1TieuHao vừa cập nhật) nên tự "theo" mẻ, không cần sửa.
        // Lọc theo IDPhieu/IsLock (mirror GetAllAsync) để chỉ chuyển đúng dòng đang hoạt động — nếu có 1
        // clone "Đề nghị hiệu chỉnh" đang mở cùng MeThoi (dòng nhân bản riêng cho clone, xem
        // DLNMHRC1Service.DuplicateHrc1RowsForCloneAsync), dòng của phiếu cha đã khóa sẽ KHÔNG bị chuyển.
        public async Task<bool> ChuyenMeThoiAsync(ChuyenMeThoiRequest request)
        {
            int? caKQ = null;
            DateOnly? ngayKQ = null;
            if (request.ChuyenToiCa == 1) // chuyển về ca trước
            {
                if (request.Ca == 1) { caKQ = 2; ngayKQ = request.NgaySX.AddDays(-1); }
                else { caKQ = 1; ngayKQ = request.NgaySX; }
            }
            else // chuyển đến ca sau
            {
                if (request.Ca == 2) { caKQ = 1; ngayKQ = request.NgaySX.AddDays(1); }
                else { caKQ = 2; ngayKQ = request.NgaySX; }
            }

            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(x =>
                x.MaBm == request.MaBM && x.NgaySX == ngayKQ && x.Ca == caKQ &&
                x.Scope == request.Scope && x.IsDelete == 0 && x.IsLock == 0);

            if (phieu == null)
                throw new ApplicationException("Phiếu chưa được tạo");
            if (phieu.TinhTrang != 0 && phieu.TinhTrang != 3 && phieu.TinhTrang != 7)
                throw new ApplicationException("Phiếu đã được gửi đi nên không nhận mẻ chuyển");

            var items = await _context.Hrc1TieuHaos
                .Where(x => x.MeThoi == request.MeThoi && x.BieuMau == request.BieuMau && x.Scope == request.Scope && !x.IsDeleted)
                .Where(x => x.IDPhieu == null ||
                    _context.BmPhieus.Any(p => p.Idphieu == x.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                .ToListAsync();

            if (items.Count == 0)
                throw new ApplicationException("Phiếu không có dữ liệu");

            foreach (var item in items)
            {
                item.NgaySanXuat = ngayKQ;
                item.Ca = (byte?)caKQ;
                item.IsChuyenCa = true;
                item.NgayCapNhat = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            // IsTrungMeThoi tính theo (BieuMau, MeThoi), không phụ thuộc Ca/Ngày — recheck cho nhất quán
            // với mọi thao tác khác ảnh hưởng tới mẻ (thêm/sửa/xóa) đều gọi lại SP này.
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.SP_HRC1_BOF_CapNhatTrangThaiTrung @BieuMau={0}, @MeThoi={1}", request.BieuMau, request.MeThoi);

            return true;
        }

        /// <summary>
        /// Group tổng khối lượng phụ liệu đã dùng thực tế (KHÔNG cộng KLPhanBo — đó là output của chính
        /// module XNT này, tránh vòng lặp tự tham chiếu) theo (BieuMau, Scope, PhuLieuID) cho ngày/ca —
        /// dùng cho nút "Làm mới" trên Sổ Xuất-Nhập-Tồn HRC1. Đơn giản hơn hẳn bản HRC2
        /// (GetHRC2GroupedByMaterialAsync) vì không cần join Header_Mapping: HRC1 dùng thẳng danh mục cố
        /// định HRC1_PhuLieuNM, không có khái niệm "phụ liệu chưa map".
        /// </summary>
        public async Task<List<FilterSTD_NXTResponse_HRC1>> GetHRC1GroupedByMaterialAsync(DateTime ngaySX, int ca)
        {
            var ngay = DateOnly.FromDateTime(ngaySX);
            var caByte = (byte)ca;

            // Loại dòng thuộc phiếu đã khóa (IsLock=1) khỏi tổng — mirror GetAllAsync, tránh đếm đôi khi 1
            // clone "Đề nghị hiệu chỉnh" đã Duyệt: dòng của phiếu cha (khóa vĩnh viễn) và dòng nhân bản
            // riêng của clone cùng tồn tại cho 1 mẻ. IDPhieu==null (dòng NM/dữ liệu cũ) luôn được tính.
            var raw = await (
                from pl in _context.Hrc1PhuLieus
                join tieuHao in _context.Hrc1TieuHaos on pl.MeID equals tieuHao.ID
                where tieuHao.NgaySanXuat == ngay
                      && tieuHao.Ca == caByte
                      && tieuHao.IsDeleted == false
                      && pl.IsDeleted == false
                      && pl.IsPhanBo == false
                      && pl.PhuLieuID != null
                      && (tieuHao.IDPhieu == null ||
                          _context.BmPhieus.Any(p => p.Idphieu == tieuHao.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                select new
                {
                    tieuHao.BieuMau,
                    tieuHao.Scope,
                    pl.PhuLieuID,
                    pl.TenPhuLieu,
                    pl.IsManual,
                    pl.KLPhuGia,
                    pl.KLPhuGia_Manual,
                }
            ).ToListAsync();

            if (raw.Count == 0)
                return new List<FilterSTD_NXTResponse_HRC1>();

            var result = raw
                .GroupBy(x => new { x.BieuMau, Scope = x.Scope ?? 0, PhuLieuID = x.PhuLieuID!.Value })
                .Select(g => new FilterSTD_NXTResponse_HRC1
                {
                    BieuMau = g.Key.BieuMau ?? "",
                    Scope = g.Key.Scope,
                    PhuLieuID = g.Key.PhuLieuID,
                    TenPhuLieu = g.First().TenPhuLieu ?? "",
                    TotalKLPhuGia = g.Sum(x => x.IsManual ? (x.KLPhuGia_Manual ?? 0) : (x.KLPhuGia ?? 0)),
                })
                .ToList();

            return result;
        }
    }
}
