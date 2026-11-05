using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NMLC
{
    public class LC_ThanhPhamRepository : ILC_ThanhPhamRepository
    {
        private readonly ProductFormContext _context;

        public LC_ThanhPhamRepository(ProductFormContext context)
        {
            _context = context;
        }

        // ─── Cụm tháp (LC_CumThap) ───

        public async Task<List<LC_CumThap>> GetAllNhomAsync(string? searchKey = null, int? scope = null)
        {
            var query = _context.LC_CumThap.AsNoTracking().Where(x => !x.IsDelete);

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                query = query.Where(x => (x.Ten != null && x.Ten.Contains(searchKey))
                                      || x.Ma.Contains(searchKey));
            }

            if (scope.HasValue)
                query = query.Where(x => x.Scope == scope.Value);

            return await query.OrderBy(x => x.ThuTu).ThenBy(x => x.ID).ToListAsync();
        }

        public async Task<LC_CumThap?> GetNhomByIdAsync(int id)
        {
            return await _context.LC_CumThap
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
        }

        public async Task<bool> NhomExistsAsync(int id)
        {
            return await _context.LC_CumThap.AnyAsync(x => x.ID == id && !x.IsDelete);
        }

        public async Task<LC_CumThap> AddNhomAsync(LC_CumThap entity)
        {
            entity.IsDelete = false;
            entity.NgayTao = DateTime.Now;
            _context.LC_CumThap.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_CumThap?> UpdateNhomAsync(LC_CumThap entity)
        {
            var existing = await _context.LC_CumThap
                .FirstOrDefaultAsync(x => x.ID == entity.ID && !x.IsDelete);
            if (existing == null) return null;

            existing.Scope = entity.Scope;
            existing.Ma = entity.Ma;
            existing.Ten = entity.Ten;
            existing.ThuTu = entity.ThuTu;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteNhomAsync(int id)
        {
            var existing = await _context.LC_CumThap
                .FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;

            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Phễu ───

        public async Task<List<LC_TP_ThapThan_Pheu>> GetAllPheuAsync(
            int? thapThanId = null, string? searchKey = null, int? scope = null)
        {
            // Join cụm tháp: chỉ lấy phễu của cụm còn hiệu lực, lọc được theo xưởng (scope)
            var query =
                from p in _context.LC_TP_ThapThan_Pheu.AsNoTracking()
                join c in _context.LC_CumThap.AsNoTracking() on p.ThapThanId equals c.ID
                where !p.IsDelete && !c.IsDelete
                select new { p, c };

            if (thapThanId.HasValue)
                query = query.Where(x => x.p.ThapThanId == thapThanId.Value);

            if (scope.HasValue)
                query = query.Where(x => x.c.Scope == scope.Value);

            if (!string.IsNullOrWhiteSpace(searchKey))
                query = query.Where(x => x.p.TenPheu != null && x.p.TenPheu.Contains(searchKey));

            // Thứ tự: cụm tháp (ThuTu), rồi thứ tự phễu (ThuTu; chưa đặt thì xếp cuối), rồi tên phễu
            return await query
                .OrderBy(x => x.c.ThuTu).ThenBy(x => x.c.ID)
                .ThenBy(x => x.p.ThuTu ?? int.MaxValue).ThenBy(x => x.p.TenPheu).ThenBy(x => x.p.Id)
                .Select(x => x.p)
                .ToListAsync();
        }

        public async Task<LC_TP_ThapThan_Pheu?> GetPheuByIdAsync(int id)
        {
            return await _context.LC_TP_ThapThan_Pheu
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<bool> PheuExistsAsync(int id)
        {
            return await _context.LC_TP_ThapThan_Pheu.AnyAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<LC_TP_ThapThan_Pheu> AddPheuAsync(LC_TP_ThapThan_Pheu entity)
        {
            entity.IsDelete = false;
            _context.LC_TP_ThapThan_Pheu.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_TP_ThapThan_Pheu?> UpdatePheuAsync(LC_TP_ThapThan_Pheu entity)
        {
            var existing = await _context.LC_TP_ThapThan_Pheu
                .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDelete);
            if (existing == null) return null;

            existing.ThapThanId = entity.ThapThanId;
            existing.TenPheu = entity.TenPheu;
            existing.ThuTu = entity.ThuTu;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeletePheuAsync(int id)
        {
            var existing = await _context.LC_TP_ThapThan_Pheu
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
            if (existing == null) return false;

            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Tab Tháp/Phễu (LC_TP_TonPheu / LC_TP_CapThap) — API by-phieu riêng, KHÔNG qua
        // DataJson, cùng nguyên tắc với LC_PhoiTronRepository. Công thức áp dụng:
        // F06a (Scope=1)/F06b (Scope=2) cho KLAmTonCuoi/KLTonCuoiQuyKho của Phễu; F07
        // (CROSS_REGION từ LC_PhoiTron) cho CapLenThapQuyKho; F08 (CUMULATIVE, mốc reset
        // Ca1/ngày1 — xem LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync) cho LuyKeCapLenThap/
        // LuyKeNapLo; PREVIOUS_SHIFT cho KLTonDauQuyKho của Phễu.
        // Xem .claude/LuyenCoc/LC_01_PhanTich_ThietKe_TP1_TP2.md mục 5 (DANH MỤC CÔNG THỨC).

        public async Task<ThapPheuTabDto> GetThapPheuTabAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            var scope = phieu.Scope ?? 0;

            var cumThaps = await _context.LC_CumThap.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .OrderBy(c => c.ThuTu).ThenBy(c => c.ID)
                .ToListAsync();
            var pheus = await (
                from p in _context.LC_TP_ThapThan_Pheu.AsNoTracking()
                join c in _context.LC_CumThap.AsNoTracking() on p.ThapThanId equals c.ID
                where !p.IsDelete && c.Scope == scope && !c.IsDelete
                orderby c.ThuTu, c.ID, p.ThuTu ?? int.MaxValue, p.Id
                select p
            ).ToListAsync();

            var tonPheuRows = await _context.LC_TP_TonPheu.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.PheuId);
            var capThapRows = await _context.LC_TP_CapThap.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.CumThapId);

            var capLenThapByCum = await ComputeCapLenThapMapAsync(idPhieu, scope);

            var thapDtos = cumThaps.Select(cum =>
            {
                capThapRows.TryGetValue(cum.ID, out var row);
                return new CapThapDto
                {
                    CumThapId = cum.ID,
                    CumThapMa = cum.Ma,
                    DoAmThap = row?.DoAmThap,
                    CapLenThapQuyKho = capLenThapByCum.GetValueOrDefault(cum.ID),
                    LuyKeCapLenThap = row?.LuyKeCapLenThap,
                    NapLoQuyKho = row?.NapLoQuyKho,
                    LuyKeNapLo = row?.LuyKeNapLo,
                    GhiChu = row?.GhiChu,
                };
            }).ToList();

            var pheuDtos = pheus.Select(p =>
            {
                tonPheuRows.TryGetValue(p.Id, out var row);
                return new TonPheuDto
                {
                    PheuId = p.Id,
                    ThapThanId = p.ThapThanId,
                    TenPheu = p.TenPheu,
                    KLTonDauQuyKho = row?.KLTonDauQuyKho,
                    MucRadar = row?.MucRadar,
                    KLAmTonCuoi = row?.KLAmTonCuoi,
                    KLTonCuoiQuyKho = row?.KLTonCuoiQuyKho,
                    GhiChu = row?.GhiChu,
                };
            }).ToList();

            return new ThapPheuTabDto { Pheu = pheuDtos, Thap = thapDtos };
        }

        public async Task<ThapPheuTabDto> SaveThapPheuTabAsync(Guid idPhieu, ThapPheuTabSaveDto dto)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX;
            var ca = phieu.Ca;

            var pheuMaster = await (
                from p in _context.LC_TP_ThapThan_Pheu.AsNoTracking()
                join c in _context.LC_CumThap.AsNoTracking() on p.ThapThanId equals c.ID
                where !p.IsDelete && c.Scope == scope && !c.IsDelete
                select p
            ).ToListAsync();
            var pheuMasterById = pheuMaster.ToDictionary(p => p.Id);

            var cumThaps = await _context.LC_CumThap.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .ToDictionaryAsync(c => c.ID);

            var existingTonPheu = await _context.LC_TP_TonPheu
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.PheuId);
            var existingCapThap = await _context.LC_TP_CapThap
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.CumThapId);

            // Lưu Tháp trước — Phễu cần DoAmThap của Tháp chứa nó để tính F06a/F06b.
            var doAmThapByCum = new Dictionary<int, decimal?>();
            foreach (var d in dto.Thap)
            {
                if (!cumThaps.TryGetValue(d.CumThapId, out var cum)) continue;
                doAmThapByCum[d.CumThapId] = d.DoAmThap;

                if (!existingCapThap.TryGetValue(d.CumThapId, out var row))
                {
                    row = new LC_TP_CapThap { IDPhieu = idPhieu, CumThapId = d.CumThapId, NgayTao = DateTime.Now };
                    _context.LC_TP_CapThap.Add(row);
                    existingCapThap[d.CumThapId] = row;
                }

                row.NgaySX = ngaySx;
                row.Ca = ca;
                row.Scope = scope;
                row.DoAmThap = d.DoAmThap;
                row.NapLoQuyKho = d.NapLoQuyKho;
                row.GhiChu = d.GhiChu;
                row.CapLenThapQuyKho = await ComputeCapLenThapAsync(idPhieu, scope, d.CumThapId);
                row.LuyKeCapLenThap = await ComputeLuyKeThapAsync(
                    idPhieu, scope, ngaySx, ca, d.CumThapId, row.CapLenThapQuyKho, x => x.LuyKeCapLenThap);
                row.LuyKeNapLo = await ComputeLuyKeThapAsync(
                    idPhieu, scope, ngaySx, ca, d.CumThapId, d.NapLoQuyKho, x => x.LuyKeNapLo);
            }

            foreach (var d in dto.Pheu)
            {
                if (!pheuMasterById.TryGetValue(d.PheuId, out var pheuInfo)) continue;

                if (!existingTonPheu.TryGetValue(d.PheuId, out var row))
                {
                    row = new LC_TP_TonPheu { IDPhieu = idPhieu, PheuId = d.PheuId, NgayTao = DateTime.Now };
                    _context.LC_TP_TonPheu.Add(row);
                    existingTonPheu[d.PheuId] = row;
                }

                var doAmThap = doAmThapByCum.TryGetValue(pheuInfo.ThapThanId, out var dA)
                    ? dA
                    : existingCapThap.TryGetValue(pheuInfo.ThapThanId, out var capThapRow) ? capThapRow.DoAmThap : null;

                row.NgaySX = ngaySx;
                row.Ca = ca;
                row.Scope = scope;
                row.MucRadar = d.MucRadar;
                row.GhiChu = d.GhiChu;
                row.KLTonDauQuyKho = await ComputeTonDauPheuAsync(idPhieu, scope, ngaySx, ca, d.PheuId);
                row.KLAmTonCuoi = ComputeKLAmTonCuoiPheu(scope, d.MucRadar, doAmThap);
                row.KLTonCuoiQuyKho = ComputeKhoiLuongKho(row.KLAmTonCuoi, doAmThap);
            }

            await _context.SaveChangesAsync();
            return await GetThapPheuTabAsync(idPhieu);
        }

        public async Task<ThapPheuTabDto> RefreshThapPheuTuCaTruocAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể làm mới.");

            var scope = phieu.Scope ?? 0;

            var tonPheuRows = await _context.LC_TP_TonPheu.Where(x => x.IDPhieu == idPhieu).ToListAsync();
            foreach (var row in tonPheuRows)
            {
                row.KLTonDauQuyKho = await ComputeTonDauPheuAsync(idPhieu, scope, phieu.NgaySX, phieu.Ca, row.PheuId);
            }

            var capThapRows = await _context.LC_TP_CapThap.Where(x => x.IDPhieu == idPhieu).ToListAsync();
            foreach (var row in capThapRows)
            {
                row.CapLenThapQuyKho = await ComputeCapLenThapAsync(idPhieu, scope, row.CumThapId);
                row.LuyKeCapLenThap = await ComputeLuyKeThapAsync(
                    idPhieu, scope, phieu.NgaySX, phieu.Ca, row.CumThapId, row.CapLenThapQuyKho, x => x.LuyKeCapLenThap);
                row.LuyKeNapLo = await ComputeLuyKeThapAsync(
                    idPhieu, scope, phieu.NgaySX, phieu.Ca, row.CumThapId, row.NapLoQuyKho, x => x.LuyKeNapLo);
            }

            await _context.SaveChangesAsync();
            return await GetThapPheuTabAsync(idPhieu);
        }

        public async Task InitializeThapPheuAsync(BmPhieu phieu)
        {
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;
            if (await _context.LC_TP_TonPheu.AnyAsync(x => x.IDPhieu == phieu.Idphieu)) return; // đã init rồi

            var scope = phieu.Scope ?? 0;

            var cumThaps = await _context.LC_CumThap.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .ToListAsync();
            if (cumThaps.Count == 0) return;

            // CapLenThapQuyKho/LuyKeCapLenThap chưa có gì để tính lúc mới tạo phiếu (chưa nhập
            // Phối trộn) — để NULL, GetThapPheuTabAsync/SaveThapPheuTabAsync sẽ tính lại khi gọi.
            foreach (var cum in cumThaps)
            {
                _context.LC_TP_CapThap.Add(new LC_TP_CapThap
                {
                    IDPhieu = phieu.Idphieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    CumThapId = cum.ID,
                    NgayTao = DateTime.Now,
                });
            }

            var pheus = await (
                from p in _context.LC_TP_ThapThan_Pheu.AsNoTracking()
                join c in _context.LC_CumThap.AsNoTracking() on p.ThapThanId equals c.ID
                where !p.IsDelete && c.Scope == scope && !c.IsDelete
                select p
            ).ToListAsync();

            foreach (var p in pheus)
            {
                _context.LC_TP_TonPheu.Add(new LC_TP_TonPheu
                {
                    IDPhieu = phieu.Idphieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    PheuId = p.Id,
                    KLTonDauQuyKho = await ComputeTonDauPheuAsync(phieu.Idphieu, scope, phieu.NgaySX, phieu.Ca, p.Id),
                    NgayTao = DateTime.Now,
                });
            }

            await _context.SaveChangesAsync();
        }

        // F07 — Σ KLQuyKhoCan của LC_PhoiTron (header) cùng Scope+ID_CumThap, CHỈ
        // LoaiPhoiTron='SAN_XUAT' (loại trừ THU_NGHIEM — xem LC_01 mục 5, "[+ thử nghiệm]"
        // trong ngoặc = chưa chắc, mặc định không gộp), của CHÍNH phiếu hiện tại.
        private async Task<decimal?> ComputeCapLenThapAsync(Guid idPhieu, int scope, int cumThapId)
        {
            var rows = await _context.LC_PhoiTron.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu && x.Scope == scope && x.ID_CumThap == cumThapId
                    && x.LoaiPhoiTron == "SAN_XUAT")
                .Select(x => x.KLQuyKhoCan)
                .ToListAsync();
            if (rows.Count == 0) return null;
            return rows.Aggregate(0m, (acc, v) => acc + (v ?? 0));
        }

        private async Task<Dictionary<int, decimal?>> ComputeCapLenThapMapAsync(Guid idPhieu, int scope)
        {
            return await _context.LC_PhoiTron.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu && x.Scope == scope && x.LoaiPhoiTron == "SAN_XUAT")
                .GroupBy(x => x.ID_CumThap)
                .Select(g => new { CumThapId = g.Key, Tong = g.Sum(x => x.KLQuyKhoCan ?? 0) })
                .ToDictionaryAsync(x => x.CumThapId, x => (decimal?)x.Tong);
        }

        // F08 — mốc reset Ca1/ngày1 hàng tháng, khoá (Scope, CumThapId) — mirror
        // LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync. `currentField`/`luyKeField` chọn cột
        // áp dụng (CapLenThap hoặc NapLo) dùng chung 1 hàm.
        private async Task<decimal?> ComputeLuyKeThapAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, int cumThapId, decimal? giaTriCaNay,
            System.Linq.Expressions.Expression<Func<LC_TP_CapThap, decimal?>> luyKeField)
        {
            if (giaTriCaNay == null) return null;
            if (ngaySx == null || ca == null) return giaTriCaNay;
            if (ngaySx.Value.Day == 1 && ca == 1) return giaTriCaNay;

            var prevQuery = _context.LC_TP_CapThap.AsNoTracking()
                .Where(x => x.Scope == scope && x.CumThapId == cumThapId && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca);

            var prev = await prevQuery.Select(luyKeField).FirstOrDefaultAsync();
            return (prev ?? 0) + giaTriCaNay.Value;
        }

        // PREVIOUS_SHIFT — tồn cuối kíp trước CỦA CHÍNH phễu đó, reset=0 mốc Ca1/ngày1.
        private async Task<decimal?> ComputeTonDauPheuAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, int pheuId)
        {
            if (ngaySx == null || ca == null) return 0;
            if (ngaySx.Value.Day == 1 && ca == 1) return 0;

            var prev = await _context.LC_TP_TonPheu.AsNoTracking()
                .Where(x => x.Scope == scope && x.PheuId == pheuId && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca)
                .Select(x => x.KLTonCuoiQuyKho)
                .FirstOrDefaultAsync();
            return prev ?? 0;
        }

        // F06a (Scope=1/TP1): KLAm = H<6 ? (H/6)^2*100 : 100+33.5*(H-6)
        // F06b (Scope=2/TP2): KLAm = V(G)*(0.707+DoAmThap/100), V(G) piecewise — xem LC_01
        // mục 5. H/G = MucRadar (mét); DoAmThap lấy từ Tháp chứa phễu đó.
        private static decimal? ComputeKLAmTonCuoiPheu(int scope, decimal? mucRadar, decimal? doAmThap)
        {
            if (mucRadar == null) return null;
            var h = mucRadar.Value;

            if (scope == 1)
            {
                if (h < 6m)
                {
                    var x = h / 6m;
                    return Math.Round(x * x * 100m, 3);
                }
                return Math.Round(100m + 33.5m * (h - 6m), 3);
            }

            if (scope == 2)
            {
                if (doAmThap == null) return null;
                decimal vG;
                if (h >= 7.368m)
                {
                    vG = (h - 7.368m) * 62.964m + 90m;
                }
                else
                {
                    var r = h * 0.442m + 0.75m;
                    vG = (r * r + 0.75m * r + 0.562m) * h * (decimal)Math.PI / 3m;
                }
                return Math.Round(vG * (0.707m + doAmThap.Value / 100m), 3);
            }

            return null;
        }

        // KLKho = KLAm x (100 - DoAmThap) / 100 — F01 áp lại cho tồn phễu (giống F05a/F05b silo).
        private static decimal? ComputeKhoiLuongKho(decimal? klAm, decimal? doAm)
        {
            if (klAm == null || doAm == null) return null;
            return Math.Round(klAm.Value * (100 - doAm.Value) / 100m, 3);
        }

        // ─── Danh mục Nơi nhận (Bụi cốc) ───

        public async Task<List<LC_TP_BuiCocNoiNhan>> GetAllBuiCocNoiNhanAsync(bool includeDeleted = false)
        {
            var query = _context.LC_TP_BuiCocNoiNhan.AsNoTracking();
            if (!includeDeleted) query = query.Where(x => !x.IsDelete);
            return await query.OrderBy(x => x.ThuTu ?? byte.MaxValue).ThenBy(x => x.Id).ToListAsync();
        }

        public async Task<LC_TP_BuiCocNoiNhan?> GetBuiCocNoiNhanByIdAsync(int id)
        {
            return await _context.LC_TP_BuiCocNoiNhan.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<LC_TP_BuiCocNoiNhan> AddBuiCocNoiNhanAsync(LC_TP_BuiCocNoiNhan entity)
        {
            entity.IsDelete = false;
            entity.NgayTao = DateTime.Now;
            _context.LC_TP_BuiCocNoiNhan.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_TP_BuiCocNoiNhan?> UpdateBuiCocNoiNhanAsync(LC_TP_BuiCocNoiNhan entity)
        {
            var existing = await _context.LC_TP_BuiCocNoiNhan
                .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDelete);
            if (existing == null) return null;

            existing.Ten = entity.Ten;
            existing.ThuTu = entity.ThuTu;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteBuiCocNoiNhanAsync(int id)
        {
            var existing = await _context.LC_TP_BuiCocNoiNhan
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
            if (existing == null) return false;

            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Tab Bụi cốc (LC_TP_BuiCoc / LC_TP_BuiCocXuat) — API by-phieu riêng, TP1 only ───
        // "I. Sản lượng" + "II. Bụi cốc tồn" + "III. Bụi cốc xuất", xem TP1.xlsx dòng 201-217.

        public async Task<BuiCocTabDto> GetBuiCocTabAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");

            var header = await _context.LC_TP_BuiCoc.AsNoTracking().FirstOrDefaultAsync(x => x.IDPhieu == idPhieu);

            var noiNhanList = await GetAllBuiCocNoiNhanAsync();
            var xuatRows = await _context.LC_TP_BuiCocXuat.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.NoiNhanId);

            var xuatDtos = noiNhanList.Select(n =>
            {
                xuatRows.TryGetValue(n.Id, out var row);
                return new BuiCocXuatDto
                {
                    NoiNhanId = n.Id,
                    TenNoiNhan = n.Ten,
                    KhoiLuong = row?.KhoiLuong,
                    LuyKeQuyKho = row?.LuyKeQuyKho,
                };
            }).ToList();

            return new BuiCocTabDto
            {
                SanLuongTrongCa = header?.SanLuongTrongCa,
                LuyKeSanLuong = header?.LuyKeSanLuong,
                TonDauKyQuyKho = header?.TonDauKyQuyKho,
                TonCuoiKyQuyKho = header?.TonCuoiKyQuyKho,
                Xuat = xuatDtos,
            };
        }

        public async Task<BuiCocTabDto> SaveBuiCocTabAsync(Guid idPhieu, BuiCocTabSaveDto dto)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX;
            var ca = phieu.Ca;

            var noiNhanIds = (await GetAllBuiCocNoiNhanAsync()).Select(n => n.Id).ToHashSet();

            var existingXuat = await _context.LC_TP_BuiCocXuat
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.NoiNhanId);

            decimal? tongKhoiLuongXuat = 0;
            foreach (var d in dto.Xuat)
            {
                if (!noiNhanIds.Contains(d.NoiNhanId)) continue;

                if (!existingXuat.TryGetValue(d.NoiNhanId, out var row))
                {
                    row = new LC_TP_BuiCocXuat { IDPhieu = idPhieu, NoiNhanId = d.NoiNhanId, NgayTao = DateTime.Now };
                    _context.LC_TP_BuiCocXuat.Add(row);
                    existingXuat[d.NoiNhanId] = row;
                }

                row.NgaySX = ngaySx;
                row.Ca = ca;
                row.Scope = scope;
                row.KhoiLuong = d.KhoiLuong;
                row.LuyKeQuyKho = await ComputeLuyKeBuiCocXuatAsync(idPhieu, scope, ngaySx, ca, d.NoiNhanId, d.KhoiLuong);
                tongKhoiLuongXuat += d.KhoiLuong ?? 0;
            }

            var header = await _context.LC_TP_BuiCoc.FirstOrDefaultAsync(x => x.IDPhieu == idPhieu);
            if (header == null)
            {
                header = new LC_TP_BuiCoc { IDPhieu = idPhieu, NgayTao = DateTime.Now };
                _context.LC_TP_BuiCoc.Add(header);
            }

            var tonDau = await ComputeTonDauBuiCocAsync(idPhieu, scope, ngaySx, ca);

            header.NgaySX = ngaySx;
            header.Ca = ca;
            header.Scope = scope;
            header.SanLuongTrongCa = dto.SanLuongTrongCa;
            header.LuyKeSanLuong = await ComputeLuyKeBuiCocSanLuongAsync(idPhieu, scope, ngaySx, ca, dto.SanLuongTrongCa);
            header.TonDauKyQuyKho = tonDau;
            header.TonCuoiKyQuyKho = (tonDau ?? 0) + (dto.SanLuongTrongCa ?? 0) - (tongKhoiLuongXuat ?? 0);

            await _context.SaveChangesAsync();
            return await GetBuiCocTabAsync(idPhieu);
        }

        public async Task<BuiCocTabDto> RefreshBuiCocTuCaTruocAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể làm mới.");

            var scope = phieu.Scope ?? 0;

            var header = await _context.LC_TP_BuiCoc.FirstOrDefaultAsync(x => x.IDPhieu == idPhieu);
            if (header != null)
            {
                var tonDau = await ComputeTonDauBuiCocAsync(idPhieu, scope, phieu.NgaySX, phieu.Ca);
                header.TonDauKyQuyKho = tonDau;
                header.TonCuoiKyQuyKho = tonDau + header.SanLuongTrongCa
                    - await _context.LC_TP_BuiCocXuat.Where(x => x.IDPhieu == idPhieu).SumAsync(x => x.KhoiLuong ?? 0);
            }

            var xuatRows = await _context.LC_TP_BuiCocXuat.Where(x => x.IDPhieu == idPhieu).ToListAsync();
            foreach (var row in xuatRows)
            {
                row.LuyKeQuyKho = await ComputeLuyKeBuiCocXuatAsync(
                    idPhieu, scope, phieu.NgaySX, phieu.Ca, row.NoiNhanId, row.KhoiLuong);
            }

            await _context.SaveChangesAsync();
            return await GetBuiCocTabAsync(idPhieu);
        }

        public async Task InitializeBuiCocAsync(BmPhieu phieu)
        {
            if (phieu.Scope != 1) return; // chỉ TP1 có khối Bụi cốc (xem SECTION 19 lc_bcxs_xnl_tp.sql)
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;
            if (await _context.LC_TP_BuiCoc.AnyAsync(x => x.IDPhieu == phieu.Idphieu)) return; // đã init rồi

            _context.LC_TP_BuiCoc.Add(new LC_TP_BuiCoc
            {
                IDPhieu = phieu.Idphieu,
                NgaySX = phieu.NgaySX,
                Ca = phieu.Ca,
                Scope = phieu.Scope.Value,
                TonDauKyQuyKho = await ComputeTonDauBuiCocAsync(phieu.Idphieu, phieu.Scope.Value, phieu.NgaySX, phieu.Ca),
                NgayTao = DateTime.Now,
            });

            await _context.SaveChangesAsync();
        }

        // CUMULATIVE "I. Sản lượng" — mốc reset Ca1/ngày1, khoá theo Scope (chỉ 1 hạng mục,
        // chỉ TP1/Scope=1).
        private async Task<decimal?> ComputeLuyKeBuiCocSanLuongAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, decimal? sanLuongCaNay)
        {
            if (sanLuongCaNay == null) return null;
            if (ngaySx == null || ca == null) return sanLuongCaNay;
            if (ngaySx.Value.Day == 1 && ca == 1) return sanLuongCaNay;

            var prev = await _context.LC_TP_BuiCoc.AsNoTracking()
                .Where(x => x.Scope == scope && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca)
                .Select(x => x.LuyKeSanLuong)
                .FirstOrDefaultAsync();
            return (prev ?? 0) + sanLuongCaNay.Value;
        }

        // PREVIOUS_SHIFT "II. Bụi cốc tồn" — tồn cuối kíp trước, reset=0 mốc Ca1/ngày1.
        private async Task<decimal?> ComputeTonDauBuiCocAsync(Guid idPhieu, int scope, DateOnly? ngaySx, int? ca)
        {
            if (ngaySx == null || ca == null) return 0;
            if (ngaySx.Value.Day == 1 && ca == 1) return 0;

            var prev = await _context.LC_TP_BuiCoc.AsNoTracking()
                .Where(x => x.Scope == scope && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca)
                .Select(x => x.TonCuoiKyQuyKho)
                .FirstOrDefaultAsync();
            return prev ?? 0;
        }

        // CUMULATIVE "III. Bụi cốc xuất" — khoá theo NoiNhanId, mốc reset Ca1/ngày1.
        private async Task<decimal?> ComputeLuyKeBuiCocXuatAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, int noiNhanId, decimal? khoiLuongCaNay)
        {
            if (khoiLuongCaNay == null) return null;
            if (ngaySx == null || ca == null) return khoiLuongCaNay;
            if (ngaySx.Value.Day == 1 && ca == 1) return khoiLuongCaNay;

            var prev = await _context.LC_TP_BuiCocXuat.AsNoTracking()
                .Where(x => x.Scope == scope && x.NoiNhanId == noiNhanId && x.IDPhieu != idPhieu
                    && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca)
                .Select(x => x.LuyKeQuyKho)
                .FirstOrDefaultAsync();
            return (prev ?? 0) + khoiLuongCaNay.Value;
        }

        // ─── Kết quả phân tích than (LC_TP_KetQuaPhanTichThan) ───

        public async Task<List<LC_TP_KetQuaPhanTichThan>> GetKetQuaPhanTichAsync(Guid phieuCaId, int scope)
        {
            return await _context.LC_TP_KetQuaPhanTichThan
                .AsNoTracking()
                .Where(x => x.PhieuCaId == phieuCaId && x.Scope == scope)
                .OrderBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<LC_TP_KetQuaPhanTichThan>> ReplaceKetQuaPhanTichAsync(
            Guid phieuCaId, int scope, IEnumerable<LC_TP_KetQuaPhanTichThan> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_KetQuaPhanTichThan
                .Where(x => x.PhieuCaId == phieuCaId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_KetQuaPhanTichThan.RemoveRange(old);

            foreach (var e in list)
            {
                e.PhieuCaId = phieuCaId;
                e.Scope = scope;
            }
            _context.LC_TP_KetQuaPhanTichThan.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteKetQuaPhanTichAsync(Guid phieuCaId, int scope)
        {
            return await _context.LC_TP_KetQuaPhanTichThan
                .Where(x => x.PhieuCaId == phieuCaId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        // ─── Sự cố trong ca (LC_TP_SuCoTrongCa) ───

        public async Task<List<LC_TP_SuCoTrongCa>> GetSuCoAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_SuCoTrongCa
                .AsNoTracking()
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .OrderBy(x => x.Stt)
                .ToListAsync();
        }

        public async Task<List<LC_TP_SuCoTrongCa>> ReplaceSuCoAsync(
            Guid phieuId, int scope, IEnumerable<LC_TP_SuCoTrongCa> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_SuCoTrongCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_SuCoTrongCa.RemoveRange(old);

            for (var i = 0; i < list.Count; i++)
            {
                list[i].PhieuId = phieuId;
                list[i].Scope = scope;
                list[i].Stt = i + 1;
            }
            _context.LC_TP_SuCoTrongCa.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteSuCoAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_SuCoTrongCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        // ─── Tiêu hao (LC_TP_TieuHao / LC_TP_DanhMucTieuHao) ───

        public async Task<List<LC_TP_NhomTieuHao>> GetNhomTieuHaoAsync()
        {
            return await _context.LC_TP_NhomTieuHao.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        }

        public async Task<List<LC_TP_DanhMucTieuHao>> GetDanhMucTieuHaoAsync()
        {
            return await _context.LC_TP_DanhMucTieuHao
                .AsNoTracking()
                .Where(x => !x.IsDelete)
                .OrderBy(x => x.ThuTu).ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<LC_TP_TieuHao>> GetTieuHaoAsync(Guid phieuId, int scope)
        {
            // Sắp theo thứ tự hạng mục trong danh mục để FE hiển thị đúng thứ tự
            return await (from t in _context.LC_TP_TieuHao.AsNoTracking()
                          join d in _context.LC_TP_DanhMucTieuHao.AsNoTracking() on t.HangMucId equals d.Id
                          where t.PhieuId == phieuId && t.Scope == scope
                          orderby d.ThuTu, d.Id
                          select t).ToListAsync();
        }

        public async Task<List<LC_TP_TieuHao>> ReplaceTieuHaoAsync(
            Guid phieuId, int scope, IEnumerable<LC_TP_TieuHao> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_TieuHao
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_TieuHao.RemoveRange(old);

            foreach (var e in list)
            {
                e.Id = 0;
                e.PhieuId = phieuId;
                e.Scope = scope;
            }
            _context.LC_TP_TieuHao.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteTieuHaoAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_TieuHao
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        // ─── Chất lượng cốc (LC_TP_ChiTieuCoc / LC_TP_TCNBCoc / LC_TP_ChatLuongCocCa) ───

        public async Task<List<LC_TP_ChiTieuCoc>> GetChiTieuCocAsync(bool includeDeleted = false)
        {
            var query = _context.LC_TP_ChiTieuCoc.AsNoTracking();
            if (!includeDeleted) query = query.Where(x => !x.IsDelete);
            return await query.OrderBy(x => x.ThuTu).ThenBy(x => x.Id).ToListAsync();
        }

        public async Task<List<LC_TP_TCNBCoc>> GetTcnbCocAsync()
        {
            return await _context.LC_TP_TCNBCoc.AsNoTracking().OrderBy(x => x.ChiTieuId).ThenBy(x => x.Loai).ToListAsync();
        }

        public async Task<List<LC_TP_ChatLuongCocCa>> GetChatLuongCocAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_ChatLuongCocCa
                .AsNoTracking()
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .OrderBy(x => x.ChiTieuId).ThenBy(x => x.Loai).ThenBy(x => x.Lan)
                .ToListAsync();
        }

        public async Task<List<LC_TP_ChatLuongCocCa>> ReplaceChatLuongCocAsync(
            Guid phieuId, int scope, IEnumerable<LC_TP_ChatLuongCocCa> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_ChatLuongCocCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_ChatLuongCocCa.RemoveRange(old);

            foreach (var e in list)
            {
                e.Id = 0;
                e.PhieuId = phieuId;
                e.Scope = scope;
            }
            _context.LC_TP_ChatLuongCocCa.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteChatLuongCocAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_ChatLuongCocCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        public async Task<List<int>> FindHangMucKhongTonTaiAsync(IEnumerable<int> hangMucIds)
        {
            var ids = hangMucIds.Distinct().ToList();
            var existing = await _context.LC_TP_DanhMucTieuHao
                .Where(x => ids.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();
            return ids.Except(existing).ToList();
        }
    }
}
