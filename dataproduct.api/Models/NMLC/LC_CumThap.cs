namespace dataproduct.api.Models.NMLC
{
    // Cụm tháp (bảng dùng chung): VD "Tháp than 1, 2". Scope: 1 = Thành phẩm 1, 2 = Thành phẩm 2
    public class LC_CumThap
    {
        public int ID { get; set; }

        public int Scope { get; set; }

        public string Ma { get; set; } = string.Empty;

        public string? Ten { get; set; }

        public int? ThuTu { get; set; }

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
