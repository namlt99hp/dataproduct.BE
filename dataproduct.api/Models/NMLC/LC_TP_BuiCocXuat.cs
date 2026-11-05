namespace dataproduct.api.Models.NMLC
{
    // Giao dịch "III. Bụi cốc xuất" — 1 dòng/Nơi nhận/phiếu (xem SECTION 20
    // lc_bcxs_xnl_tp.sql). LuyKeQuyKho = CUMULATIVE khoá theo NoiNhanId, mốc reset Ca1/ngày1.
    public class LC_TP_BuiCocXuat
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int NoiNhanId { get; set; }
        public decimal? KhoiLuong { get; set; }
        public decimal? LuyKeQuyKho { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
