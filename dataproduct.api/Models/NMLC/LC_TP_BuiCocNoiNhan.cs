namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Nơi nhận" của khối Bụi cốc xuất (TP1 only) — mirror LC_XuatCapLoaiNhan.
    // Seed 6 dòng mặc định (NM.LG1/NM.HRC1/NM.CT DAI/NM.HRC2/BP.Kho/NM.LG2), mở CRUD thêm
    // dòng vì Excel gốc ghi "Cho thêm dòng nếu cần".
    public class LC_TP_BuiCocNoiNhan
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
        public bool IsDelete { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
