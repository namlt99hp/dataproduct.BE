using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
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
