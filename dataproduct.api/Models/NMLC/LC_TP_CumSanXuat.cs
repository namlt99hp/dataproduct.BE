namespace dataproduct.api.Models.NMLC
{
    // Danh mục "cụm sản xuất" cho tab Sản lượng cốc + Dập cốc (TP1: Cụm 1,2,5 / Cụm 3,4;
    // TP2: Cụm 6,7 / Cụm 8,9). KHÔNG liên quan LC_CumThap (cụm THÁP THAN của Phối trộn) hay
    // LC_XuatCapCum (cụm "nơi nhận" của Cấp cốc) — xem SECTION 21 lc_bcxs_xnl_tp.sql.
    public class LC_TP_CumSanXuat
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
        public bool IsDelete { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
