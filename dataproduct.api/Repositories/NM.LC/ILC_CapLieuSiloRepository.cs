using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_CapLieuSiloRepository
    {
        // Đọc dữ liệu THẬT từ bảng quan hệ (LC_CapLieu/LC_CapLieuTonSilo/LC_KetQuaPhanTich) — nguồn
        // DUY NHẤT cho tab này, KHÔNG qua BmPhieu.DataJson.
        Task<LcCapLieuSiloTabDto> GetTabAsync(Guid idPhieu);

        // Lưu trực tiếp vào bảng quan hệ (gọi từ API riêng, không qua DataJson). Tự tính F01
        // (KhoiLuongKhoThucTe), gọi lại SP set TonDauKyQuyKho rồi tính F03 (LuyKeQuyKho).
        // row.IdVatTu/IdMaLo = null nghĩa là "không đổi" (giữ nguyên dữ liệu đã có), không
        // phải "xoá" — tránh mất dữ liệu carry-forward khi FE gửi dòng chưa người dùng đụng tới.
        Task<LcCapLieuSiloTabDto> SaveTabAsync(Guid idPhieu, LcCapLieuSiloTabDto dto);

        // "Làm mới" (nút ở TaoPhieuBaoCaoSanXuat_XNL.tsx) — tính lại TonDauKyQuyKho/LuyKeQuyKho
        // cho các dòng LC_CapLieu ĐÃ CÓ của phiếu này, lấy theo LuyKeQuyKho MỚI NHẤT của kíp
        // liền trước (phòng trường hợp kíp trước bị sửa SAU KHI phiếu này đã tạo/lưu lần đầu).
        // KHÔNG đổi cấu trúc dòng (không tự thêm/xoá lớp liệu theo kíp trước) — chỉ tính lại 2
        // cột dẫn xuất, dùng lại đúng SP đã dùng trong SaveTabAsync/Initializer.
        Task<LcCapLieuSiloTabDto> RefreshTonDauTuCaTruocAsync(Guid idPhieu);

        // Gọi từ LC_BCSX_XNL_TPInitializer LẦN ĐẦU phiếu được tạo (IPhieuJsonInitializer,
        // PhieuService.RunJsonInitializersAsync) — copy forward ID_VatTu/ID_MaLo từ kíp liền
        // trước (hoặc để trống nếu là phiếu đầu tiên của Scope) + set TonDauKyQuyKho qua SP.
        // Các lần lưu SAU đi qua SaveTabAsync (API riêng), hàm này tự no-op nếu đã init rồi.
        Task InitializeLC_BCSX_XNL_TPAsync(BmPhieu phieu);
    }
}
