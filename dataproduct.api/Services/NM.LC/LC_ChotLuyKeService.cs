using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_ChotLuyKeService
    {
        private readonly ILC_ChotLuyKeRepository _repo;

        public LC_ChotLuyKeService(ILC_ChotLuyKeRepository repo)
        {
            _repo = repo;
        }

        public Task<LcChotLuyKeResultDto> ChotLuyKeAsync(Guid idPhieuChot, string? nguoiChot)
            => _repo.ChotLuyKeAsync(idPhieuChot, nguoiChot);
    }
}
