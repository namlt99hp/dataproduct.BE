using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Utils;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tab "Tồn cốc silo" (`tonCoc`, TabCocTonSilo.tsx) của LC_BCSX_XNL_TP — xem
    // ILC_SiloCocTonRepository. KHÔNG dùng BmPhieu.DataJson, cùng nguyên tắc với
    // LC_CapLieuSiloRepository/LC_PhoiTronRepository.
    public class LC_SiloCocTonRepository : ILC_SiloCocTonRepository
    {
        private readonly ProductFormContext _context;
        private readonly ILC_SiloTonRepository _siloTonRepo;

        public LC_SiloCocTonRepository(ProductFormContext context, ILC_SiloTonRepository siloTonRepo)
        {
            _context = context;
            _siloTonRepo = siloTonRepo;
        }

        public async Task<List<LcCocTonCumDto>> GetByPhieuAsync(Guid idPhieu)
        {
            var rows = await _context.LC_SiloCocTon.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .OrderBy(x => x.ThuTuCum).ThenBy(x => x.ThuTuSilo)
                .ToListAsync();

            return rows
                .GroupBy(x => new { x.ThuTuCum, x.TenCum })
                .OrderBy(g => g.Key.ThuTuCum)
                .Select(g => new LcCocTonCumDto
                {
                    TenCum = g.Key.TenCum,
                    Silos = g.OrderBy(x => x.ThuTuSilo).Select(x => new LcCocTonSiloColDto
                    {
                        Id = x.ID,
                        IdSiloTon = x.ID_SiloTon,
                        TenSilo = x.TenSilo,
                        LoaiLieu = x.LoaiLieu,
                        TenNhapLieu = x.TenNhapLieu,
                        GiaTriTonCuoiCa = x.GiaTriTonCuoiCa,
                        KhoiLuongTonCuoiCa = x.KhoiLuongTonCuoiCa,
                        GiaTriTonDauCa = x.GiaTriTonDauCa,
                        KhoiLuongTonDauCa = x.KhoiLuongTonDauCa,
                        ChenhLech1080 = x.ChenhLech1080,
                        ChenhLechDuoi10 = x.ChenhLechDuoi10,
                    }).ToList(),
                })
                .ToList();
        }

        public async Task<List<LcCocTonCumDto>> SaveTabAsync(Guid idPhieu, List<LcCocTonCumDto> cums)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");

            var scope = phieu.Scope ?? 0;

            var allSiloTonIds = cums.SelectMany(c => c.Silos).Select(s => s.IdSiloTon);
            var siloTonById = await _siloTonRepo.GetByIdsAsync(allSiloTonIds);

            var existing = await _context.LC_SiloCocTon
                .Where(x => x.IDPhieu == idPhieu)
                .ToListAsync();
            var existingById = existing.ToDictionary(x => x.ID);
            var keptIds = new HashSet<int>();

            for (var cumIdx = 0; cumIdx < cums.Count; cumIdx++)
            {
                var cum = cums[cumIdx];
                var tenCum = cum.TenCum.Trim();

                for (var siloIdx = 0; siloIdx < cum.Silos.Count; siloIdx++)
                {
                    var silo = cum.Silos[siloIdx];
                    if (!siloTonById.TryGetValue(silo.IdSiloTon, out var siloTon))
                        throw new InvalidOperationException($"Không tìm thấy Silo tồn (ID={silo.IdSiloTon}) trong danh mục — có thể đã bị xoá.");

                    // BE tự tính Khối lượng từ công thức của danh mục — bỏ qua giá trị FE gửi.
                    decimal? khoiLuongCuoi = null;
                    if (silo.GiaTriTonCuoiCa != null)
                    {
                        var (kq, loi) = CongThucEvaluator.Evaluate(siloTon.CongThuc, silo.GiaTriTonCuoiCa.Value);
                        if (loi != null) throw new InvalidOperationException($"Silo '{siloTon.TenSilo}' (Tồn cuối ca): {loi}");
                        khoiLuongCuoi = kq;
                    }
                    decimal? khoiLuongDau = null;
                    if (silo.GiaTriTonDauCa != null)
                    {
                        var (kq, loi) = CongThucEvaluator.Evaluate(siloTon.CongThuc, silo.GiaTriTonDauCa.Value);
                        if (loi != null) throw new InvalidOperationException($"Silo '{siloTon.TenSilo}' (Tồn đầu ca): {loi}");
                        khoiLuongDau = kq;
                    }

                    decimal? chenhLech1080 = null;
                    decimal? chenhLechDuoi10 = null;
                    if (khoiLuongCuoi != null && khoiLuongDau != null)
                    {
                        var chenh = Math.Round(khoiLuongCuoi.Value - khoiLuongDau.Value, 3);
                        if (siloTon.LoaiLieu == "MM_10_80") chenhLech1080 = chenh;
                        else if (siloTon.LoaiLieu == "MM_DUOI_10") chenhLechDuoi10 = chenh;
                    }

                    if (silo.Id > 0 && existingById.TryGetValue(silo.Id, out var e))
                    {
                        e.ThuTuCum = cumIdx;
                        e.TenCum = tenCum;
                        e.ThuTuSilo = siloIdx;
                        e.ID_SiloTon = siloTon.ID;
                        e.TenSilo = siloTon.TenSilo;
                        e.LoaiLieu = siloTon.LoaiLieu;
                        e.TenNhapLieu = siloTon.TenNhapLieu;
                        e.GiaTriTonCuoiCa = silo.GiaTriTonCuoiCa;
                        e.KhoiLuongTonCuoiCa = khoiLuongCuoi;
                        e.GiaTriTonDauCa = silo.GiaTriTonDauCa;
                        e.KhoiLuongTonDauCa = khoiLuongDau;
                        e.ChenhLech1080 = chenhLech1080;
                        e.ChenhLechDuoi10 = chenhLechDuoi10;
                        keptIds.Add(e.ID);
                    }
                    else
                    {
                        _context.LC_SiloCocTon.Add(new LC_SiloCocTon
                        {
                            IDPhieu = idPhieu,
                            NgaySX = phieu.NgaySX,
                            Ca = phieu.Ca,
                            Scope = scope,
                            ThuTuCum = cumIdx,
                            TenCum = tenCum,
                            ThuTuSilo = siloIdx,
                            ID_SiloTon = siloTon.ID,
                            TenSilo = siloTon.TenSilo,
                            LoaiLieu = siloTon.LoaiLieu,
                            TenNhapLieu = siloTon.TenNhapLieu,
                            GiaTriTonCuoiCa = silo.GiaTriTonCuoiCa,
                            KhoiLuongTonCuoiCa = khoiLuongCuoi,
                            GiaTriTonDauCa = silo.GiaTriTonDauCa,
                            KhoiLuongTonDauCa = khoiLuongDau,
                            ChenhLech1080 = chenhLech1080,
                            ChenhLechDuoi10 = chenhLechDuoi10,
                            NgayTao = DateTime.Now,
                        });
                    }
                }
            }

            // Xoá các cột silo không còn trong danh sách gửi lên (người dùng bấm "Xoá silo"
            // hoặc "Xoá cụm").
            var toDelete = existing.Where(x => !keptIds.Contains(x.ID)).ToList();
            if (toDelete.Count > 0)
                _context.LC_SiloCocTon.RemoveRange(toDelete);

            await _context.SaveChangesAsync();

            return await GetByPhieuAsync(idPhieu);
        }

        // ─── Initializer: CHỈ copy forward CẤU TRÚC lúc phiếu được tạo — mirror
        // LC_CapLieuSiloRepository.InitializeLC_BCSX_XNL_TPAsync. Tự no-op nếu đã init rồi. ──
        public async Task InitializeLC_SiloCocTonAsync(BmPhieu phieu)
        {
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;

            if (await _context.LC_SiloCocTon.AnyAsync(x => x.IDPhieu == phieu.Idphieu))
                return; // đã init rồi — lưu/sửa tiếp theo đi qua SaveTabAsync, không phải ở đây

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Now);
            var ca = phieu.Ca ?? 0;

            var prevPhieuId = await _context.BmPhieus
                .Where(x => x.MaBm == "LC_BCSX_XNL_TP"
                         && x.Scope == scope
                         && x.Idphieu != phieu.Idphieu
                         && (x.IsDelete == null || x.IsDelete == 0)
                         && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .Select(x => x.Idphieu)
                .FirstOrDefaultAsync();

            // Phiếu đầu tiên của Scope (chưa có phiếu trước) → để trống, người dùng tự thêm
            // cụm/silo từ đầu qua tab FE (bấm "+ Thêm cụm hạng mục" / "+ Thêm silo").
            if (prevPhieuId == default) return;

            var prevRows = await _context.LC_SiloCocTon.AsNoTracking()
                .Where(x => x.IDPhieu == prevPhieuId)
                .ToListAsync();
            if (prevRows.Count == 0) return;

            // CHỈ copy CẤU TRÚC (cụm, thứ tự, silo nào) — KHÔNG copy dữ liệu nhập/tính
            // (GiaTriTonCuoiCa...ChenhLechDuoi10 để NULL hết, đúng yêu cầu "chỉ reset dữ liệu
            // chiều cao thôi").
            var newRows = prevRows.Select(prev => new LC_SiloCocTon
            {
                IDPhieu = phieu.Idphieu,
                NgaySX = phieu.NgaySX,
                Ca = phieu.Ca,
                Scope = scope,
                ThuTuCum = prev.ThuTuCum,
                TenCum = prev.TenCum,
                ThuTuSilo = prev.ThuTuSilo,
                ID_SiloTon = prev.ID_SiloTon,
                TenSilo = prev.TenSilo,
                LoaiLieu = prev.LoaiLieu,
                TenNhapLieu = prev.TenNhapLieu,
                NgayTao = DateTime.Now,
            });

            _context.LC_SiloCocTon.AddRange(newRows);
            await _context.SaveChangesAsync();
        }
    }
}
