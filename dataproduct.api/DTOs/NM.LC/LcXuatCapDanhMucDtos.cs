namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_XuatCapDanhMucCum/ChiTiet — trang quản lý "Nơi nhận" vùng Cấp cốc ────────────────
    // 1 Nơi nhận (cha, vd 'LG1-CDQ') có N Chi tiết (con, vd 'LC1'..'LC4') — xem
    // LC_XuatCapDanhMucCum/.claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11.

    public class LcXuatCapDanhMucChiTietDto
    {
        public int Id { get; set; }
        public int IdNoiNhan { get; set; }
        public int ThuTu { get; set; }
        public string? ChiTietLo { get; set; }
        public int? IdLoaiNhan { get; set; }
        public string? TenLoaiNhan { get; set; }
    }

    public class LcXuatCapDanhMucCumDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public int IdCum { get; set; }
        public string MaCum { get; set; } = string.Empty;
        public string TenCumThat { get; set; } = string.Empty; // Ten của LC_XuatCapCum, vd 'Cụm 1,2,3,4'
        public int ThuTu { get; set; }
        public string TenCan { get; set; } = string.Empty;
        public string NoiNhan { get; set; } = string.Empty;
        public int IdLoaiCoc { get; set; }
        public string TenLoaiCoc { get; set; } = string.Empty;
        public string NhomTongHop { get; set; } = string.Empty;
        public int? IdPhongBan { get; set; }
        public string? TenPhongBan { get; set; } // resolved từ PRODUCTDATA.Tbl_PhongBan, chỉ hiển thị
        public DateTime NgayTao { get; set; }
        public List<LcXuatCapDanhMucChiTietDto> ChiTiet { get; set; } = new();
    }

    public class CreateLcXuatCapDanhMucCumDto
    {
        public int Scope { get; set; }
        public int IdCum { get; set; }
        public int ThuTu { get; set; }
        public string TenCan { get; set; } = string.Empty;
        public string NoiNhan { get; set; } = string.Empty;
        public int IdLoaiCoc { get; set; }
        public string NhomTongHop { get; set; } = string.Empty;
        public int? IdPhongBan { get; set; }
    }

    public class UpdateLcXuatCapDanhMucCumDto : CreateLcXuatCapDanhMucCumDto
    {
    }

    public class CreateLcXuatCapDanhMucChiTietDto
    {
        public int IdNoiNhan { get; set; }
        public int ThuTu { get; set; }
        public string? ChiTietLo { get; set; }
        public int? IdLoaiNhan { get; set; }
    }

    public class UpdateLcXuatCapDanhMucChiTietDto
    {
        public int ThuTu { get; set; }
        public string? ChiTietLo { get; set; }
        public int? IdLoaiNhan { get; set; }
    }

    // Tuỳ chọn Phòng ban cho Select ở trang quản lý — đọc từ PRODUCTDATA.Tbl_PhongBan.
    public class LcPhongBanOptionDto
    {
        public int IdPhongBan { get; set; }
        public string? TenPhongBan { get; set; }
        public string? TenNgan { get; set; }
    }

    // ─── Danh mục MỚI: Cụm (thật)/Loại cốc/Loại nhận — options cho dropdown + CRUD trang quản lý ──

    public class LcXuatCapCumDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public int ThuTu { get; set; }
    }

    public class CreateLcXuatCapCumDto
    {
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public int ThuTu { get; set; }
    }

    public class LcXuatCapLoaiDto
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
        public int ThuTu { get; set; }
    }

    public class CreateLcXuatCapLoaiDto
    {
        public string Ten { get; set; } = string.Empty;
        public int ThuTu { get; set; }
    }
}
