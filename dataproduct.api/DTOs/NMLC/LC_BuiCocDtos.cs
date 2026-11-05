using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    // ─── Danh mục Nơi nhận ───

    public class BuiCocNoiNhanDto
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
    }

    public class BuiCocNoiNhanSaveDto
    {
        [Required]
        [MaxLength(50)]
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
    }

    // ─── Tab Bụi cốc (Sản lượng + Tồn + Xuất) — by-phieu, TP1 only ───

    // 1 dòng = 1 Nơi nhận, khớp FE "III. Bụi cốc xuất". KhoiLuong là field nhập tay duy
    // nhất; LuyKeQuyKho là CALCULATED ở backend.
    public class BuiCocXuatDto
    {
        public int NoiNhanId { get; set; }
        public string? TenNoiNhan { get; set; }
        public decimal? KhoiLuong { get; set; }
        public decimal? LuyKeQuyKho { get; set; }
    }

    public class BuiCocXuatSaveDto
    {
        [Required]
        public int NoiNhanId { get; set; }
        public decimal? KhoiLuong { get; set; }
    }

    // Gộp "I. Sản lượng" + "II. Bụi cốc tồn" + "III. Bụi cốc xuất" của 1 phiếu.
    public class BuiCocTabDto
    {
        public decimal? SanLuongTrongCa { get; set; }
        public decimal? LuyKeSanLuong { get; set; }
        public decimal? TonDauKyQuyKho { get; set; }
        public decimal? TonCuoiKyQuyKho { get; set; }
        public List<BuiCocXuatDto> Xuat { get; set; } = new();
    }

    public class BuiCocTabSaveDto
    {
        public decimal? SanLuongTrongCa { get; set; }
        public List<BuiCocXuatSaveDto> Xuat { get; set; } = new();
    }
}
