namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Chi tiết" (CON) của 1 Nơi nhận — vd Nơi nhận 'LG1-CDQ' có 4 con 'LC1'..'LC4'.
    // Xem LC_XuatCapDanhMucCum. ID_LoaiNhan đặt ở đây (không phải Nơi nhận cha) vì 1 Nơi nhận có
    // thể gộp nhiều Loại nhận khác nhau ở từng con (vd TP2 "NMLG-CDQ" có con LC5/LC6 x CDQ/ƯỚT/
    // TN/xả ôtô) — xem .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11.
    public class LC_XuatCapDanhMucChiTiet
    {
        public int ID { get; set; }

        public int ID_NoiNhan { get; set; } // FK LC_XuatCapDanhMucCum.ID (cùng DB, đổi tên từ ID_Cum)

        public int ThuTu { get; set; }

        public string? ChiTietLo { get; set; } // vd 'LC1'..'LC6', 'ca trước', 'CSR62%' — NULL nếu Nơi nhận không tách con

        public int? ID_LoaiNhan { get; set; } // FK logic LC_XuatCapLoaiNhan.ID, NULLABLE

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
