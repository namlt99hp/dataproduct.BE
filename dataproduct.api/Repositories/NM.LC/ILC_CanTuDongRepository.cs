using dataproduct.api.DTOs.NMLC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_CanTuDongRepository
    {
        // Danh sách dòng cân thô (LC_CanTuDongRawLog) của đúng slot (Scope,NgaySX,Ca) của phiếu —
        // gồm cả dòng đã IsDaGan=1 (FE tự disable checkbox, không ẩn khỏi danh sách).
        Task<List<LcCanTuDongRawLogDto>> GetRawLogByPhieuAsync(Guid idPhieu);

        // "Gán": SUM KhoiLuong các RawLogIds đã tick, ghi NGAY vào KhoiLuongAmThucTe +
        // KhoiLuongAmThucTeCanTuDong (snapshot) của dòng LC_CapLieu (SiloSo,Stt) — độc lập với
        // nút Lưu phiếu. Khoá vĩnh viễn các dòng RawLog vừa dùng (IsDaGan=1) và chặn gán lại 1
        // dòng LC_CapLieu đã từng Gán (KhoiLuongAmThucTeCanTuDong đã có giá trị). Ghi lại liên
        // kết vào LC_CanTuDongGanChiTiet để BoGanAsync tra ngược được.
        Task<LcCanTuDongGanResultDto> GanAsync(Guid idPhieu, LcCanTuDongGanRequestDto dto);

        // "Bỏ gán": tra LC_CanTuDongGanChiTiet để mở khoá lại (IsDaGan=0) đúng các dòng RawLog
        // đã dùng cho dòng LC_CapLieu (SiloSo,Stt) này, xoá liên kết, rồi xoá luôn
        // KhoiLuongAmThucTe + KhoiLuongAmThucTeCanTuDong (undo toàn bộ, coi như chưa từng Gán).
        Task<List<LcCanTuDongRawLogDto>> BoGanAsync(Guid idPhieu, LcCanTuDongBoGanRequestDto dto);
    }
}
