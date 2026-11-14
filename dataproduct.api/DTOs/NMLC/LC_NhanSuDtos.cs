using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    public class NhanSuDto
    {
        public int Id { get; set; }
        public Guid PhieuId { get; set; }
        public int Scope { get; set; }
        public int Stt { get; set; }
        public string? TenToNhom { get; set; }
        public string? SoLuongDinhBien { get; set; }
        public string? SoLuongHienCoDinhBien { get; set; }
        public string? SoLuongTrongCa { get; set; }
        public string? DienGiai { get; set; }
        public string? NoiDungTuDo { get; set; }
    }

    public class NhanSuSaveDto
    {
        [MaxLength(100)]
        public string? TenToNhom { get; set; }

        [MaxLength(50)]
        public string? SoLuongDinhBien { get; set; }

        [MaxLength(50)]
        public string? SoLuongHienCoDinhBien { get; set; }

        [MaxLength(50)]
        public string? SoLuongTrongCa { get; set; }

        [MaxLength(500)]
        public string? DienGiai { get; set; }

        [MaxLength(2000)]
        public string? NoiDungTuDo { get; set; }
    }
}
