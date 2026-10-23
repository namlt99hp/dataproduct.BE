namespace dataproduct.api.DTOs.NMTKVV_Dto
{
    // ─── Danh mục NVL (sản phẩm) theo biểu mẫu ────────────────────────────────

    public class TKVVNguyenVatLieuDto
    {
        public int Id { get; set; }
        public string MaBM { get; set; } = string.Empty;
        public string TenNVL { get; set; } = string.Empty;
        public string? DonViTinh { get; set; }
        public int? ThuTu { get; set; }
        public bool TrangThai { get; set; }
        public string? GhiChu { get; set; }
        public string? Scope { get; set; }
        public string? TenScope { get; set; }
    }

    public class CreateTKVVNguyenVatLieuDto
    {
        public string MaBM { get; set; } = string.Empty;
        public string TenNVL { get; set; } = string.Empty;
        public string? DonViTinh { get; set; }
        public int? ThuTu { get; set; }
        public string? GhiChu { get; set; }
        public string? Scope { get; set; }
        public string? TenScope { get; set; }
    }

    public class UpdateTKVVNguyenVatLieuDto
    {
        public string MaBM { get; set; } = string.Empty;
        public string TenNVL { get; set; } = string.Empty;
        public string? DonViTinh { get; set; }
        public int? ThuTu { get; set; }
        public bool TrangThai { get; set; }
        public string? GhiChu { get; set; }
        public string? Scope { get; set; }
        public string? TenScope { get; set; }
    }

    // ─── Dữ liệu thô lọc theo Scope/khoảng ngày ────────────────────────────────

    public class TKVVDuLieuRawDto
    {
        public long Id { get; set; }
        public string TagID { get; set; } = string.Empty;
        public decimal? GiaTriTuDong { get; set; }
        // Chỉ để đọc/hiển thị, không có API cập nhật.
        public decimal? GiaTriDieuChinh { get; set; }
        public DateOnly Ngay { get; set; }
        public byte Ca { get; set; }
        public string Scope { get; set; } = string.Empty;
        public DateTime? ThoiGian { get; set; }
    }

    public class GetTKVVDataByFilterDto
    {
        public string? Scope { get; set; }
        public DateTime? NgayBatDau { get; set; }
        public DateTime? NgayKetThuc { get; set; }
    }

    // ─── Tổng tự động (PLC) theo Ngay/Ca/Scope — 1 BM/xưởng chỉ có 1 số tổng duy
    //     nhất (không tách theo sản phẩm), dùng để đối chiếu — KHÔNG tự điền vào
    //     Loại 1/2/3/Phế phẩm vì PLC không phân loại được ───────────────────────

    public class TKVVTongTuDongDto
    {
        public decimal TongTuDong { get; set; }
        public string? Message { get; set; }
        public bool HasData { get; set; }
    }

    // ─── Giá trị NVL tự động từ EMS (SP_TKVV_GetGiaTriNVL_Auto) ────────────────
    // Gọi SP với (Ngay, Ca, Scope code, MaBM) — trả tổng GiaTri từ EMS_DATA_CAN
    // theo mapping TKVV_NVL_TagMapping. Dùng để đổ dữ liệu vào bảng khi tạo phiếu.

    public class TKVVGiaTriNVLAutoDto
    {
        public int NguyenVatLieuID { get; set; }
        public string MaBM { get; set; } = string.Empty;
        public string TenNVL { get; set; } = string.Empty;
        public string? DonViTinh { get; set; }
        public int? ThuTu { get; set; }
        public string? Scope { get; set; }
        public string? TenScope { get; set; }
        public decimal GiaTri { get; set; }
        public int SoLuongTag { get; set; }
        public DateTime? ThoiGianTu { get; set; }
        public DateTime? ThoiGianDen { get; set; }
    }

    // ─── Dữ liệu cân từ SP_TKVV_GetDuLieuCan — đổ bảng khi tạo phiếu BC SLCP ──
    // Gọi với (Ngay, Ca, MaBM, LoaiDuLieu, Scope int) — trả từng dòng NVL/Silo.

    public class TKVVDuLieuCanDto
    {
        public DateTime Ngay { get; set; }
        public int Ca { get; set; }
        public string? MaBM { get; set; }
        public string? Scope { get; set; }
        public string? Xuong { get; set; }
        public int? SiloID { get; set; }
        public string? MaSilo { get; set; }
        public int NguyenVatLieuID { get; set; }
        public string TenNVL { get; set; } = string.Empty;
        public string? DonViTinh { get; set; }
        public decimal GiaTri { get; set; }
        public decimal GiaTriXuat { get; set; }
        public int SoLuongSilo { get; set; }
        public decimal? DoAm { get; set; }
    }

    // ─── Dữ liệu cân từ sp_TKVV_GetSanLuongTong_BBGN — đổ bảng khi tạo phiếu BC SLCP ──
    // Gọi với (Ngay, Ca, MaBM, LoaiDuLieu, Scope int) — trả từng dòng NVL/Silo.

    public class TKVVDuLieuSanLuongTongBBGNDto
    {
        public DateTime Ngay { get; set; }
        public int Ca { get; set; }
        public string? Scope { get; set; }
        public string? Xuong { get; set; }
        public decimal GiaTri { get; set; }
        public string? TenXuong_BN { get; set; } = string.Empty;
        public string? MaPB_BN { get; set; } = string.Empty;
        public string? MaLo { get; set; } = string.Empty;
        public string? BBGN_GhiChu { get; set; } = string.Empty;
        public int? ID_CT_BBGN { get; set; }
    }

    // ─── Tổng sản lượng tự động theo Scope (SP_TKVV_GetTongSanLuong) ───────────
    // Gọi với (Ngay, Ca, MaBM="TONGSANLUONG_{ScopeCode}", LoaiDuLieu="TONGSANLUONG").
    // 1 Scope chỉ có 1 số tổng/Ngay+Ca (không tách theo NVL) — dùng để cập nhật
    // GiaTriTuDong của TKVV_SanLuongDuLieu.

    public class TKVVTongSanLuongAutoDto
    {
        public DateTime Ngay { get; set; }
        public int Ca { get; set; }
        public string? MaBM { get; set; }
        public string? LoaiDuLieu { get; set; }
        public string? TagIDEMS_SuDung { get; set; }
        public decimal TongSanLuong { get; set; }
        public int SoTagCoDuLieu { get; set; }
    }

    // ─── TKVV_SanLuongDuLieu — dữ liệu PLC thô (GiaTriTuDong) + điều chỉnh tay ──

    public class TKVVSanLuongDuLieuDto
    {
        public long Id { get; set; }
        public string? TagID { get; set; }
        public decimal? GiaTriTuDong { get; set; }
        public decimal? GiaTriDieuChinh { get; set; }
        public DateOnly Ngay { get; set; }
        public int Ca { get; set; }
        public string? Scope { get; set; }
        public DateTime? ThoiGian { get; set; }
        public DateTime NgayTao { get; set; }
        public string? LyDoDieuChinh { get; set; }
    }

    // ─── TKVV_BaoCaoSanLuongChiPhi — bảng lưu dữ liệu cân + trạng thái điều chỉnh ─

    public class TKVVBaoCaoSanLuongChiPhiDto
    {
        public long Id { get; set; }
        public Guid? PhieuID { get; set; }
        public DateOnly NgaySX { get; set; }
        public int Ca { get; set; }
        public string? Kip { get; set; }
        public int? Scope { get; set; }          // INT — khớp cột Scope trong DB
        public int? ThuTu { get; set; }
        public int NguyenVatLieuID { get; set; }
        public string? TenNVL { get; set; }
        public decimal? KLAm { get; set; }
        public decimal? KLAmAuto { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? QuyKho { get; set; }
        public decimal? ThanhPhamL1 { get; set; }
        public decimal? ThanhPhamL2 { get; set; }
        public decimal? ThanhPhamL3 { get; set; }
        public string? GhiChu { get; set; }
        public string? ThanhPham_Note { get; set; }
        public bool IsAdjusted { get; set; }
        public int? AdjustedBy { get; set; }
        public DateTime? AdjustedDate { get; set; }
        public decimal? TiLePhanBo { get; set; }
        public bool IsDonSilo { get; set; }
        public int? ID_CT_BBGN { get; set; }
    }

    public class LoadDuLieuCanRequestDto
    {
        public DateOnly NgaySX { get; set; }
        public string MaBM { get; set; } = string.Empty;
        public string LoaiDuLieu { get; set; } = "SANLUONG";
        public int Scope { get; set; }           // INT 1-6
        public int? CaSX { get; set; }           // Ca đang chọn trên form; bắt buộc 1 hoặc 2 (1 phiếu = 1 ca)
        public int? CreatedBy { get; set; }
    }

    public class LoadDuLieuCanResultDto
    {
        public List<TKVVBaoCaoSanLuongChiPhiDto> Table { get; set; } = new(); // Chỉ dữ liệu của ca đang chọn
        public TKVVSanLuongDuLieuDto? TongSanLuong { get; set; } // TKVV_SanLuongDuLieu theo Ngay+Ca+Scope
    }

    public class SaveBcSlRowDto
    {
        public long? Id { get; set; }
        public DateOnly NgaySX { get; set; }
        public int Ca { get; set; }
        public int? Scope { get; set; }          // INT
        public int NguyenVatLieuID { get; set; }
        public string? Kip { get; set; }
        public int? ThuTu { get; set; }
        public decimal? KLAm { get; set; }
        public decimal? KLAmAuto { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? QuyKho { get; set; }
        public decimal? ThanhPhamL1 { get; set; }
        public decimal? ThanhPhamL2 { get; set; }
        public decimal? ThanhPhamL3 { get; set; }
        public string? GhiChu { get; set; }
        public decimal? TiLePhanBo { get; set; }
    }

    public class SaveBcSlPhieuRequestDto
    {
        public string MaBM { get; set; } = string.Empty;
        public Guid? PhieuID { get; set; }
        public int CurrentUserId { get; set; }
        public List<SaveBcSlRowDto> Rows { get; set; } = new();
        public string? LyDoDieuChinh { get; set; }
    }

    // ─── Chi tiết sản lượng theo phiếu ─────────────────────────────────────────

    public class TKVVChiTietDto
    {
        public long Id { get; set; }
        public Guid IdPhieu { get; set; }
        public int? Scope { get; set; }
        public DateOnly? Ngay { get; set; }
        public byte? Ca { get; set; }
        public int NguyenVatLieuID { get; set; }
        public string? TenNVL { get; set; }
        public int? ThuTuDong { get; set; }
        public TimeOnly? ThoiGian { get; set; }
        public decimal? Loai1 { get; set; }
        public decimal? Loai2 { get; set; }
        public decimal? Loai3 { get; set; }
        public decimal? PhePham { get; set; }
        public bool IsEdited { get; set; }
        public int? NguoiSuaID { get; set; }
        public DateTime? NgaySua { get; set; }
        public string? LyDoSua { get; set; }
        public string? GhiChu { get; set; }
    }
}
