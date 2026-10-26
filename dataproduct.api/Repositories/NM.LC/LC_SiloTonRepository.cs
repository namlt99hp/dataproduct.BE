using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    public class LC_SiloTonRepository : ILC_SiloTonRepository
    {
        private readonly ProductFormContext _context;

        public LC_SiloTonRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<List<LcSiloTonDto>> GetListAsync(int? scope)
        {
            return await _context.LC_SiloTon
                .Where(x => !x.IsDelete && (scope == null || x.Scope == scope))
                .OrderBy(x => x.Scope).ThenBy(x => x.TenSilo)
                .Select(x => new LcSiloTonDto
                {
                    Id = x.ID,
                    Scope = x.Scope,
                    TenSilo = x.TenSilo,
                    LoaiLieu = x.LoaiLieu,
                    TenNhapLieu = x.TenNhapLieu,
                    CongThuc = x.CongThuc,
                    NgayTao = x.NgayTao,
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LC_SiloTon?> GetByIdAsync(int id)
            => await _context.LC_SiloTon.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);

        public async Task<Dictionary<int, LC_SiloTon>> GetByIdsAsync(IEnumerable<int> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return new Dictionary<int, LC_SiloTon>();
            return await _context.LC_SiloTon.AsNoTracking()
                .Where(x => idList.Contains(x.ID))
                .ToDictionaryAsync(x => x.ID);
        }

        public async Task<LC_SiloTon> AddAsync(LC_SiloTon entity)
        {
            entity.NgayTao = DateTime.Now;
            _context.LC_SiloTon.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_SiloTon?> UpdateAsync(int id, LC_SiloTon entity)
        {
            var existing = await _context.LC_SiloTon.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return null;

            existing.Scope = entity.Scope;
            existing.TenSilo = entity.TenSilo;
            existing.LoaiLieu = entity.LoaiLieu;
            existing.TenNhapLieu = entity.TenNhapLieu;
            existing.CongThuc = entity.CongThuc;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.LC_SiloTon.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;
            existing.IsDelete = true; // xóa mềm — danh mục, không xóa vật lý
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
