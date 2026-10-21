using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using dataproduct.api.Models.MasterData;

namespace Data_Product.Models
{
    [Table("Tbl_MaLo")]
    public class Tbl_MaLo
    {
        [Key]
        public int ID_MaLo { get; set; }
        public string? TenMaLo { get; set; }
        public string? PhongBan { get; set; }
        public int ID_TinhTrang { get; set; }
        [NotMapped]
        public string[] ID_VatTu { get; set; }
        [NotMapped]
        public List<Tbl_VatTu> VatTu { get; set; }
    }
}
