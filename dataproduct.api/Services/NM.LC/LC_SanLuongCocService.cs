using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_SanLuongCocService
    {
        private readonly ILC_SanLuongCocRepository _repo;

        public LC_SanLuongCocService(ILC_SanLuongCocRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcCumSanXuatDto>> GetCumSanXuatListAsync(int? scope) => _repo.GetCumSanXuatListAsync(scope);

        public Task<List<LcViTriDapCocDto>> GetViTriDapCocListAsync(int? scope) => _repo.GetViTriDapCocListAsync(scope);

        public Task<SanLuongCocTabDto> GetTabAsync(Guid idPhieu) => _repo.GetTabAsync(idPhieu);

        public Task<SanLuongCocTabDto> SaveTabAsync(Guid idPhieu, SanLuongCocTabSaveDto dto) => _repo.SaveTabAsync(idPhieu, dto);
    }
}
