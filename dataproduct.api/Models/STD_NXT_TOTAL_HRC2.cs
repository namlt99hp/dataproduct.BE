using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.Models;

public class STD_NXT_TOTAL_HRC2
{
    [Key]
    public int Id { get; set; }

    public int Ca { get; set; }

    public DateTime NgaySX { get; set; }

    public int Id_HeaderKey { get; set; }

    [StringLength(255)]
    public string? TenNguyenLieu { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TongTonDauCa { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TongTonNhapTrongCa { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TongTonCuoiCa { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TongSuDung { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TongSDTrenSoSach { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? ChenhLech { get; set; }

    public Guid Id_Phieu { get; set; }
    public bool? HasPhanBo { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TyLeBOF { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? TyLeTinhLuyen { get; set; }

    // Khối lượng phân bổ (tính theo 1 mẻ) sau khi chia theo nhóm mẻ
    [Column(TypeName = "decimal(18,3)")]
    public decimal? KLPB_BOF { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? KLPB_TL { get; set; }
    [Column(TypeName = "decimal(18,3)")]
    public decimal? TyLeRH { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal? KLPB_RH { get; set; }

    /// <summary>Tổng khối lượng "Thống Kê" hiệu lực của Id_HeaderKey này, gộp mọi mẻ BOF trong ca, tại
    /// thời điểm bấm Phân bổ/Không phân bổ gần nhất — đã bao gồm phần phân bổ nếu có. NULL nếu chưa bấm
    /// 1 trong 2 nút, hoặc vừa Thu hồi phân bổ (reset chờ quyết định lại). Mirror
    /// STD_NXT_TOTAL_HRC1.KLTK_BOF. Xem STD_XNT_HRC2Repository.ComputeKLTKAsync.</summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal? KLTK_BOF { get; set; }

    /// <summary>Như KLTK_BOF, gộp mọi mẻ LF.</summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal? KLTK_LF { get; set; }

    /// <summary>Như KLTK_BOF, gộp mọi mẻ RH.</summary>
    [Column(TypeName = "decimal(18,3)")]
    public decimal? KLTK_RH { get; set; }
}

