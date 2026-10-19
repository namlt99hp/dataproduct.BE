namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_ThapThan_NhomThapThan
    {
        public int Id { get; set; }

        public string TenThapThan { get; set; } = string.Empty;

        public int? XuongId { get; set; }

        public bool IsDelete { get; set; }
    }
}
