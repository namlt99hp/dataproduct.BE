using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NMLC
{
    public class LC_ThapThanRepository : ILC_ThapThanRepository
    {
        private readonly ProductFormContext _context;

        public LC_ThapThanRepository(ProductFormContext context)
        {
            _context = context;
        }

        // ─── Cụm tháp (LC_CumThap) ───

        public async Task<List<LC_CumThap>> GetAllNhomAsync(string? searchKey = null, int? scope = null)
        {
            var query = _context.LC_CumThap.AsNoTracking().Where(x => !x.IsDelete);

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                query = query.Where(x => (x.Ten != null && x.Ten.Contains(searchKey))
                                      || x.Ma.Contains(searchKey));
            }

            if (scope.HasValue)
                query = query.Where(x => x.Scope == scope.Value);

            return await query.OrderBy(x => x.ThuTu).ThenBy(x => x.Id).ToListAsync();
        }

        public async Task<LC_CumThap?> GetNhomByIdAsync(int id)
        {
            return await _context.LC_CumThap
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<bool> NhomExistsAsync(int id)
        {
            return await _context.LC_CumThap.AnyAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<LC_CumThap> AddNhomAsync(LC_CumThap entity)
        {
            entity.IsDelete = false;
            entity.NgayTao = DateTime.Now;
            _context.LC_CumThap.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_CumThap?> UpdateNhomAsync(LC_CumThap entity)
        {
            var existing = await _context.LC_CumThap
                .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDelete);
            if (existing == null) return null;

            existing.Scope = entity.Scope;
            existing.Ma = entity.Ma;
            existing.Ten = entity.Ten;
            existing.ThuTu = entity.ThuTu;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteNhomAsync(int id)
        {
            var existing = await _context.LC_CumThap
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
            if (existing == null) return false;

            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Phễu ───

        public async Task<List<LC_TP_ThapThan_Pheu>> GetAllPheuAsync(int? thapThanId = null, string? searchKey = null)
        {
            var query = _context.LC_TP_ThapThan_Pheu.AsNoTracking().Where(x => !x.IsDelete);

            if (thapThanId.HasValue)
                query = query.Where(x => x.ThapThanId == thapThanId.Value);

            if (!string.IsNullOrWhiteSpace(searchKey))
                query = query.Where(x => x.TenPheu != null && x.TenPheu.Contains(searchKey));

            return await query.OrderBy(x => x.ThapThanId).ThenBy(x => x.Id).ToListAsync();
        }

        public async Task<LC_TP_ThapThan_Pheu?> GetPheuByIdAsync(int id)
        {
            return await _context.LC_TP_ThapThan_Pheu
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<bool> PheuExistsAsync(int id)
        {
            return await _context.LC_TP_ThapThan_Pheu.AnyAsync(x => x.Id == id && !x.IsDelete);
        }

        public async Task<LC_TP_ThapThan_Pheu> AddPheuAsync(LC_TP_ThapThan_Pheu entity)
        {
            entity.IsDelete = false;
            _context.LC_TP_ThapThan_Pheu.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LC_TP_ThapThan_Pheu?> UpdatePheuAsync(LC_TP_ThapThan_Pheu entity)
        {
            var existing = await _context.LC_TP_ThapThan_Pheu
                .FirstOrDefaultAsync(x => x.Id == entity.Id && !x.IsDelete);
            if (existing == null) return null;

            existing.ThapThanId = entity.ThapThanId;
            existing.TenPheu = entity.TenPheu;
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeletePheuAsync(int id)
        {
            var existing = await _context.LC_TP_ThapThan_Pheu
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDelete);
            if (existing == null) return false;

            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Chi tiết ───

        public async Task<List<LC_TP_ThapThan_ChiTiet>> GetChiTietAsync(
            DateTime? ngaySx = null, int? ca = null, int? pheuId = null)
        {
            var query = _context.LC_TP_ThapThan_ChiTiet.AsNoTracking().AsQueryable();

            if (ngaySx.HasValue)
            {
                var from = ngaySx.Value.Date;
                var to = from.AddDays(1);
                query = query.Where(x => x.NgaySx >= from && x.NgaySx < to);
            }
            if (ca.HasValue)
                query = query.Where(x => x.Ca == ca.Value);
            if (pheuId.HasValue)
                query = query.Where(x => x.PheuId == pheuId.Value);

            return await query.OrderBy(x => x.NgaySx).ThenBy(x => x.Ca).ThenBy(x => x.ThuTu).ToListAsync();
        }

        public async Task<LC_TP_ThapThan_ChiTiet?> GetChiTietByIdAsync(int id)
        {
            return await _context.LC_TP_ThapThan_ChiTiet.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<LC_TP_ThapThan_ChiTiet> AddChiTietAsync(LC_TP_ThapThan_ChiTiet entity)
        {
            _context.LC_TP_ThapThan_ChiTiet.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<List<LC_TP_ThapThan_ChiTiet>> AddRangeChiTietAsync(IEnumerable<LC_TP_ThapThan_ChiTiet> entities)
        {
            var list = entities.ToList();
            _context.LC_TP_ThapThan_ChiTiet.AddRange(list);
            await _context.SaveChangesAsync();
            return list;
        }

        public async Task<LC_TP_ThapThan_ChiTiet?> UpdateChiTietAsync(LC_TP_ThapThan_ChiTiet entity)
        {
            var existing = await _context.LC_TP_ThapThan_ChiTiet.FirstOrDefaultAsync(x => x.Id == entity.Id);
            if (existing == null) return null;

            _context.Entry(existing).CurrentValues.SetValues(entity);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteChiTietAsync(int id)
        {
            var existing = await _context.LC_TP_ThapThan_ChiTiet.FirstOrDefaultAsync(x => x.Id == id);
            if (existing == null) return false;

            _context.LC_TP_ThapThan_ChiTiet.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<LC_TP_ThapThan_ChiTiet>> ReplaceChiTietByNgayCaAsync(
            DateTime ngaySx, int ca, IEnumerable<LC_TP_ThapThan_ChiTiet> entities)
        {
            var from = ngaySx.Date;
            var to = from.AddDays(1);
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();
            var old = await _context.LC_TP_ThapThan_ChiTiet
                .Where(x => x.NgaySx >= from && x.NgaySx < to && x.Ca == ca)
                .ToListAsync();
            _context.LC_TP_ThapThan_ChiTiet.RemoveRange(old);

            foreach (var e in list)
            {
                e.NgaySx = from;
                e.Ca = ca;
            }
            _context.LC_TP_ThapThan_ChiTiet.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }
    }
}
