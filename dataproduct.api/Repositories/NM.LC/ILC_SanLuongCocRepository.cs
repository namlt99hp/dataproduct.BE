using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Models;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_SanLuongCocRepository
    {
        // Danh mục (đọc, chưa có CRUD — 6 vị trí dập TP1 / 4 vị trí TP2, seed cố định qua SQL).
        Task<List<LcCumSanXuatDto>> GetCumSanXuatListAsync(int? scope);
        Task<List<LcViTriDapCocDto>> GetViTriDapCocListAsync(int? scope);

        // Đọc trực tiếp bảng quan hệ (LC_TP_DapCoc/LC_TP_CocChuyenNhaSang) — KHÔNG qua
        // BmPhieu.DataJson. "Sản lượng cốc" KHÔNG lưu bảng riêng — tính lại live mỗi lần gọi,
        // từ Dập cốc (chính tab này) + Cốc tồn silo + Cấp cốc (2 module khác, qua ID_CumSanXuat
        // gắn trên LC_SiloTon/LC_XuatCapDanhMucCum — NULL = dùng chung 2 cụm).
        Task<SanLuongCocTabDto> GetTabAsync(Guid idPhieu);

        Task<SanLuongCocTabDto> SaveTabAsync(Guid idPhieu, SanLuongCocTabSaveDto dto);

        // Gọi từ LC_BCSX_XNL_TPInitializer lúc phiếu được tạo lần đầu — tạo sẵn 1 dòng Dập cốc
        // cho mỗi vị trí (Scope đó) + 2 dòng Cốc chuyển (CHỈ Scope=1). No-op nếu phiếu đã có.
        Task InitializeAsync(BmPhieu phieu);
    }
}
