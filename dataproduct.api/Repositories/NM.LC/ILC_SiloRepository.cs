using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_SiloRepository
    {
        Task<List<LcSiloDto>> GetListAsync(int? scope);
        Task<LC_Silo?> GetByIdAsync(int id);
        Task<bool> ExistsSiloSoAsync(int scope, int siloSo, int? excludeId = null);
        Task<LC_Silo> AddAsync(LC_Silo entity);
        Task<LC_Silo?> UpdateAsync(int id, LC_Silo entity);
        Task<bool> DeleteAsync(int id);
    }
}
