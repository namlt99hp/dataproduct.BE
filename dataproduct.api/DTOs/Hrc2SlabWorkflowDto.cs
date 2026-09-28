namespace dataproduct.api.DTOs
{
    // ── Requests ──────────────────────────────────────────────────────────────

    public class ChuyenBBSLRequest
    {
        public List<int> IdSlabs { get; set; } = [];
        public Guid IdPhieu { get; set; }
        public int NguoiThucHien { get; set; }
    }

    public class ThuHoiRequest
    {
        public List<int> IdSlabs { get; set; } = [];
        public int NguoiThucHien { get; set; }
    }

    public class XacNhanRequest
    {
        public List<int> IdSlabs { get; set; } = [];
        /// <summary>HRC2: "Duc" | "Kho" | "PKH". HRC1: "Duc" | "Can" | "C4" (dùng chung DTO, mỗi controller tự validate).</summary>
        public string LoaiXacNhan { get; set; } = "";
        public int NguoiThucHien { get; set; }
        /// <summary>HRC1 — bắt buộc khi LoaiXacNhan = "C4": chỉ phiếu cũ đã "dính" luồng C4 (có ≥1 slab
        /// đã được C4 xác nhận) mới còn cho XN C4, phiếu mới bỏ hẳn luồng C4.</summary>
        public Guid? IdPhieu { get; set; }
    }

    public class ChotPhieuRequest
    {
        public Guid IdPhieu { get; set; }
        public int NguoiThucHien { get; set; }
    }

    public class HrcSlabSearchRequest
    {
        public DateOnly? TuNgay { get; set; }
        public DateOnly? DenNgay { get; set; }
        public string? CaSanXuat { get; set; }
        public string? Kip { get; set; }
        public int? MayDuc { get; set; }
        public string? MeThep { get; set; }
        public List<string>? IdSlabs { get; set; }
        public string? MacThep { get; set; }
        public bool? IsChot { get; set; }
        public bool? IsTrungIDSlab { get; set; }
        public bool? IsDiffMacThep { get; set; }
        public int? TrangThaiKCS { get; set; }
        public int? TrangThaiDuc { get; set; }
        public int? TrangThaiKho { get; set; }
        public int? TrangThaiPKH { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    // ── Responses ─────────────────────────────────────────────────────────────

    public class PhieuBBSLItem
    {
        public Guid IdPhieu { get; set; }
        public string? SoPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public string? Kip { get; set; }
        public int? TinhTrang { get; set; }
        public int SoSlabDaChot { get; set; }  // tổng slab trong phiếu
        public int SoSlabDuc { get; set; }     // số slab TrangThaiDuc=1
        public int SoSlabKho { get; set; }     // số slab TrangThaiKho=1
        public int SoSlabPKH { get; set; }     // số slab TrangThaiPKH=1
    }

    public class SlabTongHopItem
    {
        public string? MeThep { get; set; }
        public string? MacThep { get; set; }
        public decimal? ChieuDay { get; set; }
        public decimal? ChieuRong { get; set; }
        public decimal? ChieuDai { get; set; }
        public string? LoaiPhoi { get; set; }
        public string? ChatLuongTPHH { get; set; }
        public int SoLuong { get; set; }
        public decimal? TongKhoiLuong { get; set; }
    }

    public class WorkflowResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int AffectedRows { get; set; }
    }

    public class SyncStatusItem
    {
        public int Id { get; set; }
        public string? TrangThai { get; set; }
        public DateOnly? NgayBatDau { get; set; }
        public DateOnly? NgayKetThuc { get; set; }
        public DateTime? BatDauLuc { get; set; }
        public DateTime? KetThucLuc { get; set; }
        public int? SoRecordSync { get; set; }
        public string? GhiChu { get; set; }
    }
}
