namespace dataproduct.api.Models.NMLC
{
    // Giao dịch "Tồn phễu" theo kíp — 1 dòng/phễu/phiếu. Thay thế phần Phễu của
    // LC_TP_ThapThan_ChiTiet cũ (field không khớp layout Excel/FE, xem SECTION 15
    // lc_bcxs_xnl_tp.sql). KLTonDauQuyKho = PREVIOUS_SHIFT; KLAmTonCuoi/KLTonCuoiQuyKho =
    // F06a/F06b, backend tính trong LC_ThanhPhamRepository.
    public class LC_TP_TonPheu
    {
        public int Id { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int PheuId { get; set; }
        public decimal? KLTonDauQuyKho { get; set; }
        public decimal? MucRadar { get; set; }
        public decimal? KLAmTonCuoi { get; set; }
        public decimal? KLTonCuoiQuyKho { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
