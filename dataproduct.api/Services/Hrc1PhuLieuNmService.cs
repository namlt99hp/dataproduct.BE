using dataproduct.api.Models;
using dataproduct.api.Repositories;

namespace dataproduct.api.Services
{
    public class Hrc1PhuLieuNmUpsertDto
    {
        public string TenPhuLieu { get; set; } = null!;
        public int? ThuTu_TK_BOF { get; set; }
        public int? ThuTu_TK_LF { get; set; }
        public int? ThuTu_Excel_BOF { get; set; }
        public int? ThuTu_Excel_LF { get; set; }
        public string? MaVatTuChiPhi { get; set; }
        public byte? LoaiPL { get; set; }
    }

    public class Hrc1PhuLieuNmService
    {
        private readonly IHrc1PhuLieuNmRepository _repo;

        public Hrc1PhuLieuNmService(IHrc1PhuLieuNmRepository repo)
        {
            _repo = repo;
        }

        public Task<IEnumerable<Hrc1PhuLieuNm>> GetAllAsync(bool? dangSuDung, string? searchKey)
            => _repo.GetAllAsync(dangSuDung, searchKey);

        public Task<Hrc1PhuLieuNm?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        public async Task<Hrc1PhuLieuNm> CreateAsync(Hrc1PhuLieuNmUpsertDto dto)
        {
            if (await _repo.ExistsByTenPhuLieuAsync(dto.TenPhuLieu))
                throw new InvalidOperationException("Tên phụ liệu đã tồn tại.");

            var entity = new Hrc1PhuLieuNm
            {
                TenPhuLieu = dto.TenPhuLieu,
                ThuTu_TK_BOF = dto.ThuTu_TK_BOF,
                ThuTu_TK_LF = dto.ThuTu_TK_LF,
                ThuTu_Excel_BOF = dto.ThuTu_Excel_BOF,
                ThuTu_Excel_LF = dto.ThuTu_Excel_LF,
                MaVatTuChiPhi = string.IsNullOrWhiteSpace(dto.MaVatTuChiPhi) ? null : dto.MaVatTuChiPhi.Trim(),
                LoaiPL = dto.LoaiPL,
                // Phụ liệu thêm qua trang quản lý luôn là thêm tay, không gắn với cột view nguồn (TenPhuLieuNM),
                // và không đánh dấu IsNM — chỉ SP_Sync_HRC1_PhuLieu mới ghi nhận là dữ liệu tự động.
                TenPhuLieuNM = null,
                IsNM = false,
                DangSuDung = true,
                NgayTao = DateTime.Now,
            };
            await _repo.AddAsync(entity);
            return entity;
        }

        public async Task<bool> UpdateAsync(int id, Hrc1PhuLieuNmUpsertDto dto)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;

            if (await _repo.ExistsByTenPhuLieuAsync(dto.TenPhuLieu, id))
                throw new InvalidOperationException("Tên phụ liệu đã tồn tại.");

            existing.TenPhuLieu = dto.TenPhuLieu;
            existing.ThuTu_TK_BOF = dto.ThuTu_TK_BOF;
            existing.ThuTu_TK_LF = dto.ThuTu_TK_LF;
            existing.ThuTu_Excel_BOF = dto.ThuTu_Excel_BOF;
            existing.ThuTu_Excel_LF = dto.ThuTu_Excel_LF;
            existing.MaVatTuChiPhi = string.IsNullOrWhiteSpace(dto.MaVatTuChiPhi) ? null : dto.MaVatTuChiPhi.Trim();
            existing.LoaiPL = dto.LoaiPL;
            await _repo.UpdateAsync(existing);
            return true;
        }

        public async Task<bool?> ToggleDangSuDungAsync(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return null;

            existing.DangSuDung = !existing.DangSuDung;
            await _repo.UpdateAsync(existing);
            return existing.DangSuDung;
        }

        /// <summary>Bật/tắt danh sách mặc định cho Sổ Xuất-Nhập-Tồn HRC1 (dùng khi khởi tạo phiếu
        /// HRC1_STD_NXT đầu tiên — chưa có ca trước để kế thừa). Xem .claude/hrc1_xnt.md mục 2.2.</summary>
        public async Task<bool?> ToggleIsUsedNXTAsync(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return null;

            existing.IsUsedNXT = !(existing.IsUsedNXT ?? false);
            await _repo.UpdateAsync(existing);
            return existing.IsUsedNXT;
        }

        /// <summary>Xóa phụ liệu — chặn nếu đã được dùng ở phiếu tiêu hao BOF/LF hoặc Sổ Xuất-Nhập-Tồn
        /// (chi tiết hoặc tổng hợp). Xem Hrc1PhuLieuNmRepository.GetInUseReasonAsync để biết đầy đủ các
        /// nơi được kiểm tra.</summary>
        public async Task<bool> DeleteAsync(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;

            var reason = await _repo.GetInUseReasonAsync(id);
            if (reason != null)
                throw new InvalidOperationException($"Không thể xóa phụ liệu vì {reason}.");

            await _repo.DeleteAsync(existing);
            return true;
        }
    }
}
