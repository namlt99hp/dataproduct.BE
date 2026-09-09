using dataproduct.api.Models;
using dataproduct.api.ResponseModels;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Services.PhieuEnrichers;

/// <summary>
/// Ghi đè TinhTrang trong danh sách phiếu (chỉ ở response, không đụng cột DB thật) thành
/// trạng thái tổng hợp riêng cho BBGN Phôi tấm HRC1 — không dùng chung enum TrangThaiPhieuConst:
///   11 = Chưa hoàn thành (còn slab chưa được Đúc + Cán + C4 xác nhận đầy đủ)
///   12 = Đã hoàn thành   (mọi slab đã được Đúc + Cán + C4 xác nhận, nhưng PKH chưa chốt)
///   5  = Đã chốt         (giữ nguyên TinhTrang thật của BmPhieu, KHÔNG override — 5 đã khớp
///                         nghĩa "Chốt" trong TrangThaiPhieuConst dùng chung)
///
/// Dùng 11/12 (ngoài dải 0-7 của TrangThaiPhieuConst) để tránh trùng mã với enum trạng thái
/// phiếu gốc khi hiển thị chung ở các màn hình dùng PHIEU_STATUS_CONFIG (vd trang Thống kê) —
/// trước đây dùng 1/2/3 nên phiếu "Đã chốt" (3) bị đọc nhầm thành "Đã thu hồi" ở trang Thống kê.
///
/// Cách xác định slab thuộc phiếu y hệt điều kiện validate trong
/// Hrc1SlabRepository.ChotPhieuAsync: slab "tự nhiên" (khớp NgaySX + Ca, chưa bị chuyển ca)
/// cộng với slab "được chuyển ca" vào đúng phiếu này (IsChuyenCa + IdPhieuBBSL).
/// </summary>
public class Hrc1BbgnPhoiTamEnricher : IPhieuSearchEnricher, IPhieuTinhTrangFilterEnricher
{
    private readonly ProductFormContext _context;
    public string MaBm => "HRC1_BBSL_PhoiTam";

    public Hrc1BbgnPhoiTamEnricher(ProductFormContext context) => _context = context;

    public async Task EnrichAsync(SearchPhieuResponseModel item)
    {
        var caStr = item.Ca?.ToString();

        var naturalIds = await _context.Hrc1Slabs
            .AsNoTracking()
            .Where(s => s.NgaySX == item.NgaySX && s.CaSX == caStr && !s.IsDeleted
                        && !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.IsChuyenCa))
            .Select(s => s.Id)
            .ToListAsync();

