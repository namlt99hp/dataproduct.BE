namespace dataproduct.api.DTOs
{
    public class Hrc2SlabSearchRequest
    {
        public string? TuNgay { get; set; }
        public string? DenNgay { get; set; }
        public string? TuNgayXL { get; set; }
        public string? DenNgayXL { get; set; }
        public string? CaSanXuat { get; set; }
        public string? Kip { get; set; }
        public int? MayDuc { get; set; }
        public string? MeThep { get; set; }
        public List<string>? IdSlabs { get; set; }
        public string? MacThep { get; set; }
        public bool? IsChot { get; set; }
        public bool? IsTrungIDSlab { get; set; }
        public bool? IsDiffMacThep { get; set; }
        public bool? IsSaiLotName { get; set; }
        // true = chỉ lấy slab đã sửa tay KL (KhoiLuong_Manual != null)
        public bool? IsSuaKL { get; set; }
        public int? TrangThaiKCS { get; set; }
        public int? TrangThaiDuc { get; set; }
        public int? TrangThaiKho { get; set; }
        public int? TrangThaiPKH { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
    public class Hrc2SlabItem
    {
        public int Id { get; set; }
        public int? BkmisId { get; set; }
        public string? NgaySanXuat { get; set; }
        public string? NgaySXTheoCa { get; set; }
        public string? ShiftName { get; set; }
        public string? CaSanXuat { get; set; }
        public string? KipSanXuat { get; set; }
        public string? MeThep { get; set; }
        public string? IdSlab { get; set; }
        public string? MacThep { get; set; }
        public string? ChatLuong { get; set; }
        public decimal? ChieuDay { get; set; }
        public decimal? ChieuRong { get; set; }
        public decimal? ChieuDai { get; set; }
        // KL hiệu lực = KhoiLuong_Manual ?? KhoiLuong gốc — mọi chỗ cộng tổng/hiển thị dùng field này
        public decimal? KhoiLuong { get; set; }
        // KL gốc từ nhà máy (BKMIS), để so sánh với KL sửa tay
        public decimal? KhoiLuongGoc { get; set; }
        // KL KCS sửa tay (null = chưa sửa)
        public decimal? KhoiLuongManual { get; set; }
        public string? LyDoSua { get; set; }
        public string? SoBBSV { get; set; }
        public string? NguoiSuaKL { get; set; }
        public DateTime? ThoiDiemSuaKL { get; set; }
        public decimal? KhoiLuongTinhToan { get; set; }
        public string? ChatLuongTPHH { get; set; }
        public string? ThongTinPhoi { get; set; }
        public string? TpKhongDatGangLong { get; set; }
        public string? GhiChu { get; set; }
        public string? LoaiPhoi { get; set; }
        public string? SapCode { get; set; }
        public string? SapDescription { get; set; }
        public string? SoLo { get; set; }
        public string? OrderId { get; set; }
        public int? MayDuc { get; set; }
        public bool? IsTrungIDSlab { get; set; }
        public bool? IsDiffMacThep { get; set; }
        public bool IsSaiLotName { get; set; }
        public int? Line { get; set; }
        public DateOnly? SapLastTime { get; set; }
        public bool IsChot { get; set; }
        public DateTime? NgayTao { get; set; }
        public string? PhanLoai { get; set; }
        // Thông tin phiếu
        public string? NgayXuLy { get; set; }
        public int? CaBBSL { get; set; }
        public string? KipBBSL { get; set; }
        public string? IdPhieuBBSL { get; set; }
        public string? SoPhieuBBSL { get; set; }
        // Workflow
        public int TrangThaiKCS { get; set; }
        public int TrangThaiDuc { get; set; }
        public int TrangThaiKho { get; set; }
        public int TrangThaiPKH { get; set; }
        // Thời điểm FE bắt được lúc người dùng xác nhận chuyển lên BBSL (khác NgayChuyenKCS — giờ server ghi nhận)
        public DateTime? ThoiDiemThaoTac { get; set; }
        // Người xử lý từng bước (HoVaTen, resolve từ NguoiChuyenKCS/NguoiXacNhanDuc/NguoiXacNhanKho/NguoiChotPKH)
        public string? NguoiChuyenBBSL { get; set; }
        public string? NguoiXacNhanDuc { get; set; }
        public string? NguoiXacNhanKho { get; set; }
        public string? NguoiXacNhanPKH { get; set; }
        // Đánh dấu "đã check" của RIÊNG user đang đăng nhập (BkHrc2Slab_UserCheck) — độc lập với workflow xác nhận.
        public bool DaCheck { get; set; }
    }

    public class Hrc2SlabTongHopItem
    {
        public string? MeThep { get; set; }
        public string? MacThep { get; set; }
        public decimal? ChieuDay { get; set; }
        public decimal? ChieuRong { get; set; }
        public decimal? ChieuDai { get; set; }
        public string? LoaiPhoi { get; set; }
        public string? ChatLuongTPHH { get; set; }
        public string? PhanLoai { get; set; }
        public int SoLuong { get; set; }
        public decimal? TongKhoiLuong { get; set; }
    }

    public class Hrc2PhieuBBSLItem
    {
        public Guid IdPhieu { get; set; }
        public string? SoPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public string? Kip { get; set; }
        public int? TinhTrang { get; set; }
        public int SoSlabDaChot { get; set; }
        public int SoSlabKCS { get; set; }
        public int SoSlabDuc { get; set; }
        public int SoSlabKho { get; set; }
        public int SoSlabPKH { get; set; }
    }

    public class Hrc2XacNhanRequest
    {
        public List<int> IdSlabs { get; set; } = [];
        public string LoaiXacNhan { get; set; } = "";
        public int NguoiThucHien { get; set; }
    }

    public class Hrc2ChotPhieuRequest
    {
        public Guid IdPhieu { get; set; }
        public int NguoiThucHien { get; set; }
    }

    public class Hrc2ChuyenBbslRequest
    {
        public List<int> IdSlabs { get; set; } = [];
        public Guid IdPhieu { get; set; }
        public int NguoiThucHien { get; set; }
        // Thời điểm FE bắt được lúc người dùng bấm xác nhận trong popup chọn phiếu (không phải giờ server nhận request)
        public DateTime? ThoiDiemThaoTac { get; set; }
    }

    public class Hrc2SlabCheckRequest
    {
        public List<int> IdSlabs { get; set; } = [];
        public int NguoiThucHien { get; set; }
    }

    public class Hrc2SuaKhoiLuongRequest
    {
        public int IdSlab { get; set; }
        public decimal? KhoiLuong { get; set; }
        public string? LyDoSua { get; set; }
        public string? SoBBSV { get; set; }
        public int NguoiThucHien { get; set; }
    }

    public class Hrc2SuaKhoiLuongResult
    {
        // true = KL nhập bằng KL gốc → đã khôi phục về KL nhà máy (KhoiLuong_Manual = null)
        public bool IsReset { get; set; }
        public string Message { get; set; } = "";
    }

}
