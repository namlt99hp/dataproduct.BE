using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.NMLC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_XuatCapDanhMucRepository
    {
        // Đọc toàn bộ Cụm (kèm Chi tiết con) theo Scope — 1 lần gọi render được cả trang quản lý.
        Task<List<LcXuatCapDanhMucCumDto>> GetCumListAsync(int? scope);

        Task<LC_XuatCapDanhMucCum?> GetCumByIdAsync(int id);
        Task<LC_XuatCapDanhMucCum> AddCumAsync(LC_XuatCapDanhMucCum entity);
        Task<LC_XuatCapDanhMucCum?> UpdateCumAsync(int id, LC_XuatCapDanhMucCum entity);

        // Xoá mềm Cụm + CASCADE xoá mềm toàn bộ Chi tiết con (tránh con "trôi" không cụm nào).
        Task<bool> DeleteCumAsync(int id);

        Task<LC_XuatCapDanhMucChiTiet> AddChiTietAsync(LC_XuatCapDanhMucChiTiet entity);
        Task<LC_XuatCapDanhMucChiTiet?> UpdateChiTietAsync(int id, int thuTu, string? chiTietLo);
        Task<bool> DeleteChiTietAsync(int id);

        // Đọc danh sách Phòng ban (PRODUCTDATA.Tbl_PhongBan) — field tham khảo phụ trên Cụm.
        Task<List<LcPhongBanOptionDto>> GetPhongBanOptionsAsync();
    }
}
