using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_VatTuMaLoLookupService
    {
        private readonly ILC_VatTuMaLoLookupRepository _repo;

        public LC_VatTuMaLoLookupService(ILC_VatTuMaLoLookupRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcVatTuLookupDto>> SearchVatTuAsync(string? searchKey)
            => _repo.SearchVatTuAsync(searchKey);

        public Task<List<LcMaLoLookupDto>> GetMaLoByVatTuAsync(int idVatTu)
            => _repo.GetMaLoByVatTuAsync(idVatTu);

        public Task<List<LcMaLoByVatTuGroupDto>> GetMaLoByVatTuListAsync(List<int> idVatTuList)
            => _repo.GetMaLoByVatTuListAsync(idVatTuList);
    }
}
