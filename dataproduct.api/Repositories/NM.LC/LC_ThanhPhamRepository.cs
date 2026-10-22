using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NMLC
{
    public class LC_ThanhPhamRepository : ILC_ThanhPhamRepository
    {
        private readonly ProductFormContext _context;

        public LC_ThanhPhamRepository(ProductFormContext context)
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

            return await query.OrderBy(x => x.ThuTu).ThenBy(x => x.ID).ToListAsync();
        }

        public async Task<LC_CumThap?> GetNhomByIdAsync(int id)
        {
            return await _context.LC_CumThap
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
        }

        public async Task<bool> NhomExistsAsync(int id)
        {
            return await _context.LC_CumThap.AnyAsync(x => x.ID == id && !x.IsDelete);
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
                .FirstOrDefaultAsync(x => x.ID == entity.ID && !x.IsDelete);
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
                .FirstOrDefaultAsync(x => x.ID == id && !x.IsDelete);
            if (existing == null) return false;

            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Phễu ───

        public async Task<List<LC_TP_ThapThan_Pheu>> GetAllPheuAsync(
            int? thapThanId = null, string? searchKey = null, int? scope = null)
        {
            // Join cụm tháp: chỉ lấy phễu của cụm còn hiệu lực, lọc được theo xưởng (scope)
            var query =
                from p in _context.LC_TP_ThapThan_Pheu.AsNoTracking()
                join c in _context.LC_CumThap.AsNoTracking() on p.ThapThanId equals c.ID
                where !p.IsDelete && !c.IsDelete
                select new { p, c };

            if (thapThanId.HasValue)
                query = query.Where(x => x.p.ThapThanId == thapThanId.Value);

            if (scope.HasValue)
                query = query.Where(x => x.c.Scope == scope.Value);

            if (!string.IsNullOrWhiteSpace(searchKey))
                query = query.Where(x => x.p.TenPheu != null && x.p.TenPheu.Contains(searchKey));

            // Thứ tự: cụm tháp (ThuTu), rồi thứ tự phễu (ThuTu; chưa đặt thì xếp cuối), rồi tên phễu
            return await query
                .OrderBy(x => x.c.ThuTu).ThenBy(x => x.c.ID)
                .ThenBy(x => x.p.ThuTu ?? int.MaxValue).ThenBy(x => x.p.TenPheu).ThenBy(x => x.p.Id)
                .Select(x => x.p)
                .ToListAsync();
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
            existing.ThuTu = entity.ThuTu;
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

        // ─── Kết quả phân tích than (LC_TP_KetQuaPhanTichThan) ───

        public async Task<List<LC_TP_KetQuaPhanTichThan>> GetKetQuaPhanTichAsync(Guid phieuCaId, int scope)
        {
            return await _context.LC_TP_KetQuaPhanTichThan
                .AsNoTracking()
                .Where(x => x.PhieuCaId == phieuCaId && x.Scope == scope)
                .OrderBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<LC_TP_KetQuaPhanTichThan>> ReplaceKetQuaPhanTichAsync(
            Guid phieuCaId, int scope, IEnumerable<LC_TP_KetQuaPhanTichThan> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_KetQuaPhanTichThan
                .Where(x => x.PhieuCaId == phieuCaId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_KetQuaPhanTichThan.RemoveRange(old);

            foreach (var e in list)
            {
                e.PhieuCaId = phieuCaId;
                e.Scope = scope;
            }
            _context.LC_TP_KetQuaPhanTichThan.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteKetQuaPhanTichAsync(Guid phieuCaId, int scope)
        {
            return await _context.LC_TP_KetQuaPhanTichThan
                .Where(x => x.PhieuCaId == phieuCaId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        // ─── Sự cố trong ca (LC_TP_SuCoTrongCa) ───

        public async Task<List<LC_TP_SuCoTrongCa>> GetSuCoAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_SuCoTrongCa
                .AsNoTracking()
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .OrderBy(x => x.Stt)
                .ToListAsync();
        }

        public async Task<List<LC_TP_SuCoTrongCa>> ReplaceSuCoAsync(
            Guid phieuId, int scope, IEnumerable<LC_TP_SuCoTrongCa> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_SuCoTrongCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_SuCoTrongCa.RemoveRange(old);

            for (var i = 0; i < list.Count; i++)
            {
                list[i].PhieuId = phieuId;
                list[i].Scope = scope;
                list[i].Stt = i + 1;
            }
            _context.LC_TP_SuCoTrongCa.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteSuCoAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_SuCoTrongCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        // ─── Tiêu hao (LC_TP_TieuHao / LC_TP_DanhMucTieuHao) ───

        public async Task<List<LC_TP_NhomTieuHao>> GetNhomTieuHaoAsync()
        {
            return await _context.LC_TP_NhomTieuHao.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        }

        public async Task<List<LC_TP_DanhMucTieuHao>> GetDanhMucTieuHaoAsync()
        {
            return await _context.LC_TP_DanhMucTieuHao
                .AsNoTracking()
                .Where(x => !x.IsDelete)
                .OrderBy(x => x.ThuTu).ThenBy(x => x.Id)
                .ToListAsync();
        }

        public async Task<List<LC_TP_TieuHao>> GetTieuHaoAsync(Guid phieuId, int scope)
        {
            // Sắp theo thứ tự hạng mục trong danh mục để FE hiển thị đúng thứ tự
            return await (from t in _context.LC_TP_TieuHao.AsNoTracking()
                          join d in _context.LC_TP_DanhMucTieuHao.AsNoTracking() on t.HangMucId equals d.Id
                          where t.PhieuId == phieuId && t.Scope == scope
                          orderby d.ThuTu, d.Id
                          select t).ToListAsync();
        }

        public async Task<List<LC_TP_TieuHao>> ReplaceTieuHaoAsync(
            Guid phieuId, int scope, IEnumerable<LC_TP_TieuHao> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_TieuHao
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_TieuHao.RemoveRange(old);

            foreach (var e in list)
            {
                e.Id = 0;
                e.PhieuId = phieuId;
                e.Scope = scope;
            }
            _context.LC_TP_TieuHao.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteTieuHaoAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_TieuHao
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        // ─── Chất lượng cốc (LC_TP_ChiTieuCoc / LC_TP_TCNBCoc / LC_TP_ChatLuongCocCa) ───

        public async Task<List<LC_TP_ChiTieuCoc>> GetChiTieuCocAsync(bool includeDeleted = false)
        {
            var query = _context.LC_TP_ChiTieuCoc.AsNoTracking();
            if (!includeDeleted) query = query.Where(x => !x.IsDelete);
            return await query.OrderBy(x => x.ThuTu).ThenBy(x => x.Id).ToListAsync();
        }

        public async Task<List<LC_TP_TCNBCoc>> GetTcnbCocAsync()
        {
            return await _context.LC_TP_TCNBCoc.AsNoTracking().OrderBy(x => x.ChiTieuId).ThenBy(x => x.Loai).ToListAsync();
        }

        public async Task<List<LC_TP_ChatLuongCocCa>> GetChatLuongCocAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_ChatLuongCocCa
                .AsNoTracking()
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .OrderBy(x => x.ChiTieuId).ThenBy(x => x.Loai).ThenBy(x => x.Lan)
                .ToListAsync();
        }

        public async Task<List<LC_TP_ChatLuongCocCa>> ReplaceChatLuongCocAsync(
            Guid phieuId, int scope, IEnumerable<LC_TP_ChatLuongCocCa> entities)
        {
            var list = entities.ToList();

            await using var tx = await _context.Database.BeginTransactionAsync();

            var old = await _context.LC_TP_ChatLuongCocCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ToListAsync();
            _context.LC_TP_ChatLuongCocCa.RemoveRange(old);

            foreach (var e in list)
            {
                e.Id = 0;
                e.PhieuId = phieuId;
                e.Scope = scope;
            }
            _context.LC_TP_ChatLuongCocCa.AddRange(list);

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return list;
        }

        public async Task<int> DeleteChatLuongCocAsync(Guid phieuId, int scope)
        {
            return await _context.LC_TP_ChatLuongCocCa
                .Where(x => x.PhieuId == phieuId && x.Scope == scope)
                .ExecuteDeleteAsync();
        }

        public async Task<List<int>> FindHangMucKhongTonTaiAsync(IEnumerable<int> hangMucIds)
        {
            var ids = hangMucIds.Distinct().ToList();
            var existing = await _context.LC_TP_DanhMucTieuHao
                .Where(x => ids.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();
            return ids.Except(existing).ToList();
        }
    }
}
