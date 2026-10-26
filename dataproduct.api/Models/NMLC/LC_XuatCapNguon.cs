namespace dataproduct.api.Models.NMLC
{
    // Junction: ghi lại các dòng "chấm chuyến" BBGN (Tbl_ChiTiet_BienBanGiaoNhan, PRODUCTDATA —
    // KHÁC database với LC_XuatCap ở PRODUCT_FORM, nên KHÔNG FK cứng) mà người dùng đã TỰ GÁN
    // TAY vào 1 slot LC_XuatCap, xem .claude/LuyenCoc/PhanTich_TabCapCoc.md §9.
    // 1 dòng BBGN chi tiết chỉ được gán vào ĐÚNG 1 slot (UNIQUE ID_CT_BBGN) để tránh đếm trùng.
    // KhoiLuongGan/DoAmGan là SNAPSHOT tại thời điểm gán — phòng trường hợp BBGN bị sửa/hủy sau.
    public class LC_XuatCapNguon
    {
        public int ID { get; set; }

        public int ID_XuatCap { get; set; } // FK LC_XuatCap.ID (cùng DB, FK thường)

        public int ID_BBGN { get; set; } // Tbl_BienBanGiaoNhan.ID_BBGN (PRODUCTDATA, không FK cứng)

        public int ID_CT_BBGN { get; set; } // Tbl_ChiTiet_BienBanGiaoNhan.ID_CT_BBGN (không FK cứng)

        public decimal? KhoiLuongGan { get; set; } // SNAPSHOT KhoiLuong_BG/KhoiLuong_BN lúc gán

        public decimal? DoAmGan { get; set; } // SNAPSHOT DoAm_W lúc gán

        public DateTime NgayTao { get; set; }
    }
}
