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
    // Và tab Cấp cốc: phiếu đầu tiên của Scope sinh slot từ LcXuatCapLayoutSeed (cố định), phiếu
    // sau copy forward cấu trúc từ phiếu liền trước — xem LC_XuatCapRepository.InitializeLC_XuatCapAsync.
    public class LC_BCSX_XNL_TPInitializer : IPhieuJsonInitializer
    {
        private readonly ILC_CapLieuSiloRepository _capLieuSiloRepo;
        private readonly ILC_PhoiTronRepository _phoiTronRepo;
        private readonly ILC_SiloCocTonRepository _siloCocTonRepo;
        private readonly ILC_XuatCapRepository _xuatCapRepo;

        public LC_BCSX_XNL_TPInitializer(
            ILC_CapLieuSiloRepository capLieuSiloRepo,
            ILC_PhoiTronRepository phoiTronRepo,
            ILC_SiloCocTonRepository siloCocTonRepo,
            ILC_XuatCapRepository xuatCapRepo)
        {
            _capLieuSiloRepo = capLieuSiloRepo;
            _phoiTronRepo = phoiTronRepo;
            _siloCocTonRepo = siloCocTonRepo;
            _xuatCapRepo = xuatCapRepo;
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
            await _xuatCapRepo.InitializeLC_XuatCapAsync(phieu);
            return new List<string>();
        }
    }
}
