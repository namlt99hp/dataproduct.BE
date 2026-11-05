using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tab "Sản lượng cốc + Dập cốc" của LC_BCSX_XNL_TP (TP1.xlsx dòng 218-234/264-267,
    // TP2.xlsx dòng 178-191). Dập cốc + Cốc chuyển qua X.NL,TP2 là bảng NHẬP TAY bình thường.
    // "Sản lượng cốc" KHÔNG lưu bảng riêng — tính lại mỗi lần Get/Save theo cân bằng khối
    // lượng: SảnLượng(cụm) = [Σ dùng-chung × tỉ lệ cụm] + [Σ riêng-cụm], với:
    //   - "dùng chung"/"riêng cụm" lấy từ ID_CumSanXuat gắn trên LC_SiloTon (Δ tồn silo) và
    //     LC_XuatCapDanhMucCum (Σ xuất đi) — NULL = dùng chung, có giá trị = riêng 1 cụm.
    //   - tỉ lệ cụm = LC_TP_DapCoc.KhoiLuongCocTrongCa (Tháp dập + CDQ) của cụm / tổng.
    //   - Bụi cốc KHÔNG có số hạng tồn silo (Excel gốc không dùng), chỉ từ Cấp cốc.
    //   - Cốc chuyển qua X.NL,TP2 (CHỈ Scope=1): Δ Nhà sàng 2 vào nhóm dùng chung, Δ Nhà sàng 3
    //     cộng thẳng vào cụm "125" — xem công thức K221/O221 TP1.xlsx (G267 trong dấu nhân tỉ
    //     lệ, N267 cộng thẳng ngoài).
    // LƯU Ý (CHƯA xác nhận hoàn toàn với nghiệp vụ, xem .claude/state/LuyenCoc.md): mặc định
    // ID_CumSanXuat = NULL (dùng chung) cho MỌI silo/nơi nhận hiện có — người dùng cần tự gán
    // lại qua trang quản lý Silo/Cấp cốc để khớp đúng Excel gốc theo từng cột/nơi nhận cụ thể.
    public class LC_SanLuongCocRepository : ILC_SanLuongCocRepository
    {
        private readonly ProductFormContext _context;

        private static readonly (string LoaiCoc, bool CoTonSilo)[] LoaiCocList =
        {
            ("10-80mm", true),
            ("<10mm", true),
            ("Bụi cốc", false),
        };

        public LC_SanLuongCocRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<List<LcCumSanXuatDto>> GetCumSanXuatListAsync(int? scope)
        {
            return await _context.LC_TP_CumSanXuat.AsNoTracking()
                .Where(x => !x.IsDelete && (scope == null || x.Scope == scope))
                .OrderBy(x => x.Scope).ThenBy(x => x.ThuTu)
                .Select(x => new LcCumSanXuatDto { Id = x.Id, Scope = x.Scope, Ma = x.Ma, Ten = x.Ten, ThuTu = x.ThuTu })
                .ToListAsync();
        }

        public async Task<List<LcViTriDapCocDto>> GetViTriDapCocListAsync(int? scope)
        {
            return await (
                from v in _context.LC_TP_ViTriDapCoc.AsNoTracking()
                join c in _context.LC_TP_CumSanXuat.AsNoTracking() on v.ID_CumSanXuat equals c.Id
                where !v.IsDelete && !c.IsDelete && (scope == null || v.Scope == scope)
                orderby v.Scope, v.ThuTu
                select new LcViTriDapCocDto
                {
                    Id = v.Id,
                    Scope = v.Scope,
                    IdCumSanXuat = v.ID_CumSanXuat,
                    CumSanXuatMa = c.Ma,
                    CumSanXuatTen = c.Ten,
                    Loai = v.Loai,
                    Ten = v.Ten,
                    ThuTu = v.ThuTu,
                }
            ).ToListAsync();
        }

        public async Task<SanLuongCocTabDto> GetTabAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            var scope = phieu.Scope ?? 0;

            var viTris = await GetViTriDapCocListAsync(scope);
            var dapCocRows = await _context.LC_TP_DapCoc.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.ID_ViTri);

            var dapCocDtos = viTris.Select(v =>
            {
                dapCocRows.TryGetValue(v.Id, out var row);
                return new DapCocRowDto
                {
                    IdViTri = v.Id,
                    IdCumSanXuat = v.IdCumSanXuat,
                    CumSanXuatTen = v.CumSanXuatTen,
                    Loai = v.Loai,
                    Ten = v.Ten,
                    BanhCocTiepNhan = row?.BanhCocTiepNhan,
                    LuyKeBanhCocTiepNhan = row?.LuyKeBanhCocTiepNhan,
                    KhoiLuongCocTrongCa = row?.KhoiLuongCocTrongCa,
                    LuyKeKhoiLuongCocTrongCa = row?.LuyKeKhoiLuongCocTrongCa,
                };
            }).ToList();

            var cocChuyenDtos = new List<CocChuyenNhaSangRowDto>();
            if (scope == 1)
            {
                var rows = await _context.LC_TP_CocChuyenNhaSang.AsNoTracking()
                    .Where(x => x.IDPhieu == idPhieu)
                    .ToDictionaryAsync(x => x.NhaSang);
                foreach (var nhaSang in new[] { "NS2", "NS3" })
                {
                    rows.TryGetValue(nhaSang, out var row);
                    cocChuyenDtos.Add(new CocChuyenNhaSangRowDto
                    {
                        NhaSang = nhaSang,
                        TonCuoi = row?.TonCuoi,
                        TonDau = row?.TonDau,
                        LuyKe = row?.LuyKe,
                    });
                }
            }

            var sanLuong = await ComputeSanLuongCocAsync(idPhieu, scope, phieu.NgaySX, phieu.Ca);

            return new SanLuongCocTabDto { DapCoc = dapCocDtos, CocChuyenNhaSang = cocChuyenDtos, SanLuong = sanLuong };
        }

        public async Task<SanLuongCocTabDto> SaveTabAsync(Guid idPhieu, SanLuongCocTabSaveDto dto)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX;
            var ca = phieu.Ca;

            var viTriById = (await _context.LC_TP_ViTriDapCoc.AsNoTracking()
                    .Where(v => v.Scope == scope && !v.IsDelete)
                    .ToListAsync())
                .ToDictionary(v => v.Id);

            var existingDapCoc = await _context.LC_TP_DapCoc
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.ID_ViTri);

            foreach (var d in dto.DapCoc)
            {
                if (!viTriById.ContainsKey(d.IdViTri)) continue;

                if (!existingDapCoc.TryGetValue(d.IdViTri, out var row))
                {
                    row = new LC_TP_DapCoc { IDPhieu = idPhieu, ID_ViTri = d.IdViTri, NgayTao = DateTime.Now };
                    _context.LC_TP_DapCoc.Add(row);
                    existingDapCoc[d.IdViTri] = row;
                }

                row.NgaySX = ngaySx;
                row.Ca = ca;
                row.Scope = scope;
                row.BanhCocTiepNhan = d.BanhCocTiepNhan;
                row.KhoiLuongCocTrongCa = d.KhoiLuongCocTrongCa;
                row.LuyKeBanhCocTiepNhan = await ComputeLuyKeDapCocAsync(
                    idPhieu, scope, ngaySx, ca, d.IdViTri, d.BanhCocTiepNhan, x => x.LuyKeBanhCocTiepNhan);
                row.LuyKeKhoiLuongCocTrongCa = await ComputeLuyKeDapCocAsync(
                    idPhieu, scope, ngaySx, ca, d.IdViTri, d.KhoiLuongCocTrongCa, x => x.LuyKeKhoiLuongCocTrongCa);
            }

            if (scope == 1)
            {
                var existingChuyen = await _context.LC_TP_CocChuyenNhaSang
                    .Where(x => x.IDPhieu == idPhieu)
                    .ToDictionaryAsync(x => x.NhaSang);

                foreach (var d in dto.CocChuyenNhaSang)
                {
                    if (d.NhaSang != "NS2" && d.NhaSang != "NS3") continue;

                    if (!existingChuyen.TryGetValue(d.NhaSang, out var row))
                    {
                        row = new LC_TP_CocChuyenNhaSang { IDPhieu = idPhieu, NhaSang = d.NhaSang, NgayTao = DateTime.Now };
                        _context.LC_TP_CocChuyenNhaSang.Add(row);
                        existingChuyen[d.NhaSang] = row;
                    }

                    row.NgaySX = ngaySx;
                    row.Ca = ca;
                    row.Scope = scope;
                    row.TonCuoi = d.TonCuoi;
                    row.TonDau = d.TonDau;
                    var chenhLech = d.TonCuoi == null && d.TonDau == null ? (decimal?)null : (d.TonCuoi ?? 0) - (d.TonDau ?? 0);
                    row.LuyKe = await ComputeLuyKeChuyenAsync(idPhieu, scope, ngaySx, ca, d.NhaSang, chenhLech);
                }
            }

            await _context.SaveChangesAsync();
            return await GetTabAsync(idPhieu);
        }

        public async Task InitializeAsync(BmPhieu phieu)
        {
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;
            if (await _context.LC_TP_DapCoc.AnyAsync(x => x.IDPhieu == phieu.Idphieu)) return; // đã init rồi

            var scope = phieu.Scope ?? 0;
            var viTris = await _context.LC_TP_ViTriDapCoc.AsNoTracking()
                .Where(v => v.Scope == scope && !v.IsDelete)
                .ToListAsync();

            foreach (var v in viTris)
            {
                _context.LC_TP_DapCoc.Add(new LC_TP_DapCoc
                {
                    IDPhieu = phieu.Idphieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    ID_ViTri = v.Id,
                    NgayTao = DateTime.Now,
                });
            }

            if (scope == 1)
            {
                foreach (var nhaSang in new[] { "NS2", "NS3" })
                {
                    _context.LC_TP_CocChuyenNhaSang.Add(new LC_TP_CocChuyenNhaSang
                    {
                        IDPhieu = phieu.Idphieu,
                        NgaySX = phieu.NgaySX,
                        Ca = phieu.Ca,
                        Scope = scope,
                        NhaSang = nhaSang,
                        NgayTao = DateTime.Now,
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        // Mốc reset Ca1/ngày1 hàng tháng (quy ước chung module) — khoá theo ID_ViTri, mirror
        // LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync.
        private async Task<decimal?> ComputeLuyKeDapCocAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, int idViTri, decimal? giaTriCaNay,
            System.Linq.Expressions.Expression<Func<LC_TP_DapCoc, decimal?>> luyKeField)
        {
            if (giaTriCaNay == null) return null;
            if (ngaySx == null || ca == null) return giaTriCaNay;
            if (ngaySx.Value.Day == 1 && ca == 1) return giaTriCaNay;

            var prev = await _context.LC_TP_DapCoc.AsNoTracking()
                .Where(x => x.Scope == scope && x.ID_ViTri == idViTri && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca)
                .Select(luyKeField)
                .FirstOrDefaultAsync();
            return (prev ?? 0) + giaTriCaNay.Value;
        }

        // Cùng mốc reset, khoá theo NhaSang ('NS2'|'NS3') — LuyKe của khối lượng CHÊNH LỆCH.
        private async Task<decimal?> ComputeLuyKeChuyenAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, string nhaSang, decimal? chenhLechCaNay)
        {
            if (chenhLechCaNay == null) return null;
            if (ngaySx == null || ca == null) return chenhLechCaNay;
            if (ngaySx.Value.Day == 1 && ca == 1) return chenhLechCaNay;

            var prev = await _context.LC_TP_CocChuyenNhaSang.AsNoTracking()
                .Where(x => x.Scope == scope && x.NhaSang == nhaSang && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca)
                .Select(x => x.LuyKe)
                .FirstOrDefaultAsync();
            return (prev ?? 0) + chenhLechCaNay.Value;
        }

        // ─── "Sản lượng cốc" — tính cho CHÍNH 1 phiếu (TrongCa), dùng lại cho cả Get (phiếu
        // hiện tại) và cho việc cộng dồn Lũy kế qua nhiều phiếu trong SAME kỳ (xem
        // ComputeSanLuongCocAsync) — KHÔNG lưu bảng riêng, tính thẳng từ 3 nguồn mỗi lần gọi.

        private sealed record XuatCapRow(Guid IDPhieu, string LoaiCoc, decimal? KLQuyKho, int? ID_DanhMucCum);
        private sealed record SiloTonRow(Guid IDPhieu, int ID_SiloTon, decimal? ChenhLech1080, decimal? ChenhLechDuoi10);
        private sealed record ChuyenRow(Guid IDPhieu, string NhaSang, decimal? TonCuoi, decimal? TonDau);
        private sealed record DapCocRow(Guid IDPhieu, int ID_ViTri, decimal? KhoiLuongCocTrongCa);

        private async Task<List<SanLuongCocRowDto>> ComputeSanLuongCocAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca)
        {
            var cums = await _context.LC_TP_CumSanXuat.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .OrderBy(c => c.ThuTu)
                .ToListAsync();
            if (cums.Count == 0) return new();

            // Toàn bộ phiếu trong "kỳ lũy kế" hiện tại (từ mốc Ca1/ngày1 gần nhất tới phiếu này,
            // CÙNG Scope) — không lưu lũy kế riêng cho Sản lượng cốc nên phải cộng dồn từ đây.
            var periodPhieuIds = await GetPeriodPhieuIdsAsync(scope, ngaySx, ca, idPhieu);

            // 3 nguồn, mỗi nguồn 1 query cho CẢ kỳ (tránh N+1 theo số phiếu), gộp sẵn theo
            // IDPhieu để vòng lặp dưới không phải quét lại toàn bộ danh sách mỗi lần.
            var viTriCumById = await _context.LC_TP_ViTriDapCoc.AsNoTracking()
                .Where(v => v.Scope == scope && !v.IsDelete)
                .ToDictionaryAsync(v => v.Id, v => v.ID_CumSanXuat);

            var dapCocByPhieu = (await _context.LC_TP_DapCoc.AsNoTracking()
                    .Where(x => periodPhieuIds.Contains(x.IDPhieu))
                    .Select(x => new DapCocRow(x.IDPhieu, x.ID_ViTri, x.KhoiLuongCocTrongCa))
                    .ToListAsync())
                .GroupBy(x => x.IDPhieu).ToDictionary(g => g.Key, g => g.ToList());

            var xuatCapCumById = await _context.LC_XuatCapDanhMucCum.AsNoTracking()
                .Where(c => c.Scope == scope)
                .ToDictionaryAsync(c => c.ID, c => c.ID_CumSanXuat);

            var xuatCapByPhieu = (await _context.LC_XuatCap.AsNoTracking()
                    .Where(x => periodPhieuIds.Contains(x.IDPhieu) && x.Scope == scope)
                    .Select(x => new XuatCapRow(x.IDPhieu, x.LoaiCoc, x.KLQuyKho, x.ID_DanhMucCum))
                    .ToListAsync())
                .GroupBy(x => x.IDPhieu).ToDictionary(g => g.Key, g => g.ToList());

            var siloCumById = await _context.LC_SiloTon.AsNoTracking()
                .Where(s => s.Scope == scope && !s.IsDelete)
                .ToDictionaryAsync(s => s.ID, s => s.ID_CumSanXuat);

            var siloTonByPhieu = (await _context.LC_SiloCocTon.AsNoTracking()
                    .Where(s => periodPhieuIds.Contains(s.IDPhieu) && s.Scope == scope)
                    .Select(s => new SiloTonRow(s.IDPhieu, s.ID_SiloTon, s.ChenhLech1080, s.ChenhLechDuoi10))
                    .ToListAsync())
                .GroupBy(x => x.IDPhieu).ToDictionary(g => g.Key, g => g.ToList());

            var chuyenByPhieu = scope == 1
                ? (await _context.LC_TP_CocChuyenNhaSang.AsNoTracking()
                        .Where(x => periodPhieuIds.Contains(x.IDPhieu))
                        .Select(x => new ChuyenRow(x.IDPhieu, x.NhaSang, x.TonCuoi, x.TonDau))
                        .ToListAsync())
                    .GroupBy(x => x.IDPhieu).ToDictionary(g => g.Key, g => g.ToList())
                : new Dictionary<Guid, List<ChuyenRow>>();

            var cum125Id = cums.FirstOrDefault(c => c.Ma == "125")?.Id;

            // Tỉ lệ Dập cốc theo cụm, TÍNH SẴN cho mỗi phiếu trong kỳ (dùng lại cho cả 3 loại cốc).
            var tyLeByPhieu = new Dictionary<Guid, Dictionary<int, decimal>>();
            foreach (var pid in periodPhieuIds)
            {
                var rows = dapCocByPhieu.GetValueOrDefault(pid) ?? new List<DapCocRow>();
                var tongDap = rows.Sum(x => x.KhoiLuongCocTrongCa ?? 0);
                var tyLeCum = new Dictionary<int, decimal>();
                foreach (var cum in cums)
                {
                    var dapCum = rows.Where(x => viTriCumById.TryGetValue(x.ID_ViTri, out var cid) && cid == cum.Id)
                        .Sum(x => x.KhoiLuongCocTrongCa ?? 0);
                    tyLeCum[cum.Id] = tongDap > 0 ? dapCum / tongDap : 0;
                }
                tyLeByPhieu[pid] = tyLeCum;
            }

            var result = new List<SanLuongCocRowDto>();
            foreach (var (loaiCoc, coTonSilo) in LoaiCocList)
            {
                var row = new SanLuongCocRowDto { LoaiCoc = loaiCoc };
                var luyKeByCum = cums.ToDictionary(c => c.Id, c => 0m);
                decimal trongCaTong = 0, luyKeTong = 0;
                var trongCaByCum = new Dictionary<int, decimal>();

                foreach (var pid in periodPhieuIds)
                {
                    var (shared, directByCum) = ComputeSharedAndDirect(
                        scope, loaiCoc, coTonSilo,
                        xuatCapByPhieu.GetValueOrDefault(pid) ?? new List<XuatCapRow>(), xuatCapCumById,
                        siloTonByPhieu.GetValueOrDefault(pid) ?? new List<SiloTonRow>(), siloCumById,
                        chuyenByPhieu.GetValueOrDefault(pid) ?? new List<ChuyenRow>(), cum125Id);

                    foreach (var cum in cums)
                    {
                        var giaTri = shared * tyLeByPhieu[pid][cum.Id] + directByCum.GetValueOrDefault(cum.Id);
                        luyKeByCum[cum.Id] += giaTri;
                        luyKeTong += giaTri;
                        if (pid == idPhieu)
                        {
                            trongCaByCum[cum.Id] = giaTri;
                            trongCaTong += giaTri;
                        }
                    }
                }

                foreach (var cum in cums)
                {
                    row.TheoCum.Add(new SanLuongCocCumDto
                    {
                        IdCumSanXuat = cum.Id,
                        CumSanXuatTen = cum.Ten,
                        TrongCa = Math.Round(trongCaByCum.GetValueOrDefault(cum.Id), 3),
                        LuyKe = Math.Round(luyKeByCum[cum.Id], 3),
                    });
                }
                row.TongTrongCa = Math.Round(trongCaTong, 3);
                row.LuyKeTong = Math.Round(luyKeTong, 3);
                result.Add(row);
            }

            return result;
        }

        // Σ dùng chung (shared, ID_CumSanXuat NULL) + Σ riêng từng cụm (direct), CHO 1 PHIẾU,
        // từ Cấp cốc (Σ xuất đi) + Cốc tồn silo (Δ tồn) + Cốc chuyển qua X.NL,TP2 (TP1 only,
        // chỉ áp cho loại '10-80mm' — đúng theo công thức gốc, KHÔNG áp cho <10mm/Bụi cốc).
        private static (decimal Shared, Dictionary<int, decimal> DirectByCum) ComputeSharedAndDirect(
            int scope, string loaiCoc, bool coTonSilo,
            List<XuatCapRow> xuatCapRows, Dictionary<int, int?> xuatCapCumById,
            List<SiloTonRow> siloTonRows, Dictionary<int, int?> siloCumById,
            List<ChuyenRow> chuyenRows, int? cum125Id)
        {
            decimal shared = 0;
            var direct = new Dictionary<int, decimal>();

            foreach (var x in xuatCapRows)
            {
                bool match = loaiCoc == "Bụi cốc" ? x.LoaiCoc.StartsWith("Bụi") : x.LoaiCoc == loaiCoc;
                if (!match) continue;

                var cumId = x.ID_DanhMucCum != null && xuatCapCumById.TryGetValue(x.ID_DanhMucCum.Value, out var c) ? c : null;
                var gt = x.KLQuyKho ?? 0m;
                if (cumId == null) shared += gt;
                else direct[cumId.Value] = direct.GetValueOrDefault(cumId.Value) + gt;
            }

            if (coTonSilo)
            {
                foreach (var s in siloTonRows)
                {
                    var delta = loaiCoc == "10-80mm" ? s.ChenhLech1080 : s.ChenhLechDuoi10;
                    if (delta == null) continue;

                    var cumId = siloCumById.TryGetValue(s.ID_SiloTon, out var c) ? c : null;
                    if (cumId == null) shared += delta.Value;
                    else direct[cumId.Value] = direct.GetValueOrDefault(cumId.Value) + delta.Value;
                }
            }

            if (scope == 1 && loaiCoc == "10-80mm")
            {
                foreach (var ch in chuyenRows)
                {
                    if (ch.TonCuoi == null && ch.TonDau == null) continue;
                    var delta = (ch.TonCuoi ?? 0m) - (ch.TonDau ?? 0m);
                    if (ch.NhaSang == "NS2") shared += delta;
                    else if (ch.NhaSang == "NS3" && cum125Id != null)
                        direct[cum125Id.Value] = direct.GetValueOrDefault(cum125Id.Value) + delta;
                }
            }

            return (shared, direct);
        }

        private async Task<List<Guid>> GetPeriodPhieuIdsAsync(int scope, DateOnly? ngaySx, int? ca, Guid currentIdPhieu)
        {
            if (ngaySx == null || ca == null) return new List<Guid> { currentIdPhieu };

            var resetDate = new DateOnly(ngaySx.Value.Year, ngaySx.Value.Month, 1);
            var ids = await _context.BmPhieus.AsNoTracking()
                .Where(p => p.Scope == scope
                    && p.NgaySX >= resetDate
                    && (p.NgaySX < ngaySx || (p.NgaySX == ngaySx && p.Ca <= ca)))
                .Select(p => p.Idphieu)
                .ToListAsync();
            if (!ids.Contains(currentIdPhieu)) ids.Add(currentIdPhieu);
            return ids;
        }
    }
}
