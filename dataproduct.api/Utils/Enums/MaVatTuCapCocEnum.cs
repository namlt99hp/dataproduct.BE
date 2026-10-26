namespace dataproduct.api.Utils.Enums
{
    /// <summary>
    /// ID_VatTu trong Tbl_ChiTiet_BienBanGiaoNhan ứng với từng "loại cốc" của Tab Cấp cốc
    /// (LC_BCSX_XNL_TP). Xác nhận qua Tbl_VatTu thực tế trên PRODUCTDATA — xem
    /// .claude/LuyenCoc/PhanTich_TabCapCoc.md §8.3(a).
    /// CHỐT 2026-10-23: KHÔNG dùng để auto-map "loại"/"cụm" — người dùng tự gán tay toàn bộ dòng
    /// BBGN vào slot Cấp cốc (xem LC_XuatCapNguon, §9). Enum này chỉ còn giá trị THAM KHẢO/hiển thị
    /// (vd lọc nhanh danh sách "chấm chuyến" theo loại vật tư trên FE nếu cần), không chứa logic
    /// nghiệp vụ nào tự quyết định nơi nhận.
    /// </summary>
    public enum MaVatTuCapCocEnum
    {
        ThanCoc1080CDQ = 486,
        ThanCoc1080Uot = 1653,
        ThanCoc1080TN = 1654,
        ThanCoc1080Cum5 = 1655,
        ThanCoc1080Mua = 487,
        ThanCoc1080HP = 1630,
        ThanCocDuoi10 = 484,
        ThanCocDuoi10Mua = 485,
        BuiCoc = 424,
        BuiCocDongBao = 425,
    }
}
