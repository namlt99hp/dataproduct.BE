namespace dataproduct.api.Models.NMLC
{
    // Mốc "Chốt lũy kế" thủ công — xem LC_ChotLuyKeRepository. Mỗi dòng đánh dấu: phiên NGAY SAU
    // (NgayChot, CaChot) của Scope này KHÔNG kế thừa lũy kế từ phiên trước nữa (reset), tương tự ý
    // nghĩa mốc lịch Ca1/ngày1 nhưng do người dùng tự định nghĩa thay vì theo lịch cố định. KHÔNG
    // sửa gì lũy kế của chính IdPhieuChot — chỉ dùng làm NGƯỠNG DƯỚI khi các Compute*LuyKe*Async
    // tra "kíp liền trước" (xem LcLuyKeChotMocHelper.GetMocChotGanNhatAsync).
    public class LC_LuyKeChotMoc
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public DateOnly NgayChot { get; set; }
        public int CaChot { get; set; }
        public Guid IdPhieuChot { get; set; }
        public string? NguoiChot { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
