namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_XuatCap — tab "Cấp cốc" (LcTabXuatCap) ──────────────────────────────────────────
    // 1 slot = 1 dòng layout cố định (TenCan/NoiNhan/ChiTietLo/LoaiCoc, xem LcXuatCapLayoutSeed),
    // kèm danh sách "nguồn" (dòng BBGN đã được người dùng gán tay vào slot này — §9.0 mục 4,
    // KHÔNG auto-map). KhoiLuongAm của slot = SUM(Nguon[].KhoiLuongGan) nếu có gán, hoặc giá trị
    // nhập tay thẳng nếu Nguon rỗng.
    public class LcXuatCapNguonDto
    {
        public int Id { get; set; }
        public int IdBbgn { get; set; }
        public int IdCtBbgn { get; set; }
        public decimal? KhoiLuongGan { get; set; }
        public decimal? DoAmGan { get; set; }
    }

    public class LcXuatCapSlotDto
    {
        public int Id { get; set; }
        public int ThuTu { get; set; }
        public string TenCan { get; set; } = string.Empty;
        public string NoiNhan { get; set; } = string.Empty;
        public string? ChiTietLo { get; set; }
        public string LoaiCoc { get; set; } = string.Empty;
        public string NhomTongHop { get; set; } = string.Empty; // khoá nhóm Trong ca/Lũy kế — các slot cùng nhóm hiển thị TrongCa/LuyKe giống nhau (giống merge cell Excel)
        public int? IdPhongBan { get; set; } // tham khảo phụ, KHÔNG dùng để nhóm chính
        public string? TenPhongBan { get; set; }
        public int? IdDanhMucCum { get; set; } // khoá Cụm (LC_XuatCapDanhMucCum.ID) — FE dùng để nhóm "Gán vào ô" theo Cụm (cha)

        public decimal? KhoiLuongAm { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? TyKhoi { get; set; }
        public decimal? KLQuyKho { get; set; } // F16, BE tính
        public decimal? TrongCa { get; set; }  // F17, BE tính (SUM theo nhóm TenCan+NoiNhan)
        public decimal? LuyKe { get; set; }    // BE tính (TrongCa + lũy kế phiếu liền trước)
        public string? GhiChu { get; set; }

        public List<LcXuatCapNguonDto> Nguon { get; set; } = new();
    }

    // Lưu tay (DoAm/TyKhoi/GhiChu luôn nhận; KhoiLuongAm CHỈ áp dụng khi slot không có Nguon nào
    // đã gán — nếu có Nguon, BE tự tính lại KhoiLuongAm = SUM, bỏ qua giá trị FE gửi).
    public class LcXuatCapSaveSlotDto
    {
        public int Id { get; set; }
        public decimal? KhoiLuongAm { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? TyKhoi { get; set; }
        public string? GhiChu { get; set; }
    }

    public class LcXuatCapGanRequest
    {
        public int IdXuatCap { get; set; }
        public int IdBbgn { get; set; }
        public int IdCtBbgn { get; set; }
        public decimal? KhoiLuongGan { get; set; }
        public decimal? DoAmGan { get; set; }
    }
}
