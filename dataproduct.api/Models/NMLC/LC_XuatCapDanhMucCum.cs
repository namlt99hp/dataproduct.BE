namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Cụm nhận" (CHA) vùng Cấp cốc — trang quản lý /lc-xuatcapdanhmuc. Xem
    // .claude/LuyenCoc/PhanTich_TabCapCoc.md §9 (chốt 2026-10-23, lượt 6).
    //
    // 1 Cụm = 1 đơn vị "Nơi nhận" THẬT trên layout Excel (vd 'LG1-CDQ', 'LG1-Ướt', 'Cốc xả bãi')
    // — đây là đơn vị dùng để GÁN (nguồn BBGN nhiều khi chỉ biết tới mức thô, vd chỉ biết "LG1",
    // người dùng phải tự quyết định rơi vào Cụm CDQ hay Ướt) và cũng là đơn vị tính Trong ca/Lũy
    // kế (qua NhomTongHop — nhiều Cụm có thể cùng NhomTongHop nếu Excel merge chung, vd TP1 dòng
    // 284-295 gộp chung "Cốc xả bãi"+"LG2-TN"+"LG2-CDQ"+"Xuất bán").
    //
    // ID_PhongBan: field THAM KHẢO PHỤ (FK logic tới PRODUCTDATA.Tbl_PhongBan, không FK cứng) —
    // KHÔNG dùng để nhóm chính (quá thô, 1 phòng ban gồm cả CDQ/Ướt/TN), chỉ hỗ trợ lọc/tham
    // chiếu khi cần đối chiếu với IdPhongBanBn của nguồn BBGN.
    public class LC_XuatCapDanhMucCum
    {
        public int ID { get; set; }

        public int Scope { get; set; } // 1=TP1, 2=TP2

        public int ThuTu { get; set; }

        public string TenCan { get; set; } = string.Empty; // vd 'Cân BTC8,17 – NM.LC'

        public string TenCum { get; set; } = string.Empty; // vd 'LG1-CDQ', 'Cốc xả bãi' (= "Nơi nhận" cũ)

        public string LoaiCoc { get; set; } = string.Empty;

        public string NhomTongHop { get; set; } = string.Empty;

        public int? ID_PhongBan { get; set; } // tham khảo phụ — xem comment trên

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
