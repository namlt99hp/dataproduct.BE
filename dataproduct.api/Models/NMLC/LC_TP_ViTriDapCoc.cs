namespace dataproduct.api.Models.NMLC
{
    // Danh mục "vị trí dập" (Tháp dập / CDQ), mỗi vị trí thuộc đúng 1 cụm sản xuất
    // (ID_CumSanXuat NOT NULL) — xem SECTION 22 lc_bcxs_xnl_tp.sql.
    public class LC_TP_ViTriDapCoc
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public int ID_CumSanXuat { get; set; }
        public string Loai { get; set; } = string.Empty; // 'THAP_DAP' | 'CDQ'
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
        public bool IsDelete { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
