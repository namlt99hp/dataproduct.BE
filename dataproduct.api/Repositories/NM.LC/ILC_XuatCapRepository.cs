using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_XuatCapRepository
    {
        // Đọc toàn bộ slot của phiếu (sắp theo ThuTu), kèm danh sách Nguon đã gán mỗi slot.
        Task<List<LcXuatCapSlotDto>> GetByPhieuAsync(Guid idPhieu);

        // Lưu giá trị nhập tay (KhoiLuongAm/DoAm/TyKhoi/GhiChu) cho các slot của phiếu — CHỈ áp
        // dụng KhoiLuongAm nếu slot KHÔNG có Nguon nào đã gán (có Nguon → BE tự SUM, bỏ qua giá
        // trị gửi lên). Tính lại F16 (KLQuyKho)/F17 (TrongCa, theo nhóm TenCan+NoiNhan)/LuyKe cho
        // TOÀN BỘ slot của phiếu sau khi lưu (không chỉ slot vừa sửa, vì nhóm có thể gồm nhiều
        // slot khác).
        Task<List<LcXuatCapSlotDto>> SaveTabAsync(Guid idPhieu, List<LcXuatCapSaveSlotDto> slots);

        // Gán 1 dòng "chấm chuyến" BBGN (Tbl_ChiTiet_BienBanGiaoNhan, PRODUCTDATA) vào 1 slot —
        // người dùng tự chọn, KHÔNG auto-map (§9.0 mục 4). Chặn gán trùng nếu ID_CT_BBGN đã gán
        // ở slot khác (UNIQUE ID_CT_BBGN). Tính lại KhoiLuongAm = SUM(Nguon.KhoiLuongGan) +
        // F16/F17/LuyKe của nhóm sau khi gán. Trả về slot vừa cập nhật.
        Task<LcXuatCapSlotDto> GanNguonAsync(LcXuatCapGanRequest req);

        // Bỏ gán 1 dòng nguồn (theo ID của LC_XuatCapNguon) — tính lại KhoiLuongAm/F16/F17/LuyKe
        // như trên. Trả về slot vừa cập nhật.
        Task<LcXuatCapSlotDto> BoGanNguonAsync(int idXuatCapNguon);

        // Gọi từ LC_BCSX_XNL_TPInitializer lúc phiếu được tạo — phiếu ĐẦU TIÊN của Scope sinh
        // slot từ LcXuatCapLayoutSeed; các phiếu sau copy forward CẤU TRÚC (ThuTu/TenCan/NoiNhan/
        // ChiTietLo/LoaiCoc) từ phiếu liền trước (giống LC_SiloCocTon), CHỈ RESET giá trị nhập/
        // tính. Tự no-op nếu phiếu đã có dòng LC_XuatCap.
        Task InitializeLC_XuatCapAsync(BmPhieu phieu);
    }
}
