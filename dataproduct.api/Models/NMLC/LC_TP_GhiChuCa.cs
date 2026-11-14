namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_GhiChuCa
    {
        public int Id { get; set; }

        public Guid PhieuId { get; set; }

        // Luôn = 1 — TP2 không có mục 5S/Bàn giao (xem PhanTichLayout.md dòng 78).
        public int Scope { get; set; }

        // "5S" | "BAN_GIAO"
        public string Loai { get; set; } = string.Empty;

        public int Stt { get; set; }

        public string? NoiDung { get; set; }
    }
}
