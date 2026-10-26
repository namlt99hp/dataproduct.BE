using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    // Tồn silo than cuối kíp (vật lý, gộp cả silo) — xem SECTION 4 lc_bcxs_xnl_tp.sql.
    // KLAmTonCuoi: CALCULATED (F05a, CHỈ TP1 — xem LC_CapLieuSiloRepository.ComputeKLAmTonCuoi_TP1;
    // TP2 dùng F05b, CHƯA triển khai, OQ-03 còn mở riêng cho TP2, giá trị NULL).
    // KLTonCuoiQuyKho: CALCULATED (F01 = KLAmTonCuoi × (100−DoAmSilo)/100) — tự NULL theo TP2
    // vì KLAmTonCuoi NULL ở đó.
    public class LC_CapLieuTonSilo
    {
        [Key]
        public int ID { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public int ID_Silo { get; set; }
        public int SiloSo { get; set; }
        public decimal? AmSiloCuoiKy { get; set; }
        public decimal? KLAmTonCuoi { get; set; }
        public decimal? DoAmSilo { get; set; }
        public decimal? KLTonCuoiQuyKho { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
