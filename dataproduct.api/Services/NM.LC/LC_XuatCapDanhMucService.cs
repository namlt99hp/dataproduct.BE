using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_XuatCapDanhMucService
    {
        private readonly ILC_XuatCapDanhMucRepository _repo;

        public LC_XuatCapDanhMucService(ILC_XuatCapDanhMucRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcXuatCapDanhMucCumDto>> GetCumListAsync(int? scope)
            => _repo.GetCumListAsync(scope);

        public Task<List<LcPhongBanOptionDto>> GetPhongBanOptionsAsync()
            => _repo.GetPhongBanOptionsAsync();

        public async Task<(LC_XuatCapDanhMucCum? Result, string? Error)> AddCumAsync(CreateLcXuatCapDanhMucCumDto dto)
        {
            var error = ValidateCum(dto);
            if (error != null) return (null, error);

            var entity = new LC_XuatCapDanhMucCum
            {
                Scope = dto.Scope,
                ThuTu = dto.ThuTu,
                TenCan = dto.TenCan.Trim(),
                TenCum = dto.TenCum.Trim(),
                LoaiCoc = dto.LoaiCoc.Trim(),
                NhomTongHop = dto.NhomTongHop.Trim(),
                ID_PhongBan = dto.IdPhongBan,
            };
            return (await _repo.AddCumAsync(entity), null);
        }

        public async Task<(LC_XuatCapDanhMucCum? Result, string? Error)> UpdateCumAsync(int id, UpdateLcXuatCapDanhMucCumDto dto)
        {
            var error = ValidateCum(dto);
            if (error != null) return (null, error);

            var entity = new LC_XuatCapDanhMucCum
            {
                Scope = dto.Scope,
                ThuTu = dto.ThuTu,
                TenCan = dto.TenCan.Trim(),
                TenCum = dto.TenCum.Trim(),
                LoaiCoc = dto.LoaiCoc.Trim(),
                NhomTongHop = dto.NhomTongHop.Trim(),
                ID_PhongBan = dto.IdPhongBan,
            };
            var result = await _repo.UpdateCumAsync(id, entity);
            return (result, result == null ? "Không tìm thấy Cụm nhận." : null);
        }

        public Task<bool> DeleteCumAsync(int id)
            => _repo.DeleteCumAsync(id);

        public async Task<(LC_XuatCapDanhMucChiTiet? Result, string? Error)> AddChiTietAsync(CreateLcXuatCapDanhMucChiTietDto dto)
        {
            if (await _repo.GetCumByIdAsync(dto.IdCum) == null) return (null, "Không tìm thấy Cụm nhận.");

            var entity = new LC_XuatCapDanhMucChiTiet
            {
                ID_Cum = dto.IdCum,
                ThuTu = dto.ThuTu,
                ChiTietLo = string.IsNullOrWhiteSpace(dto.ChiTietLo) ? null : dto.ChiTietLo.Trim(),
            };
            return (await _repo.AddChiTietAsync(entity), null);
        }

        public async Task<(LC_XuatCapDanhMucChiTiet? Result, string? Error)> UpdateChiTietAsync(int id, UpdateLcXuatCapDanhMucChiTietDto dto)
        {
            var chiTietLo = string.IsNullOrWhiteSpace(dto.ChiTietLo) ? null : dto.ChiTietLo.Trim();
            var result = await _repo.UpdateChiTietAsync(id, dto.ThuTu, chiTietLo);
            return (result, result == null ? "Không tìm thấy Chi tiết." : null);
        }

        public Task<bool> DeleteChiTietAsync(int id)
            => _repo.DeleteChiTietAsync(id);

        private static string? ValidateCum(CreateLcXuatCapDanhMucCumDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TenCan)) return "Tên cân không được để trống.";
            if (string.IsNullOrWhiteSpace(dto.TenCum)) return "Tên cụm (Nơi nhận) không được để trống.";
            if (string.IsNullOrWhiteSpace(dto.LoaiCoc)) return "Loại cốc không được để trống.";
            if (string.IsNullOrWhiteSpace(dto.NhomTongHop)) return "Nhóm tổng hợp (Trong ca/Lũy kế) không được để trống.";
            return null;
        }
    }
}
