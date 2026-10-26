using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dataproduct.api.Models.MasterData
{
    // Xác nhận qua INFORMATION_SCHEMA.COLUMNS thực tế trên PRODUCTDATA (192.168.240.3,1434).
    // Ca/Kip là nchar(1) chứa giá trị dạng text ("1"/"2", "A"/"B"/"C"), không phải số nguyên.
    // TẤT CẢ cột trừ ID_BBGN đều NULLABLE trên DB thật — không khai báo non-nullable (EF sẽ ném
    // SqlNullValueException khi đọc NULL). Không map các cột thuần workflow/tài liệu không cần cho
    // nguồn dữ liệu Cấp cốc: ID_QuyTrinh, ID_BBGN_Cu, FileBBGN, FileDinhKem, IsLock, NoiDungTrichYeu,
    // ThoiGianXuLyBN (xem .claude/LuyenCoc/PhanTich_TabCapCoc.md §8).
    [Table("Tbl_BienBanGiaoNhan")]
    public class Tbl_BienBanGiaoNhan
    {
        [Key]
        public int ID_BBGN { get; set; }
        public DateTime? ThoiGianXuLyBG { get; set; }
        public string? Ca { get; set; }
        public string? Kip { get; set; }
        public int? ID_Xuong_BG { get; set; }
        public int? ID_PhongBan_BG { get; set; }
        public int? ID_ViTri_BG { get; set; }
        public int? ID_TrangThai_BG { get; set; }
        public int? ID_Xuong_BN { get; set; }
        public int? ID_PhongBan_BN { get; set; }
        public int? ID_ViTri_BN { get; set; }
        public int? ID_TrangThai_BN { get; set; }
        public int? ID_TrangThai_BBGN { get; set; }
        public string? SoPhieu { get; set; }
        public int? ID_Kip { get; set; }
        public DateTime? NgayTao { get; set; }
        public bool? IsDelete { get; set; }
    }
}
