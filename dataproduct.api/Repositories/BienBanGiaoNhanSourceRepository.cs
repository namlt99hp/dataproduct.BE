using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.MasterData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    // Đọc BBGN (Tbl_BienBanGiaoNhan/Tbl_ChiTiet_BienBanGiaoNhan, PRODUCTDATA) qua stored procedure
    // sp_LG_PhanBo_TongNhanVe_BBGN — dùng chung cho QHLC (ID_VatTu=470) và Than cốc <10mm (ID_VatTu=484).
    // SP không lọc theo Xưởng — việc map ID_Xuong -> IDLoCao (LG_PB_Map_Xuong_LoCao, ở PRODUCT_FORM,
    // khác database) và lọc/gộp theo lò cao thực hiện ở tầng C# (PhanBoService).
    public class BienBanGiaoNhanSourceRepository : IBienBanGiaoNhanSourceRepository
    {
        private readonly ProductDataMasterDbContext _context;

        public BienBanGiaoNhanSourceRepository(ProductDataMasterDbContext context)
        {
            _context = context;
        }

        public async Task<List<(int IdXuong, byte? Ca, decimal KhoiLuong)>> GetTongNhanVeAsync(
            DateTime ngay, int idVatTu, IEnumerable<int> idXuongList)
        {
            var idXuongs = idXuongList.ToList();

            var raw = await _context.TongNhanVeBbgnResults
                .FromSqlRaw(
                    "EXEC dbo.sp_LG_PhanBo_TongNhanVe_BBGN @Ngay, @IdVatTu",
                    new SqlParameter("@Ngay", ngay.Date),
                    new SqlParameter("@IdVatTu", idVatTu))
                .AsNoTracking()
                .ToListAsync();

            return raw
                .Where(r => idXuongs.Contains(r.IdXuong))
                .Select(r => (r.IdXuong, ParseCa(r.Ca), (decimal)r.KhoiLuong))
                .ToList();
        }

        // Ca lưu dạng nchar(1) chứa text "1"/"2" trên Tbl_BienBanGiaoNhan
        private static byte? ParseCa(string? ca) => byte.TryParse(ca?.Trim(), out var v) ? v : null;

        public async Task<List<LcCapCocNguonBbgnDto>> GetCapCocNguonAsync(
            DateTime ngay, string ca, int idXuongBg)
        {
            // ID_TrangThai_BBGN: chỉ quan tâm 0 ("đang xử lý") hoặc 1 ("hoàn thành") — FE có nút
            // "Làm mới" để query lại dữ liệu mới nhất nên không cần theo dõi các trạng thái khác
            // (2 BN Hủy, 3 Đề nghị hiệu chỉnh, 4 Xóa phiếu, 5 PKH Hủy phiếu). Chốt 2026-10-23,
            // xem .claude/LuyenCoc/PhanTich_TabCapCoc.md §9.
            // KHÔNG lọc theo Kip (chốt 2026-10-23, lượt 4) — chỉ lọc theo Ngày (ThoiGianXuLyBG)
            // + Ca (lấy mặc định từ Ca của phiếu BCSX_XNL, cùng quy ước 1=ngày/2=đêm).
            var query =
                from bb in _context.Tbl_BienBanGiaoNhan
                join ct in _context.Tbl_ChiTiet_BienBanGiaoNhan on bb.ID_BBGN equals ct.ID_BBGN
                where bb.ThoiGianXuLyBG == ngay.Date
                      && bb.Ca == ca
                      && bb.ID_Xuong_BG == idXuongBg
                      && (bb.ID_TrangThai_BBGN == 0 || bb.ID_TrangThai_BBGN == 1)
                      && bb.IsDelete != true
                join xuong in _context.Tbl_Xuong on bb.ID_Xuong_BN equals xuong.ID_Xuong into xuongJoin
                from xuong in xuongJoin.DefaultIfEmpty()
                join phongBan in _context.Tbl_PhongBan on bb.ID_PhongBan_BN equals phongBan.ID_PhongBan into phongBanJoin
                from phongBan in phongBanJoin.DefaultIfEmpty()
                join vatTu in _context.Tbl_VatTu on ct.ID_VatTu equals vatTu.ID_VatTu into vatTuJoin
                from vatTu in vatTuJoin.DefaultIfEmpty()
                select new LcCapCocNguonBbgnDto
                {
                    IdBbgn = bb.ID_BBGN,
                    IdCtBbgn = ct.ID_CT_BBGN,
                    SoPhieu = bb.SoPhieu,
                    Ngay = bb.ThoiGianXuLyBG,
                    Ca = bb.Ca,
                    Kip = bb.Kip,
                    IdXuongBg = bb.ID_Xuong_BG,
                    IdXuongBn = bb.ID_Xuong_BN,
                    IdPhongBanBn = bb.ID_PhongBan_BN,
                    TenXuongBn = xuong.TenXuong,
                    TenPhongBanBn = phongBan.TenPhongBan,
                    IdVatTu = ct.ID_VatTu,
                    TenVatTu = vatTu.TenVatTu,
                    MaLo = ct.MaLo,
                    KhoiLuongBg = ct.KhoiLuong_BG,
                    DoAmW = ct.DoAm_W,
                    KlQuyKhoBg = ct.KL_QuyKho_BG,
                    IdTrangThaiBbgn = bb.ID_TrangThai_BBGN,
                    GhiChu = ct.GhiChu
                };

            return await query.AsNoTracking().ToListAsync();
        }
    }
}
