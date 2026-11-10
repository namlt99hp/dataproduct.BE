using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.Models.NMLC
{
    // Dữ liệu thô đồng bộ từ hệ cân tự động của nhà máy (2 nguồn riêng TP1/TP2, nguồn KHÔNG có
    // PK/ID ổn định) — xem SECTION 27 .claude/migrations/LuyenCoc_Migration/lc_bcxs_xnl_tp.sql.
    // 1 dòng = 1 lần cân rời rạc (nhiều dòng/ca). SP đồng bộ (viết riêng ngoài phạm vi BE) sync
    // theo kiểu THAY TOÀN BỘ 1 slot (Scope,NgaySX,Ca): xoá hết dòng cũ của slot rồi insert lại.
    public class LC_CanTuDongRawLog
    {
        [Key]
        public int ID { get; set; }
        public int Scope { get; set; }
        public DateOnly NgaySX { get; set; }
        public int Ca { get; set; }
        public DateTime NgayGio { get; set; }
        public string? TenCan { get; set; }
        public string? LoaiLieuTen { get; set; }
        public decimal KhoiLuong { get; set; }
        // Đã dùng trong 1 lần "Gán" (LC_CanTuDongRepository.GanAsync) — khoá vĩnh viễn, không
        // mở lại dù kết quả gán sau đó bị sửa tay (xem [[lc-bcsx-xnl-cantudong-design]]).
        public bool IsDaGan { get; set; }
        public DateTime NgaySync { get; set; }
    }
}
