using System;

namespace dataproduct.api.Models;

public partial class BkHrc2Slab
{
    public int Id { get; set; }

    public int? BkmisId { get; set; }

    public DateOnly? NgaySanXuat { get; set; }

    public string? ShiftName { get; set; }     // Giá trị gốc từ BKMIS (vd: "1A", "2B")

    public string? CaSanXuat { get; set; }     // Ký tự ca: "1" (ngày) / "2" (đêm)

    public string? KipSanXuat { get; set; }    // Ký tự kíp: "A", "B", "C" (từ ShiftName[1])

    public string? MeThep { get; set; }

    public string? IdSlab { get; set; }

    public string? MacThep { get; set; }

    public string? ChatLuong { get; set; }         // Đổi từ IdPhanLoai INT

    public decimal? ChieuDay { get; set; }

    public decimal? ChieuRong { get; set; }

    public decimal? ChieuDai { get; set; }

    public decimal? KhoiLuong { get; set; }

    public decimal? KhoiLuongTinhToan { get; set; }

    public string? ChatLuongTPHH { get; set; }     // Đổi từ TenPhanLoai

    public string? ThongTinPhoi { get; set; }

    public string? TpKhongDatGangLong { get; set; }

    public string? GhiChu { get; set; }

    public string? LoaiPhoi { get; set; }

    public string? SapCode { get; set; }

    public string? SapDescription { get; set; }

    public string? SoLo { get; set; }

    public string? OrderId { get; set; }

    public int? MayDuc { get; set; }

    public bool? IsTrungIDSlab { get; set; }

    public bool? IsDiffMacThep { get; set; }

    public int? Line { get; set; }

    public DateOnly? SapLastTime { get; set; }

    public bool IsChot { get; set; }

    public string? ChecksumVal { get; set; }

    public DateTime? NgayTao { get; set; }

    public virtual BkHrc2SlabTrangThai? TrangThai { get; set; }
}
