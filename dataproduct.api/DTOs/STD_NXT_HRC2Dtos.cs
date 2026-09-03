using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.DTOs
{
    public class STD_NXT_HRC2_UpsertDto
    {
        public Guid IdPhieu { get; set; }
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
        public string? BieuMau { get; set; }
        // Danh sách chi tiết
        public List<NXTDetailDto> Details { get; set; } = new();
        // Danh sách tổng hợp
        public List<NXTSummaryDto> Summary { get; set; } = new();
        // Danh sách kiểm kê (snapshot)
        public List<SaveKiemKeRequest>? KiemKe { get; set; }
    }

    public class NXTDetailDto
    {
        public int Scope { get; set; }       
        public int Id_HeaderKey { get; set; }
        public string? TenNguyenLieu { get; set; }
        public int? ViTri { get; set; }
        public decimal? TonDauCa { get; set; }
        public string? TuongQuanDauCa { get; set; }

        public decimal? NhapVaoTrongCa { get; set; }
        public decimal? MucLieu { get; set; }
        public decimal? TheTich { get; set; }
        public decimal? TyTrong { get; set; }

        public decimal? TonCuoiCa { get; set; }
        public string? TuongQuanCuoiCa { get; set; }
        public decimal? TongThucTe { get; set; }
        public int? IDSilo { get; set; }
        public decimal? LuongSuDungKiemKe { get; set; }
    }


    public class NXTSummaryDto
    {
        public int Id_HeaderKey { get; set; }
        public string? TenNguyenLieu { get; set; }

        public decimal? TongTonDauCa { get; set; }
        public decimal? TongNhapTrongCa { get; set; }
        public decimal? TongTonCuoiCa { get; set; }

        public decimal? TongSuDung { get; set; }
        public decimal? TongSDTrenSoSach { get; set; }
        public decimal? ChenhLech { get; set; }
        public decimal? TyLeBOF { get; set; }
        public decimal? TyLeTinhLuyen { get; set; }
        public decimal? TyLeRH { get; set; }
    }

    public enum ToHopSTDNXT
    {
        BOF6 = 1,
        BOF7 = 2,
        LF = 3,
        RH1 = 4,
        RH2 = 5
    }


    public class IdHeaderKeyModel
    {
        public int Id_HeaderKey { get; set; }
    }

    public class InitXuatNhapTonHRC2Request
    {
        public DateTime NgaySX { get; set; }     // Ngày sản xuất
        public int Ca { get; set; }              // 1 hoặc 2
        public Guid IdPhieu { get; set; }        // Phiếu mới

        public List<IdHeaderKeyModel> HeaderKeys { get; set; }
    }

    public class STD_NXT_HRC2_PhanBoDto
    {
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
        public int Id_HeaderKey { get; set; }
        public decimal ChenhLech { get; set; }
        public Guid IdPhieu { get; set; }
        public decimal? TyLeBOF { get; set; }
        public decimal? TyLeTinhLuyen { get; set; }
        public decimal? TyLeRH { get; set; }
    }

    public class STD_NXT_HRC2_KhongPhanBoDto
    {
        public Guid IdPhieu { get; set; }
        public int Id_HeaderKey { get; set; }
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
    }

    public class STD_NXT_RelatedPhieuStatusRequest
    {
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
        public List<STD_NXT_RelatedPhieuTarget> Targets { get; set; } = new();
    }

    public class STD_NXT_RelatedPhieuTarget
    {
        public string? TabKey { get; set; }
        public string? Label { get; set; }
        public string? BieuMau { get; set; } // BOF/LF/RH hoặc full mã BM
        public int? Scope { get; set; }
    }

    public class STD_NXT_RelatedPhieuStatusItem
    {
        public string? TabKey { get; set; }
        public string? Label { get; set; }
        public string? BieuMau { get; set; }
        public int? Scope { get; set; }
        public Guid? IdPhieu { get; set; }
        public int? TinhTrang { get; set; }
    }

    public class STD_NXT_RelatedPhieuStatusResponse
    {
        public List<STD_NXT_RelatedPhieuStatusItem> Items { get; set; } = new();
        public bool CanPhanBo { get; set; }
        public int IncompleteCount { get; set; }
    }

    public class SaveKiemKeRequest
    {
        public DateTime NgaySX { get; set; }
        public string Ca { get; set; } = null!;
        public int HeaderKeyId { get; set; }
        public int SiloId { get; set; }
        public int PhuLieuNMId { get; set; }
        public decimal? TheTich { get; set; }
        public decimal? TyTrong { get; set; }
        public int? Scope { get; set; }
    }

    public class STD_NXT_HRC2_NhapXuatTonSearchRequest
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
    }

    /// <summary>
    /// 1 dòng so sánh giữa Tồn cuối của phiếu ca trước và Tồn đầu của phiếu ca kế tiếp,
    /// cùng Scope (tổ hợp BOF/LF/RH) + Id_HeaderKey — dùng để đối chiếu tính liên tục của sổ.
    /// </summary>
    public class STD_NXT_HRC2_NhapXuatTonRow
    {
        public DateTime? NgaySXTruoc { get; set; }
        public int? CaTruoc { get; set; }
        public string? PhuLieuTruoc { get; set; }
        public decimal? TonCuoiTruoc { get; set; }

        public DateTime? NgaySXSau { get; set; }
        public int? CaSau { get; set; }
        public string? PhuLieuSau { get; set; }
        public decimal? TonDauSau { get; set; }
    }
}

