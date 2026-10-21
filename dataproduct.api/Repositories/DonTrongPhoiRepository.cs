using dataproduct.api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class DonTrongPhoiRepository : IDonTrongPhoiRepository
    {
        private readonly ProductFormContext _context;

        public DonTrongPhoiRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<DonTrongPhoi>> GetAllAsync(string? macPhoi, string? mac, string? kichThuoc, int? isXacNhan = null, bool excludeLocked = false, string? maVatTu = null)
        {
            var query = _context.DonTrongPhois.AsQueryable();

            if (!string.IsNullOrWhiteSpace(macPhoi))
                query = query.Where(x => x.MacPhoi.Contains(macPhoi));
            if (!string.IsNullOrWhiteSpace(mac))
                query = query.Where(x => x.Mac != null && x.Mac.Contains(mac));
            if (!string.IsNullOrWhiteSpace(kichThuoc))
                query = query.Where(x => x.KichThuoc != null && x.KichThuoc.Contains(kichThuoc));
            if (isXacNhan.HasValue)
            {
                if (isXacNhan.Value == 1)
                    query = query.Where(x => x.IsXacNhan == 1);
                else
                    query = query.Where(x => x.IsXacNhan != 1);
            }
            if (excludeLocked)
                query = query.Where(x => x.IsLock != 1);
            if (!string.IsNullOrWhiteSpace(maVatTu))
                query = query.Where(x => x.MaVatTu != null && x.MaVatTu.Contains(maVatTu));

            return await query.OrderBy(x => x.MacPhoi).ThenBy(x => x.KichThuoc).ToListAsync();
        }

        public Task<DonTrongPhoi?> GetByIdAsync(int id)
            => _context.DonTrongPhois.FirstOrDefaultAsync(x => x.Id == id);

        public Task<DonTrongPhoi?> FindByKeyAsync(string macPhoi, string? mac, string? kichThuoc)
            => _context.DonTrongPhois.FirstOrDefaultAsync(x =>
                x.MacPhoi == macPhoi &&
                x.Mac == mac &&
                x.KichThuoc == kichThuoc);

        public async Task AddAsync(DonTrongPhoi entity)
        {
            _context.DonTrongPhois.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(DonTrongPhoi entity)
        {
            _context.DonTrongPhois.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var item = await _context.DonTrongPhois.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return;
            _context.DonTrongPhois.Remove(item);
            await _context.SaveChangesAsync();
        }

        public Task<bool> ExistsAsync(string macPhoi, string? mac, string? kichThuoc, int? excludeId = null)
        {
            var query = _context.DonTrongPhois.Where(x =>
                x.MacPhoi == macPhoi &&
                x.Mac == mac &&
                x.KichThuoc == kichThuoc);
            if (excludeId.HasValue)
                query = query.Where(x => x.Id != excludeId.Value);
            return query.AnyAsync();
        }

        public async Task SyncMaVatTuAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("EXEC sp_Sync_DonTrongPhoi_MaVatTu");
        }

        public async Task<bool> ToggleLockAsync(int id)
        {
            var item = await _context.DonTrongPhois.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return false;
            item.IsLock = item.IsLock == 1 ? 0 : 1;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
