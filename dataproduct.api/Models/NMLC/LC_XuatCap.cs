namespace dataproduct.api.Models.NMLC
{
    // Vùng "Cấp cốc" (TP1 "C. Cốc xuất đi" / TP2 "3. Cấp cốc") — 1 dòng = 1 Ô "slot" cố định
    // trong layout (TenCan/NoiNhan/ChiTietLo/LoaiCoc), xem .claude/LuyenCoc/PhanTich_TabCapCoc.md
    // §2-3, §9. Layout slot là HẰNG SỐ theo Scope (khai báo trong
    // Utils/LcXuatCapLayoutSeed.cs — KHÔNG phải bảng danh mục DB, vì cố định theo Excel, không
    // user-quản-lý được), Initializer copy forward CẤU TRÚC (TenCan/NoiNhan/ChiTietLo/LoaiCoc/
    // ThuTu) cho mỗi phiếu mới, CHỈ reset giá trị nhập/tính (giống SECTION 10 LC_SiloCocTon).
    //
    // KHÔNG auto-map "loại"/"cụm" từ BBGN (chốt 2026-10-23, theo yêu cầu người dùng — "để user tự
    // map tay hết", không cần phân biệt ID_VatTu/Xưởng nhận tự động). Người dùng tự gán 1 hoặc
    // nhiều dòng "chấm chuyến" BBGN (Tbl_ChiTiet_BienBanGiaoNhan, liệt kê thô giống hàm
    // ExportToExcel, KHÔNG gộp sẵn) vào đúng slot qua LC_XuatCap_Nguon; KhoiLuongAm/DoAm của slot
    // là SUM/trung bình trọng số từ các dòng đã gán (hoặc nhập tay thẳng nếu không có nguồn BBGN
    // tương ứng, ví dụ "ca trước"/"xả ôtô"/"CSR62%").
    public class LC_XuatCap
    {
        public int ID { get; set; }

        public Guid IDPhieu { get; set; }

        public DateOnly? NgaySX { get; set; }

        public int? Ca { get; set; }

        public int Scope { get; set; } // 1=TP1, 2=TP2 (khớp BmPhieu.Scope)

        public int ThuTu { get; set; } // thứ tự hiển thị theo layout cố định (LcXuatCapLayoutSeed)

        public string TenCan { get; set; } = string.Empty; // SNAPSHOT, vd "Cân BTC8,17 – NM.LC"

        public string NoiNhan { get; set; } = string.Empty; // SNAPSHOT, vd "LG1-CDQ", "Xuất bán"

        public string? ChiTietLo { get; set; } // SNAPSHOT, vd "LC1".."LC6", "ca trước", "CSR62%"

        public string LoaiCoc { get; set; } = string.Empty; // SNAPSHOT, vd "10-80mm" | "<10mm" | "Bụi CDQ"

        // SNAPSHOT — khoá nhóm "Trong ca"/"Lũy kế" THẬT, đọc từ ranh giới merge cell Excel gốc
        // (LcXuatCapLayoutSeed), KHÔNG suy từ NoiNhan. Nhiều slot cùng NhomTongHop (cùng phiếu)
        // chia sẻ 1 giá trị TrongCa/LuyKe.
        public string NhomTongHop { get; set; } = string.Empty;

        // SNAPSHOT từ LC_XuatCapDanhMucCum.ID_PhongBan lúc Initialize — FK logic tới
        // PRODUCTDATA.dbo.Tbl_PhongBan (không FK cứng). Field tham khảo phụ, KHÔNG dùng để
        // nhóm chính (xem LC_XuatCapDanhMucCum).
        public int? ID_PhongBan { get; set; }

        // SNAPSHOT ID của Cụm (LC_XuatCapDanhMucCum.ID) sinh ra slot này — dùng làm khoá nhóm
        // THẬT cho FE khi hiển thị/gán (nhiều slot cùng ID_DanhMucCum = cùng 1 Cụm, vd 4 slot
        // LC1-4 đều thuộc Cụm "LG1-CDQ"). KHÔNG FK cứng (snapshot lịch sử, danh mục có thể đổi/
        // xoá sau mà không ảnh hưởng phiếu cũ).
        public int? ID_DanhMucCum { get; set; }

        public decimal? KhoiLuongAm { get; set; } // nhập tay HOẶC SUM từ LC_XuatCap_Nguon đã gán

        public decimal? DoAm { get; set; }

        public decimal? TyKhoi { get; set; } // "nhập tay khi phát sinh chấm chuyến" — có thể NULL

        public decimal? KLQuyKho { get; set; } // CALCULATED (F16 = ROUND(KhoiLuongAm - KhoiLuongAm*DoAm/100, 3))

        public decimal? TrongCa { get; set; } // CALCULATED (F17 = SUM KLQuyKho theo nhóm NhomTongHop cùng phiếu)

        public decimal? LuyKe { get; set; } // CALCULATED = TrongCa + LuyKe của phiếu/NoiNhan liền trước (DB, không external ref)

        public string? GhiChu { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
