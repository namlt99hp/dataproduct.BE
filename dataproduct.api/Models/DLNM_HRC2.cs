using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.Models;

public partial class DLNM_HRC2
{
    public long ID { get; set; }
    public int? REPORT_NO { get; set; }
    public DateTime? NgaySx { get; set; }
    public DateTime? Ngay { get; set; }  
    public int? Ca { get; set; }
    public string? BieuMau { get; set; }
    public int? Scope { get; set; }
    public string? MeThoi { get; set; }
    public string? MacThep { get; set; }
    /// <summary>Giá trị Mác thép user sửa tay trên dòng NM (null = chưa sửa) — MacThep giữ nguyên giá trị gốc từ NM, không bị ghi đè. Giá trị hiệu lực = MacThep_Manual ?? MacThep.</summary>
    public string? MacThep_Manual { get; set; }
    public bool? IsManualMacThep { get; set; }
    public double? O2 { get; set; }
    public double? AR_RH { get; set; }
    public double? N2 { get; set; }
    public double? AR_BOF { get; set; }
    public double? AR_LF { get; set; }
    public double? KLGangLong { get; set; }
    public double? KLThepPhe { get; set; }
    public double? KLThepLong { get; set; }
    public int? QueLayMau { get; set; }
    public int? QueDoNhiet { get; set; }
    public string? GhiChu { get; set; }
    public bool? IsNM { get; set; }
    public bool? IsChuyenCa  { get; set; }
    public double? KLGangLongCCT { get; set; }
    public double? KLGangLongCR { get; set; }
    public double? KLThepPheGang { get; set; }
    public bool? IsTrungMeThoi { get; set; } = false;
    /// <summary>True = mẻ được SP sync (sp_Sync_HRC2_FromNM) chèn vào SAU KHI phiếu của slot
    /// (Ngay/Ca/Scope/BieuMau) này đã gửi duyệt (TinhTrang &gt;= DaGui). Các chỗ tổng hợp/tính toán
    /// (Thống kê, Phân bổ...) phải loại trừ record này cho tới khi được xử lý qua Hiệu chỉnh.</summary>
    public bool? IsSyncSauGui { get; set; } = false;
    public bool? IsDelete { get; set; } = false;

    [NotMapped]
    public Guid TempKey { get; set; }
}


