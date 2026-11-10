using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models.NMLC
{
    // Junction ghi lại ĐÚNG những dòng LC_CanTuDongRawLog đã dùng cho 1 lần "Gán" vào 1 dòng
    // LC_CapLieu — xem SECTION 29 lc_bcxs_xnl_tp.sql. Tạo khi Gán, XOÁ HẾT khi Bỏ gán (KHÔNG
    // phải audit trail lịch sử — chỉ phản ánh liên kết đang có hiệu lực).
    public class LC_CanTuDongGanChiTiet
    {
        [Key]
        public int ID { get; set; }
        public int ID_CapLieu { get; set; }
        public int ID_RawLog { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
