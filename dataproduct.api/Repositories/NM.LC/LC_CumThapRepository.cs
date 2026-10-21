using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    public class LC_CumThapRepository : ILC_CumThapRepository
    {
        private readonly ProductFormContext _context;

        public LC_CumThapRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<List<LcCumThapDto>> GetListAsync(int? scope)
        {
            return await _context.LC_CumThap
                .Where(x => !x.IsDelete && (scope == null || x.Scope == scope))
                .OrderBy(x => x.Scope).ThenBy(x => x.ThuTu).ThenBy(x => x.Ma)
                .Select(x => new LcCumThapDto
                {
                    Id = x.ID,
                    Scope = x.Scope,
                    Ma = x.Ma,
                    Ten = x.Ten,
                    ThuTu = x.ThuTu,
                    NgayTao = x.NgayTao,
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LC_CumThap?> GetByIdAsync(int id)
            => await _context.LC_CumThap.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);

        public async Task<bool> ExistsMaAsync(int scope, string ma, int? excludeId = null)
            => await _context.LC_CumThap.AnyAsync(x => !x.IsDelete
                && x.Scope == scope
                && x.Ma == ma
                && (excludeId == null || x.ID != excludeId));

        public async Task<LC_CumThap> AddAsync(LC_CumThap entity)
        {
            entity.NgayTao = DateTime.Now;
            _context.LC_CumThap.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_CumThap?> UpdateAsync(int id, LC_CumThap entity)
        {
            var existing = await _context.LC_CumThap.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return null;

            existing.Scope = entity.Scope;
            existing.Ma = entity.Ma;
            existing.Ten = entity.Ten;
            existing.ThuTu = entity.ThuTu;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _context.LC_CumThap.FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;
            existing.IsDelete = true; // xóa mềm — danh mục, không xóa vật lý
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
