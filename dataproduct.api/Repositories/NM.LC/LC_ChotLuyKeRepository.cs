using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // "Chốt lũy kế" (xem SECTION 34 lc_bcxs_xnl_tp.sql / LC_LuyKeChotMoc) — orchestrator gọi lại
    // các "Refresh*TuCaTruocAsync" ĐÃ CÓ của 6 repository khác (tái dùng công thức, KHÔNG viết
    // lại), không tự tính toán gì ở đây ngoài việc xác định phạm vi ảnh hưởng + ghi mốc.
    public class LC_ChotLuyKeRepository : ILC_ChotLuyKeRepository
    {
        private const string MA_BM = "LC_BCSX_XNL_TP";

        private readonly ProductFormContext _context;
        private readonly ILC_CapLieuSiloRepository _capLieuSiloRepo;
        private readonly ILC_PhoiTronRepository _phoiTronRepo;
        private readonly ILC_ThanhPhamRepository _thanhPhamRepo;
        private readonly ILC_XuatCapRepository _xuatCapRepo;
        private readonly ILC_SanLuongCocRepository _sanLuongCocRepo;

        public LC_ChotLuyKeRepository(
            ProductFormContext context,
            ILC_CapLieuSiloRepository capLieuSiloRepo,
            ILC_PhoiTronRepository phoiTronRepo,
            ILC_ThanhPhamRepository thanhPhamRepo,
            ILC_XuatCapRepository xuatCapRepo,
            ILC_SanLuongCocRepository sanLuongCocRepo)
        {
            _context = context;
            _capLieuSiloRepo = capLieuSiloRepo;
            _phoiTronRepo = phoiTronRepo;
            _thanhPhamRepo = thanhPhamRepo;
            _xuatCapRepo = xuatCapRepo;
            _sanLuongCocRepo = sanLuongCocRepo;
        }

        public async Task<LcChotLuyKeResultDto> ChotLuyKeAsync(Guid idPhieuChot, string? nguoiChot)
        {
            var phieuChot = await _context.BmPhieus.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieuChot)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");

            if (phieuChot.MaBm != MA_BM)
                throw new InvalidOperationException("Chỉ áp dụng Chốt lũy kế cho phiếu Báo cáo sản xuất Luyện cốc.");
            if (phieuChot.Scope == null || phieuChot.NgaySX == null || phieuChot.Ca == null)
                throw new InvalidOperationException("Phiếu chưa đủ Thành phẩm/Ngày SX/Ca để chốt lũy kế.");

            var scope = phieuChot.Scope.Value;
            var ngayChot = phieuChot.NgaySX.Value;
            var caChot = phieuChot.Ca.Value;

            // Phiếu bị ảnh hưởng — cùng Scope/MaBm, chưa xoá, SAU mốc muốn chốt, sắp theo thời
            // gian tăng dần. Loại HẲN IsLock=1 (phiếu GỐC đã "đề nghị hiệu chỉnh" và clone sang
            // phiếu khác — không đụng tới, không tính vào điều kiện chặn, xem SECTION 5 SỬA
            // 2026-11-13 ở lc_bcxs_xnl_tp.sql).
            var affected = await _context.BmPhieus.AsNoTracking()
                .Where(p => p.MaBm == MA_BM && p.Scope == scope
                    && (p.IsDelete == null || p.IsDelete == 0)
                    && p.IsLock != 1
                    && (p.NgaySX > ngayChot || (p.NgaySX == ngayChot && p.Ca > caChot)))
                .OrderBy(p => p.NgaySX).ThenBy(p => p.Ca)
                .Select(p => new { p.Idphieu, p.NgaySX, p.Ca, p.TinhTrang, p.SoPhieu })
                .ToListAsync();

            // Tất cả-hoặc-không: chỉ cần 1 phiếu Hoàn thành(2)/Đã chốt(5) trong vùng ảnh hưởng là
            // chặn hẳn, không chốt phần nào — tránh cascade nửa chừng tạo lũy kế đứt gãy.
            var bloker = affected.FirstOrDefault(p => p.TinhTrang == 2 || p.TinhTrang == 5);
            if (bloker != null)
            {
                var tenTrangThai = bloker.TinhTrang == 5 ? "Đã chốt" : "Hoàn thành";
                var tenPhieu = string.IsNullOrWhiteSpace(bloker.SoPhieu) ? bloker.Idphieu.ToString() : bloker.SoPhieu;
                throw new InvalidOperationException(
                    $"Không thể chốt lũy kế: phiếu {tenPhieu} (ngày {bloker.NgaySX:dd/MM/yyyy}, ca {bloker.Ca}) " +
                    $"đang ở trạng thái \"{tenTrangThai}\" — vui lòng xử lý phiếu này trước rồi thử lại.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.LC_LuyKeChotMoc.Add(new LC_LuyKeChotMoc
                {
                    Scope = scope,
                    NgayChot = ngayChot,
                    CaChot = caChot,
                    IdPhieuChot = idPhieuChot,
                    NguoiChot = nguoiChot,
                    NgayTao = DateTime.Now,
                });
                await _context.SaveChangesAsync();

                // Cascade theo ĐÚNG thứ tự thời gian — phiếu sau phụ thuộc giá trị vừa ghi lại
                // của phiếu ngay trước, không thể song song/nhảy cóc. Gọi lại 6 "Refresh*
                // TuCaTruocAsync" đã có (8 chuỗi Lũy kế, xem SECTION 34) — không viết lại công
                // thức ở đây.
                foreach (var p in affected)
                {
                    await _capLieuSiloRepo.RefreshTonDauTuCaTruocAsync(p.Idphieu);
                    await _phoiTronRepo.RefreshLuyKeTuCaTruocAsync(p.Idphieu);
                    await _thanhPhamRepo.RefreshThapPheuTuCaTruocAsync(p.Idphieu);
                    await _thanhPhamRepo.RefreshBuiCocTuCaTruocAsync(p.Idphieu);
                    await _xuatCapRepo.RefreshLuyKeTuCaTruocAsync(p.Idphieu);
                    await _sanLuongCocRepo.RefreshLuyKeTuCaTruocAsync(p.Idphieu);
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            return new LcChotLuyKeResultDto
            {
                NgayChot = ngayChot,
                CaChot = caChot,
                SoPhieuDaTinhLai = affected.Count,
            };
        }
    }
}
