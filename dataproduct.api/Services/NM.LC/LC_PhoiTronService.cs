using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_PhoiTronService
    {
        private readonly ILC_PhoiTronRepository _repo;

        public LC_PhoiTronService(ILC_PhoiTronRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcPhoiTronCardDto>> GetTabAsync(Guid idPhieu) => _repo.GetTabAsync(idPhieu);

        public Task<List<LcPhoiTronCardDto>> SaveTabAsync(Guid idPhieu, List<LcPhoiTronCardDto> cards)
            => _repo.SaveTabAsync(idPhieu, cards);

        public Task<List<LcPhoiTronCardDto>> RefreshLuyKeTuCaTruocAsync(Guid idPhieu)
            => _repo.RefreshLuyKeTuCaTruocAsync(idPhieu);
    }
}