        var naturalTTMap = naturalIds.Count > 0
            ? await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => naturalIds.Contains(t.IdSlab))
                .ToDictionaryAsync(t => t.IdSlab)
            : new Dictionary<int, Hrc1SlabTrangThai>();

        // Join Hrc1Slabs để loại slab đã xóa mềm (xem Hrc1SlabRepository.ChotPhieuAsync)
        var transferredRecords = await (
            from t in _context.Hrc1SlabTrangThais.AsNoTracking()
            join s in _context.Hrc1Slabs.AsNoTracking() on t.IdSlab equals s.Id
            where t.IsChuyenCa && t.IdPhieuBBSL == item.Idphieu && !s.IsDeleted
            select t
        ).ToListAsync();

        // Số lượng ID Slab đã xác nhận theo từng bộ phận (Đúc/Cán/C4/PKH) trên tổng số ID Slab
        // của phiếu — tính cho MỌI phiếu, kể cả đã chốt (TinhTrang == 5), vì UI danh sách phiếu
        // cần hiển thị dù phiếu đã chốt hay chưa.
        item.SoLuongSlab = naturalIds.Count + transferredRecords.Count;
        item.SoLuongXNDuc = naturalIds.Count(id => naturalTTMap.TryGetValue(id, out var tt) && tt.TrangThaiDuc == 1)
            + transferredRecords.Count(t => t.TrangThaiDuc == 1);
        item.SoLuongXNCan = naturalIds.Count(id => naturalTTMap.TryGetValue(id, out var tt) && tt.TrangThaiCan == 1)
            + transferredRecords.Count(t => t.TrangThaiCan == 1);
        item.SoLuongXNC4 = naturalIds.Count(id => naturalTTMap.TryGetValue(id, out var tt) && tt.TrangThaiC4)
            + transferredRecords.Count(t => t.TrangThaiC4);
        item.SoLuongXNPKH = naturalIds.Count(id => naturalTTMap.TryGetValue(id, out var tt) && tt.TrangThaiPKH == 1)
            + transferredRecords.Count(t => t.TrangThaiPKH == 1);

        if (item.TinhTrang == 5) return;

        if (item.SoLuongSlab == 0)
        {
            item.TinhTrang = 11;
            return;
        }

        var hoanThanh = item.SoLuongXNDuc == item.SoLuongSlab
            && item.SoLuongXNCan == item.SoLuongSlab
            && item.SoLuongXNC4 == item.SoLuongSlab;

        item.TinhTrang = hoanThanh ? 12 : 11;
    }

    /// <summary>Bản batch của EnrichAsync ở trên, dùng để lọc TinhTrang trước khi phân trang.</summary>
    public async Task<List<Guid>> FilterByTinhTrangAsync(List<BmPhieu> candidates, int tinhTrang)
    {
        if (tinhTrang == 5)
            return candidates.Where(p => p.TinhTrang == 5).Select(p => p.Idphieu).ToList();

        if (tinhTrang != 11 && tinhTrang != 12)
            return [];

        var openCandidates = candidates.Where(p => p.TinhTrang != 5).ToList();
        if (openCandidates.Count == 0) return [];

        var ngaySXSet = openCandidates.Where(p => p.NgaySX.HasValue).Select(p => p.NgaySX!.Value).Distinct().ToList();
        var idphieuSet = openCandidates.Select(p => p.Idphieu).ToList();

        var naturalSlabs = ngaySXSet.Count > 0
            ? await _context.Hrc1Slabs
                .AsNoTracking()
                .Where(s => s.NgaySX.HasValue && ngaySXSet.Contains(s.NgaySX.Value) && !s.IsDeleted)
                .Select(s => new { s.Id, s.NgaySX, s.CaSX })
                .ToListAsync()
            : [];

        var naturalSlabIds = naturalSlabs.Select(s => s.Id).ToList();

        var chuyenCaIds = naturalSlabIds.Count > 0
            ? (await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => naturalSlabIds.Contains(t.IdSlab) && t.IsChuyenCa)
                .Select(t => t.IdSlab)
                .ToListAsync()).ToHashSet()
            : [];

        var naturalTTMap = naturalSlabIds.Count > 0
            ? await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => naturalSlabIds.Contains(t.IdSlab))
                .ToDictionaryAsync(t => t.IdSlab)
            : new Dictionary<int, Hrc1SlabTrangThai>();

        // Join Hrc1Slabs để loại slab đã xóa mềm (xem Hrc1SlabRepository.ChotPhieuAsync)
        var transferredAll = await (
            from t in _context.Hrc1SlabTrangThais.AsNoTracking()
            join s in _context.Hrc1Slabs.AsNoTracking() on t.IdSlab equals s.Id
            where t.IsChuyenCa && t.IdPhieuBBSL != null && idphieuSet.Contains(t.IdPhieuBBSL.Value) && !s.IsDeleted
            select t
        ).ToListAsync();
        var transferredByPhieu = transferredAll
            .GroupBy(t => t.IdPhieuBBSL!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<Guid>();
        foreach (var p in openCandidates)
        {
            var caStr = p.Ca?.ToString();
            var naturalIds = naturalSlabs
                .Where(s => s.NgaySX == p.NgaySX && s.CaSX == caStr && !chuyenCaIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToList();

            transferredByPhieu.TryGetValue(p.Idphieu, out var transferredRecords);
            transferredRecords ??= [];

            bool hoanThanh;
            if (naturalIds.Count == 0 && transferredRecords.Count == 0)
            {
                hoanThanh = false;
            }
            else
            {
                var chuaXacNhan = naturalIds.Count(id =>
                        !naturalTTMap.TryGetValue(id, out var tt) || tt.TrangThaiDuc != 1 || tt.TrangThaiCan != 1 || !tt.TrangThaiC4)
                    + transferredRecords.Count(t => t.TrangThaiDuc != 1 || t.TrangThaiCan != 1 || !t.TrangThaiC4);
                hoanThanh = chuaXacNhan == 0;
            }

            if ((tinhTrang == 12) == hoanThanh)
                result.Add(p.Idphieu);
        }
        return result;
    }
}
