namespace dataproduct.api.ResponseModels
{
    // -------------------------------------------------------
    // View model chung — dùng cho cả lò thổi, tinh luyện, đúc
    // Form frontend render cột phù hợp dựa theo CongDoan
    // -------------------------------------------------------
    public class HRC1_MeThepVm
    {
        public int Id { get; set; }
        public int? MePhanCongId { get; set; }  // cần cho thao tác TL (xác nhận, update...)
        public int? ThuTuTL { get; set; }       // null=lần đầu; 1,2,...=lần 2+

        // Thông tin chung
        public string? MaMe { get; set; }
        public string? ThungSo { get; set; }
        public int? LoSo { get; set; }

        public string? ThoiGian { get; set; }       // TL nhập, HH:mm
        public decimal? KLLFSauThep { get; set; }  // LT nhập
        public decimal? KlLan1 { get; set; }       // TL nhập (disabled nếu DichChuyen=len_thang)
        public decimal? KlLan2 { get; set; }       // TL nhập
        public decimal? KlLan3 { get; set; }       // LT nhập
        public decimal? KlThepLong { get; set; }   // Auto-tính
        public decimal? KlThepLongChot { get; set; }
        public decimal? KlThepLongPhanBo { get; set; }
        public string? DichChuyen { get; set; }
        public int? TLDichSo { get; set; }
        public int? IdMayDucDich { get; set; }
        public string? TenMayDucDich { get; set; }
        public bool? IsThuNghiem { get; set; }
        public bool? IsTrungMeThoi { get; set; }
        public bool? IsGhost { get; set; }
        public bool? IsChot { get; set; }
        public string? GhiChuLo { get; set; }
        public string? PhanLoai { get; set; }
        public string? MacThep { get; set; }
        public string? MacThepBKMIS { get; set; }
        public int? IdMacThep { get; set; }
        public string? GhiChuTL { get; set; }
        public string? GhiChuDuc { get; set; }
        public string? GhiChuPCN { get; set; }

        // Trạng thái
        // LT/Đúc: 0=chờ, 1=đã xác nhận, 2=đã chốt
        // TL:     0=chờ, 1=đã nhận, 2=đã xác nhận, 3=đã chốt
        public int? TrangThaiLo { get; set; }
        public int? TrangThaiTL { get; set; }
        public int? TrangThaiDuc { get; set; }
        public bool? TrangThaiPCN { get; set; }
        public bool? TrangThaiChotPCN { get; set; }

        // Tinh luyện đã nhận (chỉ có giá trị khi xem từ lò thổi panel)
        public int? SoTinhLuyenNhan { get; set; }  // scope TL đã nhận mẻ; null = chưa nhận

        // Audit
        public int? CapNhatBoi { get; set; }
        public DateTime? CapNhatLuc { get; set; }
        public int? XacNhanBoi { get; set; }
        public DateTime? XacNhanLuc { get; set; }

        // true = TL thêm tay thủ công (có thể xóa), null/false = nhận tự động
        public bool? IsManualTL { get; set; }

        // Tên người sửa cuối theo công đoạn hiện tại (populated by service)
        public string? TenCapNhatBoi { get; set; }

        // Chuyển mẻ (Tinh luyện chọn mẻ đích để báo cáo chuyển sang)
        public int? ChuyenVeMeId { get; set; }       // FK→HRC1_MeThep của mẻ đích
        public string? ChuyenVeMaMe { get; set; }    // MaMe của mẻ đích
        public string? TenMayDucChuyen { get; set; } // Tên máy đúc của mẻ đích

        // Chuyển ca Đúc (routing sang phiếu Đúc ca sau) — không đổi Ca của chính công đoạn TL
        public bool IsChuyenCaDuc { get; set; }

        // Ca/Ngày của phiếu Đúc mà mẻ này đang thuộc về (đã áp dụng Chuyển ca nếu có).
        // Mặc định = NgayNhanTL/CaTinhLuyen; nếu đã Chuyển ca thì = NgaySXDucChuyen/CaDucChuyen.
        public DateOnly? NgayDuc { get; set; }
        public int? CaDuc { get; set; }
    }

