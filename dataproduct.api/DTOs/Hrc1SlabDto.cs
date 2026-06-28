namespace dataproduct.api.DTOs
{
    // ── Sync ──────────────────────────────────────────────────────────────────

    public class Hrc1SlabSyncRequest
    {
        public DateOnly NgaySX { get; set; }
        /// <summary>1 = Ca ngày (8h–19:59), 2 = Ca đêm (20h–7:59 hôm sau)</summary>
        public int CaSX { get; set; }
    }

    public class Hrc1SlabSyncResult
    {
        public bool Success { get; set; }
        public int TotalFromApi { get; set; }
        public int RowsUpserted { get; set; }
        public int MacThepFilled { get; set; }
        public string Message { get; set; } = "";
    }

    // ── TSC API response models ────────────────────────────────────────────────

    public class TscApiResponse
    {
        public bool Success { get; set; }
        public int Count { get; set; }
        public List<TscSlabItem>? Data { get; set; }
    }

    public class TscSlabItem
    {
        public string? SLAB_ID { get; set; }
        public string? PIECE_ID { get; set; }
        public string? CA { get; set; }
        public string? HEAT_ID { get; set; }
        public DateTime? CUT_DATE { get; set; }
        public decimal? THICKNESS { get; set; }
        public decimal? LENGTH { get; set; }     // API có thể trả decimal (vd: 22199.0)
        public decimal? WEIGHT { get; set; }     // API có thể trả decimal
        public decimal? WIDTH_HEAD { get; set; } // API có thể trả decimal
        public string? TSC_NO { get; set; }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public class Hrc1SlabSearchRequest
    {
        public DateOnly? TuNgay { get; set; }
        public DateOnly? DenNgay { get; set; }
        public string? CaSX { get; set; }
        public string? KipSX { get; set; }
        public string? MayDuc { get; set; }
        public string? MaMe { get; set; }
        public string? IDSlab { get; set; }
        public string? MacThep { get; set; }
        /// <summary>null = tất cả, true = đã chốt (CutDate IS NOT NULL), false = chưa</summary>
        public bool? IsChot { get; set; }
        public int? TrangThaiDuc { get; set; }
        public int? TrangThaiKho { get; set; }
        public int? TrangThaiPKH { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    // ── Response items ────────────────────────────────────────────────────────

    public class Hrc1SlabItem
    {
        public int Id { get; set; }
        public string IDSlab { get; set; } = "";
        public string? IDPiece { get; set; }
        public string? MaMe { get; set; }
        public string? MacThep { get; set; }
        public DateOnly? NgaySX { get; set; }
        public string? CaSX { get; set; }
        public string? KipSX { get; set; }
        public string? MayDuc { get; set; }
        public DateTime? CutDate { get; set; }
        public decimal? ChieuDay { get; set; }
        public decimal? ChieuRong { get; set; }
        public decimal? ChieuDai { get; set; }
        public decimal? KhoiLuong { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
        public string? GhiChu { get; set; }
        public string? MaVatTu { get; set; }

        // Workflow (LEFT JOIN HRC1_Slab_TrangThai)
        public bool IsChuyenCa { get; set; }
        public Guid? IdPhieuGoc { get; set; }
        public int TrangThaiDuc { get; set; }
        public int TrangThaiKho { get; set; }
        public int TrangThaiPKH { get; set; }
        public Guid? IdPhieuBBSL { get; set; }
        public string? SoPhieuBBSL { get; set; }

        // Phiếu BBSL info (join BM_Phieu khi slab được chuyển ca)
        public DateOnly? NgayXuLy { get; set; }
        public int? CaBBSL { get; set; }
        public string? KipBBSL { get; set; }
    }

    // ── Chuyển phôi sang ca kề ────────────────────────────────────────────────

    public class Hrc1ChuyenPhoiRequest
    {
        public List<int> IdSlabs { get; set; } = new();
        public Guid IdPhieuNguon { get; set; }
        /// <summary>"truoc" | "sau"</summary>
        public string Huong { get; set; } = "";
        public int NguoiChuyen { get; set; }
    }

    // ── Ghi chú per-slab ─────────────────────────────────────────────────────

    public class Hrc1SlabUpdateRequest
    {
        public string? GhiChu { get; set; }
        public string? MaVatTu { get; set; }
    }

    public class Hrc1BulkUpdateMaVatTuRequest
    {
        public List<int> Ids { get; set; } = [];
        public string? MaVatTu { get; set; }
    }

    // ── Tổng hợp ghi chú ─────────────────────────────────────────────────────

    public class Hrc1TongHopGhiChuItem
    {
        public string? MacThep { get; set; }
        public string? KichThuoc { get; set; }
        public string? GhiChu { get; set; }
    }

    public class Hrc1SaveTongHopGhiChuRequest
    {
        public Guid IdPhieuBBSL { get; set; }
        public string? MacThep { get; set; }
        public string? KichThuoc { get; set; }
        public string? GhiChu { get; set; }
    }

    // ── MaVatTu ──────────────────────────────────────────────────────────────

    public class Hrc1MaVatTuItem
    {
        public int Id { get; set; }
        public string MaVatTu { get; set; } = "";
        public string TenVatTu { get; set; } = "";
        public bool? IsLock { get; set; }
    }

    public class Hrc1MaVatTuUpsertDto
    {
        public string MaVatTu { get; set; } = "";
        public string? TenVatTu { get; set; }
        public bool? IsLock { get; set; }
    }

    public class Hrc1MaVatTuSearchRequest
    {
        public string? SearchKey { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class Hrc1MaVatTuBulkCreateRequest
    {
        public List<Hrc1MaVatTuUpsertDto> Items { get; set; } = new();
    }

    public class Hrc1MaVatTuBulkCreateResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<string> SkippedItems { get; set; } = new();
    }

    public class Hrc1SlabTongHopItem
    {
        public string? MaMe { get; set; }
        public string? MacThep { get; set; }
        public decimal? ChieuDay { get; set; }
        public decimal? ChieuRong { get; set; }
        public decimal? ChieuDai { get; set; }
        public string? MayDuc { get; set; }
        public int SoLuong { get; set; }
        public decimal? TongKhoiLuong { get; set; }
    }

    public class Hrc1PhieuBBSLItem
    {
        public Guid IdPhieu { get; set; }
        public string? SoPhieu { get; set; }
        public DateOnly? NgaySX { get; set; }
        public int? Ca { get; set; }
        public string? Kip { get; set; }
        public int? TinhTrang { get; set; }
        public int SoSlabDaChot { get; set; }
        public int SoSlabDuc { get; set; }
        public int SoSlabKho { get; set; }
        public int SoSlabPKH { get; set; }
    }
}
