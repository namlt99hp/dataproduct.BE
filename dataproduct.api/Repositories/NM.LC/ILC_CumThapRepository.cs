using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_CumThapRepository
    {
        Task<List<LcCumThapDto>> GetListAsync(int? scope);
        Task<LC_CumThap?> GetByIdAsync(int id);
        Task<bool> ExistsMaAsync(int scope, string ma, int? excludeId = null);
        Task<LC_CumThap> AddAsync(LC_CumThap entity);
        Task<LC_CumThap?> UpdateAsync(int id, LC_CumThap entity);
        Task<bool> DeleteAsync(int id);
    }
}
