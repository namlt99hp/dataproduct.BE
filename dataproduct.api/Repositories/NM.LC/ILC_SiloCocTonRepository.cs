using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_SiloCocTonRepository
    {
        // Đọc toàn bộ cấu trúc cụm/silo CỦA RIÊNG phiếu này (nhóm theo ThuTuCum, sắp theo
        // ThuTuSilo trong từng cụm) — KHÔNG qua BmPhieu.DataJson.
        Task<List<LcCocTonCumDto>> GetByPhieuAsync(Guid idPhieu);

        // Lưu trực tiếp (thêm/sửa/xoá) toàn bộ cấu trúc của phiếu này. ThuTuCum/ThuTuSilo LUÔN
        // gán lại theo đúng VỊ TRÍ trong danh sách gửi lên (không tin số thứ tự cũ, tránh lệch
        // khi FE kéo-thả/thêm/xoá). silo.Id = 0 → cột mới; cột cũ không còn trong danh sách
        // gửi lên → xoá vật lý. BE tự tính KhoiLuongTonCuoiCa/KhoiLuongTonDauCa/ChenhLech* qua
        // Utils/CongThucEvaluator.cs (đọc CongThuc từ LC_SiloTon.ID_SiloTon) — bỏ qua giá trị
        // FE gửi cho các cột tính toán.
        Task<List<LcCocTonCumDto>> SaveTabAsync(Guid idPhieu, List<LcCocTonCumDto> cums);

        // Gọi từ LC_BCSX_XNL_TPInitializer LẦN ĐẦU phiếu được tạo — copy forward CẤU TRÚC
        // (ThuTuCum, TenCum, ThuTuSilo, ID_SiloTon + snapshot) từ phiếu liền trước (cùng
        // Scope), CHỈ RESET dữ liệu nhập/tính (GiaTriTonCuoiCa...ChenhLechDuoi10 đều NULL ở
        // phiếu mới). Phiếu ĐẦU TIÊN của Scope (không có phiếu trước) → để trống, người dùng
        // tự thêm cụm/silo từ đầu qua tab FE. Tự no-op nếu phiếu đã có dòng LC_SiloCocTon.
        Task InitializeLC_SiloCocTonAsync(BmPhieu phieu);
    }
}
