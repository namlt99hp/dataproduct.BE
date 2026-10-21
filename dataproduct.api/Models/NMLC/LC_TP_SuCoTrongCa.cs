namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_SuCoTrongCa
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
}
