using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Repositories;

namespace dataproduct.api.Services
{
    public class MayDucService
    {
        private readonly IMayDucRepository _repo;

        public MayDucService(IMayDucRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<MayDuc>> GetAllAsync(byte? nhaMay, bool? isLock, string? tenMayDuc)
            => _repo.GetAllAsync(nhaMay, isLock, tenMayDuc);

        public Task<MayDuc?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        // HRC1: LoaiMayDuc quyết định thống kê Đúc vuông/Đúc tấm và CongDoan chi phí (PHOIVUONG/PHOITAMDQ1)
        // — để trống thì mẻ đúc vào máy đó bị bỏ qua ở các chỗ join theo LoaiMayDuc.
        private static void ValidateLoaiMayDuc(MayDuc entity)
        {
            if (entity.NhaMay == (byte)NhaMay.HRC1 && entity.LoaiMayDuc is not ("DV" or "DT"))
                throw new InvalidOperationException("Máy đúc HRC1 bắt buộc chọn Loại máy đúc (Đúc vuông/Đúc tấm).");
        }

        public async Task<MayDuc> CreateAsync(MayDuc entity)
        {
            ValidateLoaiMayDuc(entity);
            if (await _repo.ExistsByTenAsync(entity.TenMayDuc, entity.NhaMay))
                throw new InvalidOperationException("Tên máy đúc đã tồn tại trong nhà máy này.");

            await _repo.AddAsync(entity);
            return entity;
        }

        public async Task<bool> UpdateAsync(int id, MayDuc entity)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;

            ValidateLoaiMayDuc(entity);
            if (await _repo.ExistsByTenAsync(entity.TenMayDuc, entity.NhaMay, id))
                throw new InvalidOperationException("Tên máy đúc đã tồn tại trong nhà máy này.");

            existing.TenMayDuc   = entity.TenMayDuc;
            existing.NhaMay      = entity.NhaMay;
            existing.IsLock      = entity.IsLock;
            existing.LoaiMayDuc  = entity.LoaiMayDuc;
            await _repo.UpdateAsync(existing);
            return true;
        }

        // Không xóa cứng: MayDuc.Id được dùng làm BmPhieu.Scope, BmQuyenXl.MaKhuVuc, HRC1_MeThep.IdMayDucDich
        // — xóa sẽ làm phiếu/quyền/mẻ cũ mất tham chiếu. Chỉ khóa (IsLock) để ngừng sử dụng.
        public async Task<bool> LockAsync(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;
            existing.IsLock = true;
            await _repo.UpdateAsync(existing);
            return true;
        }
    }
}

