namespace dataproduct.api.DTOs.NM.LC
{
    // 1 dòng = 1 lớp liệu (SiloSo+Stt) trong 1 thẻ phối trộn — xem LC_PhoiTronChiTiet. Khớp
    // 1:1 theo (SiloSo, Stt) với danh sách lớp liệu ở tab Cấp liệu (1 silo có thể nhiều lớp
    // sau khi "Tách liệu").
    public class LcPhoiTronChiTietRowDto
    {
        public int SiloSo { get; set; }
        public int Stt { get; set; } = 1;
        // SNAPSHOT — FE hiển thị readonly, không nhận giá trị nhập tay (BE tự lấy theo
        // LC_CapLieu của CÙNG phiếu lúc lưu, xem LC_PhoiTronRepository.SaveTabAsync).
        public string? VatLieuTen { get; set; }
        public decimal? TyLeKeHoach { get; set; }
        public decimal? TyLeThucTe { get; set; }
        // CALCULATED (F02 = TyLeThucTe * KLQuyKhoCan / 100) — backend tính khi lưu.
        public decimal? KLQuyKho { get; set; }
        // CALCULATED (F03, reset tại Ca 1 ngày 1 hàng tháng) — backend tính khi lưu.
        public decimal? LuyKeQuyKho { get; set; }
        public string? GhiChu { get; set; }
    }

    // 1 "thẻ" công thức phối trộn (header) — 1 cụm tháp có thể có nhiều thẻ, phân biệt bởi
    // (CumThapMa, LoaiPhoiTron, ThuTu). Id = 0/null -> thẻ mới (FE vừa "+ Thêm công thức phối").
    public class LcPhoiTronCardDto
    {
        public int Id { get; set; }
        public string CumThapMa { get; set; } = string.Empty;
        // 'SAN_XUAT' | 'THU_NGHIEM'
        public string LoaiPhoiTron { get; set; } = "SAN_XUAT";
        public byte ThuTu { get; set; } = 1;
        public string? SoPhieuYeuCau { get; set; }
        public string? MaMauThuNghiem { get; set; }
        public string? GhiChuPhoi { get; set; }
        public decimal? KLCanAm { get; set; }
        public decimal? DoAm { get; set; }
        // CALCULATED (F01 áp cho KLCanAm/DoAm) — backend tính khi lưu.
        public decimal? KLQuyKhoCan { get; set; }
        public List<LcPhoiTronChiTietRowDto> ChiTiet { get; set; } = new();
    }
}
