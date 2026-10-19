using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    // ─── Nhóm tháp than ───

    public class NhomThapThanDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string? Ten { get; set; }
        public int? ThuTu { get; set; }
    }

    public class NhomThapThanSaveDto
    {
        // Xưởng: 1 = Thành phẩm 1, 2 = Thành phẩm 2
        [Required]
        public int Scope { get; set; }

        [Required, MaxLength(10)]
        public string Ma { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Ten { get; set; }

        public int? ThuTu { get; set; }
    }

    // ─── Phễu ───

    public class PheuDto
    {
        public int Id { get; set; }
        public int ThapThanId { get; set; }
        public string? TenThapThan { get; set; }
        public string? TenPheu { get; set; }
    }

    public class PheuSaveDto
    {
        [Required]
        public int ThapThanId { get; set; }

        [MaxLength(100)]
        public string? TenPheu { get; set; }
    }

    // ─── Chi tiết tháp than ───

    public class ChiTietThapThanDto
    {
        public int Id { get; set; }
        public DateTime? NgaySx { get; set; }
        public int? Ca { get; set; }
        public int PheuId { get; set; }
        public string? TenPheu { get; set; }
        public string? SoYCCN { get; set; }
        public string? GhiChuPhoiTron { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? KhoiLuongQuyKhoCan2A { get; set; }
        public int? ThuTu { get; set; }
        public int? SiLoId { get; set; }
        public int? LoVatLieuId { get; set; }
        public decimal? TyLeKeHoach { get; set; }
        public decimal? TyLeQuyKhoThucTe { get; set; }
        public decimal? KhoiLuongCan2A { get; set; }
        public decimal? KhoiLuongQuyKho { get; set; }
        public decimal? LuyKeQuyKho { get; set; }
    }

    public class ChiTietThapThanSaveDto
    {
        public DateTime? NgaySx { get; set; }
        public int? Ca { get; set; }

        [Required]
        public int PheuId { get; set; }

        [MaxLength(100)]
        public string? SoYCCN { get; set; }

        [MaxLength(500)]
        public string? GhiChuPhoiTron { get; set; }

        public decimal? DoAm { get; set; }
        public decimal? KhoiLuongQuyKhoCan2A { get; set; }
        public int? ThuTu { get; set; }
        public int? SiLoId { get; set; }
        public int? LoVatLieuId { get; set; }
        public decimal? TyLeKeHoach { get; set; }
        public decimal? TyLeQuyKhoThucTe { get; set; }
        public decimal? KhoiLuongCan2A { get; set; }
        public decimal? KhoiLuongQuyKho { get; set; }
        public decimal? LuyKeQuyKho { get; set; }
    }
}
