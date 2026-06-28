using dataproduct.api.DTOs;
using dataproduct.api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class Hrc2SlabWorkflowRepository : IHrc2SlabWorkflowRepository
    {
        private readonly ProductFormContext _context;

        public Hrc2SlabWorkflowRepository(ProductFormContext context)
        {
            _context = context;
        }

        // ── Search với TrangThai JOIN ─────────────────────────────────────────

        public async Task<(IEnumerable<BkHrc2SlabItem> Data, int TotalCount)> SearchWithTrangThaiAsync(HrcSlabSearchRequest request)
        {
            var query = _context.BkHrc2Slabs
                .Include(s => s.TrangThai)
                .AsQueryable();

            if (request.TuNgay.HasValue)
                query = query.Where(s => s.NgaySanXuat >= request.TuNgay);
            if (request.DenNgay.HasValue)
                query = query.Where(s => s.NgaySanXuat <= request.DenNgay);
            if (!string.IsNullOrWhiteSpace(request.CaSanXuat))
                query = query.Where(s => s.CaSanXuat == request.CaSanXuat);
            if (!string.IsNullOrWhiteSpace(request.Kip))
                query = query.Where(s => s.CaSanXuat != null && s.CaSanXuat.Contains(request.Kip));
            if (request.MayDuc.HasValue)
                query = query.Where(s => s.MayDuc == request.MayDuc);
            if (!string.IsNullOrWhiteSpace(request.MeThep))
                query = query.Where(s => s.MeThep != null && s.MeThep.Contains(request.MeThep));
            if (request.IdSlabs != null && request.IdSlabs.Count > 0)
                query = query.Where(s => s.IdSlab != null && request.IdSlabs.Contains(s.IdSlab));
            if (!string.IsNullOrWhiteSpace(request.MacThep))
                query = query.Where(s => s.MacThep != null && s.MacThep.Contains(request.MacThep));
            if (request.IsChot.HasValue)
                query = request.IsChot.Value
                    ? query.Where(s => s.IsChot == true)
                    : query.Where(s => s.IsChot != true);
            if (request.IsTrungIDSlab.HasValue)
                query = query.Where(s => s.IsTrungIDSlab == request.IsTrungIDSlab);
            if (request.IsDiffMacThep.HasValue)
                query = query.Where(s => s.IsDiffMacThep == request.IsDiffMacThep);

            // Filter theo trạng thái workflow
            if (request.TrangThaiKCS.HasValue)
                query = query.Where(s => s.TrangThai != null && s.TrangThai.TrangThaiKCS == request.TrangThaiKCS
                                      || request.TrangThaiKCS == 0 && s.TrangThai == null);
            if (request.TrangThaiDuc.HasValue)
                query = query.Where(s => s.TrangThai != null && s.TrangThai.TrangThaiDuc == request.TrangThaiDuc
                                      || request.TrangThaiDuc == 0 && s.TrangThai == null);
            if (request.TrangThaiKho.HasValue)
                query = query.Where(s => s.TrangThai != null && s.TrangThai.TrangThaiKho == request.TrangThaiKho
                                      || request.TrangThaiKho == 0 && s.TrangThai == null);
            if (request.TrangThaiPKH.HasValue)
                query = query.Where(s => s.TrangThai != null && s.TrangThai.TrangThaiPKH == request.TrangThaiPKH
                                      || request.TrangThaiPKH == 0 && s.TrangThai == null);

            var totalCount = await query.CountAsync();

            var data = await query
                .OrderByDescending(s => s.BkmisId)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(s => new BkHrc2SlabItem
                {
                    Id = s.Id,
                    BkmisId = s.BkmisId,
                    NgaySanXuat = s.NgaySanXuat,
                    ShiftName = s.ShiftName,
                    CaSanXuat = s.CaSanXuat,
                    KipSanXuat = s.KipSanXuat,
                    MeThep = s.MeThep,
                    IdSlab = s.IdSlab,
                    MacThep = s.MacThep,
                    ChatLuong = s.ChatLuong,
                    ChieuDay = s.ChieuDay,
                    ChieuRong = s.ChieuRong,
                    ChieuDai = s.ChieuDai,
                    KhoiLuong = s.KhoiLuong,
                    KhoiLuongTinhToan = s.KhoiLuongTinhToan,
                    ChatLuongTPHH = s.ChatLuongTPHH,
                    ThongTinPhoi = s.ThongTinPhoi,
                    TpKhongDatGangLong = s.TpKhongDatGangLong,
                    GhiChu = s.GhiChu,
                    LoaiPhoi = s.LoaiPhoi,
                    SapCode = s.SapCode,
                    SapDescription = s.SapDescription,
                    SoLo = s.SoLo,
                    OrderId = s.OrderId,
                    MayDuc = s.MayDuc,
                    IsTrungIDSlab = s.IsTrungIDSlab,
                    IsDiffMacThep = s.IsDiffMacThep,
                    Line = s.Line,
                    SapLastTime = s.SapLastTime,
                    IsChot = s.IsChot,
                    NgayTao = s.NgayTao,
                    TrangThaiKCS = s.TrangThai == null ? 0 : s.TrangThai.TrangThaiKCS,
                    TrangThaiDuc = s.TrangThai == null ? 0 : s.TrangThai.TrangThaiDuc,
                    TrangThaiKho = s.TrangThai == null ? 0 : s.TrangThai.TrangThaiKho,
                    TrangThaiPKH = s.TrangThai == null ? 0 : s.TrangThai.TrangThaiPKH,
                    IdPhieuBBSL = s.TrangThai == null ? null : s.TrangThai.IdPhieuBBSL,
                })
                .ToListAsync();

            // Load SoPhieu cho các record có IdPhieuBBSL
            var phieuIds = data.Where(d => d.IdPhieuBBSL != null)
                               .Select(d => d.IdPhieuBBSL!.Value)
                               .Distinct()
                               .ToList();
            if (phieuIds.Any())
            {
                var phieuMap = await _context.BmPhieus
                    .Where(p => phieuIds.Contains(p.Idphieu))
                    .Select(p => new { p.Idphieu, p.SoPhieu, p.NgaySX, p.Ca, p.Kip })
                    .ToDictionaryAsync(p => p.Idphieu);
                foreach (var d in data.Where(d => d.IdPhieuBBSL != null))
                {
                    if (phieuMap.TryGetValue(d.IdPhieuBBSL!.Value, out var phieu))
                    {
                        d.SoPhieuBBSL = phieu.SoPhieu;
                        d.NgayXuLy    = phieu.NgaySX;
                        d.CaBBSL      = phieu.Ca;
                        d.KipBBSL     = phieu.Kip;
                    }
                }
            }

            return (data, totalCount);
        }

        // ── Tổng hợp (GROUP BY 5 điều kiện) ─────────────────────────────────

        public async Task<IEnumerable<SlabTongHopItem>> GetTongHopAsync(
            DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip)
        {
            var q = _context.BkHrc2SlabTrangThais
                .Include(tt => tt.Slab)
                .Where(tt => tt.TrangThaiKCS == 1)
                .AsQueryable();

            if (tuNgay.HasValue) q = q.Where(tt => tt.Slab.NgaySanXuat >= tuNgay);
            if (denNgay.HasValue) q = q.Where(tt => tt.Slab.NgaySanXuat <= denNgay);
            if (!string.IsNullOrWhiteSpace(ca)) q = q.Where(tt => tt.Slab.CaSanXuat == ca);
            if (!string.IsNullOrWhiteSpace(kip)) q = q.Where(tt => tt.Slab.CaSanXuat != null && tt.Slab.CaSanXuat.Contains(kip));

            return await q
                .GroupBy(tt => new
                {
                    tt.Slab.MeThep,
                    tt.Slab.MacThep,
                    tt.Slab.ChieuDay,
                    tt.Slab.ChieuRong,
                    tt.Slab.ChieuDai,
                    tt.Slab.LoaiPhoi,
                    tt.Slab.ChatLuongTPHH
                })
                .Select(g => new SlabTongHopItem
                {
                    MeThep = g.Key.MeThep,
                    MacThep = g.Key.MacThep,
                    ChieuDay = g.Key.ChieuDay,
                    ChieuRong = g.Key.ChieuRong,
                    ChieuDai = g.Key.ChieuDai,
                    LoaiPhoi = g.Key.LoaiPhoi,
                    ChatLuongTPHH = g.Key.ChatLuongTPHH,
                    SoLuong = g.Count(),
                    TongKhoiLuong = g.Sum(tt => tt.Slab.KhoiLuong)
                })
                .OrderBy(x => x.MeThep).ThenBy(x => x.MacThep)
                .ToListAsync();
        }

        // ── Danh sách phiếu BBSL có thể chọn ────────────────────────────────

        public async Task<IEnumerable<PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca)
        {
            var q = _context.BmPhieus
                .Where(p => p.MaBm == "HRC2_BBGN_PhoiTam"
                         && p.TinhTrang != 5
                         && (p.IsDelete == null || p.IsDelete == 0)
                         && (p.IsLock == null || p.IsLock == 0))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(kip))
                q = q.Where(p => p.Kip == kip);
            if (ca.HasValue)
                q = q.Where(p => p.Ca == ca);

            var phieus = await q
                .OrderByDescending(p => p.NgaySX)
                .Take(100)
                .Select(p => new PhieuBBSLItem
                {
                    IdPhieu = p.Idphieu,
                    SoPhieu = p.SoPhieu,
                    NgaySX = p.NgaySX,
                    Ca = p.Ca,
                    Kip = p.Kip,
                    TinhTrang = p.TinhTrang,
                })
                .ToListAsync();

            // Đếm slab theo trạng thái cho mỗi phiếu
            var phieuIds = phieus.Select(p => p.IdPhieu).ToList();
            var statMap = await _context.BkHrc2SlabTrangThais
                .Where(tt => tt.IdPhieuBBSL != null && phieuIds.Contains(tt.IdPhieuBBSL!.Value))
                .GroupBy(tt => tt.IdPhieuBBSL!.Value)
                .Select(g => new
                {
                    IdPhieu = g.Key,
                    Total   = g.Count(),
                    SoDuc   = g.Count(tt => tt.TrangThaiDuc == 1),
                    SoKho   = g.Count(tt => tt.TrangThaiKho == 1),
                    SoPKH   = g.Count(tt => tt.TrangThaiPKH == 1),
                })
                .ToDictionaryAsync(x => x.IdPhieu, x => x);

            foreach (var p in phieus)
            {
                if (statMap.TryGetValue(p.IdPhieu, out var stat))
                {
                    p.SoSlabDaChot = stat.Total;
                    p.SoSlabDuc    = stat.SoDuc;
                    p.SoSlabKho    = stat.SoKho;
                    p.SoSlabPKH    = stat.SoPKH;
                }
            }

            return phieus;
        }

        // ── Ruột phiếu (GROUP BY 5 điều kiện trong phiếu) ──────────────────

        public async Task<IEnumerable<SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu)
        {
            return await _context.BkHrc2SlabTrangThais
                .Include(tt => tt.Slab)
                .Where(tt => tt.IdPhieuBBSL == idPhieu && tt.TrangThaiKCS == 1)
                .GroupBy(tt => new
                {
                    tt.Slab.MeThep,
                    tt.Slab.MacThep,
                    tt.Slab.ChieuDay,
                    tt.Slab.ChieuRong,
                    tt.Slab.ChieuDai,
                    tt.Slab.LoaiPhoi,
                    tt.Slab.ChatLuongTPHH
                })
                .Select(g => new SlabTongHopItem
                {
                    MeThep = g.Key.MeThep,
                    MacThep = g.Key.MacThep,
                    ChieuDay = g.Key.ChieuDay,
                    ChieuRong = g.Key.ChieuRong,
                    ChieuDai = g.Key.ChieuDai,
                    LoaiPhoi = g.Key.LoaiPhoi,
                    ChatLuongTPHH = g.Key.ChatLuongTPHH,
                    SoLuong = g.Count(),
                    TongKhoiLuong = g.Sum(tt => tt.Slab.KhoiLuong)
                })
                .OrderBy(x => x.MeThep).ThenBy(x => x.MacThep)
                .ToListAsync();
        }

        // ── Danh sách slab cá nhân trong phiếu ──────────────────────────────

        public async Task<IEnumerable<BkHrc2SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu)
        {
            return await _context.BkHrc2SlabTrangThais
                .Include(tt => tt.Slab)
                .Where(tt => tt.IdPhieuBBSL == idPhieu && tt.TrangThaiKCS == 1)
                .OrderByDescending(tt => tt.Slab.BkmisId)
                .Select(tt => new BkHrc2SlabItem
                {
                    Id              = tt.Slab.Id,
                    BkmisId         = tt.Slab.BkmisId,
                    NgaySanXuat     = tt.Slab.NgaySanXuat,
                    ShiftName       = tt.Slab.ShiftName,
                    CaSanXuat       = tt.Slab.CaSanXuat,
                    KipSanXuat      = tt.Slab.KipSanXuat,
                    MeThep          = tt.Slab.MeThep,
                    IdSlab          = tt.Slab.IdSlab,
                    MacThep         = tt.Slab.MacThep,
                    ChatLuong       = tt.Slab.ChatLuong,
                    ChieuDay        = tt.Slab.ChieuDay,
                    ChieuRong       = tt.Slab.ChieuRong,
                    ChieuDai        = tt.Slab.ChieuDai,
                    KhoiLuong       = tt.Slab.KhoiLuong,
                    ChatLuongTPHH   = tt.Slab.ChatLuongTPHH,
                    LoaiPhoi        = tt.Slab.LoaiPhoi,
                    MayDuc          = tt.Slab.MayDuc,
                    IsChot          = tt.Slab.IsChot,
                    TrangThaiKCS    = tt.TrangThaiKCS,
                    TrangThaiDuc    = tt.TrangThaiDuc,
                    TrangThaiKho    = tt.TrangThaiKho,
                    TrangThaiPKH    = tt.TrangThaiPKH,
                })
                .ToListAsync();
        }

        // ── Workflow operations ──────────────────────────────────────────────

        public async Task ChuyenBBSLAsync(List<int> idSlabs, Guid idPhieu, int nguoiThucHien)
        {
            var now = DateTime.Now;

            // Validate: slab không được IsChot
            var hasChot = await _context.BkHrc2Slabs
                .AnyAsync(s => idSlabs.Contains(s.Id) && s.IsChot == true);
            if (hasChot)
                throw new InvalidOperationException("Một số mẻ đã được chốt, không thể chuyển.");

            // Validate: chưa chuyển BBSL (TrangThaiKCS = 0)
            var alreadyChuyen = await _context.BkHrc2SlabTrangThais
                .AnyAsync(t => idSlabs.Contains(t.IdSlab) && t.TrangThaiKCS == 1);
            if (alreadyChuyen)
                throw new InvalidOperationException("Một số mẻ đã được chuyển BBSL, không thể chuyển lại.");

            var existing = await _context.BkHrc2SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToListAsync();

            var existingMap = existing.ToDictionary(t => t.IdSlab);

            var toAdd = new List<BkHrc2SlabTrangThai>();
            foreach (var idSlab in idSlabs)
            {
                if (existingMap.TryGetValue(idSlab, out var r))
                {
                    r.IdPhieuBBSL    = idPhieu;
                    r.TrangThaiKCS   = 1;
                    r.NguoiChuyenKCS = nguoiThucHien;
                    r.NgayChuyenKCS  = now;
                }
                else
                {
                    toAdd.Add(new BkHrc2SlabTrangThai
                    {
                        IdSlab         = idSlab,
                        IdPhieuBBSL    = idPhieu,
                        TrangThaiKCS   = 1,
                        NguoiChuyenKCS = nguoiThucHien,
                        NgayChuyenKCS  = now,
                        NgayTao        = now,
                    });
                }
            }

            if (toAdd.Count > 0)
                await _context.BkHrc2SlabTrangThais.AddRangeAsync(toAdd);

            await _context.SaveChangesAsync();
        }

        public async Task ThuHoiAsync(List<int> idSlabs, int nguoiThucHien)
        {
            // Validate: PKH chưa chốt
            var pkhChot = await _context.BkHrc2SlabTrangThais
                .AnyAsync(t => idSlabs.Contains(t.IdSlab) && t.TrangThaiPKH == 1);
            if (pkhChot)
                throw new InvalidOperationException("Một số mẻ đã được PKH chốt, không thể thu hồi.");

            var records = await _context.BkHrc2SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToListAsync();

            foreach (var r in records)
            {
                r.TrangThaiKCS   = 0;
                r.IdPhieuBBSL    = null;
                r.NguoiChuyenKCS = null;
                r.NgayChuyenKCS  = null;
            }

            await _context.SaveChangesAsync();
        }

        public async Task XacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien)
        {
            var now = DateTime.Now;
            var existing = await _context.BkHrc2SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToListAsync();

            var existingMap = existing.ToDictionary(t => t.IdSlab);
            var toAdd = new List<BkHrc2SlabTrangThai>();

            foreach (var idSlab in idSlabs)
            {
                if (existingMap.TryGetValue(idSlab, out var r))
                {
                    switch (loaiXacNhan)
                    {
                        case "Duc": r.TrangThaiDuc = 1; r.NguoiXacNhanDuc = nguoiThucHien; r.NgayXacNhanDuc = now; break;
                        case "Kho": r.TrangThaiKho = 1; r.NguoiXacNhanKho = nguoiThucHien; r.NgayXacNhanKho = now; break;
                        case "PKH": r.TrangThaiPKH = 1; r.NguoiChotPKH    = nguoiThucHien; r.NgayChotPKH    = now; break;
                    }
                }
                else
                {
                    var newRecord = new BkHrc2SlabTrangThai { IdSlab = idSlab, NgayTao = now };
                    switch (loaiXacNhan)
                    {
                        case "Duc": newRecord.TrangThaiDuc = 1; newRecord.NguoiXacNhanDuc = nguoiThucHien; newRecord.NgayXacNhanDuc = now; break;
                        case "Kho": newRecord.TrangThaiKho = 1; newRecord.NguoiXacNhanKho = nguoiThucHien; newRecord.NgayXacNhanKho = now; break;
                        case "PKH": newRecord.TrangThaiPKH = 1; newRecord.NguoiChotPKH    = nguoiThucHien; newRecord.NgayChotPKH    = now; break;
                    }
                    toAdd.Add(newRecord);
                }
            }

            if (toAdd.Count > 0)
                await _context.BkHrc2SlabTrangThais.AddRangeAsync(toAdd);

            await _context.SaveChangesAsync();
        }

        public async Task HuyXacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien)
        {
            var records = await _context.BkHrc2SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToListAsync();

            foreach (var r in records)
            {
                switch (loaiXacNhan)
                {
                    case "Duc":
                        r.TrangThaiDuc     = 0;
                        r.NguoiXacNhanDuc  = null;
                        r.NgayXacNhanDuc   = null;
                        break;
                    case "Kho":
                        r.TrangThaiKho     = 0;
                        r.NguoiXacNhanKho  = null;
                        r.NgayXacNhanKho   = null;
                        break;
                    case "PKH":
                        r.TrangThaiPKH     = 0;
                        r.NguoiChotPKH     = null;
                        r.NgayChotPKH      = null;
                        break;
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien)
        {
            var phieu = await _context.BmPhieus.FindAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu");
            phieu.TinhTrang = 5;
            phieu.IsLock = 1;
            await _context.SaveChangesAsync();
        }

        public async Task HuyChotPhieuAsync(Guid idPhieu, int nguoiThucHien)
        {
            var phieu = await _context.BmPhieus.FindAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu");
            phieu.TinhTrang = 0;
            phieu.IsLock = 0;
            await _context.SaveChangesAsync();
        }

        public async Task<SyncStatusItem> SyncAsync(DateOnly? ngayBatDau, DateOnly? ngayKetThuc)
        {
            var p1 = ngayBatDau.HasValue
                ? new SqlParameter("@NgayBatDau", ngayBatDau.Value.ToDateTime(TimeOnly.MinValue))
                : new SqlParameter("@NgayBatDau", DBNull.Value);
            var p2 = ngayKetThuc.HasValue
                ? new SqlParameter("@NgayKetThuc", ngayKetThuc.Value.ToDateTime(TimeOnly.MinValue))
                : new SqlParameter("@NgayKetThuc", DBNull.Value);

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC [dbo].[usp_SyncBK_HRC2_Slab] @NgayBatDau, @NgayKetThuc", p1, p2);

            return await GetSyncStatusAsync() ?? new SyncStatusItem { TrangThai = "DONE" };
        }

        public async Task<SyncStatusItem?> GetSyncStatusAsync()
        {
            var latest = await _context.BkSyncHrc2SlabControls
                .OrderByDescending(x => x.BatDauLuc)
                .FirstOrDefaultAsync();

            if (latest == null) return null;

            return new SyncStatusItem
            {
                Id = latest.Id,
                TrangThai = latest.TrangThai,
                NgayBatDau = latest.NgayBatDau,
                NgayKetThuc = latest.NgayKetThuc,
                BatDauLuc = latest.BatDauLuc,
                KetThucLuc = latest.KetThucLuc,
                SoRecordSync = latest.SoRecordSync,
                GhiChu = latest.GhiChu,
            };
        }

    }
}