    public class HRC1_ChoNhanMeVm
    {
        public int MeId { get; set; }
        public string? MaMe { get; set; }
        public string? ThungSo { get; set; }
        public int? LoSo { get; set; }
        public string? ThoiGian { get; set; }       // HH:mm
        public decimal? KlThepLong { get; set; }
        public int? TLDichSo { get; set; }
        public string? DichChuyen { get; set; }     // tinh_luyen | len_thang
        public int? SoTinhLuyenNhan { get; set; }   // scope TL đã nhận; null = chưa nhận
        public int? TrangThaiTL { get; set; }  // 0/null=chờ nhận, 1=đã nhận
        public string? TenNguoiNhan { get; set; }   // HoVaTen của người TL nhận mẻ
        public DateTime NgayTao { get; set; }
        public DateTime? NgayNhanTL { get; set; }
        public string? TenMayDuc { get; set; }   // chỉ có khi DichChuyen=len_thang
    }

    // Mẻ trùng khi thêm tay (phiếu TL khác đã nhận mẻ này)
    public class HRC1_TrungMeInfo
    {
        public string SoPhieu { get; set; } = string.Empty;
        public string TenTinhLuyen { get; set; } = string.Empty;
    }

    // Kết quả thêm mẻ tay
    public class HRC1_ThemMeTayResult
    {
        public List<HRC1_TrungMeInfo> TrungVoi { get; set; } = new();
        public bool DaThemVao { get; set; }
    }

    // Search autocomplete mẻ thổi
    public class HRC1_MeThepSearchVm
    {
        public int MeId { get; set; }
        public string MaMe { get; set; } = string.Empty;
        public string? ThungSo { get; set; }
        public int? LoSo { get; set; }
    }

    public class HRC1_PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Total { get; set; }
    }

    // Thông tin máy đúc (cho dropdown)
    public class HRC1_MayDucOptionVm
    {
        public int Id { get; set; }
        public string TenMayDuc { get; set; } = null!;
        // Máy đã khóa vẫn trả về để hiển thị tên cho mẻ/phiếu cũ; FE disable, không cho chọn mới
        public bool IsLock { get; set; }
    }

    // Mẻ BBGN thép lỏng lọc theo khoảng ThoiGian thực tế (giao ca)
    public class HRC1_MeTheoThoiGianVm
    {
        public string? MeThoi { get; set; }       // HRC1_MeThep.MaMe
        public decimal? KLThepLong { get; set; }
        public string? ThoiGian { get; set; }      // vd: "02/01/2026 7h30"
        public string? MayDuc { get; set; }
        public string? ThungSo { get; set; }
        public decimal? KLLFSauThep { get; set; }
        public decimal? KLLan1 { get; set; }
        public decimal? KLLan2 { get; set; }
        public decimal? KLLan3 { get; set; }
        public bool? IsThuNghiem { get; set; }
        public string? PhanLoai { get; set; }
        public string? MacThepBKMIS { get; set; }
        public string? TrangThai { get; set; }  // "Đã xác nhận" | "Chưa xác nhận"
    }

    // -------------------------------------------------------
    // Response chính — 1 kiểu duy nhất cho cả 3 công đoạn
    // -------------------------------------------------------
    public class HRC1_PhieuDataVm
    {
        public Guid IdPhieu { get; set; }
        public string? SoPhieu { get; set; }
        public string? MaBm { get; set; }         // HRC1_LoThoi | HRC1_TinhLuyen | HRC1_BBGN_ThepLong
        public string? CongDoan { get; set; }     // lo_thoi | tinh_luyen | duc
        public int? Scope { get; set; }           // lò/TL số (1–5) hoặc MayDuc.Id
        public string? TenScope { get; set; }     // tên máy đúc chốt lúc tạo phiếu (BmPhieu.TenScope)
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public string? Kip { get; set; }
        public int? TinhTrang { get; set; }

        public List<HRC1_MeThepVm> DanhSachMe { get; set; } = new();

        // Chỉ có khi CongDoan = tinh_luyen
        public List<HRC1_ChoNhanMeVm> ChoNhan { get; set; } = new();

        // Dropdown máy đúc (cho lò thổi và tinh luyện)
        public List<HRC1_MayDucOptionVm> DanhSachMayDuc { get; set; } = new();
    }
}
