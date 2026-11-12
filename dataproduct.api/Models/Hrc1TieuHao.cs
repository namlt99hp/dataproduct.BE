using System;

namespace dataproduct.api.Models;

public partial class Hrc1TieuHao
{
    public int ID { get; set; }
    public int? IDNM { get; set; }
    public bool IsNM { get; set; } = true;
    public bool IsEdited { get; set; }
    public string? BieuMau { get; set; }
    public int? Scope { get; set; }
    public string? MeThoi { get; set; }
    public string? MacThep { get; set; }
    // Snapshot giá trị NM gốc của MacThep tại thời điểm sửa tay lần đầu — dùng để FE dựng
    // macThep__orig, mirror KLPhuGia/KLPhuGia_Manual của Hrc1PhuLieu. Xem DLNMHRC1Service.SaveHRC1ManualDataAsync.
    public string? MacThepOrig { get; set; }
    // true nếu MacThep đã từng bị sửa tay trên dòng NM — KHÔNG suy luận từ MacThepOrig != null, vì
    // MacThepOrig chính nó có thể null hợp lệ (giá trị NM gốc lúc sửa là null, vd MacThep chưa sync
    // được lần đầu) mà vẫn cần biết đã sửa hay chưa.
    public bool MacThepIsManual { get; set; }
    public double? O2 { get; set; }
    public double? N2 { get; set; }
    public double? AR { get; set; }
    public bool? IsChuyenCa { get; set; }
    public int? CaChuyen { get; set; }
    public bool? IsTrungMeThoi { get; set; }
    public int? QueLayMau { get; set; }
    public int? QueDoNhiet { get; set; }
    public string? GhiChu { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? NgayXoa { get; set; }
    public int? NguoiXoa { get; set; }
    public DateTime? ThoiDiemKetThuc { get; set; }
    public decimal? KLGang { get; set; }
    public decimal? KLGangLongCCT { get; set; }
    public decimal? KLGangLongTong { get; set; }
    public decimal? KLThepPhe { get; set; }
    // Snapshot giá trị NM gốc của KLThepPhe tại thời điểm sửa tay lần đầu — xem MacThepOrig.
    public decimal? KLThepPheOrig { get; set; }
    // xem MacThepIsManual.
    public bool KLThepPheIsManual { get; set; }
    public decimal? KLThepPheGang { get; set; }
    public decimal? KLThepLong { get; set; }
    public byte? Ca { get; set; }
    public DateOnly? NgaySanXuat { get; set; }
    public DateTime? ThoiDiemBatDau { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public int? NguoiTao { get; set; }
    public DateTime? NgayCapNhat { get; set; }
    public int? NguoiCapNhat { get; set; }
    public string? ThoiGianLF {get;set;}
    public Guid? IDPhieu { get; set; }
    // Chỉ có giá trị trên dòng "bản sao clone" của 1 dòng IsNM=true: giữ IDNM gốc mà dòng này che
    // (bản thân IDNM luôn NULL trên dòng bản sao để né unique index UX_HRC1_TieuHao_IDNM_Scope_BieuMau —
    // xem DLNMHRC1Service.DuplicateHrc1RowsForCloneAsync). Không đánh index — chỉ dùng để merge ngược
    // giá trị về dòng canonical (IDNM == SourceIDNM) lúc phiếu clone được Duyệt, xem
    // DLNMHRC1Service.MergeAndCleanupHrc1CloneChainOnApproveAsync.
    public int? SourceIDNM { get; set; }

    public int? IDMeThep { get; set; }
}
    