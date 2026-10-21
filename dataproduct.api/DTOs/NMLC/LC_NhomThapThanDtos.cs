using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
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
}
