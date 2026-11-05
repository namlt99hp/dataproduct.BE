using System.ComponentModel.DataAnnotations;

namespace dataproduct.api.DTOs.NMLC
{
    // 1 dòng = 1 phễu, khớp FE TabThapPheu.tsx PheuRow. KLTonDauQuyKho/KLAmTonCuoi/
    // KLTonCuoiQuyKho đều CALCULATED ở backend (xem LC_TP_TonPheu) — FE chỉ hiển thị.
    public class TonPheuDto
    {
        public int PheuId { get; set; }
        // FK logic LC_CumThap.ID — FE dùng để khớp vào đúng nhóm tháp (TabThapPheu.tsx
        // cumThapMa), bắt buộc phải có để tránh FE tự sinh dòng skeleton trùng.
        public int ThapThanId { get; set; }
        public string? TenPheu { get; set; }
        public decimal? KLTonDauQuyKho { get; set; }
        public decimal? MucRadar { get; set; }
        public decimal? KLAmTonCuoi { get; set; }
        public decimal? KLTonCuoiQuyKho { get; set; }
        public string? GhiChu { get; set; }
    }

    // Chỉ các field người dùng thực sự nhập tay — các field CALCULATED bị bỏ qua nếu FE gửi.
    public class TonPheuSaveDto
    {
        [Required]
        public int PheuId { get; set; }
        public decimal? MucRadar { get; set; }
        public string? GhiChu { get; set; }
    }

    // 1 dòng = 1 cụm tháp, khớp FE TabThapPheu.tsx ThapRow. CapLenThapQuyKho/
    // LuyKeCapLenThap/LuyKeNapLo đều CALCULATED/CROSS_REGION/CUMULATIVE ở backend.
    public class CapThapDto
    {
        public int CumThapId { get; set; }
        public string CumThapMa { get; set; } = string.Empty;
        public decimal? DoAmThap { get; set; }
        public decimal? CapLenThapQuyKho { get; set; }
        public decimal? LuyKeCapLenThap { get; set; }
        public decimal? NapLoQuyKho { get; set; }
        public decimal? LuyKeNapLo { get; set; }
        public string? GhiChu { get; set; }
    }

    public class CapThapSaveDto
    {
        [Required]
        public int CumThapId { get; set; }
        public decimal? DoAmThap { get; set; }
        public decimal? NapLoQuyKho { get; set; }
        public string? GhiChu { get; set; }
    }

    public class ThapPheuTabDto
    {
        public List<TonPheuDto> Pheu { get; set; } = new();
        public List<CapThapDto> Thap { get; set; } = new();
    }

    public class ThapPheuTabSaveDto
    {
        public List<TonPheuSaveDto> Pheu { get; set; } = new();
        public List<CapThapSaveDto> Thap { get; set; } = new();
    }
}
