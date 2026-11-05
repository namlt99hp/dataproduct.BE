namespace dataproduct.api.Models.NMLC
{
    // Giao dịch "Dập cốc" (TP1.xlsx dòng 227-234, TP2.xlsx dòng 186-191), 1 dòng/vị trí
    // dập/phiếu. LuyKe* = CUMULATIVE, mốc reset Ca1/ngày1, khoá theo ID_ViTri — xem SECTION 23
    // lc_bcxs_xnl_tp.sql.
    public class LC_TP_DapCoc
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int ID_ViTri { get; set; }
        public decimal? BanhCocTiepNhan { get; set; }
        public decimal? LuyKeBanhCocTiepNhan { get; set; }
        public decimal? KhoiLuongCocTrongCa { get; set; }
        public decimal? LuyKeKhoiLuongCocTrongCa { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
