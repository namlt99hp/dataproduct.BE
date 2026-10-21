using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_SiloService
    {
        private readonly ILC_SiloRepository _repo;

        public LC_SiloService(ILC_SiloRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcSiloDto>> GetListAsync(int? scope)
            => _repo.GetListAsync(scope);

        public Task<LC_Silo?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        public async Task<(LC_Silo? Result, string? Error)> AddAsync(CreateLcSiloDto dto)
        {
            if (await _repo.ExistsSiloSoAsync(dto.Scope, dto.SiloSo))
                return (null, $"Silo số {dto.SiloSo} đã tồn tại ở Scope {dto.Scope}.");

            var entity = new LC_Silo
            {
                Scope = dto.Scope,
                SiloSo = dto.SiloSo,
                TenSilo = dto.TenSilo,
                LoaiSilo = string.IsNullOrWhiteSpace(dto.LoaiSilo) ? "THAN" : dto.LoaiSilo,
                ThuTu = dto.ThuTu,
            };
            return (await _repo.AddAsync(entity), null);
        }

        public async Task<(LC_Silo? Result, string? Error)> UpdateAsync(int id, UpdateLcSiloDto dto)
        {
            if (await _repo.ExistsSiloSoAsync(dto.Scope, dto.SiloSo, id))
                return (null, $"Silo số {dto.SiloSo} đã tồn tại ở Scope {dto.Scope}.");

            var entity = new LC_Silo
            {
                Scope = dto.Scope,
                SiloSo = dto.SiloSo,
                TenSilo = dto.TenSilo,
                LoaiSilo = string.IsNullOrWhiteSpace(dto.LoaiSilo) ? "THAN" : dto.LoaiSilo,
                ThuTu = dto.ThuTu,
            };
            var result = await _repo.UpdateAsync(id, entity);
            return (result, result == null ? "Không tìm thấy Silo." : null);
        }

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id);
    }
}
