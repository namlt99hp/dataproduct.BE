using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models
{
    // Dùng chung cho "Cỡ hạt sau nghiền 1" (tab Cấp liệu) và các chỉ tiêu phân tích khác sau
    // này — xem SECTION 6 lc_bcxs_xnl_tp.sql. NhomPhanTich: 'CO_HAT_1MM' | 'CO_HAT_3MM' | ...
    public class LC_KetQuaPhanTich
    {
        [Key]
        public int ID { get; set; }
        public Guid IDPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public int Scope { get; set; }
        public string NhomPhanTich { get; set; } = null!;
        public byte LanPhanTich { get; set; }
        public decimal? GiaTri { get; set; }
        public string? GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
    }
}
