using dataproduct.api.DTOs.NMTKVV_Dto;
using dataproduct.api.Models;
using dataproduct.api.Repositories.NMTKVV;

namespace dataproduct.api.Services.NMTKVV
{
    public class TKVV_SiloService
    {
        private readonly ITKVV_SiloRepository _repo;

        public TKVV_SiloService(ITKVV_SiloRepository repo)
        {
            _repo = repo;
        }

        // ─── NVL ──────────────────────────────────────────────────────────────

        public Task<List<TKVVNguyenVatLieuDto>> GetNvlListAsync(string? maBM, string? scope)
            => _repo.GetNvlListAsync(maBM, scope);

        // ─── Silo ─────────────────────────────────────────────────────────────

        public Task<List<TKVVSiloDto>> GetSiloListAsync(string? scope)
            => _repo.GetSiloListAsync(scope);

        public Task<TKVV_Silo?> GetSiloByIdAsync(int id)
            => _repo.GetSiloByIdAsync(id);

        public Task<TKVV_NguyenVatLieu?> GetNvlByIdAsync(int id)
            => _repo.GetNvlByIdAsync(id);

        public Task<TKVV_Silo> AddSiloAsync(CreateTKVVSiloDto dto)
        {
            var entity = new TKVV_Silo
            {
                MaXuong = dto.MaXuong,
                Scope = dto.Scope,
                TenScope = dto.TenScope,
                MaSilo = dto.MaSilo,
                TenSilo = dto.TenSilo,
                GhiChu = dto.GhiChu,
                TrangThai = true,
            };
            return _repo.AddSiloAsync(entity);
        }

        public Task<TKVV_Silo?> UpdateSiloAsync(int id, UpdateTKVVSiloDto dto)
        {
            var entity = new TKVV_Silo
            {
                MaXuong = dto.MaXuong,
                Scope = dto.Scope,
                TenScope = dto.TenScope,
                MaSilo = dto.MaSilo,
                TenSilo = dto.TenSilo,
                GhiChu = dto.GhiChu,
                TrangThai = dto.TrangThai,
            };
            return _repo.UpdateSiloAsync(id, entity);
        }

        public Task<bool> DeleteSiloAsync(int id)
            => _repo.DeleteSiloAsync(id);

        // ─── NVL ↔ Silo Mapping ───────────────────────────────────────────────

        public Task<List<TKVVNvlSiloMappingDto>> GetNvlSiloMappingListAsync(string? maBM, string? scope, int? nvlId, int? siloId, DateOnly? ngaySX = null, int? ca = null)
            => _repo.GetNvlSiloMappingListAsync(maBM, scope, nvlId, siloId, ngaySX, ca);

        public Task<List<TKVVNvlSiloMappingDto>> GetNearestMappingAsync(string? maBM, string scope, DateOnly beforeDate)
            => _repo.GetNearestMappingAsync(maBM, scope, beforeDate);

        public Task<int> BatchCreateNvlSiloMappingAsync(BatchCreateNvlSiloMappingDto dto)
            => _repo.BatchCreateNvlSiloMappingAsync(dto);

        public Task<List<TKVV_NVL_SiloMapping>> EnsureMappingAsync(DateOnly ngaySX, int ca, string scope)
            => _repo.EnsureMappingAsync(ngaySX, ca, scope);

        public Task<TKVV_NVL_SiloMapping?> GetNvlSiloMappingByIdAsync(int id)
            => _repo.GetNvlSiloMappingByIdAsync(id);

        public async Task<TKVV_NVL_SiloMapping?> AddNvlSiloMappingAsync(CreateTKVVNvlSiloMappingDto dto)
        {
            var nvl = await _repo.GetNvlByIdAsync(dto.NguyenVatLieuID);
            if (nvl == null) return null;

            if (dto.SiloID.HasValue)
            {
                var silo = await _repo.GetSiloByIdAsync(dto.SiloID.Value);
                if (silo == null) return null;
                // if (!string.IsNullOrWhiteSpace(nvl.Scope) && !string.IsNullOrWhiteSpace(silo.Scope) && !string.Equals(nvl.Scope, silo.Scope, StringComparison.OrdinalIgnoreCase))
                //     return null;
            }

            var entity = new TKVV_NVL_SiloMapping
            {
                MaBM = string.IsNullOrWhiteSpace(dto.MaBM) ? nvl.MaBM : dto.MaBM,
                NguyenVatLieuID = dto.NguyenVatLieuID,
                Scope = string.IsNullOrWhiteSpace(dto.Scope) ? nvl.Scope : dto.Scope,
                SiloID = dto.SiloID,
                Ca = dto.Ca,
                NgaySX = dto.NgaySX,
                ThuTu = dto.ThuTu ?? nvl.ThuTu,
                GhiChu = dto.GhiChu,
                TrangThai = true,
            };
            return await _repo.AddNvlSiloMappingAsync(entity);
        }

