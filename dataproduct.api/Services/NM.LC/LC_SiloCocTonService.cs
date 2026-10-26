using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_SiloCocTonService
    {
        private readonly ILC_SiloCocTonRepository _repo;

        public LC_SiloCocTonService(ILC_SiloCocTonRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcCocTonCumDto>> GetTabAsync(Guid idPhieu)
            => _repo.GetByPhieuAsync(idPhieu);

        public Task<List<LcCocTonCumDto>> SaveTabAsync(Guid idPhieu, List<LcCocTonCumDto> cums)
            => _repo.SaveTabAsync(idPhieu, cums);
    }
}
