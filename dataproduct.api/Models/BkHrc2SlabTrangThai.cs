namespace dataproduct.api.Models;

public class BkHrc2SlabTrangThai
{
    public int Id { get; set; }
    public int IdSlab { get; set; }
    public Guid? IdPhieuBBSL { get; set; }
    public int TrangThaiKCS { get; set; }
    public int? NguoiChuyenKCS { get; set; }
    public DateTime? NgayChuyenKCS { get; set; }
    public int TrangThaiDuc { get; set; }
    public int? NguoiXacNhanDuc { get; set; }
    public DateTime? NgayXacNhanDuc { get; set; }
    public int TrangThaiKho { get; set; }
    public int? NguoiXacNhanKho { get; set; }
    public DateTime? NgayXacNhanKho { get; set; }
    public int TrangThaiPKH { get; set; }
    public int? NguoiChotPKH { get; set; }
    public DateTime? NgayChotPKH { get; set; }
    public DateTime NgayTao { get; set; }

    public virtual BkHrc2Slab Slab { get; set; } = null!;
}
