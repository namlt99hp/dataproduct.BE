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
        public decimal? KhoiLuong { get; set; }
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

    // ── Thống kê slab (ThongKeSlab.tsx) — API riêng, KHÔNG dùng chung với Hrc2SlabSearchRequest/Item.
    // Gom nhóm (pivot) thực hiện ở BE — chỉ cho tìm theo 1 ngày (bắt buộc) + ca, để lượng dữ liệu xử
    // lý trong 1 lần gọi luôn nhỏ. Sẽ bổ sung thêm logic lấy HangCXL / TyLeTieuHao (chưa xác định
    // nguồn, tạm để null).
    public class Hrc2ThongKeSlabRequest
    {
        public string Ngay { get; set; } = "";  // bắt buộc, "yyyy-MM-dd" — ngày lên BBSL (BM_Phieu.NgaySX)
        public int? Ca { get; set; }            // ca của phiếu BBSL (BM_Phieu.Ca), không truyền = tất cả ca
    }

    // 1 dòng đã gom theo (MayDuc, MacThep, MeThep, OrderId, NgayXuLy, KipBBSL). Tên field dùng
    // JsonPropertyName khớp đúng dataIndex của bảng FE (ThongKeSlab.tsx) — không theo camelCase mặc định
    // vì FE dùng tiền tố "pn_"/"png_" (phôi nóng/nguội) + hậu tố "_kl"/"_st" (khối lượng/số tấm).
    public class Hrc2ThongKeSlabRow
    {
        public int? Ca { get; set; }
        public string? NgayLenBBSL { get; set; }
        public string? KipLenBBSL { get; set; }
        public int? MayDuc { get; set; }
        public int? Lo { get; set; }
        public string? MacThep { get; set; }
        public string? MeThep { get; set; }
        public string? Lsx { get; set; }
        public decimal TongSanLuongPhoi { get; set; }
        // Chưa có nguồn dữ liệu — để null, bổ sung logic sau.
        public string? HangCXL { get; set; }
        public decimal? TyLeTieuHao { get; set; }

        // ── Phôi nóng ──
        [System.Text.Json.Serialization.JsonPropertyName("pn_kichThuoc")]
        public string PnKichThuoc { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("pn_kichThuoc_st")]
        public int PnKichThuocSt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai1_kl")]
        public decimal PnLoai1Kl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai1_st")]
        public int PnLoai1St { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai2_kl")]
        public decimal PnLoai2Kl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai2_st")]
        public int PnLoai2St { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai2Tphh_kl")]
        public decimal PnLoai2TphhKl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai2Tphh_st")]
        public int PnLoai2TphhSt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai3_kl")]
        public decimal PnLoai3Kl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai3_st")]
        public int PnLoai3St { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai3Tphh_kl")]
        public decimal PnLoai3TphhKl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_loai3Tphh_st")]
        public int PnLoai3TphhSt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_nganDai_kl")]
        public decimal PnNganDaiKl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("pn_nganDai_st")]
        public int PnNganDaiSt { get; set; }

        // ── Phôi nguội ── ("Loại 2 giao thoa" và "Phế phẩm" chưa có nguồn dữ liệu — không có field)
        [System.Text.Json.Serialization.JsonPropertyName("png_loai1_kl")]
        public decimal PngLoai1Kl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai1_st")]
        public int PngLoai1St { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai2_kl")]
        public decimal PngLoai2Kl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai2_st")]
        public int PngLoai2St { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai2Tphh_kl")]
        public decimal PngLoai2TphhKl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai2Tphh_st")]
        public int PngLoai2TphhSt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai3_kl")]
        public decimal PngLoai3Kl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_loai3_st")]
        public int PngLoai3St { get; set; }
        // Không có "ST" cho Loại 3 TPHH nguội (khớp đúng cột FE hiện tại).
        [System.Text.Json.Serialization.JsonPropertyName("png_loai3Tphh_kl")]
        public decimal PngLoai3TphhKl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_kichThuoc")]
        public string PngKichThuoc { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("png_kichThuoc_st")]
        public int PngKichThuocSt { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_nganDai_kl")]
        public decimal PngNganDaiKl { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("png_nganDai_st")]
        public int PngNganDaiSt { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public HashSet<string> PnKichThuocSet { get; } = [];
        [System.Text.Json.Serialization.JsonIgnore]
        public HashSet<string> PngKichThuocSet { get; } = [];
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

}
