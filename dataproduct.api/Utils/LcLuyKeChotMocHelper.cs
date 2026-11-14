using dataproduct.api.Models;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Utils
{
    // Dùng chung bởi mọi Compute*LuyKe*Async của module LC (xem các LC_*Repository ở
    // Repositories/NM.LC) — xem LC_LuyKeChotMoc (SECTION 34 lc_bcxs_xnl_tp.sql) cho ý nghĩa đầy
    // đủ. CHỈ áp dụng cho các hàm tính LŨY KẾ (kế toán) — KHÔNG áp dụng cho Tồn đầu/Tồn cuối
    // (vật lý).
    public static class LcLuyKeChotMocHelper
    {
        // Mốc chốt GẦN NHẤT (NgayChot, CaChot) ≤ (ngaySx, ca) của Scope này, hoặc null nếu chưa
        // từng chốt. Gọi 1 LẦN trước khi build query "tra kíp liền trước", rồi dùng kết quả làm
        // NGƯỠNG DƯỚI bổ sung (ứng viên phải SAU mốc này mới được kế thừa) — xem cách dùng ở
        // LC_PhoiTronRepository.ComputeLuyKeQuyKhoAsync.
        public static async Task<(DateOnly NgaySX, int Ca)?> GetMocChotGanNhatAsync(
            ProductFormContext context, int scope, DateOnly ngaySx, int ca)
        {
            var m = await context.LC_LuyKeChotMoc.AsNoTracking()
                .Where(x => x.Scope == scope && (x.NgayChot < ngaySx || (x.NgayChot == ngaySx && x.CaChot <= ca)))
                .OrderByDescending(x => x.NgayChot).ThenByDescending(x => x.CaChot)
                .Select(x => new { x.NgayChot, x.CaChot })
                .FirstOrDefaultAsync();
            return m == null ? null : (m.NgayChot, m.CaChot);
        }

        // Gọi helper trên trước rồi tự inline điều kiện ngay trong `.Where()` của query — KHÔNG
        // gọi 1 hàm C# tùy ý trong lambda LINQ-to-Entities (EF Core không dịch được sang SQL),
        // chỉ so sánh trực tiếp với các field của tuple `mocChot` (đọc như closure variable):
        //   && (mocChot == null || x.NgaySX > mocChot.Value.NgaySX
        //       || (x.NgaySX == mocChot.Value.NgaySX && x.Ca > mocChot.Value.Ca))
    }
}
