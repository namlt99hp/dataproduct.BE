namespace dataproduct.api.DTOs.NMLC
{
    public class DanhMucTieuHaoDto
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
        public int? NhomId { get; set; }
        public string? TenNhom { get; set; }
        public string? DonVi { get; set; }
        public string? DonViTieuHao { get; set; }
        public byte? ThuTu { get; set; }
    }

    public class TieuHaoDto
    {
        public int Id { get; set; }
        public Guid PhieuId { get; set; }
        public int Scope { get; set; }
        public int HangMucId { get; set; }
        public decimal? ChiSoDauCa { get; set; }
        public decimal? ChiSoCuoiCa { get; set; }
        public decimal? TrongCa { get; set; }
        public decimal? TongNhaMay { get; set; }
        public decimal? LuyKe { get; set; }
        public decimal? TieuHaoTrenTan { get; set; }
    }

    public class TieuHaoSaveDto
    {
        public int HangMucId { get; set; }
        public decimal? ChiSoDauCa { get; set; }
        public decimal? ChiSoCuoiCa { get; set; }
        public decimal? TrongCa { get; set; }
        public decimal? TongNhaMay { get; set; }
        public decimal? LuyKe { get; set; }
        public decimal? TieuHaoTrenTan { get; set; }
    }
}
