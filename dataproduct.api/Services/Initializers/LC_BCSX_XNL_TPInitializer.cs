using dataproduct.api.Models;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.Initializers
{
    // Phiếu "Báo cáo sản xuất Xưởng Nguyên liệu Luyện cốc" (TP1/TP2, Scope 1/2) — khởi tạo
    // tab Cấp liệu & Silo: phiếu đầu dùng danh mục LC_Silo mặc định, phiếu sau copy forward
    // từ kíp liền trước + set tồn đầu qua SP. Xem LC_CapLieuSiloRepository.InitializeLC_BCSX_XNL_TPAsync.
    // Đồng thời khởi tạo tab Phối trộn: 1 thẻ mặc định (SAN_XUAT, thứ tự 1) cho mỗi cụm
    // tháp của Scope đó — xem LC_PhoiTronRepository.InitializeLC_BCSX_XNL_TP_PhoiTronAsync.
    // Và tab Tồn cốc silo: copy forward TOÀN BỘ danh sách cụm/silo từ phiếu liền trước (phiếu
    // đầu tiên của Scope để trống, người dùng tự thêm) — xem
    // LC_SiloCocTonRepository.InitializeLC_SiloCocTonAsync.
    // Tab Cấp cốc KHÔNG có bước init riêng (bỏ 2026-10-26, theo yêu cầu người dùng — "không cần
    // Initialize khi tạo phiếu mới, mỗi phiếu sẽ gán thôi") — slot sinh LAZY khi người dùng nhập/
    // gán, xem LC_XuatCapRepository.EnsureSlotAsync + .claude/LuyenCoc/PhanTich_TabCapCoc.md §10/§11.
    public class LC_BCSX_XNL_TPInitializer : IPhieuJsonInitializer
    {
        private readonly ILC_CapLieuSiloRepository _capLieuSiloRepo;
        private readonly ILC_PhoiTronRepository _phoiTronRepo;
        private readonly ILC_SiloCocTonRepository _siloCocTonRepo;

        public LC_BCSX_XNL_TPInitializer(
            ILC_CapLieuSiloRepository capLieuSiloRepo,
            ILC_PhoiTronRepository phoiTronRepo,
            ILC_SiloCocTonRepository siloCocTonRepo)
        {
            _capLieuSiloRepo = capLieuSiloRepo;
            _phoiTronRepo = phoiTronRepo;
            _siloCocTonRepo = siloCocTonRepo;
        }

        public bool CanHandle(string? maBm)
        {
            if (string.IsNullOrWhiteSpace(maBm)) return false;
            return maBm.Equals("LC_BCSX_XNL_TP", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<string>> InitializeAsync(BmPhieu phieu)
        {
            await _capLieuSiloRepo.InitializeLC_BCSX_XNL_TPAsync(phieu);
            await _phoiTronRepo.InitializeLC_BCSX_XNL_TP_PhoiTronAsync(phieu);
            await _siloCocTonRepo.InitializeLC_SiloCocTonAsync(phieu);
            return new List<string>();
        }
    }
}
