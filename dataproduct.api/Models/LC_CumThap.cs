using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    public class LC_CumThap
    {
        [Key]
        public int ID { get; set; }
        public int Scope { get; set; } // 1=TP1, 2=TP2
        public string Ma { get; set; } = string.Empty; // '1-2' | '3-4' | '5-6' | '7-8' | '9-10'
        public string? Ten { get; set; }
        public int? ThuTu { get; set; }
        public bool IsDelete { get; set; } = false;
        public DateTime NgayTao { get; set; }
    }
}
