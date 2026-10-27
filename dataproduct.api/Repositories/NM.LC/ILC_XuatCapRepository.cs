using dataproduct.api.DTOs.NM.LC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_XuatCapRepository
    {
        // Đọc FULL layout (mọi Nơi nhận×Chi tiết của Scope, từ danh mục) LEFT JOIN dữ liệu đã có
        // của phiếu (sparse) — slot chưa ai nhập/gán gì trả về với Id=0 (ảo). KHÔNG còn
        // InitializeLC_XuatCapAsync (bỏ 2026-10-26 — "mỗi phiếu chỉ gán thôi", xem
        // .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11).
        Task<List<LcXuatCapSlotDto>> GetByPhieuAsync(Guid idPhieu);

        // Lưu giá trị nhập tay (KhoiLuongAm/DoAm/TyKhoi/GhiChu) theo IdDanhMucChiTiet — TẠO MỚI
        // (upsert) slot nếu chưa tồn tại và có ít nhất 1 giá trị cần lưu; CHỈ áp dụng KhoiLuongAm
        // nếu slot KHÔNG có Nguon nào đã gán. Tính lại F16/F17 (theo NhomTongHop)/LuyKe cho TOÀN
        // BỘ slot đã có của phiếu sau khi lưu.
        Task<List<LcXuatCapSlotDto>> SaveTabAsync(Guid idPhieu, List<LcXuatCapSaveSlotDto> slots);

        // Gán 1 dòng "chấm chuyến" BBGN vào 1 slot — xác định bằng (IdPhieu, IdDanhMucChiTiet),
        // TẠO MỚI slot nếu chưa tồn tại. Chặn gán trùng nếu ID_CT_BBGN đã gán ở slot khác (UNIQUE
        // ID_CT_BBGN). Tính lại KhoiLuongAm = SUM(Nguon.KhoiLuongGan) + F16/F17/LuyKe sau khi gán.
        Task<LcXuatCapSlotDto> GanNguonAsync(LcXuatCapGanRequest req);

        // Bỏ gán 1 dòng nguồn (theo ID của LC_XuatCapNguon) — tính lại KhoiLuongAm/F16/F17/LuyKe;
        // nếu slot sau đó không còn Nguon nào và không có giá trị nhập tay nào, XOÁ hẳn dòng
        // (giữ đúng triết lý sparse — "mỗi phiếu chỉ gán thôi").
        Task<LcXuatCapSlotDto> BoGanNguonAsync(int idXuatCapNguon);
    }
}
