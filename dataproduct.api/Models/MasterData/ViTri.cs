using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models.MasterData
{
    public class ViTri
    {
        [Key]
        public int ID_ViTri { get; set; }
        public string? TenViTri { get; set; }
        public int? ID_TrangThai { get; set; }
    }
}
