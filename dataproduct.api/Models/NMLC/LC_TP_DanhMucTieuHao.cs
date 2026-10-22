namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_DanhMucTieuHao
    {
        public int Id { get; set; }

        public string Ten { get; set; } = string.Empty;

        // -> LC_TP_NhomTieuHao.Id (null = chưa phân nhóm)
        public int? NhomId { get; set; }

        public string? DonVi { get; set; }

        public string? DonViTieuHao { get; set; }

        public byte? ThuTu { get; set; }

        // Xóa mềm: hạng mục bị xóa không hiện ở danh mục nhưng số liệu cũ đã nhập vẫn giữ nguyên
        public bool IsDelete { get; set; }
    }
}
