namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_SiloCocTon — tab `tonCoc` (TabCocTonSilo.tsx) ───────────────────────────────────
    // GetTabAsync/SaveTabAsync làm việc với danh sách CỤM (LcCocTonCumDto), mỗi cụm chứa N cột
    // silo (LcCocTonSiloColDto) — khớp đúng cách FE dựng UI (mỗi cụm 1 Card, mỗi silo 1 cột).

    public class LcCocTonSiloColDto
    {
        public int Id { get; set; } // 0 = cột mới (chưa lưu)
        public int IdSiloTon { get; set; } // chọn từ danh mục LC_SiloTon lúc "+ Thêm silo"
        public string TenSilo { get; set; } = string.Empty; // snapshot, FE hiển thị
        public string LoaiLieu { get; set; } = string.Empty; // snapshot
        public string TenNhapLieu { get; set; } = string.Empty; // snapshot
        public decimal? GiaTriTonCuoiCa { get; set; }
        public decimal? KhoiLuongTonCuoiCa { get; set; }
        public decimal? GiaTriTonDauCa { get; set; }
        public decimal? KhoiLuongTonDauCa { get; set; }
        public decimal? ChenhLech1080 { get; set; }
        public decimal? ChenhLechDuoi10 { get; set; }
    }

    public class LcCocTonCumDto
    {
        public string TenCum { get; set; } = string.Empty;
        public List<LcCocTonSiloColDto> Silos { get; set; } = new();
    }
}
