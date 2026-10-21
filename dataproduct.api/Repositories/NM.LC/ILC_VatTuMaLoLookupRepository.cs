using dataproduct.api.DTOs.NM.LC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_VatTuMaLoLookupRepository
    {
        Task<List<LcVatTuLookupDto>> SearchVatTuAsync(string? searchKey, int take = 50);
        Task<List<LcMaLoLookupDto>> GetMaLoByVatTuAsync(int idVatTu);
        Task<List<LcMaLoByVatTuGroupDto>> GetMaLoByVatTuListAsync(List<int> idVatTuList);
    }
}
