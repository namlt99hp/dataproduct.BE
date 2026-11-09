using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.DTOs
{
    public class DLNM_HRC2SearchDto
    {
        public DateTime? NgaySX { get; set; }
        public int? Ca { get; set; }
        public string? LoaiBM { get; set; }
        public int? Scope { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class DLNM_HRC2GroupedDto
    {
        public int Id { get; set; }
        public int REPORT_NO { get; set; }
        public DateTime? NgaySx { get; set; }
        public DateTime? Ngay { get; set; }
        public int Ca { get; set; }
        public string? BieuMau { get; set; }
        public int? Scope { get; set; }
        public string? MeThoi { get; set; }
        public string? MacThep { get; set; }
        public double? O2 { get; set; }
        public double? AR_RH { get; set; }
        public double? N2 { get; set; }
        public double? AR_BOF { get; set; }
        public double? AR_LF { get; set; }
        public double? KLGangLong { get; set; }
        public double? KLThepPhe { get; set; }
        public double? KlThepLong { get; set; }
        public int? QueLayMau { get; set; }
        public int? QueDoNhiet { get; set; }
        public string? GhiChu { get; set; }
    }

    public class ChuyenMeThoiRequest
    {
        public string MaBM { get; set; }
        public DateOnly NgaySX { get; set; }
        public int Ca { get; set; }
        public int Scope { get; set; }
        public int ChuyenToiCa { get; set; }
        public string MeThoi { get; set; }
        public string BieuMau { get; set; }
    }

    public class HRC2InsertModel
    {
        public long? Id { get; set; }
        public DateTime Ngay { get; set; }
        public int Ca { get; set; }
        public string BieuMau { get; set; }
        public int Scope { get; set; }

        public string MeThoi { get; set; }
        /// <summary>Giá trị macThep hiện đang hiển thị trên dòng (gốc hoặc đã sửa tay — phân biệt qua IsManualMacThep).</summary>
        public string MacThep { get; set; }
        /// <summary>null: FE không gửi flag (vd RH chưa bật allowEditMacThepOnNMRow) → không đụng tới override đã lưu. true/false: có gửi flag rõ ràng → set/xoá override tương ứng. Chỉ áp dụng cho dòng NM.</summary>
        public bool? IsManualMacThep { get; set; }

        public double? O2 { get; set; }
        public double? AR_RH { get; set; }
        public double? AR_LF { get; set; }
        public double? AR_BOF { get; set; }
        public double? N2 { get; set; }

        public double? KLGangLong { get; set; }
        public double? KLThepPhe { get; set; }
        public double? KlThepLong { get; set; }
        public int? QueLayMau { get; set; }
        public int? QueDoNhiet { get; set; }
        public string? GhiChu { get; set; }

        public bool IsNM { get; set; }
        public bool IsChuyenCa { get; set; }
        [NotMapped]
        public Guid RowKey { get; set; } = Guid.NewGuid();
        /// <summary>Chỉ số của dòng này trong mảng table1 gốc (formData) — dùng để ghi ngược ID vừa
        /// tạo vào đúng dòng trong DataJson sau khi lưu (xem SaveHRC2ManualFromPhieuFormAsync).</summary>
        [NotMapped]
        public int? SourceRowIndex { get; set; }

        public List<HRC2_PhuLieuInSertModel> hRC2_PhuLieus { get; set; } = new List<HRC2_PhuLieuInSertModel>();
    }
    public class HRC2_PhuLieuInSertModel
    {
        public string MeThoi { get; set; }
        public string BieuMau { get; set; }
        public int? ID_PhuLieu { get; set; }
        public string TenPhuLieu { get; set; }
        public double? KLPhuGia { get; set; }

        public int? ID_HeaderKey { get; set; }
        public string TenHienThi { get; set; }
        public bool? IsPhanBo { get; set; } = false; // ⭐ Đánh dấu là phân bổ
        public bool? IsManual { get; set; } = false;
        public double? KLPhuGia_Manual { get; set; }
        public bool? IsAddManual { get; set; } = false;
    }

    public class FilterSTD_NXTRequest
    {
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
        /// <summary>Nếu có: sau khi filter sẽ gọi sp_Init_XuatNhapTon_HRC2 để cập nhật dữ liệu phiếu hiện tại.</summary>
        public Guid? IdPhieu { get; set; }
        /// <summary>Danh sách Id_HeaderKey đang hiển thị trên FE (kể cả dòng mới chưa lưu). Dùng cho Init khi có IdPhieu.</summary>
        public List<int>? HeaderKeyIds { get; set; }
    }

    public class RefreshGangMetricsRequest
    {
        public List<Guid> PhieuIds { get; set; } = new();
    }

    public class RefreshGangMetricsResult
    {
        public int UpdatedRows { get; set; }
        public int SkippedPhieu { get; set; }
        public string Message { get; set; } = "";
    }

    public class SearchThongKe
    {
        public DateTime? TuNgay { get; set; }

        public DateTime? DenNgay { get; set; }

        public int? Ca { get; set; }

        public string? LoaiBM { get; set; }

        public int? Scope { get; set; }

        public string? SearchText { get; set; }
        public bool? IsTrungMeThoi { get; set; } = false;
        public bool? IsDelete { get; set; }
        public int? Page { get; set; } = 1;

        public int? PageSize { get; set; } = 20;
    }
}

