using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    public class PheuDto
    {
        public int Id { get; set; }
        public int ThapThanId { get; set; }
        public string? TenThapThan { get; set; }
        public string? TenPheu { get; set; }
        public int? ThuTu { get; set; }
    }

    public class PheuSaveDto
    {
        [Required]
        public int ThapThanId { get; set; }

        [MaxLength(100)]
        public string? TenPheu { get; set; }

        public int? ThuTu { get; set; }
    }
}
