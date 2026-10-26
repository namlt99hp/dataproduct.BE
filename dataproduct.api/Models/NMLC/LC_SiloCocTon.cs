namespace dataproduct.api.Models.NMLC
{
    // Vùng "Cốc tồn silo" (tab `tonCoc`, TabCocTonSilo.tsx) — 1 dòng = 1 CỘT SILO trong 1 CỤM
    // của 1 phiếu (khớp layout TP1.xlsx dòng 235-267: 1 cụm = N cột silo × {Chiều cao, Khối
    // lượng}, 4 "dòng hạng mục" cố định chỉ là cách FE hiển thị, không phải 4 dòng DB).
    // Danh sách RIÊNG của mỗi phiếu — copy forward CẤU TRÚC (không copy giá trị) từ phiếu liền
    // trước lúc Initialize, xem LC_SiloCocTonRepository. Tham chiếu LC_SiloTon để lấy CongThuc.
    // TenSilo/LoaiLieu/TenNhapLieu là SNAPSHOT được LC_SiloCocTonRepository.SaveTabAsync ghi
    // lại từ danh mục MỖI LẦN Lưu (không phải đông cứng từ lúc thêm) — ngừng thay đổi khi
    // phiếu bị chốt/khoá (SaveTabAsync tự chặn), nên vẫn đúng tinh thần "không đổi theo quá
    // khứ" dù danh mục có sửa tên sau khi phiếu đã chốt.
    public class LC_SiloCocTon
    {
        public int ID { get; set; }

        public Guid IDPhieu { get; set; }

        public DateOnly? NgaySX { get; set; }

        public int? Ca { get; set; }

        public int Scope { get; set; }

        public int ThuTuCum { get; set; }

        public string TenCum { get; set; } = string.Empty;

        public int ThuTuSilo { get; set; }

        public int ID_SiloTon { get; set; }

        public string TenSilo { get; set; } = string.Empty;

        public string LoaiLieu { get; set; } = string.Empty;

        public string TenNhapLieu { get; set; } = string.Empty;

        public decimal? GiaTriTonCuoiCa { get; set; }

        public decimal? KhoiLuongTonCuoiCa { get; set; }

        public decimal? GiaTriTonDauCa { get; set; }

        public decimal? KhoiLuongTonDauCa { get; set; }

        public decimal? ChenhLech1080 { get; set; }

        public decimal? ChenhLechDuoi10 { get; set; }

        public DateTime NgayTao { get; set; }
    }
}
