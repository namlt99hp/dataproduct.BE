using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    public class TKVV_BaoCaoSanLuongChiPhi
    {
        [Key]
        public long ID { get; set; }
        public Guid? PhieuID { get; set; }
        public DateOnly NgaySX { get; set; }
        public byte Ca { get; set; }
        public string? Kip { get; set; }
        public int? Scope { get; set; }          // INT — khớp unique index NgaySX+Ca+Scope+NguyenVatLieuID
        public int? ThuTu { get; set; }
        public int NguyenVatLieuID { get; set; }
        public decimal? KLAm { get; set; }
        public decimal? KLAmAuto { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? QuyKho { get; set; }
        public decimal? ThanhPhamL1 { get; set; }
        public decimal? ThanhPhamL2 { get; set; }
        public decimal? ThanhPhamL3 { get; set; }
        public string? ThanhPham_Note { get; set; }
        public string? GhiChu { get; set; }
        public bool IsAdjusted { get; set; }
        public int? AdjustedBy { get; set; }
        public DateTime? AdjustedDate { get; set; }
        public decimal? TiLePhanBo { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public bool IsDelete { get; set; }
        public int? ID_CT_BBGN { get; set; }
    }
}
