using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_PhoiTronRepository
    {
        // Đọc trực tiếp bảng quan hệ (LC_PhoiTron/LC_PhoiTronChiTiet) — KHÔNG qua
        // BmPhieu.DataJson. Mỗi cụm tháp (LC_CumThap theo Scope của phiếu) luôn trả về ít
        // nhất 1 thẻ (nếu chưa lưu gì -> thẻ rỗng mặc định Id=0, loại SAN_XUAT, thứ tự 1).
        Task<List<LcPhoiTronCardDto>> GetTabAsync(Guid idPhieu);

        // Lưu danh sách thẻ gửi lên: Id=0 -> tạo mới; thẻ cũ không còn trong danh sách -> xoá
        // (cùng chi tiết). Tự tính KLQuyKhoCan (F01)/KLQuyKho (F02) server-side, tự gắn
        // VatLieuTen/ID_VatTu/ID_CapLieu theo LC_CapLieu hiện tại của CÙNG phiếu (snapshot).
        Task<List<LcPhoiTronCardDto>> SaveTabAsync(Guid idPhieu, List<LcPhoiTronCardDto> cards);

        // "Làm mới" (nút ở TaoPhieuBaoCaoSanXuat_XNL.tsx) — tính lại LuyKeQuyKho cho các dòng
        // LC_PhoiTronChiTiet ĐÃ CÓ của phiếu này, theo LuyKeQuyKho MỚI NHẤT của kíp liền trước
        // (phòng trường hợp kíp trước bị sửa SAU KHI phiếu này đã tạo/lưu lần đầu). Không đổi
        // KLQuyKho/cấu trúc thẻ — chỉ tính lại phần lũy kế kế thừa.
        Task<List<LcPhoiTronCardDto>> RefreshLuyKeTuCaTruocAsync(Guid idPhieu);

        // Gọi từ LC_BCSX_XNL_TPInitializer lúc phiếu được tạo lần đầu — tạo sẵn 1 thẻ mặc
        // định (SAN_XUAT, thứ tự 1) cho mỗi cụm tháp của Scope đó. No-op nếu phiếu đã có
        // dòng LC_PhoiTron (đã init hoặc đã lưu rồi).
        Task InitializeLC_BCSX_XNL_TP_PhoiTronAsync(BmPhieu phieu);
    }
}
