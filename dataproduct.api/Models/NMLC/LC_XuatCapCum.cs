namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Cụm" THẬT vùng Cấp cốc (header merge row A269/A318/A214 trong Excel — xem
    // .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11), theo Scope, quản lý qua trang riêng để
    // mở rộng khi phát sinh Cụm mới. KHÔNG liên quan LC_CumThap (cụm THÁP THAN của tab Phối trộn).
    public class LC_XuatCapCum
    {
        public int ID { get; set; }

        public int Scope { get; set; } // 1=TP1, 2=TP2

        public string Ma { get; set; } = string.Empty; // vd '1234', '5' (TP1) | '67' (TP2)

        public string Ten { get; set; } = string.Empty; // vd 'Cụm 1,2,3,4', 'Cụm 5', 'Cụm 6-7'

        public int ThuTu { get; set; }

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
