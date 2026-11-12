namespace dataproduct.api.DTOs.NMLC
{
    // ─── Tab Cỡ hạt cốc (LC_TP_CoHatCoc) — by-phieu, TP1 only ───

    public class CoHatCocDto
    {
        public int Stt { get; set; }
        public DateTime? ThoiGianLayMau { get; set; }
        public string? ViTriLayMau { get; set; }
        public decimal? PctDuoi10mm { get; set; }
        public decimal? Pct10Den25mm { get; set; }
        public decimal? Pct25Den80mm { get; set; }
        public decimal? Pct40Den80mm { get; set; }
        public decimal? PctTren80mm { get; set; }
    }

    public class CoHatCocSaveDto
    {
        public int Stt { get; set; }
        public DateTime? ThoiGianLayMau { get; set; }
        public string? ViTriLayMau { get; set; }
        public decimal? PctDuoi10mm { get; set; }
        public decimal? Pct10Den25mm { get; set; }
        public decimal? Pct25Den80mm { get; set; }
        public decimal? Pct40Den80mm { get; set; }
        public decimal? PctTren80mm { get; set; }
    }
}
