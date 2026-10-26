using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Repositories;
using dataproduct.api.Repositories.NM.LC;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_XuatCapService
    {
        private readonly ILC_XuatCapRepository _repo;
        private readonly IBienBanGiaoNhanSourceRepository _bbgnRepo;

        public LC_XuatCapService(ILC_XuatCapRepository repo, IBienBanGiaoNhanSourceRepository bbgnRepo)
        {
            _repo = repo;
            _bbgnRepo = bbgnRepo;
        }

        public Task<List<LcXuatCapSlotDto>> GetTabAsync(Guid idPhieu)
            => _repo.GetByPhieuAsync(idPhieu);

        public Task<List<LcXuatCapSlotDto>> SaveTabAsync(Guid idPhieu, List<LcXuatCapSaveSlotDto> slots)
            => _repo.SaveTabAsync(idPhieu, slots);

        public Task<LcXuatCapSlotDto> GanNguonAsync(LcXuatCapGanRequest req)
            => _repo.GanNguonAsync(req);

        public Task<LcXuatCapSlotDto> BoGanNguonAsync(int idXuatCapNguon)
            => _repo.BoGanNguonAsync(idXuatCapNguon);

        // "Làm mới" — liệt kê THÔ danh sách chấm chuyến BBGN (ngay/ca + xưởng giao cố định theo
        // Scope: 162=TP1, 163=TP2, xem PhanTich_TabCapCoc.md §8.2) cho FE hiển thị bên cạnh để
        // người dùng tự gán tay vào slot (§9.0 mục 4 — KHÔNG auto-map). KHÔNG lọc theo Kip (chốt
        // 2026-10-23 lượt 4) — `ca` lấy mặc định từ Ca của phiếu BCSX_XNL (cùng quy ước 1=ngày/
        // 2=đêm với Tbl_BienBanGiaoNhan.Ca).
        public Task<List<LcCapCocNguonBbgnDto>> GetNguonBbgnAsync(DateTime ngay, string ca, int scope)
        {
            var idXuongBg = scope == 2 ? 163 : 162;
            return _bbgnRepo.GetCapCocNguonAsync(ngay, ca, idXuongBg);
        }
    }
}
