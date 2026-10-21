using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_CumThapService
    {
        private readonly ILC_CumThapRepository _repo;

        public LC_CumThapService(ILC_CumThapRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcCumThapDto>> GetListAsync(int? scope)
            => _repo.GetListAsync(scope);

        public Task<LC_CumThap?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        public async Task<(LC_CumThap? Result, string? Error)> AddAsync(CreateLcCumThapDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Ma))
                return (null, "Mã cụm tháp không được để trống.");

            if (await _repo.ExistsMaAsync(dto.Scope, dto.Ma))
                return (null, $"Cụm tháp '{dto.Ma}' đã tồn tại ở Scope {dto.Scope}.");

            var entity = new LC_CumThap
            {
                Scope = dto.Scope,
                Ma = dto.Ma.Trim(),
                Ten = dto.Ten,
                ThuTu = dto.ThuTu,
            };
            return (await _repo.AddAsync(entity), null);
        }

        public async Task<(LC_CumThap? Result, string? Error)> UpdateAsync(int id, UpdateLcCumThapDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Ma))
                return (null, "Mã cụm tháp không được để trống.");

            if (await _repo.ExistsMaAsync(dto.Scope, dto.Ma, id))
                return (null, $"Cụm tháp '{dto.Ma}' đã tồn tại ở Scope {dto.Scope}.");

            var entity = new LC_CumThap
            {
                Scope = dto.Scope,
                Ma = dto.Ma.Trim(),
                Ten = dto.Ten,
                ThuTu = dto.ThuTu,
            };
            var result = await _repo.UpdateAsync(id, entity);
            return (result, result == null ? "Không tìm thấy Cụm tháp." : null);
        }

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id);
    }
}
