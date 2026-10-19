namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_ThapThan_Pheu
    {
        public int Id { get; set; }

        public int ThapThanId { get; set; }

        public string? TenPheu { get; set; }

        public bool IsDelete { get; set; }
    }
}
