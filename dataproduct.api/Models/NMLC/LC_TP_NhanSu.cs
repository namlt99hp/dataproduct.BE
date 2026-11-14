namespace dataproduct.api.Models.NMLC
{
    public class LC_TP_NhanSu
    {
        public int Id { get; set; }

        public Guid PhieuId { get; set; }

        public int Scope { get; set; }

        public int Stt { get; set; }

        // TP2: tên tổ/nhóm cố định (Trưởng/Phó kíp, KTV Vận hành, Tổ Nguyên liệu, Tổ Thành phẩm).
        // TP1: NULL — chỉ 1 dòng duy nhất, nội dung nằm ở NoiDungTuDo.
        public string? TenToNhom { get; set; }

        public string? SoLuongDinhBien { get; set; }

        public string? SoLuongHienCoDinhBien { get; set; }

        public string? SoLuongTrongCa { get; set; }

        public string? DienGiai { get; set; }

        // TP1 only: nguyên đoạn văn tự do "A. Tình hình nhân sự trong kíp".
        public string? NoiDungTuDo { get; set; }
    }
}
