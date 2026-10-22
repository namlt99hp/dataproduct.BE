namespace dataproduct.api.DTOs.NMLC
{
    public class ChiTieuCocDto
    {
        public int Id { get; set; }
        public string Ten { get; set; } = string.Empty;
        public string? DonVi { get; set; }
        public bool CoPhanLoai { get; set; }
        public byte? ThuTu { get; set; }
    }

    public class TcnbCocDto
    {
        public int ChiTieuId { get; set; }
        public byte Loai { get; set; }
        public decimal? GiaTriTCNB { get; set; }
    }

    public class ChatLuongCocDanhMucDto
    {
        public List<ChiTieuCocDto> ChiTieu { get; set; } = new();
        public List<TcnbCocDto> Tcnb { get; set; } = new();
    }

    public class ChatLuongCocDto
    {
        public int Id { get; set; }
        public Guid PhieuId { get; set; }
        public int Scope { get; set; }
        public int ChiTieuId { get; set; }
        public byte Loai { get; set; }
        public byte Lan { get; set; }
        public decimal? GiaTri { get; set; }
        public bool? DatYeuCau { get; set; }
    }

    public class ChatLuongCocSaveDto
    {
        public int ChiTieuId { get; set; }
        public byte Lan { get; set; }
        public decimal? GiaTri { get; set; }
        // "Đánh giá" của chỉ tiêu do người dùng chọn (Đạt = true, Không đạt = false, chưa đánh giá = null)
        public bool? DatYeuCau { get; set; }
    }
}
