using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    // Cấp liệu theo silo/kíp — xem SECTION 3 .claude/migrations/LuyenCoc_Migration/lc_bcxs_xnl_tp.sql.
    // Stt = lớp liệu trong CÙNG 1 silo (1 = lớp hiện tại; >1 = tách liệu, chưa expose ở FE).
    public class LC_CapLieu
    {
        [Key]
        public int ID { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int ID_Silo { get; set; }
        public int SiloSo { get; set; }
        public byte Stt { get; set; } = 1;
        public int? ID_VatTu { get; set; }
        public string? VatLieuTen { get; set; }
        public int? ID_MaLo { get; set; }
        public string? SoHieuLo { get; set; }
        public decimal? KhoiLuongKeHoach { get; set; }
        public decimal? KhoiLuongAmThucTe { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? KhoiLuongKhoThucTe { get; set; }
        public decimal? LuyKeQuyKho { get; set; }
        public decimal? TonDauKyQuyKho { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        public string? NguoiTao { get; set; }
    }
}
