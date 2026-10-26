using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Trang quản lý danh mục "Cụm nhận" vùng Cấp cốc (CHA) + Chi tiết (CON) — xem
    // LC_XuatCapDanhMucCum/ChiTiet, .claude/LuyenCoc/PhanTich_TabCapCoc.md §9 (chốt lượt 6).
    // ID_PhongBan là FK LOGIC tới PRODUCTDATA.Tbl_PhongBan (khác database) nên resolve
    // TenPhongBan bằng round-trip riêng, giống LC_VatTuMaLoLookupRepository.
    public class LC_XuatCapDanhMucRepository : ILC_XuatCapDanhMucRepository
    {
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterContext;

        public LC_XuatCapDanhMucRepository(ProductFormContext context, ProductDataMasterDbContext masterContext)
        {
            _context = context;
            _masterContext = masterContext;
        }

        public async Task<List<LcXuatCapDanhMucCumDto>> GetCumListAsync(int? scope)
        {
            var cums = await _context.LC_XuatCapDanhMucCum
                .Where(x => !x.IsDelete && (scope == null || x.Scope == scope))
                .OrderBy(x => x.Scope).ThenBy(x => x.ThuTu)
                .AsNoTracking()
                .ToListAsync();

            var cumIds = cums.Select(c => c.ID).ToList();
            var chiTiets = await _context.LC_XuatCapDanhMucChiTiet
                .Where(c => !c.IsDelete && cumIds.Contains(c.ID_Cum))
                .OrderBy(c => c.ThuTu)
                .AsNoTracking()
                .ToListAsync();
            var chiTietByCum = chiTiets.GroupBy(c => c.ID_Cum).ToDictionary(g => g.Key, g => g.ToList());

            var phongBanIds = cums.Where(x => x.ID_PhongBan != null).Select(x => x.ID_PhongBan!.Value).Distinct().ToList();
            var tenPhongBanById = phongBanIds.Count == 0
                ? new Dictionary<int, string?>()
                : await _masterContext.Tbl_PhongBan.AsNoTracking()
                    .Where(p => phongBanIds.Contains(p.ID_PhongBan))
                    .ToDictionaryAsync(p => p.ID_PhongBan, p => p.TenPhongBan);

            return cums.Select(c => new LcXuatCapDanhMucCumDto
            {
                Id = c.ID,
                Scope = c.Scope,
                ThuTu = c.ThuTu,
                TenCan = c.TenCan,
                TenCum = c.TenCum,
                LoaiCoc = c.LoaiCoc,
                NhomTongHop = c.NhomTongHop,
                IdPhongBan = c.ID_PhongBan,
                TenPhongBan = c.ID_PhongBan != null ? tenPhongBanById.GetValueOrDefault(c.ID_PhongBan.Value) : null,
                NgayTao = c.NgayTao,
                ChiTiet = (chiTietByCum.GetValueOrDefault(c.ID) ?? new List<LC_XuatCapDanhMucChiTiet>())
                    .Select(ct => new LcXuatCapDanhMucChiTietDto
                    {
                        Id = ct.ID,
                        IdCum = ct.ID_Cum,
                        ThuTu = ct.ThuTu,
                        ChiTietLo = ct.ChiTietLo,
                    }).ToList(),
            }).ToList();
        }

        public async Task<LC_XuatCapDanhMucCum?> GetCumByIdAsync(int id)
            => await _context.LC_XuatCapDanhMucCum.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);

        public async Task<LC_XuatCapDanhMucCum> AddCumAsync(LC_XuatCapDanhMucCum entity)
        {
            entity.NgayTao = DateTime.Now;
            _context.LC_XuatCapDanhMucCum.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_XuatCapDanhMucCum?> UpdateCumAsync(int id, LC_XuatCapDanhMucCum entity)
        {
            var existing = await _context.LC_XuatCapDanhMucCum.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return null;

            existing.Scope = entity.Scope;
            existing.ThuTu = entity.ThuTu;
            existing.TenCan = entity.TenCan;
            existing.TenCum = entity.TenCum;
            existing.LoaiCoc = entity.LoaiCoc;
            existing.NhomTongHop = entity.NhomTongHop;
            existing.ID_PhongBan = entity.ID_PhongBan;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteCumAsync(int id)
        {
            var existing = await _context.LC_XuatCapDanhMucCum.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;
            existing.IsDelete = true;

            var children = await _context.LC_XuatCapDanhMucChiTiet.Where(c => c.ID_Cum == id && !c.IsDelete).ToListAsync();
            foreach (var c in children) c.IsDelete = true;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<LC_XuatCapDanhMucChiTiet> AddChiTietAsync(LC_XuatCapDanhMucChiTiet entity)
        {
            entity.NgayTao = DateTime.Now;
            _context.LC_XuatCapDanhMucChiTiet.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_XuatCapDanhMucChiTiet?> UpdateChiTietAsync(int id, int thuTu, string? chiTietLo)
        {
            var existing = await _context.LC_XuatCapDanhMucChiTiet.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return null;
            existing.ThuTu = thuTu;
            existing.ChiTietLo = chiTietLo;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteChiTietAsync(int id)
        {
            var existing = await _context.LC_XuatCapDanhMucChiTiet.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;
            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<LcPhongBanOptionDto>> GetPhongBanOptionsAsync()
        {
            return await _masterContext.Tbl_PhongBan.AsNoTracking()
                .OrderBy(p => p.TenPhongBan)
                .Select(p => new LcPhongBanOptionDto
                {
                    IdPhongBan = p.ID_PhongBan,
                    TenPhongBan = p.TenPhongBan,
                    TenNgan = p.TenNgan,
                })
                .ToListAsync();
        }
    }
}
