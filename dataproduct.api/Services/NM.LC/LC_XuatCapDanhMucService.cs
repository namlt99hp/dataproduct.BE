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

        public Task<List<LcXuatCapCumDto>> GetCumOptionsAsync(int? scope)
            => _repo.GetCumOptionsAsync(scope);

        public Task<List<LcXuatCapLoaiDto>> GetLoaiCocOptionsAsync()
            => _repo.GetLoaiCocOptionsAsync();

        public Task<List<LcXuatCapLoaiDto>> GetLoaiNhanOptionsAsync()
            => _repo.GetLoaiNhanOptionsAsync();

        public async Task<(LC_XuatCapDanhMucCum? Result, string? Error)> AddCumAsync(CreateLcXuatCapDanhMucCumDto dto)
        {
            var error = ValidateCum(dto);
            if (error != null) return (null, error);

            var entity = new LC_XuatCapDanhMucCum
            {
                Scope = dto.Scope,
                ID_Cum = dto.IdCum,
                ThuTu = dto.ThuTu,
                TenCan = dto.TenCan.Trim(),
                NoiNhan = dto.NoiNhan.Trim(),
                ID_LoaiCoc = dto.IdLoaiCoc,
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
                ID_Cum = dto.IdCum,
                ThuTu = dto.ThuTu,
                TenCan = dto.TenCan.Trim(),
                NoiNhan = dto.NoiNhan.Trim(),
                ID_LoaiCoc = dto.IdLoaiCoc,
                NhomTongHop = dto.NhomTongHop.Trim(),
                ID_PhongBan = dto.IdPhongBan,
            };
            var result = await _repo.UpdateCumAsync(id, entity);
            return (result, result == null ? "Không tìm thấy Nơi nhận." : null);
        }

        public Task<bool> DeleteCumAsync(int id)
            => _repo.DeleteCumAsync(id);

        public async Task<(LC_XuatCapDanhMucChiTiet? Result, string? Error)> AddChiTietAsync(CreateLcXuatCapDanhMucChiTietDto dto)
        {
            if (await _repo.GetCumByIdAsync(dto.IdNoiNhan) == null) return (null, "Không tìm thấy Nơi nhận.");

            var entity = new LC_XuatCapDanhMucChiTiet
            {
                ID_NoiNhan = dto.IdNoiNhan,
                ThuTu = dto.ThuTu,
                ChiTietLo = string.IsNullOrWhiteSpace(dto.ChiTietLo) ? null : dto.ChiTietLo.Trim(),
                ID_LoaiNhan = dto.IdLoaiNhan,
            };
            return (await _repo.AddChiTietAsync(entity), null);
        }

        public async Task<(LC_XuatCapDanhMucChiTiet? Result, string? Error)> UpdateChiTietAsync(int id, UpdateLcXuatCapDanhMucChiTietDto dto)
        {
            var chiTietLo = string.IsNullOrWhiteSpace(dto.ChiTietLo) ? null : dto.ChiTietLo.Trim();
            var result = await _repo.UpdateChiTietAsync(id, dto.ThuTu, chiTietLo, dto.IdLoaiNhan);
            return (result, result == null ? "Không tìm thấy Chi tiết." : null);
        }

        public Task<bool> DeleteChiTietAsync(int id)
            => _repo.DeleteChiTietAsync(id);

        private static string? ValidateCum(CreateLcXuatCapDanhMucCumDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TenCan)) return "Tên cân không được để trống.";
            if (string.IsNullOrWhiteSpace(dto.NoiNhan)) return "Tên Nơi nhận không được để trống.";
            if (dto.IdCum <= 0) return "Vui lòng chọn Cụm.";
            if (dto.IdLoaiCoc <= 0) return "Vui lòng chọn Loại cốc.";
            if (string.IsNullOrWhiteSpace(dto.NhomTongHop)) return "Nhóm tổng hợp (Trong ca/Lũy kế) không được để trống.";
            return null;
        }
    }
}
