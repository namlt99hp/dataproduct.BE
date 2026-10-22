namespace dataproduct.api.Models.NMLC
{
    // Danh mục chỉ tiêu chất lượng cốc (W, Ak, V, Ccd, M40, M10, CSR, CRI)
    public class LC_TP_ChiTieuCoc
    {
        public int Id { get; set; }

        public string Ten { get; set; } = string.Empty;

        public string? DonVi { get; set; }

        // false = chỉ tiêu không phân loại (chỉ dùng Loai = 1, vd V)
        public bool CoPhanLoai { get; set; }

        public byte? ThuTu { get; set; }

        public bool IsDelete { get; set; }
    }
}
