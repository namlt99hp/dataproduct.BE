namespace dataproduct.api.Models.NMLC
{
    // Giao dịch "4. Cỡ hạt cốc trong ca" (xem SECTION 31 lc_bcxs_xnl_tp.sql, TP1.xlsx dòng
    // 359-363). CHỈ dùng cho TP1 (Scope luôn = 1) — TP2 không có mục này, có "Chất lượng cốc"
    // (LC_TP_ChatLuongCocCa) thay thế, 2 mục loại trừ nhau theo Scope. 1 dòng = 1 lần lấy mẫu.
    public class LC_TP_CoHatCoc
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int Stt { get; set; }
        public DateTime? ThoiGianLayMau { get; set; }
        public string? ViTriLayMau { get; set; }
        public decimal? PctDuoi10mm { get; set; }
        public decimal? Pct10Den25mm { get; set; }
        public decimal? Pct25Den80mm { get; set; }
        public decimal? Pct40Den80mm { get; set; }
        public decimal? PctTren80mm { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
