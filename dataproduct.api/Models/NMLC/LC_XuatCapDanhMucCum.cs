namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Nơi nhận" (CHA) vùng Cấp cốc — trang quản lý /lc-xuatcapdanhmuc. Xem
    // .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11 (sửa lại 2026-10-26 — tên cũ "Cụm nhận" bị
    // nhầm, đây thực chất là "Nơi nhận"; "Cụm" THẬT nằm ở LC_XuatCapCum).
    //
    // 1 Nơi nhận (vd 'LG1-CDQ', 'LG1-Ướt', 'Cốc xả bãi') thuộc đúng 1 Cụm (`ID_Cum`) — đây là đơn
    // vị dùng để GÁN (nguồn BBGN nhiều khi chỉ biết tới mức thô, vd chỉ biết "LG1", người dùng
    // phải tự quyết định rơi vào Nơi nhận CDQ hay Ướt) và cũng là đơn vị tính Trong ca/Lũy kế (qua
    // NhomTongHop — nhiều Nơi nhận có thể cùng NhomTongHop nếu Excel merge chung).
    //
    // ID_PhongBan: field THAM KHẢO PHỤ (FK logic tới PRODUCTDATA.Tbl_PhongBan, không FK cứng) —
    // KHÔNG dùng để nhóm chính (quá thô, 1 phòng ban gồm cả CDQ/Ướt/TN), chỉ hỗ trợ lọc/tham
    // chiếu khi cần đối chiếu với IdPhongBanBn của nguồn BBGN.
    public class LC_XuatCapDanhMucCum
    {
        public int ID { get; set; }

        public int Scope { get; set; } // 1=TP1, 2=TP2

        public int ID_Cum { get; set; } // FK logic LC_XuatCapCum.ID — Cụm THẬT (không FK cứng)

        public int ThuTu { get; set; }

        public string TenCan { get; set; } = string.Empty; // vd 'Cân BTC8,17 – NM.LC' — tham khảo hiển thị, KHÔNG dùng để nhóm

        public string NoiNhan { get; set; } = string.Empty; // vd 'LG1-CDQ', 'Cốc xả bãi' (đổi tên từ TenCum)

        public int ID_LoaiCoc { get; set; } // FK logic LC_XuatCapLoaiCoc.ID (đổi từ text tự do)

        public string NhomTongHop { get; set; } = string.Empty;

        public int? ID_PhongBan { get; set; } // tham khảo phụ — xem comment trên

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
