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
        private const string MaBm = "HRC1_BBSL_PhoiTam";
        private const string NhaMayHrc1 = "HRC1";

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

            // Load TrangThai riêng để kiểm tra guard (CutDate + TrangThaiCan + TrangThaiC4 + TrangThaiPKH)
            var existingInternalIds = existing.Values.Select(s => s.Id).ToList();
            var trangThaiMap = existingInternalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .Where(t => existingInternalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            // Slab đã qua "Sửa slab thủ công" (có record Hrc1SlabEdit) → khóa vĩnh viễn khỏi ghi đè
            // Sync cho MỌI field dữ liệu (IDPiece/MaMe/MacThep/MayDuc/CutDate/ChieuDay/Rong/Dai/
            // KhoiLuong), không chỉ IDSlab — user đã tự tay xác nhận dữ liệu đúng thì Sync không
            // được ghi đè lại nữa, kể cả khi slab chưa cắt xong (CutDate vẫn null).
            var manualEditedIds = existingInternalIds.Count > 0
                ? (await _context.Hrc1SlabEdits
                    .Where(e => existingInternalIds.Contains(e.IdSlab))
                    .Select(e => e.IdSlab)
                    .ToListAsync()).ToHashSet()
                : [];

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
                    // IsDeleted: slab đã bị xóa mềm thủ công — Sync KHÔNG được ghi đè / hồi sinh,
                    // dù TSC luôn trả về đủ slab của ca (xem comment trên Hrc1Slab.IsDeleted).
                    if (slab.IsDeleted
                        || slab.CutDate.HasValue
                        || tt?.TrangThaiCan == 1
                        || tt?.TrangThaiC4 == true
                        || tt?.TrangThaiPKH == 1
                        || manualEditedIds.Contains(slab.Id)) continue;

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
            var query = _context.Hrc1Slabs.AsNoTracking().Where(s => !s.IsDeleted);

            if (req.TuNgay.HasValue)  query = query.Where(s => s.NgaySX >= req.TuNgay);
            if (req.DenNgay.HasValue) query = query.Where(s => s.NgaySX <= req.DenNgay);
            if (!string.IsNullOrEmpty(req.CaSX))    query = query.Where(s => s.CaSX == req.CaSX);
            if (!string.IsNullOrEmpty(req.KipSX))   query = query.Where(s => s.KipSX == req.KipSX);
            if (!string.IsNullOrEmpty(req.MayDuc))  query = query.Where(s => s.MayDuc == req.MayDuc);
            if (!string.IsNullOrEmpty(req.MaMe))    query = query.Where(s => s.MaMe!.Contains(req.MaMe));
            if (!string.IsNullOrEmpty(req.IDSlab))
            {
                // So khớp theo IDSlab hiệu lực (đã sửa tay nếu có) — xem Hrc1SlabEdit.
                var idSlabTerm = req.IDSlab;
                query = query.Where(s =>
                    (_context.Hrc1SlabEdits.Where(e => e.IdSlab == s.Id).Select(e => e.IDSlab).FirstOrDefault() ?? s.IDSlab)
                        .Contains(idSlabTerm));
            }
            if (!string.IsNullOrEmpty(req.MacThep)) query = query.Where(s => s.MacThep!.Contains(req.MacThep));
            if (req.IsChot.HasValue)
                query = req.IsChot.Value
                    ? query.Where(s => s.CutDate != null)
                    : query.Where(s => s.CutDate == null);

            if (req.TrangThaiDuc.HasValue)
                query = req.TrangThaiDuc == 0
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiDuc == 1))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiDuc == req.TrangThaiDuc));
            if (req.TrangThaiCan.HasValue)
                query = req.TrangThaiCan == 0
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiCan == 1))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiCan == req.TrangThaiCan));
            if (req.TrangThaiC4.HasValue)
                query = !req.TrangThaiC4.Value
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiC4))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiC4));
            if (req.TrangThaiPKH.HasValue)
                query = req.TrangThaiPKH == 0
                    ? query.Where(s => !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiPKH == 1))
                    : query.Where(s => _context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.TrangThaiPKH == req.TrangThaiPKH));

            var total = await query.CountAsync();

            var slabs = await query
                .OrderByDescending(s => s.NgaySX)
                .ThenByDescending(s => s.CaSX)
                .ThenBy(s => s.MayDuc)
                .ThenBy(s => _context.Hrc1SlabEdits.Where(e => e.IdSlab == s.Id).Select(e => e.IDSlab).FirstOrDefault() ?? s.IDSlab)
                .Skip((req.Page - 1) * req.PageSize)
                .Take(req.PageSize)
                .ToListAsync();

            // Overlay IDSlab đã sửa tay lên object in-memory (AsNoTracking, không ảnh hưởng DB) để
            // MapToItem bên dưới trả về đúng giá trị hiệu lực.
            await ApplyIdSlabOverridesAsync(slabs);

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

            var maVatTuMap = await GetMaVatTuLookupAsync(slabs.Select(s => s.MacThep));
            var manualEditedIds = await GetManualEditedIdsAsync(slabInternalIds);

            var items = slabs.Select(s =>
            {
                ttMap.TryGetValue(s.Id, out var tt);
                BmPhieu? phieu = null;
                if (tt?.IdPhieuBBSL != null) phieuMap.TryGetValue(tt.IdPhieuBBSL.Value, out phieu);
                maVatTuMap.TryGetValue(s.MacThep ?? "", out var mvt);
                return MapToItem(s, tt, phieu, ResolveMaVatTu(s, tt, mvt), mvt?.TenVatTu, manualEditedIds.Contains(s.Id));
            });

            return (items, total);
        }

        // ── Tổng hợp (GROUP BY) ───────────────────────────────────────────────

        public async Task<IEnumerable<Hrc1SlabTongHopItem>> GetTongHopAsync(
            DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip)
        {
            var query = _context.Hrc1Slabs.AsNoTracking().Where(s => !s.IsDeleted);

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
                .Where(s => ngaySXList.Contains(s.NgaySX!.Value) && !s.IsDeleted)
                .ToListAsync();

            var naturalIds = allNatural.Select(s => s.Id).ToList();
            var naturalTTMap = naturalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .AsNoTracking()
                    .Where(t => naturalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            var phieuIds = phieus.Select(p => p.Idphieu).ToList();
            // Join Hrc1Slabs để loại slab đã xóa mềm — TrangThai không tự biết slab bị xóa.
            var transferredIn = await (
                from t in _context.Hrc1SlabTrangThais.AsNoTracking()
                join s in _context.Hrc1Slabs.AsNoTracking() on t.IdSlab equals s.Id
                where t.IsChuyenCa && t.IdPhieuBBSL != null && phieuIds.Contains(t.IdPhieuBBSL!.Value) && !s.IsDeleted
                select t
            ).ToListAsync();
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
                var kho = naturalSlabs.Count(s => naturalTTMap.TryGetValue(s.Id, out var tt) && tt.TrangThaiCan == 1)
                        + transferred.Count(t => t.TrangThaiCan == 1);
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
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr && !s.IsDeleted
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
                    .Where(s => transferredSlabIds.Contains(s.Id) && !s.IsDeleted)
                    .ToDictionaryAsync(s => s.Id)
                : new Dictionary<int, Hrc1Slab>();

            // Overlay IDSlab đã sửa tay lên object in-memory (xem Hrc1SlabEdit) trước khi map DTO.
            await ApplyIdSlabOverridesAsync(naturalSlabs.Concat(transferredSlabMap.Values));

            var maVatTuMap = await GetMaVatTuLookupAsync(
                naturalSlabs.Select(s => s.MacThep).Concat(transferredSlabMap.Values.Select(s => s.MacThep)));

            var manualEditedIds = await GetManualEditedIdsAsync(
                naturalSlabs.Select(s => s.Id).Concat(transferredSlabMap.Values.Select(s => s.Id)));

            var naturalItems = naturalSlabs.Select(s =>
            {
                naturalTTMap.TryGetValue(s.Id, out var tt);
                maVatTuMap.TryGetValue(s.MacThep ?? "", out var mvt);
                return MapToItem(s, tt, null, ResolveMaVatTu(s, tt, mvt), mvt?.TenVatTu, manualEditedIds.Contains(s.Id));
            });

            var transferredItems = transferredTTs
                .Where(t => transferredSlabMap.ContainsKey(t.IdSlab))
                .Select(t =>
                {
                    var s = transferredSlabMap[t.IdSlab];
                    maVatTuMap.TryGetValue(s.MacThep ?? "", out var mvt);
                    return MapToItem(s, t, null, ResolveMaVatTu(s, t, mvt), mvt?.TenVatTu, manualEditedIds.Contains(s.Id));
                });

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
                .Where(s => idSlabs.Contains(s.Id) && !s.IsDeleted)
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
                .Where(s => idSlabs.Contains(s.Id) && !s.IsDeleted)
                .ToListAsync();

            var trangThaiMap = await _context.Hrc1SlabTrangThais
                .Where(t => idSlabs.Contains(t.IdSlab))
                .ToDictionaryAsync(t => t.IdSlab);

            var now = DateTime.Now;
            // Đúc, Cán và C4 đồng cấp, xác nhận song song → snapshot MaVatTu chốt tại lần xác nhận ĐẦU TIÊN
            // (bên nào xác nhận trước), lần xác nhận còn lại không ghi đè snapshot đã có.
            var slabsFirstXacNhan = new List<Hrc1Slab>();
            foreach (var slab in slabs)
            {
                trangThaiMap.TryGetValue(slab.Id, out var tt);
                if (tt?.TrangThaiPKH == 1) continue;

                var isFirstXacNhan = tt == null || (tt.TrangThaiDuc != 1 && tt.TrangThaiCan != 1 && !tt.TrangThaiC4);

                if (tt == null)
                {
                    var newTt = new Hrc1SlabTrangThai { IdSlab = slab.Id, NgayTao = now };
                    if (loaiXacNhan == "Duc")
                    {
                        newTt.TrangThaiDuc = 1;
                        newTt.NguoiXacNhanDuc = nguoiThucHien;
                        newTt.NgayXacNhanDuc = now;
                    }
                    else if (loaiXacNhan == "Can")
                    {
                        newTt.TrangThaiCan = 1;
                        newTt.NguoiXacNhanCan = nguoiThucHien;
                        newTt.NgayXacNhanCan = now;
                    }
                    else if (loaiXacNhan == "C4")
                    {
                        newTt.TrangThaiC4 = true;
                        newTt.NguoiXacNhanC4 = nguoiThucHien;
                        newTt.NgayXacNhanC4 = now;
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
                    else if (loaiXacNhan == "Can")
                    {
                        tt.TrangThaiCan = 1;
                        tt.NguoiXacNhanCan = nguoiThucHien;
                        tt.NgayXacNhanCan = now;
                    }
                    else if (loaiXacNhan == "C4")
                    {
                        tt.TrangThaiC4 = true;
                        tt.NguoiXacNhanC4 = nguoiThucHien;
                        tt.NgayXacNhanC4 = now;
                    }
                }

                if (isFirstXacNhan)
                    slabsFirstXacNhan.Add(slab);
            }

            // Chốt snapshot VatTuCode hiện tại (theo MacThep) vào HRC1_Slab.MaVatTu cho các slab vừa xác nhận lần đầu
            if (slabsFirstXacNhan.Count > 0)
                await FillMaVatTuForSlabsAsync(slabsFirstXacNhan, overwrite: true);

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
                else if (loaiXacNhan == "Can")
                {
                    t.TrangThaiCan = 0;
                    t.NguoiXacNhanCan = null;
                    t.NgayXacNhanCan = null;
                }
                else if (loaiXacNhan == "C4")
                {
                    t.TrangThaiC4 = false;
                    t.NguoiXacNhanC4 = null;
                    t.NgayXacNhanC4 = null;
                }
            }

            await _context.SaveChangesAsync();
        }

        // ── PKH: Chốt phiếu ──────────────────────────────────────────────────

        public async Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien)
        {
            var phieu = await _context.BmPhieus.FindAsync(idPhieu)
                ?? throw new InvalidOperationException("Phiếu không tồn tại");

            if (phieu.TinhTrang == 5)
                throw new InvalidOperationException("Phiếu đã được chốt trước đó.");

            var caStr = phieu.Ca?.ToString();
            var ngaySX = phieu.NgaySX;
            var now = DateTime.Now;

            // Natural slabs (chưa bị chuyền đi)
            var naturalSlabs = await _context.Hrc1Slabs
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr && !s.IsDeleted
                            && !_context.Hrc1SlabTrangThais.Any(t => t.IdSlab == s.Id && t.IsChuyenCa))
                .ToListAsync();

            var naturalIds = naturalSlabs.Select(s => s.Id).ToList();
            var naturalTTMap = naturalIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .Where(t => naturalIds.Contains(t.IdSlab))
                    .ToDictionaryAsync(t => t.IdSlab)
                : new Dictionary<int, Hrc1SlabTrangThai>();

            // Transferred-in slabs (join Hrc1Slabs để loại slab đã xóa mềm)
            var transferredRecords = await (
                from t in _context.Hrc1SlabTrangThais
                join s in _context.Hrc1Slabs on t.IdSlab equals s.Id
                where t.IsChuyenCa && t.IdPhieuBBSL == idPhieu && !s.IsDeleted
                select t
            ).ToListAsync();

            var chuaXacNhan = naturalSlabs.Count(s =>
                    !naturalTTMap.TryGetValue(s.Id, out var tt) || tt.TrangThaiDuc != 1 || tt.TrangThaiCan != 1 || !tt.TrangThaiC4)
                + transferredRecords.Count(t => t.TrangThaiDuc != 1 || t.TrangThaiCan != 1 || !t.TrangThaiC4);
            if (chuaXacNhan > 0)
                throw new InvalidOperationException(
                    $"Còn {chuaXacNhan} slab chưa được Đúc, Cán và C4 xác nhận đầy đủ, không thể chốt phiếu.");

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
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr && !s.IsDeleted)
                .Select(s => s.Id)
                .ToListAsync();

            var naturalTrangThais = naturalSlabIds.Count > 0
                ? await _context.Hrc1SlabTrangThais
                    .Where(t => !t.IsChuyenCa && naturalSlabIds.Contains(t.IdSlab))
                    .ToListAsync()
                : [];

            // Transferred-in (join Hrc1Slabs để loại slab đã xóa mềm)
            var transferredRecords = await (
                from t in _context.Hrc1SlabTrangThais
                join s in _context.Hrc1Slabs on t.IdSlab equals s.Id
                where t.IsChuyenCa && t.IdPhieuBBSL == idPhieu && !s.IsDeleted
                select t
            ).ToListAsync();

            foreach (var t in naturalTrangThais.Concat(transferredRecords))
            {
                t.TrangThaiPKH = 0;
                t.NguoiChotPKH = null;
                t.NgayChotPKH = null;
            }

            phieu.TinhTrang = 0;

            await _context.SaveChangesAsync();
        }

        // ── Fill MacThep ──────────────────────────────────────────────────────

        public async Task<int> FillMacThepAsync()
        {
            var slabs = await _context.Hrc1Slabs
                .Where(s => s.MacThep == null && s.MaMe != null && !s.IsDeleted)
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

        // ── MaVatTu: snapshot khi Đúc hoặc Cán xác nhận (đồng cấp), live-join khi cả 2 chưa xác nhận ───

        // Đã Đúc HOẶC Cán xác nhận → dùng snapshot đã lưu trên slab (lịch sử tại thời điểm xác nhận đầu tiên)
        // Cả 2 chưa xác nhận → lấy VatTuCode hiện tại từ bảng MaVatTu (theo MacThep) để hiển thị, không lưu lại
        private static string? ResolveMaVatTu(Hrc1Slab s, Hrc1SlabTrangThai? tt, MaVatTu? mvt)
            => (tt?.TrangThaiDuc == 1 || tt?.TrangThaiCan == 1 || tt?.TrangThaiC4 == true) ? s.MaVatTu : mvt?.VatTuCode;

        public async Task<Dictionary<string, string>> GetTenVatTuMapAsync(IEnumerable<string?> macTheps)
        {
            var map = await GetMaVatTuLookupAsync(macTheps);
            return map.ToDictionary(kv => kv.Key, kv => kv.Value.TenVatTu);
        }

        private async Task<Dictionary<string, MaVatTu>> GetMaVatTuLookupAsync(IEnumerable<string?> macThepList)
        {
            var names = macThepList.Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList()!;
            if (names.Count == 0) return new Dictionary<string, MaVatTu>();

            var rows = await _context.MaVatTus
                .AsNoTracking()
                .Where(x => x.NhaMay == NhaMayHrc1 && x.MacThep != null && names.Contains(x.MacThep))
                .ToListAsync();

            // GroupBy + First thay vì ToDictionaryAsync trực tiếp: phòng trường hợp dữ liệu cũ (trước khi
            // áp filtered unique index UX_MaVatTu_NhaMay_MacThep) vẫn còn 2 dòng trùng MacThep, tránh crash.
            return rows
                .GroupBy(x => x.MacThep!)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private async Task<int> FillMaVatTuForSlabsAsync(List<Hrc1Slab> slabs, bool overwrite)
        {
            var macTheps = slabs.Where(s => !string.IsNullOrEmpty(s.MacThep)).Select(s => s.MacThep!).Distinct().ToList();
            if (macTheps.Count == 0) return 0;

            var map = await GetMaVatTuLookupAsync(macTheps);
            if (map.Count == 0) return 0;

            int updated = 0;
            var now = DateTime.Now;
            foreach (var slab in slabs)
            {
                if (string.IsNullOrEmpty(slab.MacThep)) continue;
                if (!overwrite && slab.MaVatTu != null) continue;
                if (!map.TryGetValue(slab.MacThep, out var mvt)) continue;

                slab.MaVatTu = mvt.VatTuCode;
                slab.NgayCapNhat = now;
                updated++;
            }
            return updated;
        }

        // ── Update GhiChu / MaVatTu ──────────────────────────────────────────

        public async Task UpdateSlabAsync(int id, Hrc1SlabUpdateRequest req)
        {
            var slab = await _context.Hrc1Slabs.FindAsync(id)
                ?? throw new InvalidOperationException($"Slab {id} không tồn tại.");
            if (slab.IsDeleted)
                throw new InvalidOperationException("Slab đã bị xóa, không thể chỉnh sửa.");

            var tt = await _context.Hrc1SlabTrangThais.FirstOrDefaultAsync(t => t.IdSlab == id);
            if (tt?.TrangThaiPKH == 1)
                throw new InvalidOperationException("Slab thuộc phiếu đã chốt, không thể chỉnh sửa.");

            slab.GhiChu = req.GhiChu;
            slab.MaVatTu = req.MaVatTu;
            slab.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        // ── Thêm mới slab thủ công (tab "Chi tiết slab") ──────────────────────
        // NgaySX/CaSX/KipSX lấy từ phiếu (không nhận từ client) để slab mới khớp đúng phiếu
        // đang xem — cùng điều kiện match "slab tự nhiên" dùng trong GetSlabsByPhieuAsync.
        public async Task<Hrc1SlabItem> CreateSlabAsync(Hrc1SlabCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.IDSlab))
                throw new InvalidOperationException("ID Slab không được để trống.");

            var phieu = await _context.BmPhieus
                .FirstOrDefaultAsync(p => p.Idphieu == req.IdPhieu && p.MaBm == MaBm)
                ?? throw new InvalidOperationException("Phiếu không tồn tại.");

            if (phieu.TinhTrang == 5)
                throw new InvalidOperationException("Phiếu đã chốt, không thể thêm slab mới.");

            var idSlab = req.IDSlab.Trim();
            if (await IsIDSlabTrungAsync(idSlab, excludeId: null))
                throw new InvalidOperationException($"ID Slab '{idSlab}' đã tồn tại.");

            var slab = new Hrc1Slab
            {
                IDSlab = idSlab,
                IDPiece = req.IDPiece,
                MaMe = req.MaMe,
                MacThep = req.MacThep,
                NgaySX = phieu.NgaySX,
                CaSX = phieu.Ca?.ToString(),
                KipSX = phieu.Kip,
                MayDuc = req.MayDuc,
                CutDate = req.CutDate,
                ChieuDay = req.ChieuDay,
                ChieuRong = req.ChieuRong,
                ChieuDai = req.ChieuDai,
                KhoiLuong = req.KhoiLuong,
            };
            _context.Hrc1Slabs.Add(slab);
            await _context.SaveChangesAsync();

            var mvt = string.IsNullOrEmpty(slab.MacThep)
                ? null
                : (await GetMaVatTuLookupAsync([slab.MacThep])).GetValueOrDefault(slab.MacThep);

            return MapToItem(slab, null, phieu, ResolveMaVatTu(slab, null, mvt), mvt?.TenVatTu);
        }

        // ── Sửa slab thủ công (tab "Chi tiết slab") ────────────────────────────
        // Full-replace toàn bộ field nhập tay (mirror CreateSlabAsync) khi user bấm "Sửa" trên popup —
        // khác UpdateSlabAsync ở trên (chỉ patch GhiChu/MaVatTu cho inline-edit, xem comment DTO).
        //
        // IDSlab KHÔNG ghi đè trực tiếp lên HRC1_Slab — lưu vào bảng phụ Hrc1SlabEdit (xem comment
        // trên class đó) để cột gốc luôn bất biến, không phá khóa đối chiếu của UpsertFromApiAsync.
        //
        // Record Hrc1SlabEdit LUÔN được giữ lại (kể cả khi IDSlab sửa về đúng giá trị gốc) — sự
        // tồn tại của nó còn đóng vai trò "đã qua sửa tay" để UpsertFromApiAsync khóa vĩnh viễn
        // KHÔNG ghi đè lại các field khác (IDPiece/MaMe/MacThep/MayDuc/CutDate/ChieuDay/Rong/Dai/
        // KhoiLuong) ở những lần Sync sau, kể cả khi slab chưa cắt xong/chưa xác nhận.
        public async Task EditSlabAsync(int id, Hrc1SlabEditRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.IDSlab))
                throw new InvalidOperationException("ID Slab không được để trống.");

            var slab = await _context.Hrc1Slabs.FindAsync(id)
                ?? throw new InvalidOperationException($"Slab {id} không tồn tại.");
            if (slab.IsDeleted)
                throw new InvalidOperationException("Slab đã bị xóa, không thể chỉnh sửa.");

            var tt = await _context.Hrc1SlabTrangThais.FirstOrDefaultAsync(t => t.IdSlab == id);
            if (tt?.TrangThaiPKH == 1)
                throw new InvalidOperationException("Slab thuộc phiếu đã chốt, không thể chỉnh sửa.");

            var idSlab = req.IDSlab.Trim();
            if (await IsIDSlabTrungAsync(idSlab, excludeId: id))
                throw new InvalidOperationException($"ID Slab '{idSlab}' đã tồn tại.");

            var edit = await _context.Hrc1SlabEdits.FirstOrDefaultAsync(e => e.IdSlab == id);
            if (edit == null)
            {
                _context.Hrc1SlabEdits.Add(new Hrc1SlabEdit { IdSlab = id, IDSlab = idSlab, NgayCapNhat = DateTime.Now });
            }
            else
            {
                edit.IDSlab = idSlab;
                edit.NgayCapNhat = DateTime.Now;
            }

            slab.IDPiece = req.IDPiece;
            slab.MaMe = req.MaMe;
            slab.MacThep = req.MacThep;
            slab.MayDuc = req.MayDuc;
            slab.CutDate = req.CutDate;
            slab.ChieuDay = req.ChieuDay;
            slab.ChieuRong = req.ChieuRong;
            slab.ChieuDai = req.ChieuDai;
            slab.KhoiLuong = req.KhoiLuong;
            slab.NgayCapNhat = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        // ── Xóa mềm slab ──────────────────────────────────────────────────────
        // Xóa cứng (DELETE) không dùng được: TSC luôn trả về đủ slab của ca nên SyncAsync sẽ
        // insert lại ngay lần làm mới kế tiếp. Xóa mềm + guard IsDeleted trong UpsertFromApiAsync
        // là cách duy nhất giữ được trạng thái "đã loại bỏ" qua các lần Sync.
        public async Task<int> DeleteSlabsAsync(List<int> idSlabs, int nguoiThucHien)
        {
            var slabs = await _context.Hrc1Slabs
                .Where(s => idSlabs.Contains(s.Id) && !s.IsDeleted)
                .ToListAsync();
            if (slabs.Count == 0) return 0;

            var slabIds = slabs.Select(s => s.Id).ToList();
            var trangThaiMap = await _context.Hrc1SlabTrangThais
                .Where(t => slabIds.Contains(t.IdSlab))
                .ToDictionaryAsync(t => t.IdSlab);

            var daChot = slabs.Count(s => trangThaiMap.TryGetValue(s.Id, out var tt) && tt.TrangThaiPKH == 1);
            if (daChot > 0)
                throw new InvalidOperationException($"Có {daChot} slab đã chốt PKH, không thể xóa.");

            var now = DateTime.Now;
            foreach (var slab in slabs)
            {
                slab.IsDeleted = true;
                slab.NguoiXoa = nguoiThucHien;
                slab.NgayXoa = now;
                slab.NgayCapNhat = now;
            }

            await _context.SaveChangesAsync();
            return slabs.Count;
        }

        // ── Khôi phục slab đã xóa mềm ─────────────────────────────────────────
        public async Task<int> RestoreSlabsAsync(List<int> idSlabs, int nguoiThucHien)
        {
            var slabs = await _context.Hrc1Slabs
                .Where(s => idSlabs.Contains(s.Id) && s.IsDeleted)
                .ToListAsync();
            if (slabs.Count == 0) return 0;

            var now = DateTime.Now;
            foreach (var slab in slabs)
            {
                slab.IsDeleted = false;
                slab.NguoiXoa = null;
                slab.NgayXoa = null;
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
                    MacThep = x.MacThep,
                    MaVatTu = x.MaVatTu,
                    GhiChu  = x.GhiChu,
                })
                .ToListAsync();
        }

        public async Task SaveTongHopGhiChuAsync(Hrc1SaveTongHopGhiChuRequest req)
        {
            var existing = await _context.Hrc1BbslTongHopGhiChus
                .FirstOrDefaultAsync(x =>
                    x.IdPhieuBBSL == req.IdPhieuBBSL &&
                    x.MacThep == req.MacThep &&
                    x.MaVatTu == req.MaVatTu);

            if (existing == null)
            {
                _context.Hrc1BbslTongHopGhiChus.Add(new Hrc1BbslTongHopGhiChu
                {
                    IdPhieuBBSL = req.IdPhieuBBSL,
                    MacThep     = req.MacThep,
                    MaVatTu     = req.MaVatTu,
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
                .Where(s => s.NgaySX == ngaySX && s.CaSX == caStr && !s.IsDeleted
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
                    .Where(s => transferredSlabIds.Contains(s.Id) && !s.IsDeleted)
                    .OrderBy(s => s.MayDuc).ThenBy(s => s.IDSlab)
                    .ToListAsync()
                : [];

            var all = natural.Concat(transferred).ToList();
            // Overlay IDSlab đã sửa tay (xem Hrc1SlabEdit) — dùng cho export Excel/PDF đọc trực
            // tiếp entity này. Thứ tự sort ở trên vẫn theo IDSlab gốc (không ảnh hưởng GetRuotPhieuAsync
            // vì hàm đó GROUP BY MaMe/MacThep, không theo IDSlab).
            await ApplyIdSlabOverridesAsync(all);
            return all;
        }

        // ── Helper: IDSlab đã sửa tay (Hrc1SlabEdit) ──────────────────────────

        // Ghi đè IDSlab trong bộ nhớ (các slab truyền vào phải AsNoTracking, không ảnh hưởng DB)
        // bằng giá trị đã sửa tay nếu có — mọi nơi hiển thị/xuất dữ liệu gọi hàm này ngay sau khi
        // load Hrc1Slab để luôn thấy đúng giá trị hiệu lực, trong khi cột gốc trên HRC1_Slab không
        // đổi (giữ đúng khóa đối chiếu cho UpsertFromApiAsync/Sync — xem comment Hrc1SlabEdit).
        private async Task ApplyIdSlabOverridesAsync(IEnumerable<Hrc1Slab> slabs)
        {
            var ids = slabs.Select(s => s.Id).Distinct().ToList();
            if (ids.Count == 0) return;

            var overrides = await _context.Hrc1SlabEdits
                .AsNoTracking()
                .Where(e => ids.Contains(e.IdSlab))
                .ToDictionaryAsync(e => e.IdSlab, e => e.IDSlab);
            if (overrides.Count == 0) return;

            foreach (var s in slabs)
                if (overrides.TryGetValue(s.Id, out var idSlab))
                    s.IDSlab = idSlab;
        }

        // Trả về tập Id (nội bộ) các slab đã qua "Sửa slab thủ công" (có record Hrc1SlabEdit) —
        // dùng để set Hrc1SlabItem.IsManualEdited cho FE highlight dòng.
        private async Task<HashSet<int>> GetManualEditedIdsAsync(IEnumerable<int> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0) return [];
            return (await _context.Hrc1SlabEdits
                .AsNoTracking()
                .Where(e => idList.Contains(e.IdSlab))
                .Select(e => e.IdSlab)
                .ToListAsync()).ToHashSet();
        }

        // Kiểm tra idSlab có trùng với IDSlab hiệu lực (đã sửa tay nếu có, không thì lấy gốc) của
        // một slab KHÁC hay không — dùng cho validate ở CreateSlabAsync (excludeId: null) và
        // EditSlabAsync (excludeId: id đang sửa).
        private async Task<bool> IsIDSlabTrungAsync(string idSlab, int? excludeId)
        {
            var trungOverride = await _context.Hrc1SlabEdits
                .AnyAsync(e => e.IDSlab == idSlab && (excludeId == null || e.IdSlab != excludeId));
            if (trungOverride) return true;

            var overriddenIds = await _context.Hrc1SlabEdits.Select(e => e.IdSlab).ToListAsync();
            return await _context.Hrc1Slabs.AnyAsync(s =>
                s.IDSlab == idSlab
                && !overriddenIds.Contains(s.Id)
                && (excludeId == null || s.Id != excludeId));
        }

        // ── Helper: map model → DTO ───────────────────────────────────────────

        private static Hrc1SlabItem MapToItem(Hrc1Slab s, Hrc1SlabTrangThai? tt, BmPhieu? phieu, string? maVatTu, string? tenVatTu, bool isManualEdited = false)
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
                MaVatTu = maVatTu,
                TenVatTu = tenVatTu,
                IsManualEdited = isManualEdited,
                IsChuyenCa = tt?.IsChuyenCa ?? false,
                IdPhieuGoc = tt?.IdPhieuGoc,
                TrangThaiDuc = tt?.TrangThaiDuc ?? 0,
                TrangThaiCan = tt?.TrangThaiCan ?? 0,
                TrangThaiC4 = tt?.TrangThaiC4 ?? false,
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
