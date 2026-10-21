namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_CumThap ──────────────────────────────────────────────────────────

    public class LcCumThapDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string? Ten { get; set; }
        public int? ThuTu { get; set; }
        public DateTime NgayTao { get; set; }
    }

    public class CreateLcCumThapDto
    {
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string? Ten { get; set; }
        public int? ThuTu { get; set; }
    }

    public class UpdateLcCumThapDto : CreateLcCumThapDto
    {
    }
}
