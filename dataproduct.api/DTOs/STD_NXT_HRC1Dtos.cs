using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.DTOs
{
    public class STD_NXT_HRC1_UpsertDto
    {
        public Guid IdPhieu { get; set; }
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
        public string? BieuMau { get; set; }
        // Danh sách chi tiết
        public List<NXTDetailDto_HRC1> Details { get; set; } = new();
        // Danh sách tổng hợp
        public List<NXTSummaryDto_HRC1> Summary { get; set; } = new();
    }

    public class NXTDetailDto_HRC1
    {
        public int Scope { get; set; }
        public int PhuLieuID { get; set; }
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
        /// <summary>Thứ tự hiển thị dòng trong bảng (theo Scope), do người dùng sắp xếp trên UI.</summary>
        public int? ThuTu { get; set; }
    }

    public class NXTSummaryDto_HRC1
    {
        public int PhuLieuID { get; set; }
        public string? TenNguyenLieu { get; set; }

        public decimal? TongTonDauCa { get; set; }
        public decimal? TongNhapTrongCa { get; set; }
        public decimal? TongTonCuoiCa { get; set; }

        public decimal? TongSuDung { get; set; }
        public decimal? TongSDTrenSoSach { get; set; }
        public decimal? ChenhLech { get; set; }
        public decimal? TyLeBOF { get; set; }
        /// <summary>KHÔNG có TyLeRH — HRC1 không có công đoạn RH.</summary>
        public decimal? TyLeLF { get; set; }
    }

    /// <summary>
    /// Scope trên STD_XUAT_NHAP_TON_HRC1/STD_NXT_TOTAL_HRC1 lưu giá trị enum này (1-10),
    /// KHÔNG phải số lò/tổ vật lý — mirror ToHopSTDNXT của HRC2.
    /// </summary>
    public enum ToHopSTDNXT_HRC1
    {
        LoThoi1 = 1,
        LoThoi2 = 2,
        LoThoi3 = 3,
        LoThoi4 = 4,
        LoThoi5 = 5,
        TinhLuyen1 = 6,
        TinhLuyen2 = 7,
        TinhLuyen3 = 8,
        TinhLuyen4 = 9,
        TinhLuyen5 = 10,
    }

    public class IdPhuLieuModel
    {
        public int Id_PhuLieu { get; set; }
    }

    public class InitXuatNhapTonHRC1Request
    {
        public DateTime NgaySX { get; set; }     // Ngày sản xuất
        public int Ca { get; set; }              // 1 hoặc 2
        public Guid IdPhieu { get; set; }        // Phiếu mới

        public List<IdPhuLieuModel> PhuLieus { get; set; } = new();
    }

    public class STD_NXT_HRC1_PhanBoDto
    {
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
        public int PhuLieuID { get; set; }
        public decimal ChenhLech { get; set; }
        public Guid IdPhieu { get; set; }
        public decimal? TyLeBOF { get; set; }
        /// <summary>KHÔNG có TyLeRH — HRC1 không có công đoạn RH.</summary>
        public decimal? TyLeLF { get; set; }
    }

    public class STD_NXT_HRC1_KhongPhanBoDto
    {
        public Guid IdPhieu { get; set; }
        public int PhuLieuID { get; set; }
        public DateTime NgaySX { get; set; }
        public int Ca { get; set; }
    }

    public class STD_NXT_HRC1_NhapXuatTonSearchRequest
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
    }

    /// <summary>
    /// 1 dòng so sánh giữa Tồn cuối của phiếu ca trước và Tồn đầu của phiếu ca kế tiếp,
    /// cùng Scope (tổ hợp Lò thổi/Tinh luyện) + PhuLieuID — dùng để đối chiếu tính liên tục của sổ.
    /// </summary>
    public class STD_NXT_HRC1_NhapXuatTonRow
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
