namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_SiloTon — danh mục "Silo Tồn" (trang quản lý) ───────────────────────────────────

    public class LcSiloTonDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string TenSilo { get; set; } = string.Empty;
        public string LoaiLieu { get; set; } = string.Empty;
        public string TenNhapLieu { get; set; } = string.Empty;
        public string CongThuc { get; set; } = string.Empty;
        public DateTime NgayTao { get; set; }
    }

    public class CreateLcSiloTonDto
    {
        public int Scope { get; set; }
        public string TenSilo { get; set; } = string.Empty;
        public string LoaiLieu { get; set; } = string.Empty;
        public string? TenNhapLieu { get; set; }
        public string CongThuc { get; set; } = string.Empty;
    }

    public class UpdateLcSiloTonDto : CreateLcSiloTonDto
    {
    }

    // ─── Tính thử công thức (dùng cho UI "máy tính") ────────────────────────────────────────

    public class TestCongThucDto
    {
        public string CongThuc { get; set; } = string.Empty;
        public decimal GiaTriNhap { get; set; }
    }

    public class TestCongThucResultDto
    {
        public decimal? KetQua { get; set; }
        public string? Loi { get; set; }
    }
}
