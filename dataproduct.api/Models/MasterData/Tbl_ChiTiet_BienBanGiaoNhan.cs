using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.Models.MasterData
{
    // Xác nhận qua INFORMATION_SCHEMA.COLUMNS thực tế trên PRODUCTDATA (192.168.240.3,1434).
    // KL_QuyKho_BG/BN, KhoiLuong_BG/BN, DoAm_W là float (double); tất cả cột trừ ID_CT_BBGN đều
    // NULLABLE trên DB thật (gồm cả ID_BBGN) — không khai báo non-nullable (EF ném
    // SqlNullValueException khi đọc NULL). TenVatTu/TenNhomVatTu/HienThiBM/DonViTinh KHÔNG tồn tại
    // trên bảng này — chỉ là field hiển thị populate sau khi JOIN Tbl_VatTu ở tầng ứng dụng, phải
    // [NotMapped] để EF không sinh SQL tham chiếu cột không có thật.
    [Table("Tbl_ChiTiet_BienBanGiaoNhan")]
    public class Tbl_ChiTiet_BienBanGiaoNhan
    {
        [Key]
        public int ID_CT_BBGN { get; set; }
        public int? ID_BBGN { get; set; }
        public int? ID_VatTu { get; set; }
        public string? MaLo { get; set; }
        public double? DoAm_W { get; set; }
        public double? KhoiLuong_BG { get; set; }
        public double? KL_QuyKho_BG { get; set; }
        public double? KhoiLuong_BN { get; set; }
        public double? KL_QuyKho_BN { get; set; }
        public string? GhiChu { get; set; }

        [NotMapped]
        public string? TenVatTu { get; set; }
        [NotMapped]
        public string? DonViTinh { get; set; }
        [NotMapped]
        public string? TenNhomVatTu { get; set; }
        [NotMapped]
        public int? HienThiBM { get; set; }
    }
}
