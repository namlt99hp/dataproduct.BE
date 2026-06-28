namespace dataproduct.api.DTOs
{
    public class Hrc2SlabSyncRequest
    {
        public DateOnly? NgayBatDau { get; set; }
        public DateOnly? NgayKetThuc { get; set; }
    }

    public class BkHrc2SlabItem
    {
        public int Id { get; set; }
        public int? BkmisId { get; set; }
        public DateOnly? NgaySanXuat { get; set; }
        public string? ShiftName { get; set; }
        public string? CaSanXuat { get; set; }
        public string? KipSanXuat { get; set; }
        public string? MeThep { get; set; }
        public string? IdSlab { get; set; }
        public string? MacThep { get; set; }
        public string? ChatLuong { get; set; }          // Đổi từ IdPhanLoai INT
        public decimal? ChieuDay { get; set; }
        public decimal? ChieuRong { get; set; }
        public decimal? ChieuDai { get; set; }
        public decimal? KhoiLuong { get; set; }
        public decimal? KhoiLuongTinhToan { get; set; }
        public string? ChatLuongTPHH { get; set; }      // Đổi từ TenPhanLoai
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
        public int? Line { get; set; }
        public DateOnly? SapLastTime { get; set; }
        public bool IsChot { get; set; }
        public DateTime? NgayTao { get; set; }

        // Trạng thái từ BK_HRC2_Slab_TrangThai (LEFT JOIN)
        public int TrangThaiKCS { get; set; }
        public int TrangThaiDuc { get; set; }
        public int TrangThaiKho { get; set; }
        public int TrangThaiPKH { get; set; }
        public Guid? IdPhieuBBSL { get; set; }
        public string? SoPhieuBBSL { get; set; }

        // Thông tin phiếu BBSL (join từ BM_Phieu khi slab đã được chuyển)
        public DateOnly? NgayXuLy { get; set; }
        public int? CaBBSL { get; set; }
        public string? KipBBSL { get; set; }
    }
}
