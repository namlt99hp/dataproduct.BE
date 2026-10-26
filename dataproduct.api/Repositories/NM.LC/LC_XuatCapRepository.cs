using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tab "Cấp cốc" (LcTabXuatCap) của LC_BCSX_XNL_TP — xem ILC_XuatCapRepository và
    // .claude/LuyenCoc/PhanTich_TabCapCoc.md §9. KHÔNG dùng BmPhieu.DataJson, cùng nguyên tắc với
    // LC_SiloCocTonRepository/LC_PhoiTronRepository.
    public class LC_XuatCapRepository : ILC_XuatCapRepository
    {
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterContext;

        public LC_XuatCapRepository(ProductFormContext context, ProductDataMasterDbContext masterContext)
        {
            _context = context;
            _masterContext = masterContext;
        }

        public async Task<List<LcXuatCapSlotDto>> GetByPhieuAsync(Guid idPhieu)
        {
            var rows = await _context.LC_XuatCap.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .OrderBy(x => x.ThuTu)
                .ToListAsync();
            if (rows.Count == 0) return new List<LcXuatCapSlotDto>();

            var nguonByXuatCap = await _context.LC_XuatCapNguon.AsNoTracking()
                .Where(n => rows.Select(r => r.ID).Contains(n.ID_XuatCap))
                .ToListAsync();
            var nguonLookup = nguonByXuatCap.GroupBy(n => n.ID_XuatCap).ToDictionary(g => g.Key, g => g.ToList());

            // ID_PhongBan là FK logic tới PRODUCTDATA.Tbl_PhongBan (khác database) — resolve
            // TenPhongBan bằng round-trip riêng, giống LC_XuatCapDanhMucRepository.GetListAsync.
            var phongBanIds = rows.Where(x => x.ID_PhongBan != null).Select(x => x.ID_PhongBan!.Value).Distinct().ToList();
            var tenPhongBanById = phongBanIds.Count == 0
                ? new Dictionary<int, string?>()
                : await _masterContext.Tbl_PhongBan.AsNoTracking()
                    .Where(p => phongBanIds.Contains(p.ID_PhongBan))
                    .ToDictionaryAsync(p => p.ID_PhongBan, p => p.TenPhongBan);

            return rows.Select(r => ToDto(r, nguonLookup.GetValueOrDefault(r.ID) ?? new List<LC_XuatCapNguon>(),
                r.ID_PhongBan != null ? tenPhongBanById.GetValueOrDefault(r.ID_PhongBan.Value) : null)).ToList();
        }

        public async Task<List<LcXuatCapSlotDto>> SaveTabAsync(Guid idPhieu, List<LcXuatCapSaveSlotDto> slots)
        {
            var phieu = await GetEditablePhieuAsync(idPhieu);

            var ids = slots.Select(s => s.Id).ToList();
            var existing = await _context.LC_XuatCap
                .Where(x => x.IDPhieu == idPhieu && ids.Contains(x.ID))
                .ToListAsync();
            var byId = existing.ToDictionary(x => x.ID);

            var xuatCapIds = existing.Select(x => x.ID).ToList();
            var ganCountBySlot = await _context.LC_XuatCapNguon
                .Where(n => xuatCapIds.Contains(n.ID_XuatCap))
                .GroupBy(n => n.ID_XuatCap)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count);

            foreach (var s in slots)
            {
                if (!byId.TryGetValue(s.Id, out var e)) continue;

                // Slot đã có Nguon gán → KhoiLuongAm do BE tính (SUM), bỏ qua giá trị FE gửi.
                if (!ganCountBySlot.TryGetValue(e.ID, out var ganCount) || ganCount == 0)
                    e.KhoiLuongAm = s.KhoiLuongAm;

                e.DoAm = s.DoAm;
                e.TyKhoi = s.TyKhoi;
                e.GhiChu = s.GhiChu;
            }

            await _context.SaveChangesAsync();
            await RecomputeDerivedAsync(idPhieu);

            return await GetByPhieuAsync(idPhieu);
        }

        public async Task<LcXuatCapSlotDto> GanNguonAsync(LcXuatCapGanRequest req)
        {
            var slot = await _context.LC_XuatCap.FirstOrDefaultAsync(x => x.ID == req.IdXuatCap)
                ?? throw new InvalidOperationException("Không tìm thấy slot Cấp cốc.");
            await GetEditablePhieuAsync(slot.IDPhieu);

            var daGan = await _context.LC_XuatCapNguon.AnyAsync(n => n.ID_CT_BBGN == req.IdCtBbgn);
            if (daGan)
                throw new InvalidOperationException("Dòng BBGN này đã được gán vào 1 slot khác — không thể gán trùng.");

            _context.LC_XuatCapNguon.Add(new LC_XuatCapNguon
            {
                ID_XuatCap = req.IdXuatCap,
                ID_BBGN = req.IdBbgn,
                ID_CT_BBGN = req.IdCtBbgn,
                KhoiLuongGan = req.KhoiLuongGan,
                DoAmGan = req.DoAmGan,
                NgayTao = DateTime.Now,
            });
            await _context.SaveChangesAsync();

            await RecomputeSlotKhoiLuongAsync(slot.ID);
            await RecomputeDerivedAsync(slot.IDPhieu);

            return (await GetByPhieuAsync(slot.IDPhieu)).First(s => s.Id == slot.ID);
        }

        public async Task<LcXuatCapSlotDto> BoGanNguonAsync(int idXuatCapNguon)
        {
            var nguon = await _context.LC_XuatCapNguon.FirstOrDefaultAsync(n => n.ID == idXuatCapNguon)
                ?? throw new InvalidOperationException("Không tìm thấy dòng nguồn đã gán.");
            var slot = await _context.LC_XuatCap.FirstOrDefaultAsync(x => x.ID == nguon.ID_XuatCap)
                ?? throw new InvalidOperationException("Không tìm thấy slot Cấp cốc.");
            await GetEditablePhieuAsync(slot.IDPhieu);

            _context.LC_XuatCapNguon.Remove(nguon);
            await _context.SaveChangesAsync();

            await RecomputeSlotKhoiLuongAsync(slot.ID);
            await RecomputeDerivedAsync(slot.IDPhieu);

            return (await GetByPhieuAsync(slot.IDPhieu)).First(s => s.Id == slot.ID);
        }

        // ─── Initializer: sinh slot lúc phiếu được tạo — ĐỌC THẲNG từ danh mục
        // LC_XuatCapDanhMuc (Scope, !IsDelete, sắp theo ThuTu), KHÔNG còn copy-forward từ phiếu
        // liền trước (chốt 2026-10-23 lượt 5 — danh mục quản lý qua trang riêng là nguồn
        // canonical DUY NHẤT, sửa danh mục có hiệu lực ngay cho phiếu MỚI tạo sau đó; phiếu đã
        // tạo trước giữ nguyên snapshot, không bị ảnh hưởng ngược). Tự no-op nếu đã init rồi. ──
        public async Task InitializeLC_XuatCapAsync(BmPhieu phieu)
        {
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;
            if (await _context.LC_XuatCap.AnyAsync(x => x.IDPhieu == phieu.Idphieu)) return;

            var scope = phieu.Scope ?? 0;

            var cums = await _context.LC_XuatCapDanhMucCum.AsNoTracking()
                .Where(x => !x.IsDelete && x.Scope == scope)
                .OrderBy(x => x.ThuTu)
                .ToListAsync();
            if (cums.Count == 0) return; // chưa khai báo Cụm nhận nào cho Scope này

            var cumIds = cums.Select(c => c.ID).ToList();
            var chiTiets = await _context.LC_XuatCapDanhMucChiTiet.AsNoTracking()
                .Where(c => !c.IsDelete && cumIds.Contains(c.ID_Cum))
                .OrderBy(c => c.ThuTu)
                .ToListAsync();
            var chiTietByCum = chiTiets.GroupBy(c => c.ID_Cum).ToDictionary(g => g.Key, g => g.ToList());

            var newRows = new List<LC_XuatCap>();
            var thuTu = 0;
            foreach (var cum in cums)
            {
                var cons = chiTietByCum.GetValueOrDefault(cum.ID) ?? new List<LC_XuatCapDanhMucChiTiet>();
                if (cons.Count == 0) continue; // Cụm chưa có Chi tiết con nào — bỏ qua, không sinh slot "rỗng"
                foreach (var con in cons)
                {
                    thuTu++;
                    newRows.Add(new LC_XuatCap
                    {
                        IDPhieu = phieu.Idphieu,
                        NgaySX = phieu.NgaySX,
                        Ca = phieu.Ca,
                        Scope = scope,
                        ThuTu = thuTu,
                        TenCan = cum.TenCan,
                        NoiNhan = cum.TenCum,
                        ChiTietLo = con.ChiTietLo,
                        LoaiCoc = cum.LoaiCoc,
                        NhomTongHop = cum.NhomTongHop,
                        ID_PhongBan = cum.ID_PhongBan,
                        ID_DanhMucCum = cum.ID,
                        NgayTao = DateTime.Now,
                    });
                }
            }
            if (newRows.Count == 0) return;

            _context.LC_XuatCap.AddRange(newRows);
            await _context.SaveChangesAsync();
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────────────

        private async Task<BmPhieu> GetEditablePhieuAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");
            return phieu;
        }

        private async Task RecomputeSlotKhoiLuongAsync(int idXuatCap)
        {
            var slot = await _context.LC_XuatCap.FirstAsync(x => x.ID == idXuatCap);
            var nguon = await _context.LC_XuatCapNguon.Where(n => n.ID_XuatCap == idXuatCap).ToListAsync();

            if (nguon.Count == 0) return; // không có nguồn gán — giữ giá trị nhập tay hiện có

            slot.KhoiLuongAm = nguon.Sum(n => n.KhoiLuongGan ?? 0);
            // Độ ẩm trung bình TRỌNG SỐ theo khối lượng từng dòng đã gán (xem
            // PhanTich_TabCapCoc.md §8.4 mục 4 — KHÔNG cộng thẳng DoAm, phải theo trọng số).
            var tongKl = nguon.Sum(n => n.KhoiLuongGan ?? 0);
            slot.DoAm = tongKl > 0
                ? Math.Round(nguon.Sum(n => (n.KhoiLuongGan ?? 0) * (n.DoAmGan ?? 0)) / tongKl, 4)
                : null;

            await _context.SaveChangesAsync();
        }

        // Tính lại F16 (KLQuyKho/slot) + F17 (TrongCa, SUM theo NhomTongHop trong CÙNG phiếu) +
        // LuyKe (TrongCa + lũy kế nhóm tương ứng của phiếu liền trước, mốc reset Ca1/ngày1 —
        // giống LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync) cho TOÀN BỘ slot của phiếu.
        // NhomTongHop là khoá nhóm THẬT đọc từ ranh giới merge cell Excel gốc (xem
        // LcXuatCapLayoutSeed) — KHÔNG còn group theo (TenCan, NoiNhan) như bản v1.
        private async Task RecomputeDerivedAsync(Guid idPhieu)
        {
            var rows = await _context.LC_XuatCap.Where(x => x.IDPhieu == idPhieu).ToListAsync();
            if (rows.Count == 0) return;

            foreach (var r in rows)
                r.KLQuyKho = ComputeKlQuyKho(r.KhoiLuongAm, r.DoAm);

            var groups = rows.GroupBy(r => r.NhomTongHop).ToList();
            foreach (var g in groups)
            {
                decimal? trongCa = g.Any(x => x.KLQuyKho != null) ? g.Sum(x => x.KLQuyKho ?? 0) : null;
                foreach (var r in g) r.TrongCa = trongCa;
            }

            foreach (var g in groups)
            {
                var sample = g.First();
                var luyKe = await ComputeLuyKeAsync(idPhieu, sample.Scope, sample.NgaySX, sample.Ca, sample.NhomTongHop, sample.TrongCa);
                foreach (var r in g) r.LuyKe = luyKe;
            }

            await _context.SaveChangesAsync();
        }

        private static decimal? ComputeKlQuyKho(decimal? khoiLuongAm, decimal? doAm)
        {
            if (khoiLuongAm == null || doAm == null) return null;
            return Math.Round(khoiLuongAm.Value - (khoiLuongAm.Value * doAm.Value) / 100m, 3);
        }

        // Mốc reset lũy kế Ca1/ngày1 hàng tháng (chuẩn chung của module LC, xem
        // lc-bcsx-xnl-db-design-decisions / LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync):
        // Ca 1 ngày 1 KHÔNG kế thừa từ tháng trước — LuyKe = đúng TrongCa của chính ca đó.
        private async Task<decimal?> ComputeLuyKeAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca, string nhomTongHop, decimal? trongCaThisShift)
        {
            if (trongCaThisShift == null) return null;
            if (ngaySx == null || ca == null) return trongCaThisShift;
            if (ngaySx.Value.Day == 1 && ca == 1) return trongCaThisShift;

            var prev = await (
                from d in _context.LC_XuatCap.AsNoTracking()
                where d.Scope == scope
                    && d.NhomTongHop == nhomTongHop
                    && d.IDPhieu != idPhieu
                    && (d.NgaySX < ngaySx || (d.NgaySX == ngaySx && d.Ca < ca))
                orderby d.NgaySX descending, d.Ca descending
                select d.LuyKe
            ).FirstOrDefaultAsync();

            return (prev ?? 0) + trongCaThisShift.Value;
        }

        private static LcXuatCapSlotDto ToDto(LC_XuatCap r, List<LC_XuatCapNguon> nguon, string? tenPhongBan) => new()
        {
            Id = r.ID,
            ThuTu = r.ThuTu,
            TenCan = r.TenCan,
            NoiNhan = r.NoiNhan,
            ChiTietLo = r.ChiTietLo,
            LoaiCoc = r.LoaiCoc,
            NhomTongHop = r.NhomTongHop,
            IdPhongBan = r.ID_PhongBan,
            TenPhongBan = tenPhongBan,
            IdDanhMucCum = r.ID_DanhMucCum,
            KhoiLuongAm = r.KhoiLuongAm,
            DoAm = r.DoAm,
            TyKhoi = r.TyKhoi,
            KLQuyKho = r.KLQuyKho,
            TrongCa = r.TrongCa,
            LuyKe = r.LuyKe,
            GhiChu = r.GhiChu,
            Nguon = nguon.Select(n => new LcXuatCapNguonDto
            {
                Id = n.ID,
                IdBbgn = n.ID_BBGN,
                IdCtBbgn = n.ID_CT_BBGN,
                KhoiLuongGan = n.KhoiLuongGan,
                DoAmGan = n.DoAmGan,
            }).ToList(),
        };
    }
}
