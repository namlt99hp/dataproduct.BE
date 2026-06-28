namespace dataproduct.api.Models;

public class Hrc1MaVatTu
{
    public int Id { get; set; }
    public string MaVatTu { get; set; } = "";
    public DateTime NgayTao { get; set; }
    public string TenVatTu { get; set; } = "";
    public bool? IsLock { get; set; }
}
