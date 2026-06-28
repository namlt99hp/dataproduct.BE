namespace dataproduct.api.Models;

public class BkSyncHrc2SlabControl
{
    public int Id { get; set; }
    public string? TableName { get; set; }
    public string? TrangThai { get; set; }
    public DateOnly? NgayBatDau { get; set; }
    public DateOnly? NgayKetThuc { get; set; }
    public DateTime? BatDauLuc { get; set; }
    public DateTime? KetThucLuc { get; set; }
    public int? SoRecordSync { get; set; }
    public string? GhiChu { get; set; }
}
