namespace dataproduct.api.DTOs.NM.LC
{
    // 1 dòng = 1 lớp liệu của 1 silo (gộp LC_CapLieu + LC_CapLieuTonSilo, khớp layout FE
    // TabCapLieuSilo.tsx — xem comment đầu TabCapLieuSilo.tsx). Từ khi có "Tách liệu"
    // (2026-10-21), 1 SiloSo có thể có NHIỀU dòng (nhiều Stt) — 4 cột tồn-silo
    // (AmSiloCuoiKy/KLAmTonCuoi/DoAmSilo/KLTonCuoiQuyKho) chỉ có giá trị thật ở dòng Stt=1
    // của mỗi silo (đo vật lý 1 lần/silo, không nhân theo lớp), các dòng Stt>1 luôn NULL.
    public class LcCapLieuSiloRowDto
    {
        public int SiloSo { get; set; }
        // Thứ tự lớp liệu trong CÙNG silo — 1 = lớp hiện tại, 2.. = lớp tách thêm ("Tách liệu").
        public int Stt { get; set; } = 1;
        public int? IdVatTu { get; set; }
        public string? VatLieuTen { get; set; }
        public int? IdMaLo { get; set; }
        public string? SoHieuLo { get; set; }
        public decimal? KhoiLuongKeHoach { get; set; }
        public decimal? KhoiLuongAmThucTe { get; set; }
        public decimal? DoAm { get; set; }
        // CALCULATED (F01) — backend tính khi lưu, giá trị client gửi lên bị bỏ qua.
        public decimal? KhoiLuongKhoThucTe { get; set; }
        // CALCULATED (F03 = TonDauKyQuyKho + KhoiLuongKhoThucTe) — backend tính khi lưu.
        public decimal? LuyKeQuyKho { get; set; }
        // PREVIOUS_SHIFT — chỉ do initializer/SP set, không nhận từ client.
        public decimal? TonDauKyQuyKho { get; set; }
        public decimal? AmSiloCuoiKy { get; set; }
        // CALCULATED (F05a, CHỈ TP1 — TP2 dùng F05b, CHƯA triển khai, luôn NULL) — backend
        // tính khi lưu, giá trị client gửi lên bị bỏ qua.
        public decimal? KLAmTonCuoi { get; set; }
        public decimal? DoAmSilo { get; set; }
        // CALCULATED (F01 = KLAmTonCuoi × (100−DoAmSilo)/100) — backend tính khi lưu.
        public decimal? KLTonCuoiQuyKho { get; set; }
        public string? GhiChu { get; set; }
    }

    public class LcCoHatNghien1RowDto
    {
        // 'CO_HAT_1MM' | 'CO_HAT_3MM' — khớp NhomPhanTich trên LC_KetQuaPhanTich.
        public string NhomPhanTich { get; set; } = null!;
        public decimal? Lan1 { get; set; }
        public decimal? Lan2 { get; set; }
        public decimal? Lan3 { get; set; }
        public string? GhiChu { get; set; }
    }

    public class LcCapLieuSiloTabDto
    {
        public List<LcCapLieuSiloRowDto> CapLieu { get; set; } = new();
        public List<LcCoHatNghien1RowDto> CoHatNghien1 { get; set; } = new();
    }
}
