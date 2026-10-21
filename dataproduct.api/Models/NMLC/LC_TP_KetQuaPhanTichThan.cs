namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_KetQuaPhanTichThan
    {
        public int Id { get; set; }

        public Guid PhieuCaId { get; set; }

        public int Scope { get; set; }

        public string ChiTieu { get; set; } = string.Empty;

        public decimal? Lan1 { get; set; }

        public decimal? Lan2 { get; set; }

        public decimal? Lan3 { get; set; }

        public decimal? Lan4 { get; set; }

        public decimal? Lan5 { get; set; }

        public decimal? Lan6 { get; set; }

        public string? GhiChu { get; set; }
    }
}
