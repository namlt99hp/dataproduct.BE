using dataproduct.api.Models;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class MayDucRepository : IMayDucRepository
    {
        private readonly ProductFormContext _context;

        public MayDucRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<MayDuc>> GetAllAsync(byte? nhaMay, bool? isLock, string? tenMayDuc)
        {
            var query = _context.MayDucs.AsQueryable();

            if (nhaMay.HasValue)
                query = query.Where(x => x.NhaMay == nhaMay.Value);
            if (isLock.HasValue)
                query = query.Where(x => x.IsLock == isLock.Value);
            if (!string.IsNullOrWhiteSpace(tenMayDuc))
                query = query.Where(x => x.TenMayDuc.Contains(tenMayDuc));

            return await query.OrderBy(x => x.NhaMay).ThenBy(x => x.TenMayDuc).ToListAsync();
        }

        public Task<MayDuc?> GetByIdAsync(int id)
            => _context.MayDucs.FirstOrDefaultAsync(x => x.Id == id);

        public async Task AddAsync(MayDuc entity)
        {
            _context.MayDucs.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(MayDuc entity)
        {
            _context.MayDucs.Update(entity);
            await _context.SaveChangesAsync();
        }

        public Task<bool> ExistsByTenAsync(string tenMayDuc, byte nhaMay, int? excludeId = null)
        {
            var query = _context.MayDucs.Where(x => x.TenMayDuc == tenMayDuc && x.NhaMay == nhaMay);
            if (excludeId.HasValue)
                query = query.Where(x => x.Id != excludeId.Value);
            return query.AnyAsync();
        }
    }
}

