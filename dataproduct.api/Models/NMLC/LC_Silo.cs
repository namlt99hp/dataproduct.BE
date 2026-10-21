using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    public class LC_Silo
    {
        [Key]
        public int ID { get; set; }
        public int Scope { get; set; } // 1=TP1, 2=TP2
        public int SiloSo { get; set; }
        public string? TenSilo { get; set; }
        public string LoaiSilo { get; set; } = "THAN";
        public int? ThuTu { get; set; }
        public bool IsDelete { get; set; } = false;
        public DateTime NgayTao { get; set; }
    }
}
