namespace dataproduct.api.DTOs.NMLC
{
    // 1 dòng = 1 lần cân rời rạc trong LC_CanTuDongRawLog, cho đúng slot (Scope,NgaySX,Ca) của
    // phiếu đang mở — xem LC_CanTuDongRepository.GetRawLogByPhieuAsync.
    public class LcCanTuDongRawLogDto
    {
        public int Id { get; set; }
        public DateTime NgayGio { get; set; }
        public string? TenCan { get; set; }
        public string? LoaiLieuTen { get; set; }
        public decimal KhoiLuong { get; set; }
        // true = đã dùng trong 1 lần Gán trước đó — FE disable checkbox dòng này.
        public bool IsDaGan { get; set; }
    }

    // Request "Gán": SiloSo+Stt định danh dòng LC_CapLieu đích (khớp khoá đã dùng ở
    // LcCapLieuSiloRowDto), RawLogIds là các dòng LC_CanTuDongRawLog user đã tick.
    public class LcCanTuDongGanRequestDto
    {
        public int SiloSo { get; set; }
        public int Stt { get; set; } = 1;
        public List<int> RawLogIds { get; set; } = new();
    }

    // Request "Bỏ gán" — chỉ cần định danh dòng LC_CapLieu, không cần RawLogIds (tự tra qua
    // LC_CanTuDongGanChiTiet, xem LC_CanTuDongRepository.BoGanAsync).
    public class LcCanTuDongBoGanRequestDto
    {
        public int SiloSo { get; set; }
        public int Stt { get; set; } = 1;
    }

    // Kết quả sau "Gán" — FE dùng để cập nhật ngay dòng Cấp liệu tương ứng + làm mới danh sách
    // RawLog (đã khoá các dòng vừa dùng), không cần gọi lại GetByPhieu của tab Cấp liệu & Silo.
    public class LcCanTuDongGanResultDto
    {
        public int SiloSo { get; set; }
        public int Stt { get; set; }
        public decimal KhoiLuongAmThucTe { get; set; }
        public decimal KhoiLuongAmThucTeCanTuDong { get; set; }
        public List<LcCanTuDongRawLogDto> RawLogRows { get; set; } = new();
    }
}
