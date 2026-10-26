using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.NMLC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_SiloTonRepository
    {
        Task<List<LcSiloTonDto>> GetListAsync(int? scope);
        Task<LC_SiloTon?> GetByIdAsync(int id);
        Task<Dictionary<int, LC_SiloTon>> GetByIdsAsync(IEnumerable<int> ids);
        Task<LC_SiloTon> AddAsync(LC_SiloTon entity);
        Task<LC_SiloTon?> UpdateAsync(int id, LC_SiloTon entity);
        Task<bool> DeleteAsync(int id);
    }
}
