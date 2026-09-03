using dataproduct.api.DTOs;
using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;

namespace dataproduct.api.Services
{
    public class STD_NXT_HRC1Service
    {
        private readonly ISTD_NXT_HRC1Repository _repo;

        public STD_NXT_HRC1Service(ISTD_NXT_HRC1Repository repo)
        {
            _repo = repo;
        }

        public async Task<STD_NXT_HRC1_UpsertResponse> UpsertAsync(STD_NXT_HRC1_UpsertDto entity)
        {
            return await _repo.UpsertAsync(entity);
        }

        public async Task<STD_NXT_HRC1_GetDetailResponse> GetByPhieuIdAsync(Guid phieuId)
        {
            return await _repo.GetByPhieuIdAsync(phieuId);
        }

        public async Task<STD_NXT_RelatedPhieuStatusResponse> GetRelatedPhieuStatusesAsync(STD_NXT_RelatedPhieuStatusRequest request)
        {
            return await _repo.GetRelatedPhieuStatusesAsync(request);
        }

        public async Task<bool> PhanBoAsync(STD_NXT_HRC1_PhanBoDto entity)
        {
            return await _repo.PhanBoAsync(entity);
        }

        public async Task<bool> ThuHoiPhanBoAsync(STD_NXT_HRC1_PhanBoDto entity)
        {
            return await _repo.ThuHoiPhanBoAsync(entity);
        }

        public async Task<bool> KhongPhanBoAsync(STD_NXT_HRC1_KhongPhanBoDto entity)
        {
            return await _repo.KhongPhanBoAsync(entity);
        }

        public async Task<List<STD_NXT_HRC1_NhapXuatTonRow>> GetNhapXuatTonAsync(STD_NXT_HRC1_NhapXuatTonSearchRequest request)
        {
            return await _repo.GetNhapXuatTonAsync(request);
        }

    }
}
