using dataproduct.api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class BKNguyenLieuRepository : IBKNguyenLieuRepository
    {
        private readonly ProductFormContext _context;

        public BKNguyenLieuRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<BkNguyenLieu>> GetAllAsync(DateOnly? NgaySX,int? Ca, string? Kip)
        {
            var query = _context.BkNguyenLieus.AsQueryable();

            if (NgaySX.HasValue)
                query = query.Where(x => x.NgaySx == NgaySX);

            if (Ca.HasValue)
                query = query.Where(x => x.Ca == Ca.Value);

            if (!string.IsNullOrEmpty(Kip))
                query = query.Where(x => x.Kip == Kip);

            return await query.ToListAsync();
        }

        public async Task<BkNguyenLieu?> GetByIdAsync(int id)
        {
            return await _context.BkNguyenLieus.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task AddAsync(BkNguyenLieu entity)
        {
            _context.BkNguyenLieus.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(BkNguyenLieu entity)
        {
            _context.BkNguyenLieus.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var item = await _context.BmPhieus.FindAsync(id);
            if (item != null)
            {
                _context.BmPhieus.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.BkPhoiThep.AnyAsync(e => e.Id == id);
        }

        public async Task<List<CanBangTaiRow>> GetCanBangTaiAsync(DateOnly ngaySX)
        {
            var param = new SqlParameter("@NgaySX", ngaySX.ToDateTime(TimeOnly.MinValue).Date);
            return await _context.CanBangTaiRows
                .FromSqlRaw("EXEC [dbo].[sp_Get_NMNL_CanBangTai] @NgaySX", param)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
