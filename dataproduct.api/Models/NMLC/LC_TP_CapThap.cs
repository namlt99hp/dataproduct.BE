namespace dataproduct.api.Models.NMLC
{
    // Giao dịch "Cấp lên tháp / Nạp lò" theo kíp — 1 dòng/cụm tháp/phiếu. Thay thế phần Tháp
    // của LC_TP_ThapThan_ChiTiet cũ (xem SECTION 16 lc_bcxs_xnl_tp.sql). CapLenThapQuyKho =
    // F07 (CROSS_REGION từ LC_PhoiTron, tính lại mỗi lần Get/Save, KHÔNG snapshot);
    // LuyKeCapLenThap/LuyKeNapLo = F08 (CUMULATIVE, mốc reset Ca1/ngày1); NapLoQuyKho = F09
    // (CALCULATED, cân bằng khối lượng — xem LC_ThanhPhamRepository.ComputeNapLoQuyKho, thay
    // cho giá trị nhập tay cũ theo OQ-06 đã lỗi thời).
    public class LC_TP_CapThap
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int CumThapId { get; set; }
        public decimal? DoAmThap { get; set; }
        public decimal? CapLenThapQuyKho { get; set; }
        public decimal? LuyKeCapLenThap { get; set; }
        public decimal? NapLoQuyKho { get; set; }
        public decimal? LuyKeNapLo { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
