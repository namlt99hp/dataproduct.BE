using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.NMLC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_XuatCapDanhMucRepository
    {
        // Đọc toàn bộ Nơi nhận (kèm Chi tiết con) theo Scope — 1 lần gọi render được cả trang quản lý.
        Task<List<LcXuatCapDanhMucCumDto>> GetCumListAsync(int? scope);

        Task<LC_XuatCapDanhMucCum?> GetCumByIdAsync(int id);
        Task<LC_XuatCapDanhMucCum> AddCumAsync(LC_XuatCapDanhMucCum entity);
        Task<LC_XuatCapDanhMucCum?> UpdateCumAsync(int id, LC_XuatCapDanhMucCum entity);

        // Xoá mềm Nơi nhận + CASCADE xoá mềm toàn bộ Chi tiết con (tránh con "trôi" không cha nào).
        Task<bool> DeleteCumAsync(int id);

        Task<LC_XuatCapDanhMucChiTiet> AddChiTietAsync(LC_XuatCapDanhMucChiTiet entity);
        Task<LC_XuatCapDanhMucChiTiet?> UpdateChiTietAsync(int id, int thuTu, string? chiTietLo, int? idLoaiNhan);
        Task<bool> DeleteChiTietAsync(int id);

        // Đọc danh sách Phòng ban (PRODUCTDATA.Tbl_PhongBan) — field tham khảo phụ trên Nơi nhận.
        Task<List<LcPhongBanOptionDto>> GetPhongBanOptionsAsync();

        // Options cho dropdown (trang quản lý + Select "Gán vào ô" ở TabXuatCapCoc).
        Task<List<LcXuatCapCumDto>> GetCumOptionsAsync(int? scope);
        Task<List<LcXuatCapLoaiDto>> GetLoaiCocOptionsAsync();
        Task<List<LcXuatCapLoaiDto>> GetLoaiNhanOptionsAsync();
    }
}
