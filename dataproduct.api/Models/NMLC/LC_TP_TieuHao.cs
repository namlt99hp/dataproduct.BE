namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_TieuHao
    {
        public int Id { get; set; }

        public Guid PhieuId { get; set; }

        // 1 = TP1, 2 = TP2 (cùng quy ước Scope với LC_TP_SuCoTrongCa)
        public int Scope { get; set; }

        public int HangMucId { get; set; }

        public decimal? ChiSoDauCa { get; set; }

        public decimal? ChiSoCuoiCa { get; set; }

        public decimal? TrongCa { get; set; }

        public decimal? TongNhaMay { get; set; }

        public decimal? LuyKe { get; set; }

        public decimal? TieuHaoTrenTan { get; set; }
    }
}
