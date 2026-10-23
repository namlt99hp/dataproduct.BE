using dataproduct.api.DTOs.NMTKVV_Dto;
using dataproduct.api.Models;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NMTKVV
{
    public class TKVV_SiloRepository : ITKVV_SiloRepository
    {
        private readonly ProductFormContext _context;

        public TKVV_SiloRepository(ProductFormContext context)
        {
            _context = context;
        }

        // ─── TKVV_NguyenVatLieu ───────────────────────────────────────────────

        public async Task<List<TKVVNguyenVatLieuDto>> GetNvlListAsync(string? maBM, string? scope)
        {
            return await _context.TKVV_NguyenVatLieu
                .Where(x => (string.IsNullOrWhiteSpace(maBM) || x.MaBM == maBM)
                         && (string.IsNullOrWhiteSpace(scope) || x.Scope.Contains(scope))
                         && x.TrangThai == true)
                .OrderBy(x => x.ThuTu)
                .Select(x => new TKVVNguyenVatLieuDto
                {
                    Id = x.ID,
                    MaBM = x.MaBM,
                    TenNVL = x.TenNVL,
                    DonViTinh = x.DonViTinh,
                    ThuTu = x.ThuTu,
                    TrangThai = x.TrangThai,
                    GhiChu = x.GhiChu,
                    Scope = x.Scope,
                    TenScope = x.TenScope,
                })
                .AsNoTracking()
                .ToListAsync();
        }

        // ─── TKVV_Silo ────────────────────────────────────────────────────────

        public async Task<List<TKVVSiloDto>> GetSiloListAsync(string? scope)
        {
            return await _context.TKVV_Silo
                .Where(x => scope == null || x.Scope == scope)
                .OrderBy(x => x.Scope).ThenBy(x => x.MaSilo)
                .Select(x => new TKVVSiloDto
                {
                    Id = x.ID,
                    MaXuong = x.MaXuong,
                    Scope = x.Scope,
                    TenScope = x.TenScope,
                    MaSilo = x.MaSilo,
                    TenSilo = x.TenSilo,
                    GhiChu = x.GhiChu,
                    TrangThai = x.TrangThai,
                    NgayCapNhat = x.NgayCapNhat,
                })
                .ToListAsync();
        }

        public async Task<TKVV_Silo?> GetSiloByIdAsync(int id)
            => await _context.TKVV_Silo.FindAsync(id);

        public async Task<TKVV_NguyenVatLieu?> GetNvlByIdAsync(int id)
            => await _context.TKVV_NguyenVatLieu.FindAsync(id);

        public async Task<TKVV_Silo> AddSiloAsync(TKVV_Silo entity)
        {
            entity.NgayCapNhat = DateTime.Now;
            _context.TKVV_Silo.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<TKVV_Silo?> UpdateSiloAsync(int id, TKVV_Silo entity)
        {
            var existing = await _context.TKVV_Silo.FindAsync(id);
            if (existing == null) return null;

            existing.MaXuong = entity.MaXuong;
            existing.Scope = entity.Scope;
            existing.TenScope = entity.TenScope;
            existing.MaSilo = entity.MaSilo;
            existing.TenSilo = entity.TenSilo;
            existing.GhiChu = entity.GhiChu;
            existing.TrangThai = entity.TrangThai;
            existing.NgayCapNhat = DateTime.Now;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteSiloAsync(int id)
        {
            var entity = await _context.TKVV_Silo.FindAsync(id);
            if (entity == null) return false;
            _context.TKVV_Silo.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── TKVV_NVL_SiloMapping ─────────────────────────────────────────────

        public async Task<List<TKVVNvlSiloMappingDto>> GetNvlSiloMappingListAsync(string? maBM, string? scope, int? nvlId, int? siloId, DateOnly? ngaySX = null, int? ca = null)
        {
            var query = from m in _context.TKVV_NVL_SiloMapping
                        join nvl in _context.TKVV_NguyenVatLieu on m.NguyenVatLieuID equals nvl.ID into nvlGroup
                        from nvl in nvlGroup.DefaultIfEmpty()
                        join silo in _context.TKVV_Silo on m.SiloID equals silo.ID into siloGroup
                        from silo in siloGroup.DefaultIfEmpty()
                        where (string.IsNullOrWhiteSpace(maBM) || m.MaBM == maBM)
                           && (string.IsNullOrWhiteSpace(scope) || m.Scope == scope)
                           && (nvlId == null || m.NguyenVatLieuID == nvlId)
                           && (siloId == null || m.SiloID == siloId)
                           && (ngaySX == null || m.NgaySX == ngaySX)
                           && (ca == null || m.Ca == ca)
                           && m.TrangThai == true
                        orderby m.ThuTu, m.NgaySX descending, m.Ca, m.NguyenVatLieuID
                        select new TKVVNvlSiloMappingDto
                        {
                            Id = m.ID,
                            MaBM = m.MaBM,
                            NguyenVatLieuID = m.NguyenVatLieuID,
                            TenNVL = nvl != null ? nvl.TenNVL : null,
                            ScopeNVL = nvl != null ? nvl.Scope : null,
                            Scope = m.Scope,
                            SiloID = m.SiloID,
                            TenSilo = silo != null ? silo.TenSilo : null,
                            MaSilo = silo != null ? silo.MaSilo : null,
                            Ca = m.Ca,
                            NgaySX = m.NgaySX,
                            ThuTu = m.ThuTu,
                            GhiChu = m.GhiChu,
                            TrangThai = m.TrangThai,
                            NgayCapNhat = m.NgayCapNhat,
                        };
            return await query.ToListAsync();
        }

        public async Task<List<TKVVNvlSiloMappingDto>> GetNearestMappingAsync(string? maBM, string scope, DateOnly beforeDate)
        {
            // Nếu đã có data cho beforeDate thì trả về luôn
            var existsForDate = await _context.TKVV_NVL_SiloMapping
                .AnyAsync(m => (string.IsNullOrWhiteSpace(maBM) || m.MaBM == maBM)
                            && m.Scope == scope
                            && m.NgaySX == beforeDate
                            && m.TrangThai == true);

            if (!existsForDate)
            {
                // Tìm ngày gần nhất trước beforeDate có data
                var nearestNgaySX = await _context.TKVV_NVL_SiloMapping
                    .Where(m => (string.IsNullOrWhiteSpace(maBM) || m.MaBM == maBM)
                             && m.Scope == scope
                             && m.NgaySX < beforeDate
                             && m.TrangThai == true)
                    .OrderByDescending(m => m.NgaySX)
                    .Select(m => m.NgaySX)
                    .FirstOrDefaultAsync();

                if (nearestNgaySX == default) return new();

                // Clone các record của ngày gần nhất sang beforeDate
                var sourceRecords = await _context.TKVV_NVL_SiloMapping
                    .Where(m => (string.IsNullOrWhiteSpace(maBM) || m.MaBM == maBM)
                             && m.Scope == scope
                             && m.NgaySX == nearestNgaySX
                             && m.TrangThai == true)
                    .ToListAsync();

                var cloned = sourceRecords.Select(m => new TKVV_NVL_SiloMapping
                {
                    MaBM = m.MaBM,
                    Scope = m.Scope,
                    NgaySX = beforeDate,
                    NguyenVatLieuID = m.NguyenVatLieuID,
                    SiloID = m.SiloID,
                    Ca = m.Ca,
                    ThuTu = m.ThuTu,
                    GhiChu = m.GhiChu,
                    TrangThai = true,
                    NgayCapNhat = DateTime.Now,
                }).ToList();

                _context.TKVV_NVL_SiloMapping.AddRange(cloned);
                await _context.SaveChangesAsync();
            }

            return await (from m in _context.TKVV_NVL_SiloMapping
                          join nvl in _context.TKVV_NguyenVatLieu on m.NguyenVatLieuID equals nvl.ID into nvlG
                          from nvl in nvlG.DefaultIfEmpty()
                          join silo in _context.TKVV_Silo on m.SiloID equals silo.ID into siloG
                          from silo in siloG.DefaultIfEmpty()
                          where (string.IsNullOrWhiteSpace(maBM) || m.MaBM == maBM)
                             && m.Scope == scope
                             && m.NgaySX == beforeDate
                             && m.TrangThai == true
                          orderby m.Ca, m.ThuTu, m.NguyenVatLieuID
                          select new TKVVNvlSiloMappingDto
                          {
                              Id = m.ID,
                              MaBM = m.MaBM,
                              NguyenVatLieuID = m.NguyenVatLieuID,
                              TenNVL = nvl != null ? nvl.TenNVL : null,
                              ScopeNVL = nvl != null ? nvl.Scope : null,
                              Scope = m.Scope,
                              SiloID = m.SiloID,
                              TenSilo = silo != null ? silo.TenSilo : null,
                              MaSilo = silo != null ? silo.MaSilo : null,
                              Ca = m.Ca,
                              NgaySX = m.NgaySX,
                              ThuTu = m.ThuTu,
                              GhiChu = m.GhiChu,
                              TrangThai = m.TrangThai,
                              NgayCapNhat = m.NgayCapNhat,
                          }).ToListAsync();
        }

        public async Task<int> BatchCreateNvlSiloMappingAsync(BatchCreateNvlSiloMappingDto dto)
        {
            var ngaySX = dto.NgaySX;
            var scope = dto.Scope;
            var cas = dto.Rows.Select(r => r.Ca).Distinct().ToList();

            var existing = await _context.TKVV_NVL_SiloMapping
                .Where(m => m.Scope == scope && m.NgaySX == ngaySX && cas.Contains(m.Ca))
                .ToListAsync();

            var submittedSiloIds = dto.Rows
                .Where(r => r.SiloID.HasValue)
                .Select(r => r.SiloID!.Value)
                .ToHashSet();

            // Phân loại từng group: SiloID còn trong submit → giữ primary (ID nhỏ nhất), xóa duplicate
            //                        SiloID user đã xóa    → xóa hết
            var primaryBySilo = new Dictionary<int, TKVV_NVL_SiloMapping>();
            var toRemove = new List<TKVV_NVL_SiloMapping>();
            foreach (var grp in existing.Where(m => m.SiloID.HasValue).GroupBy(m => m.SiloID!.Value))
            {
                var sorted = grp.OrderBy(m => m.ID).ToList();
                if (submittedSiloIds.Contains(grp.Key))
                {
                    primaryBySilo[grp.Key] = sorted[0];
                    toRemove.AddRange(sorted.Skip(1));
                }
                else
                {
                    toRemove.AddRange(sorted);
                }
            }
            if (toRemove.Count > 0)
                _context.TKVV_NVL_SiloMapping.RemoveRange(toRemove);

            int inserted = 0;
            for (int i = 0; i < dto.Rows.Count; i++)
            {
                var r = dto.Rows[i];
                if (r.SiloID.HasValue && primaryBySilo.TryGetValue(r.SiloID.Value, out var rec))
                {
                    rec.NguyenVatLieuID = r.NguyenVatLieuID;
                    rec.ThuTu = r.ThuTu ?? (i + 1);
                    rec.MaBM = dto.MaBM;
                    rec.TrangThai = true;
                    rec.NgayCapNhat = DateTime.Now;
                }
                else
                {
                    _context.TKVV_NVL_SiloMapping.Add(new TKVV_NVL_SiloMapping
                    {
                        MaBM = dto.MaBM,
                        Scope = scope,
                        NgaySX = ngaySX,
                        NguyenVatLieuID = r.NguyenVatLieuID,
                        SiloID = r.SiloID,
                        Ca = r.Ca,
                        ThuTu = r.ThuTu ?? (i + 1),
                        TrangThai = true,
                        NgayCapNhat = DateTime.Now,
                    });
                    inserted++;
                }
            }

            await _context.SaveChangesAsync();
            return inserted;
        }

        public async Task<List<TKVV_NVL_SiloMapping>> EnsureMappingAsync(DateOnly ngaySX, int ca, string scope)
        {
            var siloIds = await _context.TKVV_Silo
                .Where(x => x.Scope == scope && x.TrangThai)
                .Select(x => x.ID)
                .ToListAsync();

            var existing = await _context.TKVV_NVL_SiloMapping
                .Where(m => m.SiloID.HasValue && siloIds.Contains(m.SiloID.Value)
                         && m.Ca == ca && m.NgaySX == ngaySX && m.TrangThai && m.Scope == scope)
                .ToListAsync();

            // Đã đủ mapping cho tất cả silo đang hoạt động → không cần clone thêm
            if (existing.Count >= siloIds.Count) return existing;

            var existingSiloIds = existing
                .Where(m => m.SiloID.HasValue)
                .Select(m => m.SiloID!.Value)
                .ToHashSet();

            // Ca kề trước: Ca 1 (ngày) ← Ca 2 (đêm) hôm trước; Ca 2 (đêm) ← Ca 1 (ngày) cùng ngày
            var prevNgaySX = ca == 1 ? ngaySX.AddDays(-1) : ngaySX;
            var prevCa = ca == 1 ? 2 : 1;

            var toClone = await _context.TKVV_NVL_SiloMapping
                .Where(m => m.SiloID.HasValue && siloIds.Contains(m.SiloID.Value)
                         && m.NgaySX == prevNgaySX && m.Ca == prevCa && m.TrangThai)
                .ToListAsync();

            // Fallback: ca kề trước chưa có → tìm ca gần nhất trước (ngaySX, ca) hiện tại
            if (toClone.Count == 0)
            {
                var historical = await _context.TKVV_NVL_SiloMapping
                    .Where(m => m.SiloID.HasValue && siloIds.Contains(m.SiloID.Value)
                             && (m.NgaySX < prevNgaySX || (m.NgaySX == prevNgaySX && m.Ca < prevCa))
                             && m.TrangThai)
                    .ToListAsync();

                if (historical.Count == 0) return existing;

                var nearest = historical
                    .OrderByDescending(m => m.NgaySX)
                    .ThenByDescending(m => m.Ca)
                    .First();

                toClone = historical
                    .Where(m => m.NgaySX == nearest.NgaySX && m.Ca == nearest.Ca)
                    .ToList();
            }

            if (toClone.Count == 0) return existing;

            // Chỉ clone những silo chưa có trong existing
            var cloned = toClone
                .Where(src => src.SiloID.HasValue && !existingSiloIds.Contains(src.SiloID.Value))
                .Select(src => new TKVV_NVL_SiloMapping
                {
                    MaBM            = src.MaBM,
                    NguyenVatLieuID = src.NguyenVatLieuID,
                    Scope           = src.Scope,
                    SiloID          = src.SiloID,
                    Ca              = ca,
                    NgaySX          = ngaySX,
                    ThuTu           = src.ThuTu,
                    GhiChu          = src.GhiChu,
                    TrangThai       = true,
                    NgayCapNhat     = DateTime.Now,
                }).ToList();

            if (cloned.Count > 0)
            {
                _context.TKVV_NVL_SiloMapping.AddRange(cloned);
                await _context.SaveChangesAsync();
            }

            return existing.Concat(cloned).ToList();
        }

        public async Task<TKVV_NVL_SiloMapping?> GetNvlSiloMappingByIdAsync(int id)
            => await _context.TKVV_NVL_SiloMapping.FindAsync(id);

        public async Task<TKVV_NVL_SiloMapping> AddNvlSiloMappingAsync(TKVV_NVL_SiloMapping entity)
        {
            entity.NgayCapNhat = DateTime.Now;
            _context.TKVV_NVL_SiloMapping.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<TKVV_NVL_SiloMapping?> UpdateNvlSiloMappingAsync(int id, TKVV_NVL_SiloMapping entity)
        {
            var existing = await _context.TKVV_NVL_SiloMapping.FindAsync(id);
            if (existing == null) return null;

            existing.NguyenVatLieuID = entity.NguyenVatLieuID;
            existing.MaBM = entity.MaBM;
            existing.Scope = entity.Scope;
            existing.SiloID = entity.SiloID;
            existing.Ca = entity.Ca;
            existing.NgaySX = entity.NgaySX;
            existing.ThuTu = entity.ThuTu;
            existing.GhiChu = entity.GhiChu;
            existing.TrangThai = entity.TrangThai;
            existing.NgayCapNhat = DateTime.Now;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteNvlSiloMappingAsync(int id)
        {
            var entity = await _context.TKVV_NVL_SiloMapping.FindAsync(id);
            if (entity == null) return false;
            _context.TKVV_NVL_SiloMapping.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── TKVV_Silo_TagMapping ──────────────────────────────────────────────

        public async Task<List<TKVVSiloTagMappingDto>> GetSiloTagMappingListAsync(int? siloId, string? maBM)
        {
            var query = from m in _context.TKVV_Silo_TagMapping
                        join silo in _context.TKVV_Silo on m.SiloID equals silo.ID into siloGroup
                        from silo in siloGroup.DefaultIfEmpty()
                        where (siloId == null || m.SiloID == siloId)
                           && (maBM == null || m.MaBM == maBM)
                        orderby m.SiloID, m.MaBM, m.LoaiDuLieu
                        select new TKVVSiloTagMappingDto
                        {
                            Id = m.ID,
                            SiloID = m.SiloID,
                            TenSilo = silo != null ? silo.TenSilo : null,
                            ScopeNVL = silo != null ? silo.Scope : null,
                            MaBM = m.MaBM,
                            LoaiDuLieu = m.LoaiDuLieu,
                            TagIDEMS = m.TagIDEMS,
                            TagName = m.TagName,
                            TagIDEMS_Ngay = m.TagIDEMS_Ngay,
                            TagName_Ngay = m.TagName_Ngay,
                            TagIDEMS_Dem = m.TagIDEMS_Dem,
                            TagName_Dem = m.TagName_Dem,
                            GhiChu = m.GhiChu,
                            TrangThai = m.TrangThai,
                            NgayCapNhat = m.NgayCapNhat,
                        };
            return await query.ToListAsync();
        }

        public async Task<TKVV_Silo_TagMapping?> GetSiloTagMappingByIdAsync(int id)
            => await _context.TKVV_Silo_TagMapping.FindAsync(id);

        public async Task<TKVV_Silo_TagMapping> AddSiloTagMappingAsync(TKVV_Silo_TagMapping entity)
        {
            entity.NgayCapNhat = DateTime.Now;
            _context.TKVV_Silo_TagMapping.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<TKVV_Silo_TagMapping?> UpdateSiloTagMappingAsync(int id, TKVV_Silo_TagMapping entity)
        {
            var existing = await _context.TKVV_Silo_TagMapping.FindAsync(id);
            if (existing == null) return null;

            existing.SiloID = entity.SiloID;
            existing.MaBM = entity.MaBM;
            existing.LoaiDuLieu = entity.LoaiDuLieu;
            existing.TagIDEMS = entity.TagIDEMS;
            existing.TagName = entity.TagName;
            existing.TagIDEMS_Ngay = entity.TagIDEMS_Ngay;
            existing.TagName_Ngay = entity.TagName_Ngay;
            existing.TagIDEMS_Dem = entity.TagIDEMS_Dem;
            existing.TagName_Dem = entity.TagName_Dem;
            existing.GhiChu = entity.GhiChu;
            existing.TrangThai = entity.TrangThai;
            existing.NgayCapNhat = DateTime.Now;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteSiloTagMappingAsync(int id)
        {
            var entity = await _context.TKVV_Silo_TagMapping.FindAsync(id);
            if (entity == null) return false;
            _context.TKVV_Silo_TagMapping.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
