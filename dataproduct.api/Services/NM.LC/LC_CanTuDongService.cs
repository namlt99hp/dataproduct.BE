using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_CanTuDongService
    {
        private readonly ILC_CanTuDongRepository _repo;

        public LC_CanTuDongService(ILC_CanTuDongRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcCanTuDongRawLogDto>> GetRawLogByPhieuAsync(Guid idPhieu) => _repo.GetRawLogByPhieuAsync(idPhieu);

        public Task<LcCanTuDongGanResultDto> GanAsync(Guid idPhieu, LcCanTuDongGanRequestDto dto) => _repo.GanAsync(idPhieu, dto);

        public Task<List<LcCanTuDongRawLogDto>> BoGanAsync(Guid idPhieu, LcCanTuDongBoGanRequestDto dto) => _repo.BoGanAsync(idPhieu, dto);
    }
}
