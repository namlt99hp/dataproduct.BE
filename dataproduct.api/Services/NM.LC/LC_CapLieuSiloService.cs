using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_CapLieuSiloService
    {
        private readonly ILC_CapLieuSiloRepository _repo;

        public LC_CapLieuSiloService(ILC_CapLieuSiloRepository repo)
        {
            _repo = repo;
        }

        public Task<LcCapLieuSiloTabDto> GetTabAsync(Guid idPhieu) => _repo.GetTabAsync(idPhieu);

        public Task<LcCapLieuSiloTabDto> SaveTabAsync(Guid idPhieu, LcCapLieuSiloTabDto dto) => _repo.SaveTabAsync(idPhieu, dto);

        public Task<LcCapLieuSiloTabDto> RefreshTonDauTuCaTruocAsync(Guid idPhieu) => _repo.RefreshTonDauTuCaTruocAsync(idPhieu);
    }
}
