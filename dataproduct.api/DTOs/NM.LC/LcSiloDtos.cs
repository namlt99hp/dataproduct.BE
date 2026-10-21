namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_Silo ─────────────────────────────────────────────────────────────

    public class LcSiloDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public int SiloSo { get; set; }
        public string? TenSilo { get; set; }
        public string LoaiSilo { get; set; } = "THAN";
        public int? ThuTu { get; set; }
        public DateTime NgayTao { get; set; }
    }

    public class CreateLcSiloDto
    {
        public int Scope { get; set; }
        public int SiloSo { get; set; }
        public string? TenSilo { get; set; }
        public string LoaiSilo { get; set; } = "THAN";
        public int? ThuTu { get; set; }
    }

    public class UpdateLcSiloDto : CreateLcSiloDto
    {
    }
}
