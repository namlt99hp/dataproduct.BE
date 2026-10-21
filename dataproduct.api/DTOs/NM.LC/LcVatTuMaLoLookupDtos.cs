namespace dataproduct.api.DTOs.NM.LC
{
    // Tra cứu Vật tư (than) cho autocomplete ở tab Cấp liệu & Silo.
    public class LcVatTuLookupDto
    {
        public int IdVatTu { get; set; }
        public string? TenVatTu { get; set; }
        public string? MaVatTuSap { get; set; }
        public string? TenVatTuSap { get; set; }
        public string? DonViTinh { get; set; }
    }

    // Mã lô ĐANG ACTIVE (Tbl_MaLo.ID_TinhTrang = 1) của 1 vật tư đã chọn.
    public class LcMaLoLookupDto
    {
        public int IdMaLo { get; set; }
        public string? TenMaLo { get; set; }
        public string? PhongBan { get; set; }
    }

    // 1 nhóm kết quả của malo-by-vattu-batch — lô đang active theo từng ID_VatTu trong request.
    public class LcMaLoByVatTuGroupDto
    {
        public int IdVatTu { get; set; }
        public List<LcMaLoLookupDto> MaLoList { get; set; } = new();
    }
}
