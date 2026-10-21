using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    // Phối trộn (header 1 "thẻ" công thức: 1.1/1.2 sản xuất song song, 1.3 thử nghiệm...) —
    // xem SECTION 8 .claude/migrations/LuyenCoc_Migration/lc_bcxs_xnl_tp.sql.
    // ThuTu phân biệt nhiều công thức CÙNG (ID_CumThap, LoaiPhoiTron) — ví dụ 2 công thức
    // sản xuất song song (cột A-K/L-R trong Excel) hoặc nhiều lần thử nghiệm trong kíp.
    public class LC_PhoiTron
    {
        [Key]
        public int ID { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int ID_CumThap { get; set; }
        public string CumThapMa { get; set; } = string.Empty;
        public string LoaiPhoiTron { get; set; } = string.Empty; // 'SAN_XUAT' | 'THU_NGHIEM'
        public byte ThuTu { get; set; } = 1;
        public string? SoPhieuYeuCau { get; set; }
        public string? MaMauThuNghiem { get; set; }
        public string? GhiChuPhoi { get; set; }
        public decimal? KLCanAm { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? KLQuyKhoCan { get; set; } // CALCULATED F01, backend tính
        public DateTime NgayTao { get; set; }
    }
}
