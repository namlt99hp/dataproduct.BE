namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_XuatCapDanhMucCum/ChiTiet — trang quản lý "Cụm nhận" vùng Cấp cốc ────────────────
    // 1 Cụm (cha, vd 'LG1-CDQ') có N Chi tiết (con, vd 'LC1'..'LC4') — xem LC_XuatCapDanhMucCum.

    public class LcXuatCapDanhMucChiTietDto
    {
        public int Id { get; set; }
        public int IdCum { get; set; }
        public int ThuTu { get; set; }
        public string? ChiTietLo { get; set; }
    }

    public class LcXuatCapDanhMucCumDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public int ThuTu { get; set; }
        public string TenCan { get; set; } = string.Empty;
        public string TenCum { get; set; } = string.Empty;
        public string LoaiCoc { get; set; } = string.Empty;
        public string NhomTongHop { get; set; } = string.Empty;
        public int? IdPhongBan { get; set; }
        public string? TenPhongBan { get; set; } // resolved từ PRODUCTDATA.Tbl_PhongBan, chỉ hiển thị
        public DateTime NgayTao { get; set; }
        public List<LcXuatCapDanhMucChiTietDto> ChiTiet { get; set; } = new();
    }

    public class CreateLcXuatCapDanhMucCumDto
    {
        public int Scope { get; set; }
        public int ThuTu { get; set; }
        public string TenCan { get; set; } = string.Empty;
        public string TenCum { get; set; } = string.Empty;
        public string LoaiCoc { get; set; } = string.Empty;
        public string NhomTongHop { get; set; } = string.Empty;
        public int? IdPhongBan { get; set; }
    }

    public class UpdateLcXuatCapDanhMucCumDto : CreateLcXuatCapDanhMucCumDto
    {
    }

    public class CreateLcXuatCapDanhMucChiTietDto
    {
        public int IdCum { get; set; }
        public int ThuTu { get; set; }
        public string? ChiTietLo { get; set; }
    }

    public class UpdateLcXuatCapDanhMucChiTietDto
    {
        public int ThuTu { get; set; }
        public string? ChiTietLo { get; set; }
    }

    // Tuỳ chọn Phòng ban cho Select ở trang quản lý — đọc từ PRODUCTDATA.Tbl_PhongBan.
    public class LcPhongBanOptionDto
    {
        public int IdPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public string? TenNgan { get; set; }
    }
}
