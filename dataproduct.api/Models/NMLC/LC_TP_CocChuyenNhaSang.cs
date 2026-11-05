namespace dataproduct.api.Models.NMLC
{
    // "Cốc chuyển qua bên X.NL,TP2" (TP1.xlsx dòng 264-267) — CHỈ TP1 (Scope luôn = 1), 2 dòng
    // cố định/phiếu (NhaSang = 'NS2'|'NS3'). LuyKe = CUMULATIVE của khối lượng chênh lệch
    // (TonCuoi−TonDau), mốc reset Ca1/ngày1, khoá theo NhaSang — xem SECTION 24
    // lc_bcxs_xnl_tp.sql.
    public class LC_TP_CocChuyenNhaSang
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public string NhaSang { get; set; } = string.Empty; // 'NS2' | 'NS3'
        public decimal? TonCuoi { get; set; }
        public decimal? TonDau { get; set; }
        public decimal? LuyKe { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
