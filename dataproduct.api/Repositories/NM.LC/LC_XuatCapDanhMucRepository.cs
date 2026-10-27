using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Trang quản lý danh mục "Nơi nhận" vùng Cấp cốc (CHA) + Chi tiết (CON) — xem
    // LC_XuatCapDanhMucCum/ChiTiet, .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11 (sửa lại
    // 2026-10-26 — tên cũ "Cụm nhận" bị nhầm, đây thực chất là "Nơi nhận"; "Cụm" THẬT ở
    // LC_XuatCapCum). ID_PhongBan là FK LOGIC tới PRODUCTDATA.Tbl_PhongBan (khác database) nên
    // resolve TenPhongBan bằng round-trip riêng, giống LC_VatTuMaLoLookupRepository.
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
                .Where(c => !c.IsDelete && cumIds.Contains(c.ID_NoiNhan))
                .OrderBy(c => c.ThuTu)
                .AsNoTracking()
                .ToListAsync();
            var chiTietByNoiNhan = chiTiets.GroupBy(c => c.ID_NoiNhan).ToDictionary(g => g.Key, g => g.ToList());

            var cumThatById = await _context.LC_XuatCapCum.AsNoTracking().ToDictionaryAsync(c => c.ID);
            var loaiCocById = await _context.LC_XuatCapLoaiCoc.AsNoTracking().ToDictionaryAsync(l => l.ID);
            var loaiNhanById = await _context.LC_XuatCapLoaiNhan.AsNoTracking().ToDictionaryAsync(l => l.ID);

            var phongBanIds = cums.Where(x => x.ID_PhongBan != null).Select(x => x.ID_PhongBan!.Value).Distinct().ToList();
            var tenPhongBanById = phongBanIds.Count == 0
                ? new Dictionary<int, string?>()
                : await _masterContext.Tbl_PhongBan.AsNoTracking()
                    .Where(p => phongBanIds.Contains(p.ID_PhongBan))
                    .ToDictionaryAsync(p => p.ID_PhongBan, p => p.TenPhongBan);

            return cums.Select(c =>
            {
                var cumThat = cumThatById.GetValueOrDefault(c.ID_Cum);
                var loaiCoc = loaiCocById.GetValueOrDefault(c.ID_LoaiCoc);
                return new LcXuatCapDanhMucCumDto
                {
                    Id = c.ID,
                    Scope = c.Scope,
                    IdCum = c.ID_Cum,
                    MaCum = cumThat?.Ma ?? string.Empty,
                    TenCumThat = cumThat?.Ten ?? string.Empty,
                    ThuTu = c.ThuTu,
                    TenCan = c.TenCan,
                    NoiNhan = c.NoiNhan,
                    IdLoaiCoc = c.ID_LoaiCoc,
                    TenLoaiCoc = loaiCoc?.Ten ?? string.Empty,
                    NhomTongHop = c.NhomTongHop,
                    IdPhongBan = c.ID_PhongBan,
                    TenPhongBan = c.ID_PhongBan != null ? tenPhongBanById.GetValueOrDefault(c.ID_PhongBan.Value) : null,
                    NgayTao = c.NgayTao,
                    ChiTiet = (chiTietByNoiNhan.GetValueOrDefault(c.ID) ?? new List<LC_XuatCapDanhMucChiTiet>())
                        .Select(ct => new LcXuatCapDanhMucChiTietDto
                        {
                            Id = ct.ID,
                            IdNoiNhan = ct.ID_NoiNhan,
                            ThuTu = ct.ThuTu,
                            ChiTietLo = ct.ChiTietLo,
                            IdLoaiNhan = ct.ID_LoaiNhan,
                            TenLoaiNhan = ct.ID_LoaiNhan != null ? loaiNhanById.GetValueOrDefault(ct.ID_LoaiNhan.Value)?.Ten : null,
                        }).ToList(),
                };
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
            existing.ID_Cum = entity.ID_Cum;
            existing.ThuTu = entity.ThuTu;
            existing.TenCan = entity.TenCan;
            existing.NoiNhan = entity.NoiNhan;
            existing.ID_LoaiCoc = entity.ID_LoaiCoc;
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

            var children = await _context.LC_XuatCapDanhMucChiTiet.Where(c => c.ID_NoiNhan == id && !c.IsDelete).ToListAsync();
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

        public async Task<LC_XuatCapDanhMucChiTiet?> UpdateChiTietAsync(int id, int thuTu, string? chiTietLo, int? idLoaiNhan)
        {
            var existing = await _context.LC_XuatCapDanhMucChiTiet.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return null;
            existing.ThuTu = thuTu;
            existing.ChiTietLo = chiTietLo;
            existing.ID_LoaiNhan = idLoaiNhan;
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

        public async Task<List<LcXuatCapCumDto>> GetCumOptionsAsync(int? scope)
        {
            return await _context.LC_XuatCapCum.AsNoTracking()
                .Where(c => !c.IsDelete && (scope == null || c.Scope == scope))
                .OrderBy(c => c.ThuTu)
                .Select(c => new LcXuatCapCumDto { Id = c.ID, Scope = c.Scope, Ma = c.Ma, Ten = c.Ten, ThuTu = c.ThuTu })
                .ToListAsync();
        }

        public async Task<List<LcXuatCapLoaiDto>> GetLoaiCocOptionsAsync()
        {
            return await _context.LC_XuatCapLoaiCoc.AsNoTracking()
                .Where(l => !l.IsDelete)
                .OrderBy(l => l.ThuTu)
                .Select(l => new LcXuatCapLoaiDto { Id = l.ID, Ten = l.Ten, ThuTu = l.ThuTu })
                .ToListAsync();
        }

        public async Task<List<LcXuatCapLoaiDto>> GetLoaiNhanOptionsAsync()
        {
            return await _context.LC_XuatCapLoaiNhan.AsNoTracking()
                .Where(l => !l.IsDelete)
                .OrderBy(l => l.ThuTu)
                .Select(l => new LcXuatCapLoaiDto { Id = l.ID, Ten = l.Ten, ThuTu = l.ThuTu })
                .ToListAsync();
        }
    }
}
