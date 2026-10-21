using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    // Dòng silo trong 1 "thẻ" phối trộn — xem SECTION 9 lc_bcxs_xnl_tp.sql.
    // VatLieuTen là SNAPSHOT (lấy từ LC_CapLieu của CÙNG phiếu lúc lưu) — FE hiển thị
    // readonly, không cho nhập tay (suy ra từ tab Cấp liệu, xem TabPhoiTron.tsx).
    // Stt/ID_CapLieu: hỗ trợ sẵn cho "tách liệu" (1 silo nhiều lớp) nhưng CHƯA expose ở FE —
    // lát cắt hiện tại chỉ dùng Stt=1, ID_CapLieu trỏ lớp Stt=1 của silo đó (nếu có).
    public class LC_PhoiTronChiTiet
    {
        [Key]
        public int ID { get; set; }
        public int ID_PhoiTron { get; set; }
        public byte Stt { get; set; } = 1;
        public int ID_Silo { get; set; }
        public int SiloSo { get; set; }
        public int? ID_CapLieu { get; set; }
        public int? ID_VatTu { get; set; }
        public string? VatLieuTen { get; set; }
        public decimal? TyLeKeHoach { get; set; }
        public decimal? TyLeThucTe { get; set; }
        public decimal? KLQuyKho { get; set; } // CALCULATED F02, backend tính
        public decimal? LuyKeQuyKho { get; set; } // CALCULATED F03, backend tính — reset tại Ca 1 ngày 1 hàng tháng (xem LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync)
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
