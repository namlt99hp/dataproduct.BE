namespace dataproduct.api.DTOs.NMLC
{
    // Danh mục "cụm sản xuất" — xem LC_TP_CumSanXuat.
    public class LcCumSanXuatDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public string Ma { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
    }

    // Danh mục "vị trí dập" — xem LC_TP_ViTriDapCoc.
    public class LcViTriDapCocDto
    {
        public int Id { get; set; }
        public int Scope { get; set; }
        public int IdCumSanXuat { get; set; }
        public string CumSanXuatMa { get; set; } = string.Empty;
        public string CumSanXuatTen { get; set; } = string.Empty;
        public string Loai { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public byte? ThuTu { get; set; }
    }

    // 1 dòng = 1 vị trí dập, khớp FE TabSanLuongCoc.tsx. LuyKe* đều CUMULATIVE ở backend.
    public class DapCocRowDto
    {
        public int IdViTri { get; set; }
        public int IdCumSanXuat { get; set; }
        public string CumSanXuatTen { get; set; } = string.Empty;
        public string Loai { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public decimal? BanhCocTiepNhan { get; set; }
        public decimal? LuyKeBanhCocTiepNhan { get; set; }
        public decimal? KhoiLuongCocTrongCa { get; set; }
        public decimal? LuyKeKhoiLuongCocTrongCa { get; set; }
    }

    public class DapCocSaveRowDto
    {
        public int IdViTri { get; set; }
        public decimal? BanhCocTiepNhan { get; set; }
        public decimal? KhoiLuongCocTrongCa { get; set; }
    }

    // "Cốc chuyển qua bên X.NL,TP2" — CHỈ TP1, 2 dòng cố định (NhaSang='NS2'|'NS3').
    public class CocChuyenNhaSangRowDto
    {
        public string NhaSang { get; set; } = string.Empty;
        public decimal? TonCuoi { get; set; }
        public decimal? TonDau { get; set; }
        public decimal? LuyKe { get; set; }
    }

    public class CocChuyenNhaSangSaveRowDto
    {
        public string NhaSang { get; set; } = string.Empty;
        public decimal? TonCuoi { get; set; }
        public decimal? TonDau { get; set; }
    }

    // 1 dòng = 1 loại cốc ('10-80mm' | '<10mm' | 'Bụi cốc') — CALCULATED, không nhập tay.
    // TheoCum: Trong ca/Lũy kế phân bổ theo từng cụm sản xuất (cùng thứ tự LcCumSanXuatDto).
    public class SanLuongCocCumDto
    {
        public int IdCumSanXuat { get; set; }
        public string CumSanXuatTen { get; set; } = string.Empty;
        public decimal? TrongCa { get; set; }
        public decimal? LuyKe { get; set; }
    }

    public class SanLuongCocRowDto
    {
        public string LoaiCoc { get; set; } = string.Empty;
        public decimal? TongTrongCa { get; set; }
        public decimal? LuyKeTong { get; set; }
        public List<SanLuongCocCumDto> TheoCum { get; set; } = new();
    }

    public class SanLuongCocTabDto
    {
        public List<DapCocRowDto> DapCoc { get; set; } = new();
        public List<CocChuyenNhaSangRowDto> CocChuyenNhaSang { get; set; } = new(); // rỗng nếu Scope=2
        public List<SanLuongCocRowDto> SanLuong { get; set; } = new();
    }

    public class SanLuongCocTabSaveDto
    {
        public List<DapCocSaveRowDto> DapCoc { get; set; } = new();
        public List<CocChuyenNhaSangSaveRowDto> CocChuyenNhaSang { get; set; } = new();
    }
}
