using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Services;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class Hrc1SlabRepository : IHrc1SlabRepository
    {
        private readonly ProductFormContext _context;
        private readonly SyncPhanLoaiService _syncPhanLoai;
        private const string MaBm = "HRC1_BBGN_PhoiTam";

        public Hrc1SlabRepository(ProductFormContext context, SyncPhanLoaiService syncPhanLoai)
        {
            _context = context;
            _syncPhanLoai = syncPhanLoai;
        }

        // ── Upsert từ TSC API ─────────────────────────────────────────────────

        public async Task<Hrc1SlabSyncResult> UpsertFromApiAsync(List<TscSlabItem> items)
        {
            var heatIds = items
                .Select(x => x.HEAT_ID)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList()!;

            var macThepMap = await _syncPhanLoai.GetMacThepMapAsync(heatIds!);

            var slabIds = items.Select(i => i.SLAB_ID).Where(x => x != null).ToList();
            var existing = await _context.Hrc1Slabs
                .Where(s => slabIds.Contains(s.IDSlab))
                .ToDictionaryAsync(s => s.IDSlab);

            // Load TrangThai riêng để kiểm tra guard (CutDate + TrangThaiKho + TrangThaiPKH)
            var existingInternalIds = existing.Values.Select(s => s.Id).ToList();
            var trangThaiMap = existingInternalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .Where(t => existingInternalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            int upserted = 0;
            int macThepFilled = 0;
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.SLAB_ID)) continue;

                macThepMap.TryGetValue(item.HEAT_ID ?? "", out var macThep);
                var ngaySX = ParseNgaySX(item.CA);
                var caSX = item.CA?.Length > 0 ? item.CA[..1] : null;
                var kipSX = item.CA?.Length > 1 ? item.CA[1..2] : null;

                if (existing.TryGetValue(item.SLAB_ID, out var slab))
                {
                    trangThaiMap.TryGetValue(slab.Id, out var tt);
                    if (slab.CutDate.HasValue
                        || tt?.TrangThaiKho == 1
                        || tt?.TrangThaiPKH == 1) continue;

                    slab.IDPiece = item.PIECE_ID;
                    slab.MaMe = item.HEAT_ID;
                    if (macThep != null && slab.MacThep == null) macThepFilled++;
                    slab.MacThep = macThep ?? slab.MacThep;
                    slab.NgaySX = ngaySX;
                    slab.CaSX = caSX;
                    slab.KipSX = kipSX;
                    slab.MayDuc = item.TSC_NO;
                    slab.CutDate = item.CUT_DATE;
                    slab.ChieuDay = item.THICKNESS;
                    slab.ChieuRong = item.WIDTH_HEAD;
                    slab.ChieuDai = item.LENGTH;
                    slab.KhoiLuong = item.WEIGHT;
                    slab.NgayCapNhat = DateTime.Now;
                }
                else
                {
                    if (macThep != null) macThepFilled++;
                    _context.Hrc1Slabs.Add(new Hrc1Slab
                    {
                        IDSlab = item.SLAB_ID,
                        IDPiece = item.PIECE_ID,
                        MaMe = item.HEAT_ID,
                        MacThep = macThep,
                        NgaySX = ngaySX,
                        CaSX = caSX,
                        KipSX = kipSX,
                        MayDuc = item.TSC_NO,
                        CutDate = item.CUT_DATE,
                        ChieuDay = item.THICKNESS,
                        ChieuRong = item.WIDTH_HEAD,
                        ChieuDai = item.LENGTH,
                        KhoiLuong = item.WEIGHT,
                    });
                }
                upserted++;
            }

            await _context.SaveChangesAsync();

            return new Hrc1SlabSyncResult
            {
                Success = true,
                TotalFromApi = items.Count,
                RowsUpserted = upserted,
                MacThepFilled = macThepFilled,
                Message = "Sync hoàn thành"
            };
        }

        private static DateOnly? ParseNgaySX(string? ca)
        {
            if (string.IsNullOrEmpty(ca) || ca.Length < 10) return null;
            var dateStr = ca[^10..];
            if (DateOnly.TryParseExact(dateStr, "dd.MM.yyyy", null,
                System.Globalization.DateTimeStyles.None, out var result))
                return result;
            return null;
        }

        // ── Search (tổng quan) ────────────────────────────────────────────────

        public async Task<(IEnumerable<Hrc1SlabItem> Data, int TotalCount)> SearchAsync(Hrc1SlabSearchRequest req)
        {
            var query = _context.Hrc1Slabs.AsNoTracking();

            if (req.TuNgay.HasValue)  query = query.Where(s => s.NgaySX >= req.TuNgay);
            if (req.DenNgay.HasValue) query = query.Where(s => s.NgaySX <= req.DenNgay);
            if (!string.IsNullOrEmpty(req.CaSX))    query = query.Where(s => s.CaSX == req.CaSX);
            if (!string.IsNullOrEmpty(req.KipSX))   query = query.Where(s => s.KipSX == req.KipSX);
            if (!string.IsNullOrEmpty(req.MayDuc))  query = query.Where(s => s.MayDuc == req.MayDuc);
            if (!string.IsNullOrEmpty(req.MaMe))    query = query.Where(s => s.MaMe!.Contains(req.MaMe));
            if (!string.IsNullOrEmpty(req.IDSlab))  query = query.Where(s => s.IDSlab.Contains(req.IDSlab));
            if (!string.IsNullOrEmpty(req.MacThep)) query = query.Where(s => s.MacThep!.Contains(req.MacThep));
            if (req.IsChot.HasValue)
                query = req.IsChot.Value
                    ? query.Where(s => s.CutDate != null)
                    : query.Where(s => s.CutDate == null);

            if (req.TrangThaiDuc.HasValue)
                query = req.TrangThaiDuc == 0
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiDuc == 1))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiDuc == req.TrangThaiDuc));
            if (req.TrangThaiKho.HasValue)
                query = req.TrangThaiKho == 0
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiKho == 1))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiKho == req.TrangThaiKho));
            if (req.TrangThaiPKH.HasValue)
                query = req.TrangThaiPKH == 0
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiPKH == 1))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiPKH == req.TrangThaiPKH));

            var total = await query.CountAsync();

            var slabs = await query
                .OrderByDescending(s => s.NgaySX)
                .ThenByDescending(s => s.CaSX)
                .ThenBy(s => s.MayDuc)
                .ThenBy(s => s.IDSlab)
                .Skip((req.Page - 1) * req.PageSize)
                .Take(req.PageSize)
                .ToListAsync();

            var slabInternalIds = slabs.Select(s => s.Id).ToList();
            var ttMap = slabInternalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .AsNoTracking()
                    .Where(t => slabInternalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            var phieuIds = ttMap.Values
                .Where(t => t.IdPhieuBBSL != null)
                .Select(t => t.IdPhieuBBSL!.Value)
                .Distinct()
                .ToList();

            var phieuMap = phieuIds.Count > 0
                ? await _context.BmPhieus
                    .Where(p => phieuIds.Contains(p.Idphieu) && p.MaBm == MaBm)
                    .ToDictionaryAsync(p => p.Idphieu, p => p)
                : new Dictionary<Guid, BmPhieu>();

            var items = slabs.Select(s =>
            {
                ttMap.TryGetValue(s.Id, out var tt);
                BmPhieu? phieu = null;
                if (tt?.IdPhieuBBSL != null) phieuMap.TryGetValue(tt.IdPhieuBBSL.Value, out phieu);
                return MapToItem(s, tt, phieu);
            });

            return (items, total);
        }

        // ── Tổng hợp (GROUP BY) ───────────────────────────────────────────────

        public async Task<IEnumerable<Hrc1SlabTongHopItem>> GetTongHopAsync(
            DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip)
        {
            var query = _context.Hrc1Slabs.AsNoTracking();

            if (tuNgay.HasValue)  query = query.Where(s => s.NgaySX >= tuNgay);
            if (denNgay.HasValue) query = query.Where(s => s.NgaySX <= denNgay);
            if (!string.IsNullOrEmpty(ca))  query = query.Where(s => s.CaSX == ca);
            if (!string.IsNullOrEmpty(kip)) query = query.Where(s => s.KipSX == kip);

            return await query
                .GroupBy(s => new { s.MaMe, s.MacThep, s.ChieuDay, s.ChieuRong, s.ChieuDai, s.MayDuc })
                .Select(g => new Hrc1SlabTongHopItem
                {
                    MaMe = g.Key.MaMe,
                    MacThep = g.Key.MacThep,
                    ChieuDay = g.Key.ChieuDay,
                    ChieuRong = g.Key.ChieuRong,
                    ChieuDai = g.Key.ChieuDai,
                    MayDuc = g.Key.MayDuc,
                    SoLuong = g.Count(),
                    TongKhoiLuong = g.Sum(s => s.KhoiLuong),
                })
                .OrderBy(x => x.MaMe)
                .ThenBy(x => x.MacThep)
                .ToListAsync();
        }

        // ── Danh sách phiếu BBSL chưa chốt ──────────────────────────────────

        public async Task<IEnumerable<Hrc1PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca)
        {
            var query = _context.BmPhieus.AsNoTracking()
                .Where(p => p.MaBm == MaBm && p.TinhTrang != 5 && p.IsLock != 1
                            && (p.IsDelete == null || p.IsDelete == 0));

            if (!string.IsNullOrEmpty(kip)) query = query.Where(p => p.Kip == kip);
            if (ca.HasValue) query = query.Where(p => p.Ca == ca);

            var phieus = await query.OrderByDescending(p => p.NgaySX).ToListAsync();
            if (phieus.Count == 0) return [];

            var ngaySXList = phieus
                .Where(p => p.NgaySX != null)
                .Select(p => p.NgaySX!.Value)
                .Distinct()
                .ToList();

            var allNatural = await _context.Hrc1Slabs
                .AsNoTracking()
                .Where(s => ngaySXList.Contains(s.NgaySX!.Value))
                .ToListAsync();

            var naturalIds = allNatural.Select(s => s.Id).ToList();
            var naturalTTMap = naturalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .AsNoTracking()
                    .Where(t => naturalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            var phieuIds = phieus.Select(p => p.Idphieu).ToList();
            var transferredIn = await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => t.IsChuyenCa && t.IdPhieuBBSL != null && phieuIds.Contains(t.IdPhieuBBSL!.Value))
                .ToListAsync();
            var transferredByPhieu = transferredIn
                .GroupBy(t => t.IdPhieuBBSL!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            return phieus.Select(p =>
            {
                var caStr = p.Ca?.ToString();
                var ngaySX = p.NgaySX;

                var naturalSlabs = allNatural
                    .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr
                                && !(naturalTTMap.TryGetValue(s.Id, out var ttCheck) && ttCheck.IsChuyenCa))
                    .ToList();

                var transferred = transferredByPhieu.TryGetValue(p.Idphieu, out var tl) ? tl : [];

                var duc = naturalSlabs.Count(s => naturalTTMap.TryGetValue(s.Id, out var tt) && tt.TrangThaiDuc == 1)
                        + transferred.Count(t => t.TrangThaiDuc == 1);
                var kho = naturalSlabs.Count(s => naturalTTMap.TryGetValue(s.Id, out var tt) && tt.TrangThaiKho == 1)
                        + transferred.Count(t => t.TrangThaiKho == 1);
                var pkh = naturalSlabs.Count(s => naturalTTMap.TryGetValue(s.Id, out var tt) && tt.TrangThaiPKH == 1)
                        + transferred.Count(t => t.TrangThaiPKH == 1);

                return new Hrc1PhieuBBSLItem
                {
                    IdPhieu = p.Idphieu,
                    SoPhieu = p.SoPhieu,
                    NgaySX = p.NgaySX,
                    Ca = p.Ca,
                    Kip = p.Kip,
                    TinhTrang = p.TinhTrang,
                    SoSlabDaChot = naturalSlabs.Count + transferred.Count,
                    SoSlabDuc = duc,
                    SoSlabKho = kho,
                    SoSlabPKH = pkh,
                };
            });
        }

        // ── Ruột phiếu (GROUP BY) ─────────────────────────────────────────────

        public async Task<IEnumerable<Hrc1SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == MaBm)
                ?? throw new InvalidOperationException("Phiếu không tồn tại");

            var allSlabs = await LoadPhieuSlabsAsync(phieu);

            return allSlabs
                .GroupBy(s => new { s.MaMe, s.MacThep, s.ChieuDay, s.ChieuRong, s.ChieuDai, s.MayDuc })
                .Select(g => new Hrc1SlabTongHopItem
                {
                    MaMe = g.Key.MaMe,
                    MacThep = g.Key.MacThep,
                    ChieuDay = g.Key.ChieuDay,
                    ChieuRong = g.Key.ChieuRong,
                    ChieuDai = g.Key.ChieuDai,
                    MayDuc = g.Key.MayDuc,
                    SoLuong = g.Count(),
                    TongKhoiLuong = g.Sum(s => s.KhoiLuong),
                })
                .OrderBy(x => x.MaMe)
                .ToList();
        }

        // ── Chi tiết slab trong phiếu ─────────────────────────────────────────

        public async Task<IEnumerable<Hrc1SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == MaBm)
                ?? throw new InvalidOperationException("Phiếu không tồn tại");

            var caStr = phieu.Ca?.ToString();
            var ngaySX = phieu.NgaySX;

            // Natural slabs (theo Ca/NgaySX, chưa bị chuyển đi)
            var naturalSlabs = await _context.Hrc1Slabs
                .AsNoTracking()
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr
                            && !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.IsChuyenCa))
                .ToListAsync();

            var naturalIds = naturalSlabs.Select(s => s.Id).ToList();
            var naturalTTMap = naturalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .AsNoTracking()
                    .Where(t => naturalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            // Transferred-in slabs (chuyển từ ca khác sang)
            var transferredTTs = await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => t.IsChuyenCa && t.IdPhieuBBSL == idPhieu)
                .ToListAsync();

            var transferredSlabIds = transferredTTs.Select(t => t.IdSlab).ToList();
            var transferredSlabMap = transferredSlabIds.Count > 0
                ? await _context.Hrc1Slabs
                    .AsNoTracking()
                    .Where(s => transferredSlabIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id)
                : new Dictionary<int, Hrc1Slab>();

            var naturalItems = naturalSlabs.Select(s =>
            {
                naturalTTMap.TryGetValue(s.Id, out var tt);
                return MapToItem(s, tt, null);
            });

            var transferredItems = transferredTTs
                .Where(t => transferredSlabMap.ContainsKey(t.IdSlab))
                .Select(t => MapToItem(transferredSlabMap[t.IdSlab], t, null));

            return naturalItems.Concat(transferredItems)
                .OrderBy(x => x.MayDuc)
                .ThenBy(x => x.IDSlab);
        }

        // ── Chuyển phôi sang ca kề ────────────────────────────────────────────

        public async Task<int> ChuyenPhoiAsync(List<int> idSlabs, Guid idPhieuNguon, string huong, int nguoiChuyen)
        {
            var phieuNguon = await _context.BmPhieus
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieuNguon && p.MaBm == MaBm)
                ?? throw new InvalidOperationException("Phiếu nguồn không tồn tại");

            var ngaySXNguon = phieuNguon.NgaySX
                ?? throw new InvalidOperationException("Phiếu nguồn chưa có ngày SX");
            var caNguon = phieuNguon.Ca
                ?? throw new InvalidOperationException("Phiếu nguồn chưa có ca");

            var (ngaySXDich, caDich) = TinhCaDich(ngaySXNguon, caNguon, huong);

            var phieuDich = await _context.BmPhieus
                .FirstOrDefaultAsync(p => p.MaBm == MaBm
                                          && p.NgaySX == ngaySXDich
                                          && p.Ca == caDich
                                          && (p.IsDelete == null || p.IsDelete == 0))
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy phiếu Ca {caDich} ngày {ngaySXDich:dd/MM/yyyy}. Vui lòng tạo phiếu trước.");

            if (phieuDich.TinhTrang == 5 || phieuDich.IsLock == 1)
                throw new InvalidOperationException("Phiếu đích đã chốt, không thể chuyển phôi vào.");

            var idPhieuDich = phieuDich.Idphieu;
            var now = DateTime.Now;

            var slabs = await _context.Hrc1Slabs
                .Where(s => idSlabs.Contains(s.Id))
                .ToListAsync();

            var trangThaiMap = await _context.Hrc1SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToDictionaryAsync(t => t.IdSlab);

            var affected = 0;
            foreach (var slab in slabs)
            {
                trangThaiMap.TryGetValue(slab.Id, out var tt);
                if (tt?.TrangThaiPKH == 1) continue;

                if (tt == null)
                {
                    _context.Hrc1SlabTrangThais.Add(new Hrc1SlabTrangThai
                    {
                        IdSlab = slab.Id,
                        IsChuyenCa = true,
                        IdPhieuBBSL = idPhieuDich,
                        IdPhieuGoc = idPhieuNguon,
                        NguoiChuyen = nguoiChuyen,
                        NgayChuyen = now,
                        NgayTao = now,
                    });
                }
                else if (tt.IdPhieuGoc == idPhieuDich)
                {
                    // Chuyển ngược về phiếu gốc → reset IsChuyenCa
                    tt.IsChuyenCa = false;
                    tt.IdPhieuBBSL = null;
                    tt.IdPhieuGoc = null;
                    tt.NguoiChuyen = null;
                    tt.NgayChuyen = null;
                }
                else
                {
                    tt.IsChuyenCa = true;
                    tt.IdPhieuBBSL = idPhieuDich;
                    tt.IdPhieuGoc = idPhieuNguon;
                    tt.NguoiChuyen = nguoiChuyen;
                    tt.NgayChuyen = now;
                }
                affected++;
            }

            await _context.SaveChangesAsync();
            return affected;
        }

        private static (DateOnly ngaySXDich, int caDich) TinhCaDich(DateOnly ngaySX, int ca, string huong)
        {
            // Ca 1 (08h-20h): trước = Ca2 ngày trước; sau = Ca2 cùng ngày
            // Ca 2 (20h-08h): trước = Ca1 cùng ngày; sau = Ca1 ngày hôm sau
            return (ca, huong) switch
            {
                (1, "truoc") => (ngaySX.AddDays(-1), 2),
                (1, "sau")   => (ngaySX, 2),
                (2, "truoc") => (ngaySX, 1),
                (2, "sau")   => (ngaySX.AddDays(1), 1),
                _ => throw new ArgumentException($"Ca {ca} hoặc hướng '{huong}' không hợp lệ")
            };
        }

        // ── Cán Tấm: Xác nhận ────────────────────────────────────────────────

        public async Task XacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien)
        {
            var slabs = await _context.Hrc1Slabs
                .Where(s => idSlabs.Contains(s.Id))
                .ToListAsync();

            var trangThaiMap = await _context.Hrc1SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToDictionaryAsync(t => t.IdSlab);

            var now = DateTime.Now;
            foreach (var slab in slabs)
            {
                trangThaiMap.TryGetValue(slab.Id, out var tt);
                if (tt?.TrangThaiPKH == 1) continue;

                if (tt == null)
                {
                    var newTt = new Hrc1SlabTrangThai { IdSlab = slab.Id, NgayTao = now };
                    if (loaiXacNhan == "Duc")
                    {
                        newTt.TrangThaiDuc = 1;
                        newTt.NguoiXacNhanDuc = nguoiThucHien;
                        newTt.NgayXacNhanDuc = now;
                    }
                    else if (loaiXacNhan == "Kho")
                    {
                        newTt.TrangThaiKho = 1;
                        newTt.NguoiXacNhanKho = nguoiThucHien;
                        newTt.NgayXacNhanKho = now;
                    }
                    _context.Hrc1SlabTrangThais.Add(newTt);
                }
                else
                {
                    if (loaiXacNhan == "Duc")
                    {
                        tt.TrangThaiDuc = 1;
                        tt.NguoiXacNhanDuc = nguoiThucHien;
                        tt.NgayXacNhanDuc = now;
                    }
                    else if (loaiXacNhan == "Kho")
                    {
                        tt.TrangThaiKho = 1;
                        tt.NguoiXacNhanKho = nguoiThucHien;
                        tt.NgayXacNhanKho = now;
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        // ── Cán Tấm: Hủy xác nhận ────────────────────────────────────────────

        public async Task HuyXacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien)
        {
            var records = await _context.Hrc1SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab) && t.TrangThaiPKH == 0)
                .ToListAsync();

            foreach (var t in records)
            {
                if (loaiXacNhan == "Duc")
                {
                    t.TrangThaiDuc = 0;
                    t.NguoiXacNhanDuc = null;
                    t.NgayXacNhanDuc = null;
                }
                else if (loaiXacNhan == "Kho")
                {
                    t.TrangThaiKho = 0;
                    t.NguoiXacNhanKho = null;
                    t.NgayXacNhanKho = null;
                }
            }

            await _context.SaveChangesAsync();
        }

        // ── PKH: Chốt phiếu ──────────────────────────────────────────────────

        public async Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien)
        {
            var phieu = await _context.BmPhieus.FindAsync(idPhieu)
                ?? throw new InvalidOperationException("Phiếu không tồn tại");

            var caStr = phieu.Ca?.ToString();
            var ngaySX = phieu.NgaySX;
            var now = DateTime.Now;

            // Natural slabs (chưa bị chuyền đi)
            var naturalSlabs = await _context.Hrc1Slabs
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr
                            && !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.IsChuyenCa))
                .ToListAsync();

            var naturalIds = naturalSlabs.Select(s => s.Id).ToList();
            var naturalTTMap = naturalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .Where(t => naturalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            // Transferred-in slabs
            var transferredRecords = await _context.Hrc1SlabTrangThais
                .Where(t => t.IsChuyenCa && t.IdPhieuBBSL == idPhieu)
                .ToListAsync();

            foreach (var slab in naturalSlabs)
            {
                if (!naturalTTMap.TryGetValue(slab.Id, out var tt))
                {
                    _context.Hrc1SlabTrangThais.Add(new Hrc1SlabTrangThai
                    {
                        IdSlab = slab.Id,
                        TrangThaiPKH = 1,
                        NguoiChotPKH = nguoiThucHien,
                        NgayChotPKH = now,
                        NgayTao = now,
                    });
                }
                else
                {
                    tt.TrangThaiPKH = 1;
                    tt.NguoiChotPKH = nguoiThucHien;
                    tt.NgayChotPKH = now;
                }
            }

            foreach (var t in transferredRecords)
            {
                t.TrangThaiPKH = 1;
                t.NguoiChotPKH = nguoiThucHien;
                t.NgayChotPKH = now;
            }

            phieu.TinhTrang = 5;
            phieu.IsLock = 1;

            await _context.SaveChangesAsync();
        }

        // ── PKH: Hủy chốt phiếu ──────────────────────────────────────────────

        public async Task HuyChotPhieuAsync(Guid idPhieu, int nguoiThucHien)
        {
            var phieu = await _context.BmPhieus.FindAsync(idPhieu)
                ?? throw new InvalidOperationException("Phiếu không tồn tại");

            var caStr = phieu.Ca?.ToString();
            var ngaySX = phieu.NgaySX;

            // Natural slabs: lấy Id trước, rồi load TrangThai
            var naturalSlabIds = await _context.Hrc1Slabs
                .AsNoTracking()
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr)
                .Select(s => s.Id)
                .ToListAsync();

            var naturalTrangThais = naturalSlabIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .Where(t => !t.IsChuyenCa && naturalSlabIds.Contains(t.IdSlab))
                    .ToListAsync()
                : [];

            // Transferred-in
            var transferredRecords = await _context.Hrc1SlabTrangThais
                .Where(t => t.IsChuyenCa && t.IdPhieuBBSL == idPhieu)
                .ToListAsync();

            foreach (var t in naturalTrangThais.Concat(transferredRecords))
            {
                t.TrangThaiPKH = 0;
                t.NguoiChotPKH = null;
                t.NgayChotPKH = null;
            }

            phieu.TinhTrang = 1;
            phieu.IsLock = 0;

            await _context.SaveChangesAsync();
        }

        // ── Fill MacThep ──────────────────────────────────────────────────────

        public async Task<int> FillMacThepAsync()
        {
            var slabs = await _context.Hrc1Slabs
                .Where(s => s.MacThep == null && s.MaMe != null)
                .ToListAsync();

            if (slabs.Count == 0) return 0;

            var maMes = slabs.Select(s => s.MaMe!).Distinct().ToList();
            var macThepMap = await _syncPhanLoai.GetMacThepMapAsync(maMes);

            if (macThepMap.Count == 0) return 0;

            int updated = 0;
            var now = DateTime.Now;
            foreach (var slab in slabs)
            {
                if (macThepMap.TryGetValue(slab.MaMe!, out var macThep))
                {
                    slab.MacThep = macThep;
                    slab.NgayCapNhat = now;
                    updated++;
                }
            }

            if (updated > 0)
                await _context.SaveChangesAsync();

            return updated;
        }

        // ── Update GhiChu / MaVatTu ──────────────────────────────────────────

        public async Task UpdateSlabAsync(int id, Hrc1SlabUpdateRequest req)
        {
            var slab = await _context.Hrc1Slabs.FindAsync(id)
                ?? throw new InvalidOperationException($"Slab {id} không tồn tại.");
            slab.GhiChu = req.GhiChu;
            slab.MaVatTu = req.MaVatTu;
            slab.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task<int> BulkUpdateMaVatTuAsync(Hrc1BulkUpdateMaVatTuRequest req)
        {
            if (req.Ids == null || req.Ids.Count == 0) return 0;
            var slabs = await _context.Hrc1Slabs
                .Where(s => req.Ids.Contains(s.Id))
                .ToListAsync();
            var now = DateTime.Now;
            foreach (var slab in slabs)
            {
                slab.MaVatTu = req.MaVatTu;
                slab.NgayCapNhat = now;
            }
            await _context.SaveChangesAsync();
            return slabs.Count;
        }

        // ── Tổng hợp ghi chú ─────────────────────────────────────────────────

        public async Task<IEnumerable<Hrc1TongHopGhiChuItem>> GetTongHopGhiChuAsync(Guid idPhieu)
        {
            return await _context.Hrc1BbslTongHopGhiChus
                .AsNoTracking()
                .Where(x => x.IdPhieuBBSL == idPhieu)
                .Select(x => new Hrc1TongHopGhiChuItem
                {
                    MacThep   = x.MacThep,
                    KichThuoc = x.KichThuoc,
                    GhiChu    = x.GhiChu,
                })
                .ToListAsync();
        }

        public async Task SaveTongHopGhiChuAsync(Hrc1SaveTongHopGhiChuRequest req)
        {
            var existing = await _context.Hrc1BbslTongHopGhiChus
                .FirstOrDefaultAsync(x =>
                    x.IdPhieuBBSL == req.IdPhieuBBSL &&
                    x.MacThep == req.MacThep &&
                    x.KichThuoc == req.KichThuoc);

            if (existing == null)
            {
                _context.Hrc1BbslTongHopGhiChus.Add(new Hrc1BbslTongHopGhiChu
                {
                    IdPhieuBBSL = req.IdPhieuBBSL,
                    MacThep     = req.MacThep,
                    KichThuoc   = req.KichThuoc,
                    GhiChu      = req.GhiChu,
                    NgayCapNhat = DateTime.Now,
                });
            }
            else
            {
                existing.GhiChu      = req.GhiChu;
                existing.NgayCapNhat = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        // ── Helper: load tất cả slab thuộc phiếu ─────────────────────────────

        internal async Task<List<Hrc1Slab>> LoadPhieuSlabsAsync(BmPhieu phieu)
        {
            var caStr = phieu.Ca?.ToString();
            var ngaySX = phieu.NgaySX;

            var natural = await _context.Hrc1Slabs
                .AsNoTracking()
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr
                            && !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.IsChuyenCa))
                .OrderBy(s => s.MayDuc).ThenBy(s => s.IDSlab)
                .ToListAsync();

            var transferredSlabIds = await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => t.IsChuyenCa && t.IdPhieuBBSL == phieu.Idphieu)
                .Select(t => t.IdSlab)
                .ToListAsync();

            var transferred = transferredSlabIds.Count > 0
                ? await _context.Hrc1Slabs
                    .AsNoTracking()
                    .Where(s => transferredSlabIds.Contains(s.Id))
                    .OrderBy(s => s.MayDuc).ThenBy(s => s.IDSlab)
                    .ToListAsync()
                : [];

            return natural.Concat(transferred).ToList();
        }

        // ── Helper: map model → DTO ───────────────────────────────────────────

        private static Hrc1SlabItem MapToItem(Hrc1Slab s, Hrc1SlabTrangThai? tt, BmPhieu? phieu)
        {
            return new Hrc1SlabItem
            {
                Id = s.Id,
                IDSlab = s.IDSlab,
                IDPiece = s.IDPiece,
                MaMe = s.MaMe,
                MacThep = s.MacThep,
                NgaySX = s.NgaySX,
                CaSX = s.CaSX,
                KipSX = s.KipSX,
                MayDuc = s.MayDuc,
                CutDate = s.CutDate,
                ChieuDay = s.ChieuDay,
                ChieuRong = s.ChieuRong,
                ChieuDai = s.ChieuDai,
                KhoiLuong = s.KhoiLuong,
                NgayTao = s.NgayTao,
                NgayCapNhat = s.NgayCapNhat,
                GhiChu = s.GhiChu,
                MaVatTu = s.MaVatTu,
                IsChuyenCa = tt?.IsChuyenCa ?? false,
                IdPhieuGoc = tt?.IdPhieuGoc,
                TrangThaiDuc = tt?.TrangThaiDuc ?? 0,
                TrangThaiKho = tt?.TrangThaiKho ?? 0,
                TrangThaiPKH = tt?.TrangThaiPKH ?? 0,
                IdPhieuBBSL = tt?.IdPhieuBBSL,
                SoPhieuBBSL = phieu?.SoPhieu,
                NgayXuLy = phieu?.NgaySX,
                CaBBSL = phieu?.Ca,
                KipBBSL = phieu?.Kip,
            };
        }
    }
}
