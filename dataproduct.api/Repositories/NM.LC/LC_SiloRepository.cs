using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    public class LC_SiloRepository : ILC_SiloRepository
    {
        private readonly ProductFormContext _context;

        public LC_SiloRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<List<LcSiloDto>> GetListAsync(int? scope)
        {
            return await _context.LC_Silo
                .Where(x => !x.IsDelete && (scope == null || x.Scope == scope))
                .OrderBy(x => x.Scope).ThenBy(x => x.ThuTu ?? x.SiloSo).ThenBy(x => x.SiloSo)
                .Select(x => new LcSiloDto
                {
                    Id = x.ID,
                    Scope = x.Scope,
                    SiloSo = x.SiloSo,
                    TenSilo = x.TenSilo,
                    LoaiSilo = x.LoaiSilo,
                    ThuTu = x.ThuTu,
                    NgayTao = x.NgayTao,
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LC_Silo?> GetByIdAsync(int id)
            => await _context.LC_Silo.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);

        public async Task<bool> ExistsSiloSoAsync(int scope, int siloSo, int? excludeId = null)
            => await _context.LC_Silo.AnyAsync(x => !x.IsDelete
                && x.Scope == scope
                && x.SiloSo == siloSo
                && (excludeId == null || x.ID != excludeId));

        public async Task<LC_Silo> AddAsync(LC_Silo entity)
        {
            entity.NgayTao = DateTime.Now;
            _context.LC_Silo.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_Silo?> UpdateAsync(int id, LC_Silo entity)
        {
            var existing = await _context.LC_Silo.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return null;

            existing.Scope = entity.Scope;
            existing.SiloSo = entity.SiloSo;
            existing.TenSilo = entity.TenSilo;
            existing.LoaiSilo = entity.LoaiSilo;
            existing.ThuTu = entity.ThuTu;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.LC_Silo.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;
            existing.IsDelete = true; // xóa mềm — danh mục, không xóa vật lý
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
