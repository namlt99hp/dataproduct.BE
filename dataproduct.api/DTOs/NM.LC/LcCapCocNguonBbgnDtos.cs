namespace dataproduct.api.DTOs.NM.LC
{
    // 1 dòng chi tiết BBGN (Tbl_BienBanGiaoNhan + Tbl_ChiTiet_BienBanGiaoNhan, PRODUCTDATA) ứng với
    // 1 "chấm chuyến" nguồn cho Tab Cấp cốc — xem .claude/LuyenCoc/PhanTich_TabCapCoc.md §8.
    // Repository chỉ lọc theo (Ngay, Ca, Kip, IdXuongBg, trạng thái) — KHÔNG lọc theo vật tư, để
    // liệt kê đầy đủ mọi chuyến trong ca cho UI gán tay phần không tự map được.
    // Việc phân loại (Cụm/Lò cao từ IdXuongBn, Loại từ IdVatTu) do tầng Service làm sau (chưa triển
    // khai ở bước này), không phải trách nhiệm của repository/DTO này.
    public class LcCapCocNguonBbgnDto
    {
        public int IdBbgn { get; set; }
        public int IdCtBbgn { get; set; } // Tbl_ChiTiet_BienBanGiaoNhan.ID_CT_BBGN — khoá dùng để gán vào slot LC_XuatCap (LC_XuatCapNguon.ID_CT_BBGN)
        public string? SoPhieu { get; set; }
        public DateTime? Ngay { get; set; }
        public string? Ca { get; set; }
        public string? Kip { get; set; }

        public int? IdXuongBg { get; set; }
        public int? IdXuongBn { get; set; }
        public int? IdPhongBanBn { get; set; }
        public string? TenXuongBn { get; set; }
        public string? TenPhongBanBn { get; set; }

        public int? IdVatTu { get; set; }
        public string? TenVatTu { get; set; }
        public string? MaLo { get; set; }

        public double? KhoiLuongBg { get; set; }
        public double? DoAmW { get; set; }
        public double? KlQuyKhoBg { get; set; }

        public int? IdTrangThaiBbgn { get; set; }
        public string? GhiChu { get; set; }
    }
}
