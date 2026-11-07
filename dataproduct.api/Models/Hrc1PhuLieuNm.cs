using System;

namespace dataproduct.api.Models;

public partial class Hrc1PhuLieuNm
{
    public int ID { get; set; }
    public string TenPhuLieu { get; set; } = null!;
    public string? TenPhuLieuNM { get; set; }
    public bool DangSuDung { get; set; } = true;
    public bool IsNM { get; set; }
    /// <summary>Danh mục mặc định dùng khi khởi tạo phiếu Sổ Xuất-Nhập-Tồn HRC1 đầu tiên (chưa có ca
    /// trước để kế thừa). Mirror Header_Key.IsUsedNXT.</summary>
    public bool? IsUsedNXT { get; set; }
    // --- Thứ tự hiển thị Thống kê/Excel export, tách riêng BOF/LF vì 2 biểu mẫu có bộ cột phụ liệu khác
    // nhau (BOF dùng 13, LF dùng subset ~8) — mirror Header_Key.cs (ThuTu_TK_BOF/ThuTu_TK_LFRH/
    // ThuTu_Excel_BOF/ThuTu_Excel_LFRH), nhưng HRC1 không có công đoạn RH nên chỉ cần BOF/LF.
    // Đây là bộ 4 cột thứ tự DUY NHẤT của danh mục — cột ThuTu đơn cũ đã bỏ (không còn ý nghĩa riêng,
    // mọi nơi từng dùng ThuTu đều đã chuyển sang 1 trong 4 cột dưới đây theo đúng ngữ cảnh BOF/LF).
    /// <summary>Thứ tự hiển thị cột phụ liệu trong ThongKeTieuHaoHRC1.tsx tab BOF.</summary>
    public int? ThuTu_TK_BOF { get; set; }
    /// <summary>Thứ tự hiển thị cột phụ liệu trong ThongKeTieuHaoHRC1.tsx tab LF.</summary>
    public int? ThuTu_TK_LF { get; set; }
    /// <summary>Thứ tự hiển thị cột phụ liệu khi xuất Excel (thống kê lẫn chi tiết phiếu) cho BOF — cũng
    /// dùng làm nguồn thứ tự cho TaoTieuHaoLoThoi.tsx/ChiTietBOF.tsx (cột "có dữ liệu" mới hiện).</summary>
    public int? ThuTu_Excel_BOF { get; set; }
    /// <summary>Thứ tự hiển thị cột phụ liệu khi xuất Excel LF, đồng thời dùng làm danh sách phụ liệu
    /// mặc định hiển thị để nhập tay trên TaoTieuHaoTinhLuyenLF.tsx/ChiTietLF.tsx: loại nào được cấu
    /// hình cột này (khác NULL) mới hiện — vì LF chưa có nguồn NM để tự lọc "đã có dữ liệu" như BOF.</summary>
    public int? ThuTu_Excel_LF { get; set; }

    /// <summary>Mã vật tư bên hệ thống chi phí (ChiPhi_ProductionData.MaVatTu/MaChiPhi). NULL = không feed
    /// phụ liệu này sang hệ thống chi phí. Xem .claude/chiphitieuhao-hrc1.md.</summary>
    public string? MaVatTuChiPhi { get; set; }
    /// <summary>Nhóm hiển thị cột phụ liệu trên bảng Tiêu hao Tinh luyện LF (TaoTieuHaoTinhLuyenLF.tsx/
    /// ChiTietLF.tsx) — BOF chỉ có 1 nhóm cột duy nhất nên không dùng field này. Xem <see cref="LoaiPhuLieuLF"/>.</summary>
    public byte? LoaiPL { get; set; }
    public DateTime NgayTao { get; set; } = DateTime.Now;
    public string? NguoiTao { get; set; }
}

/// <summary>Giá trị cho Hrc1PhuLieuNm.LoaiPL — 2 nhóm cột phụ liệu trên bảng LF, mirror đúng 2 nhóm
/// trên biểu mẫu giấy BM.14/QT.05.10 ("Chất hợp kim hóa" / "Phụ gia & chất khử oxy").</summary>
public enum LoaiPhuLieuLF : byte
{
    KimLoai = 1,
    PhuGia = 2,
}
