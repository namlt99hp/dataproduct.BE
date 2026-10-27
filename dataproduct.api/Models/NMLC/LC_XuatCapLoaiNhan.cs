namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Loại nhận" vùng Cấp cốc — hậu tố trong tên Chi tiết/Nơi nhận (CDQ/ƯỚT/TN/Xả bãi/
    // Cốc ướt/Xả ôtô...), đặt ở cấp CHI TIẾT (LC_XuatCapDanhMucChiTiet.ID_LoaiNhan, không phải
    // Nơi nhận cha) vì 1 Nơi nhận có thể gộp nhiều Loại nhận khác nhau ở từng con (vd TP2
    // "NMLG-CDQ" có con LC5/LC6 x CDQ/ƯỚT/TN/xả ôtô) — xem PhanTich_TabCapCoc.md §10/§11.
    public class LC_XuatCapLoaiNhan
    {
        public int ID { get; set; }

        public string Ten { get; set; } = string.Empty; // 'CDQ' | 'ƯỚT' | 'TN' | 'Xả bãi' | 'Cốc ướt' | 'Xả ôtô'

        public int ThuTu { get; set; }

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
