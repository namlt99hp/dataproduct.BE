using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    public class SuCoTrongCaDto
    {
        public int Id { get; set; }
        public Guid PhieuId { get; set; }
        public int Scope { get; set; }
        public int Stt { get; set; }
        public string? TinhTrangSuCo { get; set; }
        public DateTime? TgPhatHienSuCo { get; set; }
        public string? CachKhacPhuc { get; set; }
        public DateTime? TgBatDau { get; set; }
        public DateTime? TgKetThuc { get; set; }
        public string? NhanLuc { get; set; }
    }

    public class SuCoTrongCaSaveDto
    {
        [MaxLength(500)]
        public string? TinhTrangSuCo { get; set; }

        public DateTime? TgPhatHienSuCo { get; set; }

        [MaxLength(500)]
        public string? CachKhacPhuc { get; set; }

        public DateTime? TgBatDau { get; set; }

        public DateTime? TgKetThuc { get; set; }

        [MaxLength(200)]
        public string? NhanLuc { get; set; }
    }
}
