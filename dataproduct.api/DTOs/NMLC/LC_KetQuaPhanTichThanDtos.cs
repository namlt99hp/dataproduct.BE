using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    public class KetQuaPhanTichThanDto
    {
        public int Id { get; set; }
        public Guid PhieuCaId { get; set; }
        public int Scope { get; set; }
        public string ChiTieu { get; set; } = string.Empty;
        public decimal? Lan1 { get; set; }
        public decimal? Lan2 { get; set; }
        public decimal? Lan3 { get; set; }
        public decimal? Lan4 { get; set; }
        public decimal? Lan5 { get; set; }
        public decimal? Lan6 { get; set; }
        public string? GhiChu { get; set; }
    }

    public class KetQuaPhanTichThanSaveDto
    {
        [Required, MaxLength(50)]
        public string ChiTieu { get; set; } = string.Empty;

        [Range(0, 9999.99)] public decimal? Lan1 { get; set; }
        [Range(0, 9999.99)] public decimal? Lan2 { get; set; }
        [Range(0, 9999.99)] public decimal? Lan3 { get; set; }
        [Range(0, 9999.99)] public decimal? Lan4 { get; set; }
        [Range(0, 9999.99)] public decimal? Lan5 { get; set; }
        [Range(0, 9999.99)] public decimal? Lan6 { get; set; }

        [MaxLength(200)]
        public string? GhiChu { get; set; }
    }
}
