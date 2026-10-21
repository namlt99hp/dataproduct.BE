using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Data_Product.Models
{
    [Table("Tbl_VatTuMaLo")]

    public class Tbl_VatTuMaLo
    {
        [Key]
        public int ID_MaLoVT { get; set; }
        public int ID_VatTu { get; set; }
        public int ID_MaLo { get; set; }
        [NotMapped]
        public string[] MaLo { get; set; }
    }
}
