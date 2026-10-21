using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.MasterData;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tra cứu Vật tư (than) + Mã lô đang active theo vật tư đã chọn — dùng cho tab Cấp liệu
    // & Silo (chọn liệu qua autocomplete → lô đổi theo liệu). Đọc trực tiếp
    // PRODUCTDATA.dbo.Tbl_VatTu / Tbl_MaLo / Tbl_VatTuMaLo (CHỈ ĐỌC, không sửa) — cùng
    // ProductDataMasterDbContext nên join LINQ bình thường được (không vướng "khác DbContext"
    // như PRODUCT_FORM↔PRODUCTDATA). Không còn dùng LC_MaLo_VatTu riêng (đã drop table).
    public class LC_VatTuMaLoLookupRepository : ILC_VatTuMaLoLookupRepository
    {
        private readonly ProductDataMasterDbContext _context;

        public LC_VatTuMaLoLookupRepository(ProductDataMasterDbContext context)
        {
            _context = context;
        }

        public async Task<List<LcVatTuLookupDto>> SearchVatTuAsync(string? searchKey, int take = 50)
        {
            var query = _context.Tbl_VatTu.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                var key = searchKey.Trim();
                query = query.Where(x =>
                    (x.TenVatTu != null && x.TenVatTu.Contains(key)) ||
                    (x.MaVatTu_Sap != null && x.MaVatTu_Sap.Contains(key)) ||
                    (x.TenVatTu_Sap != null && x.TenVatTu_Sap.Contains(key)));
            }

            return await query
                .OrderBy(x => x.TenVatTu)
                .Take(take)
                .Select(x => new LcVatTuLookupDto
                {
                    IdVatTu = x.ID_VatTu,
                    TenVatTu = x.TenVatTu,
                    MaVatTuSap = x.MaVatTu_Sap,
                    TenVatTuSap = x.TenVatTu_Sap,
                    DonViTinh = x.DonViTinh,
                })
                .ToListAsync();
        }

        // ID_TinhTrang = 1 → lô đang active (xác nhận qua dữ liệu thật: lô mới tạo gần đây
        // đều = 1, lô cũ/đã dùng hết = 0 — không có cột trạng thái riêng trên Tbl_VatTuMaLo).
        public async Task<List<LcMaLoLookupDto>> GetMaLoByVatTuAsync(int idVatTu)
        {
            var query =
                from map in _context.Tbl_VatTuMaLo.AsNoTracking()
                join malo in _context.Tbl_MaLo.AsNoTracking() on map.ID_MaLo equals malo.ID_MaLo
                where map.ID_VatTu == idVatTu && malo.ID_TinhTrang == 1
                orderby malo.TenMaLo
                select new LcMaLoLookupDto
                {
                    IdMaLo = malo.ID_MaLo,
                    TenMaLo = malo.TenMaLo,
                    PhongBan = malo.PhongBan,
                };

            return await query.ToListAsync();
        }

        // Batch version của GetMaLoByVatTuAsync — load lô active cho nhiều vật tư trong 1 round-trip
        // (tránh N+1 khi mở phiếu đã có sẵn N dòng cấp liệu đã chọn than).
        public async Task<List<LcMaLoByVatTuGroupDto>> GetMaLoByVatTuListAsync(List<int> idVatTuList)
        {
            if (idVatTuList == null || idVatTuList.Count == 0) return new List<LcMaLoByVatTuGroupDto>();
            var distinctIds = idVatTuList.Distinct().ToList();

            var rows =
                from map in _context.Tbl_VatTuMaLo.AsNoTracking()
                join malo in _context.Tbl_MaLo.AsNoTracking() on map.ID_MaLo equals malo.ID_MaLo
                where distinctIds.Contains(map.ID_VatTu) && malo.ID_TinhTrang == 1
                orderby malo.TenMaLo
                select new { map.ID_VatTu, malo.ID_MaLo, malo.TenMaLo, malo.PhongBan };

            var list = await rows.ToListAsync();

            return list
                .GroupBy(x => x.ID_VatTu)
                .Select(g => new LcMaLoByVatTuGroupDto
                {
                    IdVatTu = g.Key,
                    MaLoList = g.Select(x => new LcMaLoLookupDto
                    {
                        IdMaLo = x.ID_MaLo,
                        TenMaLo = x.TenMaLo,
                        PhongBan = x.PhongBan,
                    }).ToList(),
                })
                .ToList();
        }
    }
}
