using System;
using System.Collections.Generic;

namespace dataproduct.api.ResponseModels
{
    public class Hrc1TieuHao_ResponseModel
    {
        public int ID { get; set; }
        public string? BieuMau { get; set; }
        public int? Scope { get; set; }
        public string? MeThoi { get; set; }
        public string? MacThep { get; set; }
        public string? MacThepOrig { get; set; }
        public bool MacThepIsManual { get; set; }
        public bool IsNM { get; set; }
        public bool IsEdited { get; set; }
        public bool? IsChuyenCa { get; set; }
        public bool? IsTrungMeThoi { get; set; }
        public decimal? KLGang { get; set; }
        public decimal? KLGangLongCCT { get; set; }
        public decimal? KLGangLongTong { get; set; }
        public decimal? KLThepPhe { get; set; }
        public decimal? KLThepPheOrig { get; set; }
        public bool KLThepPheIsManual { get; set; }
        public decimal? KLThepPheGang { get; set; }
        public decimal? KLThepLong { get; set; }
        public double? O2 { get; set; }
        public double? N2 { get; set; }
        public double? AR { get; set; }
        public int? QueLayMau { get; set; }
        public int? QueDoNhiet { get; set; }
        public string? GhiChu { get; set; }
        public DateOnly? NgaySanXuat { get; set; }
        public byte? Ca { get; set; }
        public DateTime? ThoiDiemBatDau { get; set; }
        public DateTime? ThoiDiemKetThuc { get; set; }
        public string? ThoiGianLF { get; set;}
    }

    /// <summary>
    /// Kết quả filter/grouped theo 1 mẻ (HRC1_TieuHao_BOF) — đơn giản hơn HRC2GroupedByReportNoModel
    /// vì HRC1_PhuLieuNM là danh mục cố định 13 dòng, không cần tách mapped/unmapped.
    /// Tái dùng HeaderKeyGroupedByReportNoModel (đã có sẵn ID_PhuLieu + ID_HeaderKey) cho cả 3 nhóm dưới.
    /// </summary>
    public class Hrc1GroupedByMeThoiModel
    {
        public Hrc1TieuHao_ResponseModel? data { get; set; }
        public List<HeaderKeyGroupedByReportNoModel> phuLieus { get; set; } = new List<HeaderKeyGroupedByReportNoModel>();
        public List<HeaderKeyGroupedByReportNoModel> phanBoPhulieus { get; set; } = new List<HeaderKeyGroupedByReportNoModel>();
        public List<HeaderKeyGroupedByReportNoModel> manualAdjustPhulieus { get; set; } = new List<HeaderKeyGroupedByReportNoModel>();
    }

    // =========================================================
    // Thống kê tiêu hao BOF/LF (NM.HRC1/ThongKe/ThongKeTieuHaoHRC1.tsx)
    // Riêng biệt với Hrc1GroupedByMeThoiModel vì "tổng dùng cho báo cáo" (TotalKLPhuGia = hiệu lực + phân bổ)
    // chỉ áp dụng cho thống kê — không đụng field KLPhuGia/KLPhanBo dùng cho luồng tạo/xem phiếu.
    // =========================================================

    public class Hrc1ThongKeValue
    {
        public int PhuLieuID { get; set; }
        /// <summary>Giá trị tự động từ NM (baseline).</summary>
        public double? KLPhuGia { get; set; }
        /// <summary>Giá trị chỉnh tay (ưu tiên hơn KLPhuGia khi IsManual = true).</summary>
        public double? KLPhuGia_Manual { get; set; }
        public bool IsManual { get; set; }
        /// <summary>Lượng được phân bổ chênh lệch kiểm kê (null nếu không có phân bổ).</summary>
        public double? KLPhanBo { get; set; }
        /// <summary>Giá trị tổng hợp cuối cùng để thống kê: (IsManual ? KLPhuGia_Manual : KLPhuGia) + KLPhanBo.</summary>
        public double? TotalKLPhuGia { get; set; }
    }

    public class Hrc1ThongKeRow
    {
        public Hrc1TieuHao_ResponseModel? Data { get; set; }
        public List<Hrc1ThongKeValue> Values { get; set; } = new();
    }

    /// <summary>Danh mục phụ liệu dùng làm cột động — luôn lấy toàn bộ HRC1_PhuLieuNM đang dùng, sắp theo ThuTu.</summary>
    public class Hrc1PhuLieuHeaderTable
    {
        public int PhuLieuID { get; set; }
        public string? TenPhuLieu { get; set; }
    }

    public class SearchThongKeHrc1ApiResponse
    {
        public List<Hrc1PhuLieuHeaderTable> PhuLieuHeaderTables { get; set; } = new();
        public List<Hrc1ThongKeRow> Data { get; set; } = new();
        public int TotalRecords { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>Mỗi phần tử là tổng TotalKLPhuGia theo phụ liệu trên toàn khoảng lọc (không phân trang).</summary>
    public class ThongKeSumItemHrc1
    {
        public int PhuLieuID { get; set; }
        public string? TenPhuLieu { get; set; }
        public double? TotalKLPhuGia { get; set; }
    }

    /// <summary>Kết quả group theo (BieuMau, Scope, PhuLieuID) cho nút "Làm mới" trên Sổ Xuất-Nhập-Tồn HRC1.
    /// Mirror FilterSTD_NXTResponse của HRC2 nhưng đơn giản hơn — không cần PhuLieus/HeaderKeyId vì
    /// HRC1 dùng thẳng danh mục cố định HRC1_PhuLieuNM, không có khái niệm "chưa map".</summary>
    public class FilterSTD_NXTResponse_HRC1
    {
        public string? BieuMau { get; set; }
        public int Scope { get; set; }
        public int PhuLieuID { get; set; }
        public string? TenPhuLieu { get; set; }
        public decimal TotalKLPhuGia { get; set; }
    }
}
