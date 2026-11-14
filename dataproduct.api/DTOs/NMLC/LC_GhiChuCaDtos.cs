using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    public class GhiChuCaDto
    {
        public int Id { get; set; }
        public Guid PhieuId { get; set; }
        public int Scope { get; set; }
        public string Loai { get; set; } = string.Empty;
        public int Stt { get; set; }
        public string? NoiDung { get; set; }
    }

    public class GhiChuCaSaveDto
    {
        [Required]
        [RegularExpression("^(5S|BAN_GIAO)$")]
        public string Loai { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? NoiDung { get; set; }
    }
}
