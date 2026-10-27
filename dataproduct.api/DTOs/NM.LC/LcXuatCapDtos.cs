namespace dataproduct.api.DTOs.NM.LC
{
    // ─── LC_XuatCap — tab "Cấp cốc" (LcTabXuatCap) ──────────────────────────────────────────
    // 1 slot = 1 tổ hợp Nơi nhận×Chi tiết (layout cố định theo Scope, quản lý qua danh mục
    // LC_XuatCapDanhMucCum/ChiTiet), kèm danh sách "nguồn" (dòng BBGN đã được người dùng gán tay
    // vào slot này). KhoiLuongAm của slot = SUM(Nguon[].KhoiLuongGan) nếu có gán, hoặc giá trị
    // nhập tay thẳng nếu Nguon rỗng.
    //
    // SỬA 2026-10-26: KHÔNG còn Initializer sinh sẵn — `Id` CÓ THỂ = 0 (slot chưa tồn tại trong
    // LC_XuatCap, chưa ai nhập/gán gì cho phiếu này) — khi đó mọi field giá trị đều null/mặc
    // định, chỉ các field cấu trúc (từ danh mục) có giá trị. `IdDanhMucChiTiet` LUÔN có giá trị
    // (khoá slot ổn định) — dùng field này để gửi lên khi Lưu/Gán, KHÔNG dùng `Id`.
    public class LcXuatCapNguonDto
    {
        public int Id { get; set; }
        public int IdBbgn { get; set; }
        public int IdCtBbgn { get; set; }
        public decimal? KhoiLuongGan { get; set; }
        public decimal? DoAmGan { get; set; }
    }

    public class LcXuatCapSlotDto
    {
        public int Id { get; set; } // 0 nếu slot CHƯA tồn tại trong LC_XuatCap (chưa nhập/gán gì)
        public int IdDanhMucChiTiet { get; set; } // khoá slot ổn định — dùng để Lưu/Gán, không đổi theo phiếu
        public int ThuTu { get; set; }
        public string TenCan { get; set; } = string.Empty;
        public string NoiNhan { get; set; } = string.Empty;
        public string? ChiTietLo { get; set; }
        public string LoaiCoc { get; set; } = string.Empty;
        public string NhomTongHop { get; set; } = string.Empty; // khoá nhóm Trong ca/Lũy kế — các slot cùng nhóm hiển thị TrongCa/LuyKe giống nhau (giống merge cell Excel)
        public int? IdPhongBan { get; set; } // tham khảo phụ, KHÔNG dùng để nhóm chính
        public string? TenPhongBan { get; set; }
        public int? IdDanhMucCum { get; set; } // khoá Nơi nhận (LC_XuatCapDanhMucCum.ID) — FE dùng để nhóm theo Nơi nhận
        public int? IdCum { get; set; } // khoá Cụm THẬT (LC_XuatCapCum.ID) — FE dùng để render Card ngoài cùng
        public string? MaCum { get; set; }
        public string? TenCum { get; set; } // vd 'Cụm 1,2,3,4'
        public int? IdLoaiNhan { get; set; } // chỉ có ở Chi tiết mang biến thể CDQ/ƯỚT/TN/Xả bãi/Cốc ướt/Xả ôtô riêng
        public string? TenLoaiNhan { get; set; }

        public decimal? KhoiLuongAm { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? TyKhoi { get; set; }
        public decimal? KLQuyKho { get; set; } // F16, BE tính
        public decimal? TrongCa { get; set; }  // F17, BE tính (SUM theo NhomTongHop)
        public decimal? LuyKe { get; set; }    // BE tính (TrongCa + lũy kế phiếu liền trước)
        public string? GhiChu { get; set; }

        public List<LcXuatCapNguonDto> Nguon { get; set; } = new();
    }

    // Lưu tay (DoAm/TyKhoi/GhiChu luôn nhận; KhoiLuongAm CHỈ áp dụng khi slot không có Nguon nào
    // đã gán — nếu có Nguon, BE tự tính lại KhoiLuongAm = SUM, bỏ qua giá trị FE gửi). Slot CHƯA
    // tồn tại (lần đầu nhập) sẽ được TẠO MỚI (upsert) theo IdDanhMucChiTiet.
    public class LcXuatCapSaveSlotDto
    {
        public int IdDanhMucChiTiet { get; set; }
        public decimal? KhoiLuongAm { get; set; }
        public decimal? DoAm { get; set; }
        public decimal? TyKhoi { get; set; }
        public string? GhiChu { get; set; }
    }

    // Gán 1 dòng BBGN vào 1 slot — slot xác định bằng (IdPhieu, IdDanhMucChiTiet), KHÔNG còn
    // IdXuatCap (vì slot có thể CHƯA tồn tại — BE tự tạo (upsert) trước khi ghi LC_XuatCapNguon).
    public class LcXuatCapGanRequest
    {
        public Guid IdPhieu { get; set; }
        public int IdDanhMucChiTiet { get; set; }
        public int IdBbgn { get; set; }
        public int IdCtBbgn { get; set; }
        public decimal? KhoiLuongGan { get; set; }
        public decimal? DoAmGan { get; set; }
    }
}
