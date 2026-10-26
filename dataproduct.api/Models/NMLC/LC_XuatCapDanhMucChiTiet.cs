namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Chi tiết" (CON) của 1 Cụm nhận — vd Cụm 'LG1-CDQ' có 4 con 'LC1'..'LC4'. Xem
    // LC_XuatCapDanhMucCum.
    public class LC_XuatCapDanhMucChiTiet
    {
        public int ID { get; set; }

        public int ID_Cum { get; set; } // FK LC_XuatCapDanhMucCum.ID (cùng DB)

        public int ThuTu { get; set; }

        public string? ChiTietLo { get; set; } // vd 'LC1'..'LC6', 'ca trước', 'CSR62%' — NULL nếu Cụm không tách con (vd 1 dòng đơn)

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
