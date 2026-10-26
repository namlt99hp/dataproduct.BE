namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Silo Tồn" (trang quản lý độc lập, theo Scope) — mỗi silo khai báo CongThuc
    // (biểu thức dạng Excel, biến H = giá trị nhập) 1 lần, dùng lại cho mọi phiếu sau qua
    // autocomplete ở tab TabCocTonSilo. Xem Utils/CongThucEvaluator.cs.
    public class LC_SiloTon
    {
        public int ID { get; set; }

        public int Scope { get; set; }

        public string TenSilo { get; set; } = string.Empty;

        public string LoaiLieu { get; set; } = string.Empty;

        public string TenNhapLieu { get; set; } = "Chiều cao";

        public string CongThuc { get; set; } = string.Empty;

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
