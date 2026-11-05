namespace dataproduct.api.Models.NMLC
{
    // Giao dịch "I. Sản lượng" + "II. Bụi cốc tồn" gộp 1 bảng (1 dòng/phiếu, cùng 1 hạng mục
    // "Bụi cốc đóng bao" duy nhất — xem SECTION 19 lc_bcxs_xnl_tp.sql). CHỈ dùng cho TP1.
    public class LC_TP_BuiCoc
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public decimal? SanLuongTrongCa { get; set; }
        public decimal? LuyKeSanLuong { get; set; }
        public decimal? TonDauKyQuyKho { get; set; }
        public decimal? TonCuoiKyQuyKho { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