        public async Task<TKVV_NVL_SiloMapping?> UpdateNvlSiloMappingAsync(int id, UpdateTKVVNvlSiloMappingDto dto)
        {
            var nvl = await _repo.GetNvlByIdAsync(dto.NguyenVatLieuID);
            if (nvl == null) return null;

            if (dto.SiloID.HasValue)
            {
                var silo = await _repo.GetSiloByIdAsync(dto.SiloID.Value);
                if (silo == null) return null;
                // if (!string.IsNullOrWhiteSpace(nvl.Scope) && !string.IsNullOrWhiteSpace(silo.Scope) && !string.Equals(nvl.Scope, silo.Scope, StringComparison.OrdinalIgnoreCase))
                //     return null;
            }

            var entity = new TKVV_NVL_SiloMapping
            {
                MaBM = string.IsNullOrWhiteSpace(dto.MaBM) ? nvl.MaBM : dto.MaBM,
                NguyenVatLieuID = dto.NguyenVatLieuID,
                Scope = string.IsNullOrWhiteSpace(dto.Scope) ? nvl.Scope : dto.Scope,
                SiloID = dto.SiloID,
                Ca = dto.Ca,
                NgaySX = dto.NgaySX,
                ThuTu = dto.ThuTu ?? nvl.ThuTu,
                GhiChu = dto.GhiChu,
                TrangThai = dto.TrangThai,
            };
            return await _repo.UpdateNvlSiloMappingAsync(id, entity);
        }

        public Task<bool> DeleteNvlSiloMappingAsync(int id)
            => _repo.DeleteNvlSiloMappingAsync(id);

        // ─── Silo ↔ Tag EMS Mapping ───────────────────────────────────────────

        public Task<List<TKVVSiloTagMappingDto>> GetSiloTagMappingListAsync(int? siloId, string? maBM)
            => _repo.GetSiloTagMappingListAsync(siloId, maBM);

        public Task<TKVV_Silo_TagMapping?> GetSiloTagMappingByIdAsync(int id)
            => _repo.GetSiloTagMappingByIdAsync(id);

        public Task<TKVV_Silo_TagMapping> AddSiloTagMappingAsync(CreateTKVVSiloTagMappingDto dto)
        {
            var entity = new TKVV_Silo_TagMapping
            {
                SiloID = dto.SiloID,
                MaBM = dto.MaBM,
                LoaiDuLieu = dto.LoaiDuLieu,
                TagIDEMS = dto.TagIDEMS,
                TagName = dto.TagName,
                TagIDEMS_Ngay = dto.TagIDEMS_Ngay,
                TagName_Ngay = dto.TagName_Ngay,
                TagIDEMS_Dem = dto.TagIDEMS_Dem,
                TagName_Dem = dto.TagName_Dem,
                GhiChu = dto.GhiChu,
                TrangThai = true,
            };
            return _repo.AddSiloTagMappingAsync(entity);
        }

        public Task<TKVV_Silo_TagMapping?> UpdateSiloTagMappingAsync(int id, UpdateTKVVSiloTagMappingDto dto)
        {
            var entity = new TKVV_Silo_TagMapping
            {
                SiloID = dto.SiloID,
                MaBM = dto.MaBM,
                LoaiDuLieu = dto.LoaiDuLieu,
                TagIDEMS = dto.TagIDEMS,
                TagName = dto.TagName,
                TagIDEMS_Ngay = dto.TagIDEMS_Ngay,
                TagName_Ngay = dto.TagName_Ngay,
                TagIDEMS_Dem = dto.TagIDEMS_Dem,
                TagName_Dem = dto.TagName_Dem,
                GhiChu = dto.GhiChu,
                TrangThai = dto.TrangThai,
            };
            return _repo.UpdateSiloTagMappingAsync(id, entity);
        }

        public Task<bool> DeleteSiloTagMappingAsync(int id)
            => _repo.DeleteSiloTagMappingAsync(id);
    }
}
