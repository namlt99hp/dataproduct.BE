using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tab "Cấp cốc" (LcTabXuatCap) của LC_BCSX_XNL_TP — xem ILC_XuatCapRepository và
    // .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11. KHÔNG dùng BmPhieu.DataJson, cùng nguyên
    // tắc với LC_SiloCocTonRepository/LC_PhoiTronRepository.
    //
    // SỬA 2026-10-26 (theo yêu cầu người dùng — "không cần Initialize khi tạo phiếu mới, mỗi
    // phiếu sẽ gán thôi"): BỎ HẲN Initializer. `LC_XuatCap` giờ là bảng THƯA (sparse) — 1 dòng chỉ
    // được TẠO (upsert, xem EnsureSlotAsync) khi người dùng nhập tay (SaveTabAsync) hoặc gán 1
    // nguồn BBGN (GanNguonAsync) vào đúng slot. `GetByPhieuAsync` build FULL layout (mọi Nơi
    // nhận×Chi tiết của Scope) từ DANH MỤC rồi LEFT JOIN các dòng đã có của phiếu — slot chưa có
    // dữ liệu trả về với `Id=0` (ảo), FE vẫn thấy đủ ô để nhập/gán.
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
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu);
            if (phieu == null) return new List<LcXuatCapSlotDto>();
            var scope = phieu.Scope ?? 0;

            // ─── Danh mục: shape ĐẦY ĐỦ layout của Scope, ĐỘC LẬP với phiếu đã có dữ liệu gì ───
            var noiNhans = await _context.LC_XuatCapDanhMucCum.AsNoTracking()
                .Where(x => !x.IsDelete && x.Scope == scope)
                .OrderBy(x => x.ThuTu)
                .ToListAsync();
            if (noiNhans.Count == 0) return new List<LcXuatCapSlotDto>();

            var noiNhanIds = noiNhans.Select(n => n.ID).ToList();
            var chiTiets = await _context.LC_XuatCapDanhMucChiTiet.AsNoTracking()
                .Where(c => !c.IsDelete && noiNhanIds.Contains(c.ID_NoiNhan))
                .OrderBy(c => c.ThuTu)
                .ToListAsync();
            var chiTietByNoiNhan = chiTiets.GroupBy(c => c.ID_NoiNhan).ToDictionary(g => g.Key, g => g.ToList());

            var cumById = await _context.LC_XuatCapCum.AsNoTracking()
                .Where(c => c.Scope == scope)
                .ToDictionaryAsync(c => c.ID);
            var loaiCocById = await _context.LC_XuatCapLoaiCoc.AsNoTracking().ToDictionaryAsync(l => l.ID);
            var loaiNhanById = await _context.LC_XuatCapLoaiNhan.AsNoTracking().ToDictionaryAsync(l => l.ID);

            // ─── Dữ liệu đã có của phiếu (sparse) ───
            var existingRows = await _context.LC_XuatCap.AsNoTracking().Where(x => x.IDPhieu == idPhieu).ToListAsync();
            var existingByChiTiet = existingRows.ToDictionary(x => x.ID_DanhMucChiTiet);

            var xuatCapIds = existingRows.Select(x => x.ID).ToList();
            var nguonByXuatCap = xuatCapIds.Count == 0
                ? new List<LC_XuatCapNguon>()
                : await _context.LC_XuatCapNguon.AsNoTracking().Where(n => xuatCapIds.Contains(n.ID_XuatCap)).ToListAsync();
            var nguonLookup = nguonByXuatCap.GroupBy(n => n.ID_XuatCap).ToDictionary(g => g.Key, g => g.ToList());

            // Trong ca/Lũy kế theo NhomTongHop — lấy từ dòng ĐÃ TỒN TẠI bất kỳ cùng nhóm
            // (RecomputeDerivedAsync set GIỐNG NHAU cho mọi dòng cùng nhóm); nhóm chưa có dòng nào
            // => chưa có số (null) — đúng vì chưa ai nhập/gán gì cho nhóm đó.
            var trongCaByNhom = existingRows
                .GroupBy(x => x.NhomTongHop)
                .ToDictionary(g => g.Key, g => (g.First().TrongCa, g.First().LuyKe));

            var phongBanIds = noiNhans.Where(x => x.ID_PhongBan != null).Select(x => x.ID_PhongBan!.Value).Distinct().ToList();
            var tenPhongBanById = phongBanIds.Count == 0
                ? new Dictionary<int, string?>()
                : await _masterContext.Tbl_PhongBan.AsNoTracking()
                    .Where(p => phongBanIds.Contains(p.ID_PhongBan))
                    .ToDictionaryAsync(p => p.ID_PhongBan, p => p.TenPhongBan);

            var result = new List<LcXuatCapSlotDto>();
            foreach (var nn in noiNhans)
            {
                var cum = cumById.GetValueOrDefault(nn.ID_Cum);
                var loaiCoc = loaiCocById.GetValueOrDefault(nn.ID_LoaiCoc);
                var tenPhongBan = nn.ID_PhongBan != null ? tenPhongBanById.GetValueOrDefault(nn.ID_PhongBan.Value) : null;
                var cons = chiTietByNoiNhan.GetValueOrDefault(nn.ID) ?? new List<LC_XuatCapDanhMucChiTiet>();

                foreach (var ct in cons)
                {
                    var tenLoaiNhan = ct.ID_LoaiNhan != null ? loaiNhanById.GetValueOrDefault(ct.ID_LoaiNhan.Value)?.Ten : null;

                    if (existingByChiTiet.TryGetValue(ct.ID, out var row))
                    {
                        var dto = ToDto(row, nguonLookup.GetValueOrDefault(row.ID) ?? new List<LC_XuatCapNguon>(), tenPhongBan, cum);
                        dto.IdLoaiNhan = ct.ID_LoaiNhan;
                        dto.TenLoaiNhan = tenLoaiNhan;
                        result.Add(dto);
                        continue;
                    }

                    var (trongCa, luyKe) = trongCaByNhom.TryGetValue(nn.NhomTongHop, out var t) ? t : (null, null);
                    result.Add(new LcXuatCapSlotDto
                    {
                        Id = 0, // slot ẢO — chưa ai nhập/gán gì cho phiếu này
                        IdDanhMucChiTiet = ct.ID,
                        ThuTu = nn.ThuTu,
                        TenCan = nn.TenCan,
                        NoiNhan = nn.NoiNhan,
                        ChiTietLo = ct.ChiTietLo,
                        LoaiCoc = loaiCoc?.Ten ?? string.Empty,
                        NhomTongHop = nn.NhomTongHop,
                        IdPhongBan = nn.ID_PhongBan,
                        TenPhongBan = tenPhongBan,
                        IdDanhMucCum = nn.ID,
                        IdCum = cum?.ID,
                        MaCum = cum?.Ma,
                        TenCum = cum?.Ten,
                        IdLoaiNhan = ct.ID_LoaiNhan,
                        TenLoaiNhan = tenLoaiNhan,
                        TrongCa = trongCa,
                        LuyKe = luyKe,
                    });
                }
            }
            return result;
        }

        public async Task<List<LcXuatCapSlotDto>> SaveTabAsync(Guid idPhieu, List<LcXuatCapSaveSlotDto> slots)
        {
            await GetEditablePhieuAsync(idPhieu);

            foreach (var s in slots)
            {
                var existing = await _context.LC_XuatCap
                    .FirstOrDefaultAsync(x => x.IDPhieu == idPhieu && x.ID_DanhMucChiTiet == s.IdDanhMucChiTiet);

                if (existing == null)
                {
                    var hasAnyValue = s.KhoiLuongAm != null || s.DoAm != null || s.TyKhoi != null || !string.IsNullOrWhiteSpace(s.GhiChu);
                    if (!hasAnyValue) continue; // không có gì để lưu — không tạo dòng rác (giữ đúng triết lý sparse)
                    existing = await EnsureSlotAsync(idPhieu, s.IdDanhMucChiTiet);
                }

                var ganCount = await _context.LC_XuatCapNguon.CountAsync(n => n.ID_XuatCap == existing.ID);
                // Slot đã có Nguon gán → KhoiLuongAm do BE tính (SUM), bỏ qua giá trị FE gửi.
                if (ganCount == 0) existing.KhoiLuongAm = s.KhoiLuongAm;

                existing.DoAm = s.DoAm;
                existing.TyKhoi = s.TyKhoi;
                existing.GhiChu = s.GhiChu;
            }

            await _context.SaveChangesAsync();
            await RecomputeDerivedAsync(idPhieu);

            return await GetByPhieuAsync(idPhieu);
        }

        public async Task<LcXuatCapSlotDto> GanNguonAsync(LcXuatCapGanRequest req)
        {
            await GetEditablePhieuAsync(req.IdPhieu);

            var daGan = await _context.LC_XuatCapNguon.AnyAsync(n => n.ID_CT_BBGN == req.IdCtBbgn);
            if (daGan)
                throw new InvalidOperationException("Dòng BBGN này đã được gán vào 1 slot khác — không thể gán trùng.");

            var slot = await EnsureSlotAsync(req.IdPhieu, req.IdDanhMucChiTiet);

            _context.LC_XuatCapNguon.Add(new LC_XuatCapNguon
            {
                ID_XuatCap = slot.ID,
                ID_BBGN = req.IdBbgn,
                ID_CT_BBGN = req.IdCtBbgn,
                KhoiLuongGan = req.KhoiLuongGan,
                DoAmGan = req.DoAmGan,
                NgayTao = DateTime.Now,
            });
            await _context.SaveChangesAsync();

            await RecomputeSlotKhoiLuongAsync(slot.ID);
            await RecomputeDerivedAsync(req.IdPhieu);

            return (await GetByPhieuAsync(req.IdPhieu)).First(s => s.IdDanhMucChiTiet == req.IdDanhMucChiTiet);
        }

        public async Task<LcXuatCapSlotDto> BoGanNguonAsync(int idXuatCapNguon)
        {
            var nguon = await _context.LC_XuatCapNguon.FirstOrDefaultAsync(n => n.ID == idXuatCapNguon)
                ?? throw new InvalidOperationException("Không tìm thấy dòng nguồn đã gán.");
            var slot = await _context.LC_XuatCap.FirstOrDefaultAsync(x => x.ID == nguon.ID_XuatCap)
                ?? throw new InvalidOperationException("Không tìm thấy slot Cấp cốc.");
            await GetEditablePhieuAsync(slot.IDPhieu);

            var idPhieu = slot.IDPhieu;
            var idDanhMucChiTiet = slot.ID_DanhMucChiTiet;

            _context.LC_XuatCapNguon.Remove(nguon);
            await _context.SaveChangesAsync();

            await RecomputeSlotKhoiLuongAsync(slot.ID);

            // Dọn dòng rỗng (sparse) — slot không còn nguồn và không có giá trị nhập tay nào thì
            // xoá hẳn, trả về đúng dạng "ảo" cho FE (khớp triết lý "mỗi phiếu chỉ gán thôi").
            await _context.Entry(slot).ReloadAsync();
            var ganCount = await _context.LC_XuatCapNguon.CountAsync(n => n.ID_XuatCap == slot.ID);
            if (ganCount == 0 && slot.KhoiLuongAm == null && slot.DoAm == null && slot.TyKhoi == null && string.IsNullOrWhiteSpace(slot.GhiChu))
            {
                _context.LC_XuatCap.Remove(slot);
                await _context.SaveChangesAsync();
            }

            await RecomputeDerivedAsync(idPhieu);

            return (await GetByPhieuAsync(idPhieu)).First(s => s.IdDanhMucChiTiet == idDanhMucChiTiet);
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

        // Tạo (upsert) 1 dòng LC_XuatCap cho (idPhieu, idDanhMucChiTiet) nếu CHƯA có — snapshot
        // cấu trúc từ danh mục tại thời điểm tạo (TenCan/NoiNhan/ChiTietLo/LoaiCoc/NhomTongHop/
        // ID_PhongBan/ID_DanhMucCum/ID_Cum), giống đúng cách Initializer v1 làm nhưng giờ LAZY
        // (chỉ chạy khi thật sự có nhập tay hoặc gán nguồn — xem SaveTabAsync/GanNguonAsync).
        private async Task<LC_XuatCap> EnsureSlotAsync(Guid idPhieu, int idDanhMucChiTiet)
        {
            var existing = await _context.LC_XuatCap
                .FirstOrDefaultAsync(x => x.IDPhieu == idPhieu && x.ID_DanhMucChiTiet == idDanhMucChiTiet);
            if (existing != null) return existing;

            var chiTiet = await _context.LC_XuatCapDanhMucChiTiet.FirstOrDefaultAsync(c => c.ID == idDanhMucChiTiet && !c.IsDelete)
                ?? throw new InvalidOperationException("Không tìm thấy Chi tiết trong danh mục Cấp cốc.");
            var noiNhan = await _context.LC_XuatCapDanhMucCum.FirstOrDefaultAsync(c => c.ID == chiTiet.ID_NoiNhan && !c.IsDelete)
                ?? throw new InvalidOperationException("Không tìm thấy Nơi nhận trong danh mục Cấp cốc.");
            var loaiCoc = await _context.LC_XuatCapLoaiCoc.FirstOrDefaultAsync(l => l.ID == noiNhan.ID_LoaiCoc);
            var cum = await _context.LC_XuatCapCum.FirstOrDefaultAsync(c => c.ID == noiNhan.ID_Cum);
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");

            var entity = new LC_XuatCap
            {
                IDPhieu = idPhieu,
                NgaySX = phieu.NgaySX,
                Ca = phieu.Ca,
                Scope = noiNhan.Scope,
                ID_DanhMucChiTiet = chiTiet.ID,
                ID_Cum = cum?.ID,
                ThuTu = noiNhan.ThuTu,
                TenCan = noiNhan.TenCan,
                NoiNhan = noiNhan.NoiNhan,
                ChiTietLo = chiTiet.ChiTietLo,
                LoaiCoc = loaiCoc?.Ten ?? string.Empty,
                NhomTongHop = noiNhan.NhomTongHop,
                ID_PhongBan = noiNhan.ID_PhongBan,
                ID_DanhMucCum = noiNhan.ID,
                NgayTao = DateTime.Now,
            };
            _context.LC_XuatCap.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        private async Task RecomputeSlotKhoiLuongAsync(int idXuatCap)
        {
            var slot = await _context.LC_XuatCap.FirstAsync(x => x.ID == idXuatCap);
            var nguon = await _context.LC_XuatCapNguon.Where(n => n.ID_XuatCap == idXuatCap).ToListAsync();

            if (nguon.Count == 0)
            {
                // Không còn nguồn gán — RESET (KhoiLuongAm/DoAm ở slot có Nguon CHỈ có thể đến từ
                // SUM, không phải nhập tay — SaveTabAsync chỉ cho nhập tay KhoiLuongAm khi
                // ganCount==0 tại thời điểm lưu). Giữ giá trị SUM cũ lại sau khi bỏ gán dòng cuối
                // sẽ là số liệu lỗi thời — reset về null cho người dùng tự nhập tay lại.
                slot.KhoiLuongAm = null;
                slot.DoAm = null;
                await _context.SaveChangesAsync();
                return;
            }

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
        // giống LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync) cho các dòng ĐÃ TỒN TẠI của phiếu
        // (sparse — nhóm chưa có dòng nào thì không có gì để tính, đúng ý nghĩa "chưa nhập/gán").
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

        private static LcXuatCapSlotDto ToDto(LC_XuatCap r, List<LC_XuatCapNguon> nguon, string? tenPhongBan, LC_XuatCapCum? cum) => new()
        {
            Id = r.ID,
            IdDanhMucChiTiet = r.ID_DanhMucChiTiet,
            ThuTu = r.ThuTu,
            TenCan = r.TenCan,
            NoiNhan = r.NoiNhan,
            ChiTietLo = r.ChiTietLo,
            LoaiCoc = r.LoaiCoc,
            NhomTongHop = r.NhomTongHop,
            IdPhongBan = r.ID_PhongBan,
            TenPhongBan = tenPhongBan,
            IdDanhMucCum = r.ID_DanhMucCum,
            IdCum = r.ID_Cum ?? cum?.ID,
            MaCum = cum?.Ma,
            TenCum = cum?.Ten,
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
