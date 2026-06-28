namespace dataproduct.api.Models;

public class Hrc1BbslTongHopGhiChu
{
    public int Id { get; set; }
    public Guid IdPhieuBBSL { get; set; }
    public string? MacThep { get; set; }
    public string? KichThuoc { get; set; }
    public string? GhiChu { get; set; }
    public DateTime? NgayCapNhat { get; set; }
}
