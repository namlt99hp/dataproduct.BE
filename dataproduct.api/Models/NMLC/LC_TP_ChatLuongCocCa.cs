namespace dataproduct.api.Models.NMLC
{
    // Kết quả đo chất lượng cốc trong ca: 1 dòng = 1 giá trị của (chỉ tiêu, lần đo).
    // Không có đánh giá tự động: DatYeuCau là "Đánh giá" người dùng chọn cho chỉ tiêu (lưu lặp trên
    // các dòng của chỉ tiêu đó; chỉ tiêu có đánh giá mà chưa có giá trị thì lưu 1 dòng Lan = 1, GiaTri null).
    // Loai luôn = 0 (chưa phân loại).
    public class LC_TP_ChatLuongCocCa
    {
        public int Id { get; set; }

        public Guid PhieuId { get; set; }

        public int Scope { get; set; }

        public int ChiTieuId { get; set; }

        public byte Loai { get; set; }

        // Lần đo trong ca: 1..6
        public byte Lan { get; set; }

        public decimal? GiaTri { get; set; }

        public bool? DatYeuCau { get; set; }
    }
}
