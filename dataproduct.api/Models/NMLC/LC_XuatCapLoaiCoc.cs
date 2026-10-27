namespace dataproduct.api.Models.NMLC
{
    // Danh mục "Loại cốc" vùng Cấp cốc (merge dọc cột A trong Excel: 'Cốc 10-80mm', 'Cốc <10mm',
    // 'Bụi CDQ'...) — dùng CHUNG cho cả TP1/TP2, xem PhanTich_TabCapCoc.md §10/§11.
    public class LC_XuatCapLoaiCoc
    {
        public int ID { get; set; }

        public string Ten { get; set; } = string.Empty; // '10-80mm' | '<10mm' | 'Bụi CDQ' | 'Bụi nhà sàng' | 'Bụi silo cốc'

        public int ThuTu { get; set; }

        public bool IsDelete { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
