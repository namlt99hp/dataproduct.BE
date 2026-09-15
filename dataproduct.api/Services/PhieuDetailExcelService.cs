using ClosedXML.Excel;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.ResponseModels;
using dataproduct.api.Utils;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using PaperKind = DinkToPdf.PaperKind;

namespace dataproduct.api.Services
{
    public class PhieuDetailExcelService
    {
        private readonly ProductFormContext _context;
        private readonly IConverter _pdfConverter;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly PheDuyetService _pheDuyetService;
        private readonly BmConfigService _bmConfig;

        public PhieuDetailExcelService(ProductFormContext context, PheDuyetService pheDuyetService, IConverter pdfConverter, IConfiguration configuration, IWebHostEnvironment env, BmConfigService bmConfig)
        {
            _context       = context;
            _pdfConverter  = pdfConverter;
            _configuration = configuration;
            _env           = env;
            _pheDuyetService = pheDuyetService;
            _bmConfig      = bmConfig;
        }

        public async Task<BmPhieu> GetBmPhieuByIdOrThrowAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == idPhieu);
            if (phieu == null) throw new Exception($"Không tìm thấy phiếu với IdPhieu='{idPhieu}'.");
            return phieu;
        }

        public async Task<BmPhieu?> GetBmPhieuByMaBmNgayCaScopeAsync(string maBm, DateOnly ngay, int ca, int scope)
        {
            return await _context.BmPhieus
                .Where(x => x.MaBm == maBm
                         && x.NgaySX == ngay
                         && x.Ca == ca
                         && x.Scope == scope
                         && x.IsDelete != 1)
                .OrderByDescending(x => x.NgayTao)
                .FirstOrDefaultAsync();
        }

        // -------------------------------------------------------
        // Column position helpers
        // -------------------------------------------------------

        /// <summary>
        /// Cột bắt đầu của phụ liệu động (1-based):
        /// BOF = 6 (F), LF / RH = 5 (E).
        /// </summary>
        public static int GetPhuLieuStartCol(string bieuMau) =>
            bieuMau.Contains("BOF", StringComparison.OrdinalIgnoreCase) ? 6 : 5;

        /// <summary>ColIndex (1-based) của header thứ <paramref name="zeroBasedIndex"/> trong danh sách đã sort.</summary>
        public static int GetPhuLieuColIndex(string bieuMau, int zeroBasedIndex) =>
            GetPhuLieuStartCol(bieuMau) + zeroBasedIndex;

        // -------------------------------------------------------
        // Export data query (DB-based, không dùng DataJson)
        // -------------------------------------------------------

        /// <summary>
        /// Lấy dữ liệu từ DLNM_HRC2 + PhuLieu_HRC2 để export Excel thống kê.
        /// Params: ngay (DateOnly), ca, bieuMau, scope — tương tự SearchThongKeApiAsync nhưng không phân trang.
        /// </summary>
        public async Task<(List<PhuLieuHeaderTable> HeadersBOF, List<PhuLieuHeaderTable> HeadersLF, List<PhuLieuHeaderTable> HeadersRH, List<HRC2ThongKeRow> Rows)> GetExportDataAsync(
            DateOnly ngay, int ca, string bieuMau, int scope, Guid? idPhieu = null)
        {
            // 1. Lấy header Excel theo config Header_Key hiện tại (live) — dùng cho phiếu chưa Chốt,
            // hoặc làm fallback khi phiếu Chốt không có snapshot (xem PHẦN dưới).
            var (headersBOF, headersLF, headersRH) = await GetLiveExcelHeadersAsync();

            // Chọn header list phù hợp với bieuMau hiện tại để build data rows
            var loaiBmKey = bieuMau.Trim().ToUpperInvariant();
            bool isBofExcel = loaiBmKey.Contains("BOF");
            bool isRhExcel = loaiBmKey.Contains("RH");

            // Phiếu đã Chốt: dùng snapshot TOÀN BỘ danh sách cột theo config Excel đã chụp lại
            // ĐÚNG lúc phiếu chuyển sang Chốt (PhieuService.CaptureExcelHeaderSnapshotIfApplicableAsync,
            // lưu vào DataJson["excelHeaderSnapshotAtChot"]) — không phải config hiện tại, và không
            // phải chỉ những phụ liệu có dữ liệu thật trong JSON (khác hẳn table1DynamicColumns).
            // Phiếu Chốt từ TRƯỚC KHI có cơ chế này (chưa có field snapshot) → fallback về config
            // live như hành vi gốc (chấp nhận được, vì dữ liệu lịch sử "config lúc chốt" không tồn tại).
            // Giá trị từng ô (KLPhuGia) và các cột số cố định (O2, N2, KLGangLongCCT...) luôn tính từ
            // PhuLieu_HRC2/DLNM_HRC2 live như code hiện tại bên dưới — không nằm trong snapshot này.
            var chotSnapshotHeaders = await TryGetChotSnapshotHeadersAsync(idPhieu, bieuMau);
            if (chotSnapshotHeaders != null)
            {
                if (isBofExcel) headersBOF = chotSnapshotHeaders;
                else if (isRhExcel) headersRH = chotSnapshotHeaders;
                else headersLF = chotSnapshotHeaders;
            }

            // LF có 2 nhóm phụ liệu riêng (Chất hợp kim hóa=KL, Phụ gia và chất khử oxy=PG) render
            // thành 2 khối cột liền nhau trong PDF/Excel (PdfThead_LF/RenderColumnHeaders_LF) — cần
            // đảm bảo thứ tự cột ở đây đã gộp liền khối theo nhóm (áp dụng cho cả header live lẫn
            // snapshot đã chốt), nếu không colspan merge theo nhóm sẽ sai khi ThuTu_Excel_LF xen kẽ.
            headersLF = headersLF
                .OrderBy(h => h.LoaiPhieu == "KL" ? 0 : h.LoaiPhieu == "PG" ? 1 : 2)
                .ToList();

            var headers = isBofExcel ? headersBOF : (isRhExcel ? headersRH : headersLF);

            // Phiếu Chốt dùng snapshot cột Excel chụp ĐÚNG lúc chốt (TryGetChotSnapshotHeadersAsync).
            // Nếu user cấu hình Excel cho Header_Key (IsUsed_Excel/LoaiExcel/LoaiPhieu/ThuTu_Excel_*)
            // SAU KHI phiếu đã chốt, snapshot cũ không có header đó dù phiếu vẫn tham chiếu đúng
            // headerKeyId trong table1DynamicColumns.adjust (cột "Thêm cột điều chỉnh") → cột biến
            // mất khỏi Excel dù cấu hình hiện tại đã đúng. Bổ sung các headerKeyId này (đọc trực
            // tiếp từ DataJson của CHÍNH phiếu, không phụ thuộc snapshot/config Excel) vào headers
            // nếu còn thiếu, lấy tên/loại phiếu HIỆN TẠI từ Header_Keys (lúc lưu, table1DynamicColumns
            // .adjust[] không lưu loaiPhieu — xem LoadDataJsonOverridesAsync). Chạy cả khi phiếu CHƯA
            // chốt để làm lưới an toàn chung (no-op nếu header đã có sẵn trong headers).
            var manualAdjustHeaderIds = await GetManualAdjustHeaderKeyIdsAsync(idPhieu);
            if (manualAdjustHeaderIds.Count > 0)
            {
                var existingIds = headers.Select(h => h.IDHeaderKey).ToHashSet();
                var missingIds = manualAdjustHeaderIds.Where(id => !existingIds.Contains(id)).ToList();
                if (missingIds.Count > 0)
                {
                    var missingHeaderKeys = await _context.Header_Keys
                        .Where(h => missingIds.Contains(h.Id))
                        .Select(h => new { h.Id, h.TenHienThi, h.LoaiPhieu })
                        .ToListAsync();

                    foreach (var hk in missingHeaderKeys)
                    {
                        // LF render theo 2 khối KL/PG liền nhau (RenderColumnHeaders_LF) — header không
                        // thuộc 1 trong 2 nhóm này thì bỏ qua để tránh vỡ colspan merge, giống ràng
                        // buộc của headers cấu hình sẵn (GetLiveExcelHeadersAsync).
                        if (!isBofExcel && !isRhExcel && hk.LoaiPhieu != "KL" && hk.LoaiPhieu != "PG")
                            continue;

                        headers.Add(new PhuLieuHeaderTable
                        {
                            IDHeaderKey = hk.Id,
                            TenPhuLieu = hk.TenHienThi,
                            LoaiPhieu = hk.LoaiPhieu
                        });
                    }

                    if (!isBofExcel && !isRhExcel)
                    {
                        headersLF = headersLF
                            .OrderBy(h => h.LoaiPhieu == "KL" ? 0 : h.LoaiPhieu == "PG" ? 1 : 2)
                            .ToList();
                        headers = headersLF;
                    }
                }
            }

            if (!headersBOF.Any() && !headersLF.Any() && !headersRH.Any())
                return (headersBOF, headersLF, headersRH, new List<HRC2ThongKeRow>());

            var usedHeaderKeyIds = headers.Select(x => x.IDHeaderKey).ToHashSet();

            // 2. Lấy tất cả bản ghi DLNM_HRC2 theo filter
            // - NM có REPORT_NO: group by REPORT_NO, lấy ID lớn nhất
            // - Nhập tay IsNM=false không REPORT_NO: lấy trực tiếp từng bản ghi
            var ngayDateTime = ngay.ToDateTime(TimeOnly.MinValue);

            var baseQuery = _context.DLNM_HRC2s
                .Where(x =>
                    x.IsDelete != true &&
                    x.Ngay.HasValue &&
                    x.Ngay.Value.Date == ngayDateTime.Date &&
                    x.Ca == ca &&
                    x.BieuMau == bieuMau &&
                    x.Scope == scope);

            var groupedIds = baseQuery
                .Where(x => x.REPORT_NO.HasValue)
                .GroupBy(x => x.REPORT_NO)
                .Select(g => g.Max(x => x.ID));

            var manualIds = baseQuery
                .Where(x => !x.REPORT_NO.HasValue && x.IsNM == false)
                .Select(x => x.ID);

            var selectedIds = groupedIds.Union(manualIds);

            var items = await _context.DLNM_HRC2s
                .Where(x => selectedIds.Contains(x.ID))
                .OrderBy(x => x.REPORT_NO)
                .ThenBy(x => x.ID)
                .AsNoTracking()
                .ToListAsync();

            if (!items.Any())
                return (headersBOF, headersLF, headersRH, new List<HRC2ThongKeRow>());

            // Lấy DataJson overrides từ phiếu (nếu có idPhieu) để ưu tiên giá trị user đã sửa tay.
            // DataJson là nguồn sự thật cho manual overrides trên UI.
            // Khóa chính là "id" (DLNM_HRC2.ID trong table1 row) — GIỐNG applyManualOverrides bên FE
            // (rowIdField:"id", fallbackKeyField:"meThoi") — vì MeThoi có thể TRÙNG giữa 2 mẻ khác
            // nhau (IsTrungMeThoi). Nếu chỉ khoá theo MeThoi, 2 mẻ trùng tên sẽ ghi đè lẫn nhau trong
            // dictionary và export sẽ lấy nhầm override của mẻ này gán cho mẻ kia dù UI vẫn render
            // đúng (UI khoá theo id trước). Chỉ fallback về MeThoi khi row JSON không có "id" (dữ liệu
            // cũ trước khi field này tồn tại).
            var (dataJsonOverridesById, dataJsonOverridesByMeThoi) = await LoadDataJsonOverridesAsync(idPhieu);

            // Map: mọi DLNM_HRC2.ID trong cùng REPORT_NO group → display item ID (max ID đã chọn).
            // Mục đích: bắt cả phụ liệu được lưu theo ID khác (không phải max) trong cùng REPORT_NO.
            var reportNoToDisplayId = items
                .Where(x => x.REPORT_NO.HasValue)
                .ToDictionary(x => x.REPORT_NO!.Value, x => x.ID);

            var idToDisplayId = new Dictionary<long, long>();

            // Với NM rows: tìm tất cả DLNM_HRC2 IDs trong cùng REPORT_NO → map về display item ID
            if (reportNoToDisplayId.Count > 0)
            {
                var reportNosInItems = reportNoToDisplayId.Keys.ToHashSet();
                var allNMEntries = await _context.DLNM_HRC2s
                    .Where(x => x.REPORT_NO.HasValue
                             && reportNosInItems.Contains(x.REPORT_NO!.Value)
                             && x.IsDelete != true)
                    .Select(x => new { x.ID, ReportNo = x.REPORT_NO!.Value })
                    .AsNoTracking()
                    .ToListAsync();

                foreach (var entry in allNMEntries)
                {
                    if (reportNoToDisplayId.TryGetValue(entry.ReportNo, out var dispId))
                        idToDisplayId[entry.ID] = dispId;
                }
            }

            // Với manual rows: map về chính nó
            foreach (var mid in items.Where(x => !x.REPORT_NO.HasValue).Select(x => x.ID))
                idToDisplayId[mid] = mid;

            // Dùng ID_MeThoi (= DLNM_HRC2.ID) thay vì REPORT_NO để fetch phụ liệu,
            // vì PhuLieu_HRC2 của mẻ nhập tay (IsNM=false) không có REPORT_NO.
            var itemIds = idToDisplayId.Keys.ToList();

            // 3. Batch load mapped phụ liệu (có ID_PhuLieu → Header_Mappings → Header_Keys)
            var mappedRaw = await (
                from pl in _context.PhuLieu_HRC2s
                where itemIds.Contains(pl.ID_MeThoi)
                      && (pl.IsPhanBo != true) && pl.ID_PhuLieu.HasValue
                join hm in _context.Header_Mappings on pl.ID_PhuLieu.Value equals hm.ID_PhuLieu
                join hk in _context.Header_Keys on hm.ID_HeaderKey equals hk.Id
                where hk.IsActive && usedHeaderKeyIds.Contains(hk.Id)
                select new
                {
                    ItemId = pl.ID_MeThoi,
                    ID_HeaderKey = hk.Id,
                    pl.KLPhuGia,
                    pl.KLPhuGia_Manual,
                    pl.IsManual
                }
            ).ToListAsync();

            // 3b. Manual-only (ID_PhuLieu = NULL, chỉ có ID_HeaderKey — cột điều chỉnh tay)
            var manualOnlyRaw = await _context.PhuLieu_HRC2s
                .Where(pl =>
                    itemIds.Contains(pl.ID_MeThoi) &&
                    (pl.IsPhanBo != true) &&
                    !pl.ID_PhuLieu.HasValue &&
                    pl.ID_HeaderKey.HasValue &&
                    usedHeaderKeyIds.Contains(pl.ID_HeaderKey.Value))
                .Select(pl => new
                {
                    ItemId = pl.ID_MeThoi,
                    ID_HeaderKey = pl.ID_HeaderKey!.Value,
                    KLPhuGia = (double?)0,
                    pl.KLPhuGia_Manual,
                    pl.IsManual
                })
                .ToListAsync();

            mappedRaw.AddRange(manualOnlyRaw);

            // Remap ItemId → display item ID (gộp phụ liệu của mọi ID trong cùng REPORT_NO về 1 display row)
            var mappedRemapped = mappedRaw.Select(x => new
            {
                ItemId = idToDisplayId.GetValueOrDefault(x.ItemId, x.ItemId),
                x.ID_HeaderKey,
                x.KLPhuGia,
                x.KLPhuGia_Manual,
                x.IsManual
            }).ToList();

            // Group: displayId → headerKeyId → (KLPhuGiaTotal, KLPhuGia_Manual, IsManual)
            // Ưu tiên record có IsManual=true khi lấy KLPhuGia_Manual để không bỏ sót giá trị sửa tay.
            var mappedByItemId = mappedRemapped
                .GroupBy(x => x.ItemId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.ID_HeaderKey)
                          .ToDictionary(
                              hg => hg.Key,
                              hg =>
                              {
                                  var manualRec = hg.FirstOrDefault(x => x.IsManual == true);
                                  return (
                                      KLPhuGiaTotal: (double?)hg.Sum(x => x.KLPhuGia ?? 0),
                                      KLPhuGia_Manual: manualRec?.KLPhuGia_Manual,
                                      IsManual: manualRec != null
                                  );
                              }
                          )
                );

            // 4. Batch load phanBo
            var phanBoRaw = await _context.PhuLieu_HRC2s
                .Where(x =>
                    itemIds.Contains(x.ID_MeThoi) &&
                    x.IsPhanBo == true &&
                    x.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(x.ID_HeaderKey.Value))
                .Select(x => new
                {
                    ItemId = x.ID_MeThoi,
                    ID_HeaderKey = x.ID_HeaderKey!.Value,
                    x.KLPhuGia,
                    x.KLPhuGia_Manual,
                    x.IsManual
                })
                .ToListAsync();

            var phanBoRemapped = phanBoRaw.Select(x => new
            {
                ItemId = idToDisplayId.GetValueOrDefault(x.ItemId, x.ItemId),
                x.ID_HeaderKey,
                x.KLPhuGia,
                x.KLPhuGia_Manual,
                x.IsManual
            }).ToList();

            var phanBoByItemId = phanBoRemapped
                .GroupBy(x => x.ItemId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.ID_HeaderKey)
                          .ToDictionary(
                              hg => hg.Key,
                              hg => (
                                  KLPhuGia: (double?)hg.Sum(x => x.KLPhuGia ?? 0),
                                  KLPhuGia_Manual: (double?)hg.Sum(x => x.KLPhuGia_Manual ?? 0),
                                  IsManual: hg.First().IsManual
                              )
                          )
                );

            // 5. Assemble rows (bao gồm cả mẻ nhập tay IsNM=false không có REPORT_NO)
            var rows = items
                .Select(x =>
                {
                    // Lookup theo DLNM_HRC2.ID (= ID_MeThoi trong PhuLieu_HRC2), áp dụng cả NM và nhập tay
                    mappedByItemId.TryGetValue(x.ID, out var mappedDict);
                    phanBoByItemId.TryGetValue(x.ID, out var phanBoDict);

                    var values = headers.Select(h =>
                    {
                        double? klPhuGia = null;
                        double? klPhuGia_Manual = null;
                        bool? isManual = null;

                        if (mappedDict != null && mappedDict.TryGetValue(h.IDHeaderKey, out var mapped))
                        {
                            // Header_Key Id=5: KLPhuGia ÷ 0.055, làm tròn không chữ số thập phân
                            // (đồng bộ với DLNMHRC2Repository.GetByIdGroupedAsync/SearchThongKeApiAsync)
                            klPhuGia = h.IDHeaderKey == 5
                                ? (mapped.KLPhuGiaTotal.HasValue
                                    ? (double?)Math.Round(mapped.KLPhuGiaTotal.Value / 0.055, 0, MidpointRounding.AwayFromZero)
                                    : null)
                                : RoundNumber(mapped.KLPhuGiaTotal);
                            klPhuGia_Manual = RoundNumber(mapped.KLPhuGia_Manual);
                            isManual = mapped.IsManual;
                        }

                        var effectiveKL = klPhuGia_Manual ?? klPhuGia;

                        // Ưu tiên DataJson: giá trị user đã sửa tay trên UI (nguồn sự thật cao nhất).
                        // Match theo ID trước (giống FE) để không bị lẫn khi 2 mẻ trùng MeThoi;
                        // chỉ fallback về MeThoi khi row JSON không có "id" (dữ liệu cũ).
                        Dictionary<int, double?>? rowOverrides = null;
                        if (!dataJsonOverridesById.TryGetValue(x.ID, out rowOverrides) &&
                            !string.IsNullOrEmpty(x.MeThoi))
                        {
                            dataJsonOverridesByMeThoi.TryGetValue(x.MeThoi, out rowOverrides);
                        }

                        if (rowOverrides != null && rowOverrides.TryGetValue(h.IDHeaderKey, out var jsonOverrideVal))
                        {
                            effectiveKL = jsonOverrideVal;
                            klPhuGia_Manual = jsonOverrideVal;
                            isManual = true;
                        }

                        double? klPhanBo = null;
                        double? totalKLPhuGia;

                        if (phanBoDict != null && phanBoDict.TryGetValue(h.IDHeaderKey, out var phanBo))
                        {
                            klPhanBo = RoundNumber(phanBo.KLPhuGia);
                            totalKLPhuGia = RoundNumber((phanBo.KLPhuGia ?? 0) + (effectiveKL ?? 0));
                        }
                        else
                        {
                            totalKLPhuGia = effectiveKL;
                        }

                        return new HRC2ThongKeValue
                        {
                            IDHeaderKey = h.IDHeaderKey,
                            KLPhuGia = klPhuGia,
                            KLPhuGia_Manual = klPhuGia_Manual,
                            IsManual = isManual,
                            KLPhanBo = klPhanBo,
                            EffectiveKL = RoundNumber(effectiveKL),
                            TotalKLPhuGia = totalKLPhuGia
                        };
                    }).ToList();

                    return new HRC2ThongKeRow
                    {
                        Data = new DLNM_HRC2_ResponseModels
                        {
                            ID = x.ID,
                            REPORT_NO = x.REPORT_NO,
                            NgaySx = x.NgaySx,
                            Ngay = x.Ngay,
                            Ca = x.Ca,
                            BieuMau = x.BieuMau,
                            Scope = x.Scope,
                            MeThoi = x.MeThoi,
                            MacThep = x.MacThep_Manual ?? x.MacThep,
                            O2 = RoundNumber(x.O2),
                            AR_RH = RoundNumber(x.AR_RH),
                            N2 = RoundNumber(x.N2),
                            AR_BOF = RoundNumber(x.AR_BOF),
                            AR_LF = RoundNumber(x.AR_LF),
                            KLGangLong = RoundNumber(x.KLGangLong),
                            KLThepPhe = RoundNumber(x.KLThepPhe),
                            KLThepPheGang = RoundNumber(x.KLThepPheGang),
                            KLGangLongCCT = RoundNumber(x.KLGangLongCCT),
                            KLGangLongCR = RoundNumber(x.KLGangLongCR),
                            KLThepLong = RoundNumber(x.KLThepLong),
                            IsNM = x.IsNM,
                            IsChuyenCa = x.IsChuyenCa,
                            IsTrungMeThoi = x.IsTrungMeThoi,
                            QueLayMau = x.QueLayMau,
                            QueDoNhiet = x.QueDoNhiet,
                            GhiChu = x.GhiChu
                        },
                        Values = values
                    };
                })
                .ToList();

            return (headersBOF, headersLF, headersRH, rows);
        }

        /// <summary>
        /// Tên field lưu snapshot header Excel trong BmPhieu.DataJson — dùng chung giữa
        /// PhieuService (lúc chốt, ghi snapshot) và PhieuDetailExcelService (lúc export, đọc lại).
        /// </summary>
        public const string ExcelHeaderSnapshotJsonKey = "excelHeaderSnapshotAtChot";

        /// <summary>
        /// Danh sách cột Excel theo config Header_Key HIỆN TẠI (live), tách theo BOF/LF/RH.
        /// LoaiExcel là bitmask: BOF=1, LF=2, RH=4 (1 header có thể thuộc nhiều biểu mẫu cùng lúc).
        /// Dùng cho cả export (headers phiếu chưa Chốt/fallback) lẫn chụp snapshot lúc Chốt phiếu
        /// (PhieuService.CaptureExcelHeaderSnapshotIfApplicableAsync) — 1 nguồn logic duy nhất,
        /// tránh lệch nhau giữa 2 chỗ dùng.
        /// </summary>
        public async Task<(List<PhuLieuHeaderTable> HeadersBOF, List<PhuLieuHeaderTable> HeadersLF, List<PhuLieuHeaderTable> HeadersRH)> GetLiveExcelHeadersAsync()
        {
            var allExcelHeaders = await _context.Header_Keys
                .Where(h => h.IsUsed_Excel == true)
                .Select(h => new { h.Id, h.TenHienThi, h.LoaiExcel, h.ThuTu_Excel_BOF, h.ThuTu_Excel_LF, h.ThuTu_Excel_RH, h.LoaiPhieu })
                .ToListAsync();

            var headersBOF = allExcelHeaders
                .Where(h => ((byte)(h.LoaiExcel ?? 0) & 1) != 0)
                .OrderBy(h => h.ThuTu_Excel_BOF ?? int.MaxValue)
                .ThenBy(h => h.Id)
                .Select(h => new PhuLieuHeaderTable
                {
                    IDHeaderKey = h.Id,
                    TenPhuLieu = h.TenHienThi,
                    LoaiThongKe = (byte)(h.LoaiExcel ?? 0),
                    LoaiPhieu = h.LoaiPhieu
                })
                .ToList();

            // LF: chỉ render cột phụ liệu nào có ĐỦ CẢ 2 — ThuTu_Excel_LF (để xác định thứ tự) VÀ
            // LoaiPhieu = "KL"/"PG" (để xác định thuộc nhóm nào) — thiếu 1 trong 2 thì không hiện
            // (tránh vỡ khớp cột giữa header nhóm KL/PG và dữ liệu thân bảng ở PdfThead_LF/
            // RenderColumnHeaders_LF, vốn chỉ render đúng 2 nhóm này, không còn nhóm dự phòng).
            var headersLF = allExcelHeaders
                .Where(h => ((byte)(h.LoaiExcel ?? 0) & 2) != 0
                         && h.ThuTu_Excel_LF.HasValue
                         && (h.LoaiPhieu == "KL" || h.LoaiPhieu == "PG"))
                .OrderBy(h => h.ThuTu_Excel_LF!.Value)
                .ThenBy(h => h.Id)
                .Select(h => new PhuLieuHeaderTable
                {
                    IDHeaderKey = h.Id,
                    TenPhuLieu = h.TenHienThi,
                    LoaiThongKe = (byte)(h.LoaiExcel ?? 0),
                    LoaiPhieu = h.LoaiPhieu
                })
                .ToList();

            var headersRH = allExcelHeaders
                .Where(h => ((byte)(h.LoaiExcel ?? 0) & 4) != 0)
                .OrderBy(h => h.ThuTu_Excel_RH ?? int.MaxValue)
                .ThenBy(h => h.Id)
                .Select(h => new PhuLieuHeaderTable
                {
                    IDHeaderKey = h.Id,
                    TenPhuLieu = h.TenHienThi,
                    LoaiThongKe = (byte)(h.LoaiExcel ?? 0),
                    LoaiPhieu = h.LoaiPhieu
                })
                .ToList();

            return (headersBOF, headersLF, headersRH);
        }

        /// <summary>
        /// Phiếu đã Chốt (TinhTrang=5): trả về danh sách cột phụ liệu (thứ tự + tên) đã CHỤP LẠI
        /// TOÀN BỘ config Excel (Header_Key.IsUsed_Excel/LoaiExcel/ThuTu_Excel_*) ĐÚNG lúc phiếu
        /// chuyển sang Chốt (xem PhieuService.CaptureExcelHeaderSnapshotIfApplicableAsync — lưu
        /// vào DataJson[ExcelHeaderSnapshotJsonKey]). KHÔNG dùng table1DynamicColumns vì đó chỉ là
        /// phụ liệu THỰC SỰ CÓ DỮ LIỆU trong mẻ (tập con), không phải toàn bộ danh sách theo config
        /// — vd config có 10 loại nhưng mẻ chỉ dùng 4 loại thì table1DynamicColumns chỉ có 4.
        /// Trả về null nếu phiếu chưa Chốt / không có idPhieu / parse lỗi / phiếu Chốt từ TRƯỚC KHI
        /// có cơ chế này (chưa từng lưu field snapshot) → caller fallback về headers config live
        /// (hành vi gốc trước khi có toàn bộ cơ chế snapshot này).
        /// </summary>
        private async Task<List<PhuLieuHeaderTable>?> TryGetChotSnapshotHeadersAsync(Guid? idPhieu, string bieuMau)
        {
            if (!idPhieu.HasValue) return null;

            var phieu = await _context.BmPhieus
                .AsNoTracking()
                .Where(p => p.Idphieu == idPhieu.Value)
                .Select(p => new { p.TinhTrang, p.DataJson })
                .FirstOrDefaultAsync();

            if (phieu == null || phieu.TinhTrang != 5 || string.IsNullOrWhiteSpace(phieu.DataJson))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(phieu.DataJson);
                var root = doc.RootElement;

                if (!root.TryGetProperty(ExcelHeaderSnapshotJsonKey, out var snapArr) ||
                    snapArr.ValueKind != JsonValueKind.Array)
                    return null; // Phiếu Chốt từ trước khi có cơ chế snapshot → fallback config live

                var headers = new List<PhuLieuHeaderTable>();
                foreach (var col in snapArr.EnumerateArray())
                {
                    if (!col.TryGetProperty("headerKeyId", out var hkProp) ||
                        hkProp.ValueKind != JsonValueKind.Number)
                        continue;

                    var label = col.TryGetProperty("label", out var lblProp) &&
                                 lblProp.ValueKind == JsonValueKind.String
                        ? lblProp.GetString() ?? ""
                        : "";

                    // loaiPhieu: chỉ có ở snapshot chụp SAU khi có cơ chế gộp nhóm cột KL/PG cho LF/RH;
                    // snapshot cũ hơn không có field này → null → cột rơi vào nhóm "khác" khi render.
                    var loaiPhieu = col.TryGetProperty("loaiPhieu", out var lpProp) &&
                                 lpProp.ValueKind == JsonValueKind.String
                        ? lpProp.GetString()
                        : null;

                    headers.Add(new PhuLieuHeaderTable
                    {
                        IDHeaderKey = hkProp.GetInt32(),
                        TenPhuLieu = label,
                        LoaiPhieu = loaiPhieu
                    });
                }

                return headers.Count > 0 ? headers : null;
            }
            catch
            {
                // DataJson parse lỗi → trả về null, caller fallback về live headers, không chặn export
                return null;
            }
        }

        /// <summary>
        /// Đọc DataJson.table1DynamicColumns.adjust[].headerKeyId của phiếu — danh sách headerKeyId
        /// của các cột "Thêm cột điều chỉnh" do user thêm tay (isManuallyAdded), bất kể Header_Key đó
        /// có được cấu hình dùng cho Excel hay không / có nằm trong snapshot lúc Chốt hay không. Dùng
        /// để đảm bảo cột này luôn có mặt trên Excel một khi phiếu đã lưu dữ liệu cho nó (xem
        /// GetExportDataAsync).
        /// </summary>
        private async Task<HashSet<int>> GetManualAdjustHeaderKeyIdsAsync(Guid? idPhieu)
        {
            var result = new HashSet<int>();
            if (!idPhieu.HasValue) return result;

            var dataJson = await _context.BmPhieus
                .AsNoTracking()
                .Where(p => p.Idphieu == idPhieu.Value)
                .Select(p => p.DataJson)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(dataJson)) return result;

            try
            {
                using var doc = JsonDocument.Parse(dataJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("table1DynamicColumns", out var dynCols) &&
                    dynCols.ValueKind == JsonValueKind.Object &&
                    dynCols.TryGetProperty("adjust", out var adjustArr) &&
                    adjustArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var meta in adjustArr.EnumerateArray())
                    {
                        if (meta.TryGetProperty("headerKeyId", out var hkProp) &&
                            hkProp.ValueKind == JsonValueKind.Number)
                        {
                            result.Add(hkProp.GetInt32());
                        }
                    }
                }
            }
            catch
            {
                // DataJson parse lỗi → bỏ qua, không chặn export
            }

            return result;
        }

        /// <summary>
        /// Đọc DataJson của phiếu và trích xuất manual overrides: headerKeyId → giá trị user đã sửa,
        /// cho từng row của table1. Trả về 2 dictionary tra cứu, GIỐNG cách FE khoá row
        /// (HRC2TableService.applyManualOverrides: rowIdField="id", fallbackKeyField="meThoi"):
        /// - ById: khoá theo "id" (= DLNM_HRC2.ID) của row — dùng khi row JSON có field "id".
        /// - ByMeThoi: khoá theo "meThoi" — CHỈ dùng cho row JSON không có "id" (dữ liệu cũ trước khi
        ///   field này tồn tại). Nếu khoá luôn theo meThoi thì 2 mẻ TRÙNG MeThoi (IsTrungMeThoi) sẽ
        ///   ghi đè lẫn nhau trong dictionary → export lấy nhầm override của mẻ này gán cho mẻ kia,
        ///   dù UI vẫn render đúng vì UI khoá theo id trước.
        /// </summary>
        private async Task<(Dictionary<long, Dictionary<int, double?>> ById, Dictionary<string, Dictionary<int, double?>> ByMeThoi)> LoadDataJsonOverridesAsync(Guid? idPhieu)
        {
            var resultById = new Dictionary<long, Dictionary<int, double?>>();
            var resultByMeThoi = new Dictionary<string, Dictionary<int, double?>>(StringComparer.OrdinalIgnoreCase);
            if (!idPhieu.HasValue) return (resultById, resultByMeThoi);

            var dataJson = await _context.BmPhieus
                .AsNoTracking()
                .Where(p => p.Idphieu == idPhieu.Value)
                .Select(p => p.DataJson)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(dataJson)) return (resultById, resultByMeThoi);

            try
            {
                using var doc = JsonDocument.Parse(dataJson);
                var root = doc.RootElement;

                if (root.TryGetProperty("table1", out var table1) && table1.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in table1.EnumerateArray())
                    {
                        string? meThoi = row.TryGetProperty("meThoi", out var meProp) && meProp.ValueKind == JsonValueKind.String
                            ? meProp.GetString()
                            : null;

                        long? rowId = null;
                        if (row.TryGetProperty("id", out var idProp))
                        {
                            if (idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt64(out var idNum))
                                rowId = idNum;
                            else if (idProp.ValueKind == JsonValueKind.String &&
                                     long.TryParse(idProp.GetString(), out var idParsed))
                                rowId = idParsed;
                        }

                        if (rowId == null && string.IsNullOrWhiteSpace(meThoi)) continue;

                        var rowOverrides = new Dictionary<int, double?>();

                        foreach (var prop in row.EnumerateObject())
                        {
                            if (!prop.Name.EndsWith("__IsManual", StringComparison.Ordinal)) continue;
                            if (prop.Value.ValueKind != JsonValueKind.True) continue;

                            var baseKey = prop.Name[..^"__IsManual".Length]; // e.g. "phuLieu_5"
                            if (!baseKey.StartsWith("phuLieu_", StringComparison.Ordinal)) continue;
                            if (!int.TryParse(baseKey["phuLieu_".Length..], out var headerKeyId)) continue;

                            double? val = null;
                            if (row.TryGetProperty(baseKey, out var valProp))
                            {
                                if (valProp.ValueKind == JsonValueKind.Number)
                                    val = valProp.GetDouble();
                                else if (valProp.ValueKind == JsonValueKind.String &&
                                         double.TryParse(valProp.GetString(),
                                             System.Globalization.NumberStyles.Any,
                                             System.Globalization.CultureInfo.InvariantCulture, out var d))
                                    val = d;
                            }
                            rowOverrides[headerKeyId] = val;
                        }

                        if (rowOverrides.Count == 0) continue;

                        // Khoá theo id khi có (giống FE); chỉ fallback về meThoi khi row không có id.
                        if (rowId.HasValue)
                            resultById[rowId.Value] = rowOverrides;
                        else if (!string.IsNullOrWhiteSpace(meThoi))
                            resultByMeThoi[meThoi] = rowOverrides;
                    }
                }

                // table1DynamicColumns.adjust: giá trị của các cột "Thêm cột điều chỉnh" (nút thêm tay
                // trong màn Tạo/Chi tiết phiếu, dataIndex "manual_col_{headerKeyId}"). FE xoá hết field
                // manual_col_* khỏi table1[] trước khi lưu (HRC2PhuLieuService.sanitizeRowsBeforeSubmit)
                // và chỉ lưu giá trị ở đây (table1DynamicColumns.adjust[].values[], build bởi
                // buildAdjustDynamicWithValues) — nên PHẢI đọc riêng, không nằm trong vòng lặp table1[]
                // ở trên. Thiếu đoạn này thì cột "điều chỉnh" luôn trống khi export dù UI hiển thị đủ.
                if (root.TryGetProperty("table1DynamicColumns", out var dynCols) &&
                    dynCols.ValueKind == JsonValueKind.Object &&
                    dynCols.TryGetProperty("adjust", out var adjustArr) &&
                    adjustArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var meta in adjustArr.EnumerateArray())
                    {
                        if (!meta.TryGetProperty("headerKeyId", out var hkProp) ||
                            hkProp.ValueKind != JsonValueKind.Number)
                            continue;
                        var headerKeyId = hkProp.GetInt32();

                        if (!meta.TryGetProperty("values", out var valuesArr) ||
                            valuesArr.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var v in valuesArr.EnumerateArray())
                        {
                            long? rowId = null;
                            if (v.TryGetProperty("rowId", out var rowIdProp) &&
                                rowIdProp.ValueKind == JsonValueKind.Number &&
                                rowIdProp.TryGetInt64(out var rid))
                                rowId = rid;

                            string? meThoi = v.TryGetProperty("meThoi", out var meThoiProp) &&
                                             meThoiProp.ValueKind == JsonValueKind.String
                                ? meThoiProp.GetString()
                                : null;

                            if (rowId == null && string.IsNullOrWhiteSpace(meThoi)) continue;

                            double? val = null;
                            if (v.TryGetProperty("value", out var valProp))
                            {
                                if (valProp.ValueKind == JsonValueKind.Number)
                                    val = valProp.GetDouble();
                                else if (valProp.ValueKind == JsonValueKind.String &&
                                         double.TryParse(valProp.GetString(),
                                             System.Globalization.NumberStyles.Any,
                                             System.Globalization.CultureInfo.InvariantCulture, out var d))
                                    val = d;
                            }

                            // Khoá theo id khi có (giống FE); chỉ fallback về meThoi khi không có id.
                            // Merge vào dict đã có (không ghi đè) để không mất override phuLieu_ ở trên.
                            if (rowId.HasValue)
                            {
                                if (!resultById.TryGetValue(rowId.Value, out var dict))
                                {
                                    dict = new Dictionary<int, double?>();
                                    resultById[rowId.Value] = dict;
                                }
                                dict[headerKeyId] = val;
                            }
                            else if (!string.IsNullOrWhiteSpace(meThoi))
                            {
                                if (!resultByMeThoi.TryGetValue(meThoi, out var dict))
                                {
                                    dict = new Dictionary<int, double?>();
                                    resultByMeThoi[meThoi] = dict;
                                }
                                dict[headerKeyId] = val;
                            }
                        }
                    }
                }
            }
            catch
            {
                // DataJson parse failure → trả về empty, không ảnh hưởng export
            }

            return (resultById, resultByMeThoi);
        }

        private static double? RoundNumber(double? value)
        {
            if (!value.HasValue) return null;
            var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
            return Math.Abs(rounded % 1) < 0.0000001 ? Math.Truncate(rounded) : rounded;
        }

        public async Task<byte[]> ExportAsync(Guid phieuId)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == phieuId);
            if (phieu == null)
                throw new Exception("Không tìm thấy phiếu");

            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Phiếu");

            BuildHeader(ws, phieu);

            ws.Column(1).Width = 35;
            ws.Columns(2, 30).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // -------------------------------------------------------
        // Header phiếu (generic fallback)
        // -------------------------------------------------------
        private void BuildHeader(IXLWorksheet ws, BmPhieu phieu)
        {
            var titleCell = ws.Cell(1, 1);
            titleCell.Value = $"{phieu.MaBm} - {phieu.SoPhieu}";
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 13;
            ws.Range(1, 1, 1, 6).Merge();

            int row = 3;
            WriteInfoRow(ws, row++, "Số phiếu", phieu.SoPhieu);
            WriteInfoRow(ws, row++, "Mã biểu mẫu", phieu.MaBm);
            WriteInfoRow(ws, row++, "Ngày sản xuất", phieu.NgaySX?.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy"));
            WriteInfoRow(ws, row++, "Ca", phieu.Ca?.ToString());
            WriteInfoRow(ws, row++, "Kíp", phieu.Kip);
            WriteInfoRow(ws, row++, "Tình trạng", PhieuStatusDisplay.GetText(phieu.TinhTrang ?? 0));
        }

        private void WriteInfoRow(IXLWorksheet ws, int row, string label, string? value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value ?? "";
        }

        // ===============================================================
        // HRC2 NauLuyen — DB-based render
        // Layout: rows 1-3 từ template | rows 4-5 info | rows 6-7 headers | row 8+ data
        // ===============================================================

        private const int HeaderParentRow = 6;
        private const int HeaderChildRow  = 7;
        private const int DataStartRow    = 8;

        /// <summary>
        /// Replace {{Marker}} cells trong template (rows 1-3) với metadata phiếu.
        /// </summary>
        public void ReplaceMarkers(IXLWorksheet ws, BmPhieu phieu)
        {
            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "{{SoPhieu}}",   phieu.SoPhieu ?? "" },
                { "{{NgaySX}}",    phieu.NgaySX?.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy") ?? "" },
                { "{{Ca}}",        phieu.Ca?.ToString() ?? "" },
                { "{{Kip}}",       phieu.Kip ?? "" },
                { "{{TinhTrang}}", PhieuStatusDisplay.GetText(phieu.TinhTrang ?? 0) },
            };

            foreach (var cell in ws.CellsUsed())
            {
                if (cell.DataType != XLDataType.Text) continue;
                var raw = cell.GetString();
                foreach (var (marker, value) in replacements)
                {
                    if (raw.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        raw = raw.Replace(marker, value, StringComparison.OrdinalIgnoreCase);
                }
                cell.Value = raw;
            }
        }

        /// <summary>
        /// Orchestrator: cập nhật merge rows 1-3 → render rows 4-5 (info) → rows 6-7 (headers) → rows 8+ (data).
        /// </summary>
        public async Task RenderBodyFromDbAsync(IXLWorksheet ws, string bieuMau,
            List<PhuLieuHeaderTable> headers, List<HRC2ThongKeRow> rows,
            int? scope = null, string gioBatDau = "", string gioKetThuc = "",
            DateOnly? ngayPhieu = null, int? caPhieu = null, string kip = "",
            Guid? idPhieu = null)
        {
            // Phân bổ đã được gộp vào TotalKLPhuGia — không render cột riêng
            var phanBoHeaders = new List<PhuLieuHeaderTable>();

            int lastCol = ComputeLastCol(bieuMau, headers.Count, phanBoHeaders.Count);
            ws.Column(2).Width = 25;
            ws.Column(3).Width = 25;
            ws.Column(lastCol).Width = 25;

            ClearRowsFrom(ws, 4);
            UpdateHeaderRowMerges(ws, lastCol);
            RenderInfoRows(ws, bieuMau, rows, lastCol, scope, gioBatDau, gioKetThuc, ngayPhieu, caPhieu, kip);

            // Data bắt đầu từ row 8 cho tất cả biểu mẫu.
            // Sau data: Tổng cộng (totalRow) → 1 dòng trắng → Footer.
            int dataStartRow = DataStartRow; // 8
            int dataEndRow   = dataStartRow + rows.Count - 1;
            int totalRow     = dataEndRow + 1;

            List<STD_XUAT_NHAP_TON_HRC2>? footerData = null;
            string? truongKipName = null, nguoiLapName = null;

            // Footer data lấy từ phiếu HRC2_STD_NXT (khác phiếu biên bản) → filter bằng (Ca, NgaySX, STD_Scope)
            if (ngayPhieu.HasValue && caPhieu.HasValue && scope.HasValue)
            {
                int? stdScope = GetStdScope(bieuMau, scope.Value);
                if (stdScope.HasValue)
                {
                    var ngaySxDt = ngayPhieu.Value.ToDateTime(TimeOnly.MinValue);
                    footerData = await _context.STD_XUAT_NHAP_TON_HRC2s
                        .Where(x => x.Ca == caPhieu.Value && x.NgaySX.Date == ngaySxDt.Date && x.Scope == stdScope.Value)
                        .OrderBy(x => x.ViTri)
                        .ToListAsync();
                }
            }
            if (idPhieu.HasValue)
            {
                var pheDuyets = await _pheDuyetService.GetPheDuyetPhieuAsync(idPhieu.Value);
                truongKipName = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1)?.HoVaTen;
                nguoiLapName  = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0)?.HoVaTen;
            }

            int tableLastRow;
            switch (bieuMau)
            {
                case "HRC2_BB_NauLuyen_BOF":
                    RenderColumnHeaders_BOF(ws, headers, phanBoHeaders);
                    RenderDataRows_BOF(ws, headers, phanBoHeaders, rows, dataStartRow, lastCol);
                    RenderTotalRow_BOF(ws, totalRow, lastCol, headers, phanBoHeaders, rows);
                    tableLastRow = RenderFooter(ws, totalRow + 2, lastCol, BofFooterConfig, footerData);
                    RenderSignatureRow(ws, tableLastRow + 1, lastCol, BofFooterConfig, bieuMau, truongKipName, nguoiLapName);
                    break;
                case "HRC2_BB_NauLuyen_LF":
                    RenderColumnHeaders_LF(ws, headers, phanBoHeaders);
                    RenderDataRows_LF(ws, headers, phanBoHeaders, rows, dataStartRow, lastCol);
                    RenderTotalRow_LF(ws, totalRow, lastCol, headers, phanBoHeaders, rows);
                    tableLastRow = RenderFooter(ws, totalRow + 2, lastCol, RhFooterConfig, footerData);
                    RenderSignatureRow(ws, tableLastRow + 1, lastCol, RhFooterConfig, bieuMau, truongKipName, nguoiLapName);
                    break;
                case "HRC2_BB_NauLuyen_RH":
                    RenderColumnHeaders_RH(ws, headers, phanBoHeaders);
                    RenderDataRows_RH(ws, headers, phanBoHeaders, rows, dataStartRow, lastCol);
                    RenderTotalRow_RH(ws, totalRow, lastCol, headers, phanBoHeaders, rows);
                    tableLastRow = RenderFooter(ws, totalRow + 2, lastCol, RhFooterConfig, footerData);
                    RenderSignatureRow(ws, tableLastRow + 1, lastCol, RhFooterConfig, bieuMau, truongKipName, nguoiLapName);
                    break;
                default:
                    tableLastRow = totalRow;
                    break;
            }

            // Sau khi merge header + body + footer xong: một lưới viền thống nhất (trong = mỏng, ngoài = đậm)
            ApplyUnifiedTableGridBorders(ws, HeaderParentRow, tableLastRow, lastCol);
        }

        // ===============================================================
        // BOF full render — theo spec mới (promt.md)
        // Layout: rows 1-3 template | row 4 title | row 5 kíp | rows 6-7 headers
        //         row 8 Tổng đầu kíp | row 9 labels | row 10+ data | Tổng cộng | LuongTon
        // ===============================================================

        /// <summary>
        /// Render toàn bộ biên bản BOF từ row 4 trở xuống vào worksheet đã mở sẵn từ template.
        /// Rows 1-3 giữ nguyên, chỉ cập nhật merge theo lastCol.
        /// </summary>
        public void RenderBOFExcel(
            IXLWorksheet ws,
            ExportBOFRequest request,
            List<HeaderKeyConfig> headerKeys,
            List<MeLuyenModel> data,
            List<LuongTonModel> luongTons)
        {
            int n       = headerKeys.Count;
            int lastCol = 8 + n; // 5 fixed-left + N dynamic + 3 fixed-right
            int s       = 6;     // cột bắt đầu phụ gia động (BOF = F)
            int a       = s + n; // cột Oxy

            // Không đụng rows 1-3 — giữ nguyên template (drawing, merge, nội dung)
            ClearRowsFrom(ws, 4);

            // Row 4: tiêu đề
            ws.Range(4, 1, 4, lastCol).Merge();
            var c4 = ws.Cell(4, 1);
            c4.Value                      = $"BIÊN BẢN TIÊU HAO NẤU LUYỆN LÒ THỔI {request.SoLo}";
            c4.Style.Font.Bold            = true;
            c4.Style.Font.FontSize        = 14;
            c4.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c4.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

            // Row 5: thông tin kíp
            ws.Range(5, 1, 5, lastCol).Merge();
            var c5 = ws.Cell(5, 1);
            c5.Value = $"Kíp: Từ {request.GioBatDau} ngày {request.NgaySX:dd/MM/yyyy}" +
                       $" đến {request.GioKetThuc} ngày {request.NgaySX:dd/MM/yyyy}";
            c5.Style.Font.Italic          = true;
            c5.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c5.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

            // Rows 6-7: column headers
            BofHeaderMergeVert(ws, 1, "STT");
            BofHeaderMergeVert(ws, 2, "Mẻ nấu số");
            BofHeaderMergeVert(ws, 3, "Mác thép");

            ws.Range(6, 4, 6, 5).Merge();
            BofHeaderCell(ws, 6, 4, "Nguyên liệu đầu vào\n(tấn)");
            BofHeaderCell(ws, 7, 4, "Gang lỏng");
            BofHeaderCell(ws, 7, 5, "Thép phế");

            if (n > 0)
            {
                ws.Range(6, s, 6, s + n - 1).Merge();
                BofHeaderCell(ws, 6, s, "Phụ gia công nghệ (Kg)");
                for (int i = 0; i < n; i++)
                    BofHeaderCell(ws, 7, s + i, headerKeys[i].TenHienThi);
            }

            ws.Range(6, a, 6, a + 1).Merge();
            BofHeaderCell(ws, 6, a,     "Nhiên liệu (m³)");
            BofHeaderCell(ws, 7, a,     "Oxy");
            BofHeaderCell(ws, 7, a + 1, "Nitơ");
            BofHeaderMergeVert(ws, a + 2, "Ghi chú");

            // Row 8: Tổng đầu kíp
            ws.Range(8, 1, 8, 3).Merge();
            ws.Cell(8, 1).Value           = "Tổng";
            ws.Cell(8, 1).Style.Font.Bold = true;
            for (int col = 1; col <= lastCol; col++)
                ApplyBorderOnly(ws.Cell(8, col));

            // Row 9: 3 nhãn bằng nhau
            int part = (lastCol + 2) / 3;
            int end1 = part;
            int end2 = Math.Min(part * 2, lastCol - 1);
            ws.Range(9, 1,        9, end1   ).Merge();
            ws.Range(9, end1 + 1, 9, end2   ).Merge();
            ws.Range(9, end2 + 1, 9, lastCol).Merge();
            BofHeaderCell(ws, 9, 1,        "Tên đầu kíp");
            BofHeaderCell(ws, 9, end1 + 1, "Nhập trong kíp");
            BofHeaderCell(ws, 9, end2 + 1, "Tên cuối kíp");

            // Row 10+: data
            const int DataRow = 10;
            int r   = DataRow;
            int stt = 1;
            foreach (var row in data)
            {
                ws.Cell(r, 1).Value = stt++;
                ws.Cell(r, 2).Value = row.MeNauSo  ?? "";
                ws.Cell(r, 3).Value = row.MacThep  ?? "";
                ws.Cell(r, 4).Value = Num(row.KLGangLongCCT);
                ws.Cell(r, 5).Value = Num(row.KLThepPhe);
                for (int i = 0; i < n; i++)
                    ws.Cell(r, s + i).Value = row.PhuGia.TryGetValue(headerKeys[i].Id, out var kl)
                        ? Num(kl) : Blank.Value;
                ws.Cell(r, a    ).Value = Num(row.O2);
                ws.Cell(r, a + 1).Value = Num(row.N2);
                ws.Cell(r, a + 2).Value = row.GhiChu ?? "";
                ApplyDataRowBorder(ws, r, lastCol);
                r++;
            }

            // Tổng cộng
            ws.Range(r, 1, r, 3).Merge();
            ws.Cell(r, 1).Value           = "Tổng cộng";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 4).Value = data.Sum(d => d.KLGangLongCCT ?? 0);
            ws.Cell(r, 5).Value = data.Sum(d => d.KLThepPhe  ?? 0);
            for (int i = 0; i < n; i++)
            {
                var hkId = headerKeys[i].Id;
                ws.Cell(r, s + i).Value = data.Sum(d =>
                    d.PhuGia.TryGetValue(hkId, out var kl) ? kl ?? 0 : 0);
            }
            ws.Cell(r, a    ).Value = data.Sum(d => d.O2 ?? 0);
            ws.Cell(r, a + 1).Value = data.Sum(d => d.N2 ?? 0);
            ApplyDataRowBorder(ws, r, lastCol);
            r++;

            // Lượng tồn
            foreach (var lt in luongTons)
            {
                ws.Range(r, 1, r, 2).Merge();
                ws.Cell(r, 1).Value = lt.TenLuongTon;
                ws.Cell(r, 3).Value = Num(lt.GiaTri);
                r++;
            }

            ws.SheetView.FreezeRows(DataRow - 1);
            ws.Columns().AdjustToContents();
        }

        private static void BofHeaderMergeVert(IXLWorksheet ws, int col, string text)
        {
            ws.Range(6, col, 7, col).Merge();
            ApplyBOFHeaderStyle(ws.Cell(6, col), text);
        }

        private static void BofHeaderCell(IXLWorksheet ws, int row, int col, string text)
            => ApplyBOFHeaderStyle(ws.Cell(row, col), text);

        private static void ApplyBOFHeaderStyle(IXLCell cell, string text)
        {
            cell.Value                           = text;
            cell.Style.Font.Bold                 = true;
            cell.Style.Alignment.Horizontal      = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical        = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText        = true;
            cell.Style.Fill.BackgroundColor      = XLColor.FromHtml("#D9D9D9");
            cell.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.Black;
        }

        private static void ApplyBorderOnly(IXLCell cell)
        {
            cell.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.Black;
        }

        private static void ApplyDataRowBorder(IXLWorksheet ws, int row, int lastCol)
        {
            for (int col = 1; col <= lastCol; col++)
                ApplyBorderOnly(ws.Cell(row, col));
        }

        /// <summary>
        /// Áp viền cho cả khối bảng (header 6-7, body, hàng tổng, dòng trắng trước footer nếu có, footer)
        /// sau khi đã merge xong. Trong: thin; bao ngoài cùng: medium.
        /// </summary>
        private static void ApplyUnifiedTableGridBorders(IXLWorksheet ws, int firstRow, int lastRow, int lastCol)
        {
            if (lastRow < firstRow || lastCol < 1)
                return;

            var rng = ws.Range(firstRow, 1, lastRow, lastCol);
            rng.Style.Border.InsideBorder       = XLBorderStyleValues.Thin;
            rng.Style.Border.InsideBorderColor  = XLColor.Black;
            rng.Style.Border.OutsideBorder      = XLBorderStyleValues.Medium;
            rng.Style.Border.OutsideBorderColor = XLColor.Black;
        }

        // -------------------------------------------------------
        // Xóa sạch nội dung + merge từ row startRow trở xuống
        // -------------------------------------------------------
        private static void ClearRowsFrom(IXLWorksheet ws, int startRow)
        {
            // Unmerge bất kỳ range nào chạm vào hoặc sau startRow
            // (kể cả merge bắt đầu từ row 1-3 nhưng kéo dài xuống row 4+)
            var toUnmerge = ws.MergedRanges
                .Where(m => m.RangeAddress.LastAddress.RowNumber >= startRow)
                .ToList();
            foreach (var m in toUnmerge)
                m.Unmerge();

            // Clear nội dung (không Delete để tránh shift rows gây corrupt file)
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? (startRow - 1);
            for (int r = startRow; r <= lastRow; r++)
                ws.Row(r).Clear(XLClearOptions.Contents);
        }

        // -------------------------------------------------------
        // lastCol = (cols cố định trước) + (phụ liệu động) + (cols cố định sau)
        //   BOF: 5 + N + 3  → Nhiên liệu (Oxy | Nito) + Ghi chú
        //   LF : 4 + N + 4  → Khí (Argon) + Que lấy mẫu + Que đo nhiệt + Ghi chú
        //   RH : 4 + N + 6  → Khí (Argon | Nito | Oxi) + Que lấy mẫu + Que đo nhiệt + Ghi chú
        // -------------------------------------------------------
        private static int ComputeLastCol(string bieuMau, int dynamicCount, int phanBoCount = 0)
        {
            int startCol    = GetPhuLieuStartCol(bieuMau); // 6 (BOF) hoặc 5 (LF/RH)
            int fixedBefore = startCol - 1;
            string key = bieuMau.ToUpperInvariant();
            int fixedAfter = key.Contains("BOF") ? 3
                           : key.Contains("RH")  ? 6
                           :                       4; // LF
            return fixedBefore + dynamicCount + fixedAfter + phanBoCount;
        }

        // -------------------------------------------------------
        // Rows 1-3: re-merge theo lastCol — KHÔNG sửa nội dung cells, KHÔNG đụng drawing float
        //   Vùng A: A1:A3 (thông tin biểu mẫu — giữ nguyên content từ template)
        //   Vùng giữa: B1:(lastCol-1)3 (logo float, chỉ merge cells)
        //   Vùng phải: lastCol1:lastCol3 (thông tin phụ — lấy từ merge gốc cuối cùng)
        // -------------------------------------------------------
        // private static void UpdateHeaderRowMerges(IXLWorksheet ws, int lastCol)
        // {
        //     var allMerges = ws.MergedRanges
        //         .Where(m => m.RangeAddress.FirstAddress.RowNumber >= 1
        //                  && m.RangeAddress.LastAddress.RowNumber  <= 3)
        //         .OrderByDescending(m => m.RangeAddress.FirstAddress.ColumnNumber)
        //         .ToList();

        //     // Lưu nội dung vùng phải (merge xa nhất bên phải = thông tin phụ)
        //     XLCellValue rightValue = Blank.Value;
        //     if (allMerges.Count >= 2)
        //     {
        //         var rm = allMerges.First();
        //         rightValue = ws.Cell(rm.RangeAddress.FirstAddress.RowNumber,
        //                              rm.RangeAddress.FirstAddress.ColumnNumber).Value;
        //     }

        //     foreach (var m in allMerges)
        //         m.Unmerge();

        //     // 1. A1:A3 — giữ content col A, chỉ re-merge
        //     ws.Range(1, 1, 3, 1).Merge();
        //     ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

        //     // 2. B1:(lastCol-1)3 — vùng giữa (logo float không bị ảnh hưởng)
        //     if (lastCol > 2)
        //         ws.Range(1, 2, 3, lastCol - 1).Merge();

        //     // 3. lastCol1:lastCol3 — thông tin phụ, căn trên-phải
        //     ws.Range(1, lastCol, 3, lastCol).Merge();
        //     ws.Cell(1, lastCol).Value = rightValue;
        //     ws.Cell(1, lastCol).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        //     ws.Cell(1, lastCol).Style.Alignment.Vertical   = XLAlignmentVerticalValues.Top;
        // }
        private static void UpdateHeaderRowMerges(IXLWorksheet ws, int lastCol)
        {
            if (ws == null) throw new ArgumentNullException(nameof(ws));
            if (lastCol < 1) throw new ArgumentException("lastCol phải >= 1");

            // 1. Lấy info ban đầu từ A1
            var info = ws.Cell(1, 1).Value;

            // 2. Lấy tất cả merge trong vùng header (row 1 -> 3)
            var headerMerges = ws.MergedRanges
                .Where(m => m.RangeAddress.FirstAddress.RowNumber >= 1
                        && m.RangeAddress.LastAddress.RowNumber  <= 3)
                .ToList();

            // 3. Unmerge toàn bộ header
            foreach (var m in headerMerges)
                m.Unmerge();

            // 4. Merge full từ A1 -> lastCol (3 dòng)
            var mergeRange = ws.Range(1, 1, 3, lastCol);
            mergeRange.Merge();

            // 5. Gán lại giá trị info vào A1 (cell gốc của merge)
            var cell = ws.Cell(1, 1);
            cell.Value = info;

            // 6. Style: hiển thị bên phải + căn trên
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Top;

            // (Optional) format thêm nếu cần
            // cell.Style.Font.Bold = true;
            // cell.Style.Font.FontSize = 12;

            // 7. Đảm bảo wrap text nếu info dài
            cell.Style.Alignment.WrapText = true;
        }

        // -------------------------------------------------------
        // Rows 4-5: tên biểu mẫu + thông tin kíp
        // -------------------------------------------------------
        private static void RenderInfoRows(IXLWorksheet ws, string bieuMau,
            List<HRC2ThongKeRow> rows, int lastCol,
            int? scope = null, string gioBatDau = "", string gioKetThuc = "",
            DateOnly? ngayPhieu = null, int? caPhieu = null, string kip = "")
        {
            var d = rows.FirstOrDefault()?.Data;
            var caValue = caPhieu ?? d?.Ca ?? 0;

            var rawDate = d?.Ngay ?? d?.NgaySx; // dùng fallback khi không có ngay từ BM_Phieu
            string ngayStr = ngayPhieu.HasValue ? ngayPhieu.Value.ToString("dd/MM/yyyy") : (rawDate?.ToString("dd/MM/yyyy") ?? "");

            string key   = bieuMau.ToUpperInvariant();
            string tenBm = key.Contains("BOF") ? $"BIÊN BẢN TIÊU HAO NẤU LUYỆN LÒ THỔI {scope}"
                         : key.Contains("LF")  ? "BẢNG TIÊU HAO NẤU LUYỆN LÒ TINH LUYỆN LF"
                         : key.Contains("RH")  ? $"BẢNG TIÊU HAO NẤU LUYỆN LÒ TINH LUYỆN RH {scope}"
                         : bieuMau;

            // Row 4: tên biểu mẫu — bold, font 13, căn giữa
            ws.Range(4, 1, 4, lastCol).Merge();
            var c4 = ws.Cell(4, 1);
            c4.Value                      = tenBm;
            c4.Style.Font.Bold            = true;
            c4.Style.Font.FontSize        = 13;
            c4.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c4.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

            // Row 5: thông tin kíp — italic, căn giữa
            ws.Range(5, 1, 5, lastCol).Merge();
            var c5 = ws.Cell(5, 1);

            // Tính giờ bắt đầu/kết thúc. Nếu tham số rỗng thì tự suy ra theo ca.
            string gioBatDauLocal  = string.IsNullOrWhiteSpace(gioBatDau) ? "" : gioBatDau;
            string gioKetThucLocal = string.IsNullOrWhiteSpace(gioKetThuc) ? "" : gioKetThuc;
            string ngayKetThuc     = ngayStr;

            if (string.IsNullOrWhiteSpace(gioBatDauLocal) || string.IsNullOrWhiteSpace(gioKetThucLocal))
            {
                if (caValue == 1)
                {
                    gioBatDauLocal  = "08 giờ 00";
                    gioKetThucLocal = "20 giờ 00";
                }
                else if (caValue == 2)
                {
                    gioBatDauLocal  = "20 giờ 00";
                    gioKetThucLocal = "08 giờ 00";

                    if (ngayPhieu.HasValue) ngayKetThuc = ngayPhieu.Value.AddDays(1).ToString("dd/MM/yyyy");
                    else if (rawDate.HasValue) ngayKetThuc = rawDate.Value.AddDays(1).ToString("dd/MM/yyyy");
                }
            }

            var kipSuffix = string.IsNullOrWhiteSpace(kip) ? "" : kip;
            c5.Value = $"Kíp {caValue}{kipSuffix}: Từ {gioBatDauLocal} ngày {ngayStr} đến {gioKetThucLocal} ngày {ngayKetThuc}";
            c5.Style.Font.Italic          = true;
            c5.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c5.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        }

        // ===============================================================
        // Rows 6-7: column headers
        //   Cột cố định        → merge dọc rows 6-7
        //   Cột phụ liệu động  → row 6 = tên (header cha), row 7 = rỗng (header con)
        //   Nhóm cột ghép      → row 6 merge ngang, row 7 = tên từng cột con
        //   Cột đơn sau nhóm   → merge dọc rows 6-7
        // ===============================================================

        // BOF: startCol=6 | fixed before: 5 cols | fixed after: Nhiên liệu(2) + GhiChu(1) | phân bổ: P cols
        private static void RenderColumnHeaders_BOF(IXLWorksheet ws,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders)
        {
            int s = GetPhuLieuStartCol("BOF"); // 6

            MergeVertCell(ws, 1, "STT");
            MergeVertCell(ws, 2, "Mẻ nấu số");
            MergeVertCell(ws, 3, "Mác thép");
            MergeHorizCell(ws, HeaderParentRow, 4, 5, "Nguyên liệu đầu vào (tấn)");
            HeaderCell(ws, HeaderChildRow, 4, "Gang lỏng");
            HeaderCell(ws, HeaderChildRow, 5, "Thép phế");

            if (headers.Count > 0)
            {
                MergeHorizCell(ws, HeaderParentRow, s, s + headers.Count - 1, "Phụ gia công nghệ (Kg)");
                for (int i = 0; i < headers.Count; i++)
                    HeaderCell(ws, HeaderChildRow, s + i, headers[i].TenPhuLieu);
            }

            int a = s + headers.Count;
            MergeHorizCell(ws, HeaderParentRow, a, a + 1, "Nhiên liệu (m³)");
            HeaderCell(ws, HeaderChildRow, a,     "Oxy");
            HeaderCell(ws, HeaderChildRow, a + 1, "Nito");
            MergeVertCell(ws, a + 2, "Ghi chú");

            RenderPhanBoHeaders(ws, a + 3, phanBoHeaders);
        }

        // LF: startCol=5 | fixed before: 4 cols | fixed after: Khí(1) + 3 cols đơn | phân bổ: P cols
        private static void RenderColumnHeaders_LF(IXLWorksheet ws,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders)
        {
            int s = GetPhuLieuStartCol("LF"); // 5

            MergeVertCell(ws, 1, "STT");
            MergeVertCell(ws, 2, "Mẻ nấu số");
            MergeVertCell(ws, 3, "Mác thép");
            MergeVertCell(ws, 4, "Khối lượng thép lỏng (tấn)\n(Tính cả thùng thép)");

            if (headers.Count > 0)
            {
                var (klList, pgList) = SplitByLoaiPhieuGroup(headers);
                int col = s;
                if (klList.Count > 0)
                {
                    MergeHorizCell(ws, HeaderParentRow, col, col + klList.Count - 1, "Chất hợp kim hóa");
                    for (int i = 0; i < klList.Count; i++)
                        HeaderCell(ws, HeaderChildRow, col + i, klList[i].TenPhuLieu);
                    col += klList.Count;
                }
                if (pgList.Count > 0)
                {
                    MergeHorizCell(ws, HeaderParentRow, col, col + pgList.Count - 1, "Phụ gia & chất khử oxy");
                    for (int i = 0; i < pgList.Count; i++)
                        HeaderCell(ws, HeaderChildRow, col + i, pgList[i].TenPhuLieu);
                }
            }

            int a = s + headers.Count;
            HeaderCell(ws, HeaderParentRow, a,     "Khí");
            HeaderCell(ws, HeaderChildRow,  a,     "Argon");
            MergeVertCell(ws, a + 1, "Que lấy mẫu");
            MergeVertCell(ws, a + 2, "Que đo nhiệt");
            MergeVertCell(ws, a + 3, "Ghi chú");

            RenderPhanBoHeaders(ws, a + 4, phanBoHeaders);
        }

        // RH: startCol=5 | fixed before: 4 cols | fixed after: Khí(3) + 3 cols đơn | phân bổ: P cols
        private static void RenderColumnHeaders_RH(IXLWorksheet ws,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders)
        {
            int s = GetPhuLieuStartCol("RH"); // 5

            MergeVertCell(ws, 1, "STT");
            MergeVertCell(ws, 2, "Mẻ nấu số");
            MergeVertCell(ws, 3, "Mác thép");
            MergeVertCell(ws, 4, "Khối lượng thép lỏng (tấn)\n(Tính cả thùng thép)");

            if (headers.Count > 0)
            {
                MergeHorizCell(ws, HeaderParentRow, s, s + headers.Count - 1, "Chất hợp kim hóa");
                for (int i = 0; i < headers.Count; i++)
                    HeaderCell(ws, HeaderChildRow, s + i, headers[i].TenPhuLieu);
            }

            int a = s + headers.Count;
            MergeHorizCell(ws, HeaderParentRow, a, a + 2, "Khí (m³)");
            HeaderCell(ws, HeaderChildRow, a,     "Argon");
            HeaderCell(ws, HeaderChildRow, a + 1, "Nito");
            HeaderCell(ws, HeaderChildRow, a + 2, "Oxi");
            MergeVertCell(ws, a + 3, "Que lấy mẫu (cái)");
            MergeVertCell(ws, a + 4, "Que đo nhiệt (cái)");
            MergeVertCell(ws, a + 5, "Ghi chú");

            RenderPhanBoHeaders(ws, a + 6, phanBoHeaders);
        }

        /// <summary>Render group header "Phân bổ" + tên từng cột phân bổ bắt đầu từ startCol.</summary>
        private static void RenderPhanBoHeaders(IXLWorksheet ws, int startCol, List<PhuLieuHeaderTable> phanBoHeaders)
        {
            if (phanBoHeaders.Count == 0) return;

            // Row 6: group header "Phân bổ" span toàn bộ các cột phân bổ
            MergeHorizCell(ws, HeaderParentRow, startCol, startCol + phanBoHeaders.Count - 1, "Phân bổ");

            // Row 7: tên từng cột
            for (int i = 0; i < phanBoHeaders.Count; i++)
                HeaderCell(ws, HeaderChildRow, startCol + i, phanBoHeaders[i].TenPhuLieu);
        }

        // ===============================================================
        // Rows 8+: data rows
        // ===============================================================

        private static void RenderDataRows_BOF(IXLWorksheet ws,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows, int dataStartRow, int lastCol)
        {
            int s = GetPhuLieuStartCol("BOF"); // 6
            int r = dataStartRow;
            int n = 1;

            foreach (var row in rows)
            {
                var d   = row.Data!;
                var vm  = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia);
                var pbm = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.KLPhanBo);

                ws.Cell(r, 1).Value = n++;
                ws.Cell(r, 2).Value = d.MeThoi  ?? "";
                ws.Cell(r, 3).Value = d.MacThep ?? "";
                ws.Cell(r, 4).Value = Num(d.KLGangLongCCT);
                ws.Cell(r, 5).Value = Num((d.KLThepPhe ?? 0) + (d.KLThepPheGang ?? 0));

                for (int i = 0; i < headers.Count; i++)
                    ws.Cell(r, s + i).Value = vm.TryGetValue(headers[i].IDHeaderKey, out var kl) ? Num(kl) : Blank.Value;

                int a = s + headers.Count;
                ws.Cell(r, a++).Value = Num(d.O2);
                ws.Cell(r, a++).Value = Num(d.N2);
                ws.Cell(r, a++).Value = d.GhiChu ?? "";

                for (int i = 0; i < phanBoHeaders.Count; i++)
                    ws.Cell(r, a + i).Value = pbm.TryGetValue(phanBoHeaders[i].IDHeaderKey, out var pb) ? Num(pb) : Blank.Value;

                r++;
            }
        }

        private static void RenderDataRows_LF(IXLWorksheet ws,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows, int dataStartRow, int lastCol)
        {
            int s = GetPhuLieuStartCol("LF"); // 5
            int r = dataStartRow;
            int n = 1;

            foreach (var row in rows)
            {
                var d   = row.Data!;
                var vm  = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia);
                var pbm = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.KLPhanBo);

                ws.Cell(r, 1).Value = n++;
                ws.Cell(r, 2).Value = d.MeThoi  ?? "";
                ws.Cell(r, 3).Value = d.MacThep ?? "";
                ws.Cell(r, 4).Value = Num(d.KLThepLong);

                for (int i = 0; i < headers.Count; i++)
                    ws.Cell(r, s + i).Value = vm.TryGetValue(headers[i].IDHeaderKey, out var kl) ? Num(kl) : Blank.Value;

                int a = s + headers.Count;
                ws.Cell(r, a++).Value = Num(d.AR_LF);
                ws.Cell(r, a++).Value = NumInt(d.QueLayMau);
                ws.Cell(r, a++).Value = NumInt(d.QueDoNhiet);
                ws.Cell(r, a++).Value = d.GhiChu ?? "";

                for (int i = 0; i < phanBoHeaders.Count; i++)
                    ws.Cell(r, a + i).Value = pbm.TryGetValue(phanBoHeaders[i].IDHeaderKey, out var pb) ? Num(pb) : Blank.Value;

                r++;
            }
        }

        private static void RenderDataRows_RH(IXLWorksheet ws,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows, int dataStartRow, int lastCol)
        {
            int s = GetPhuLieuStartCol("RH"); // 5
            int r = dataStartRow;
            int n = 1;

            foreach (var row in rows)
            {
                var d   = row.Data!;
                var vm  = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia);
                var pbm = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.KLPhanBo);

                ws.Cell(r, 1).Value = n++;
                ws.Cell(r, 2).Value = d.MeThoi  ?? "";
                ws.Cell(r, 3).Value = d.MacThep ?? "";
                ws.Cell(r, 4).Value = Num(d.KLThepLong);

                for (int i = 0; i < headers.Count; i++)
                    ws.Cell(r, s + i).Value = vm.TryGetValue(headers[i].IDHeaderKey, out var kl) ? Num(kl) : Blank.Value;

                int a = s + headers.Count;
                ws.Cell(r, a++).Value = Num(d.AR_RH);
                ws.Cell(r, a++).Value = Num(d.N2);
                ws.Cell(r, a++).Value = Num(d.O2);
                ws.Cell(r, a++).Value = NumInt(d.QueLayMau);
                ws.Cell(r, a++).Value = NumInt(d.QueDoNhiet);
                ws.Cell(r, a++).Value = d.GhiChu ?? "";

                for (int i = 0; i < phanBoHeaders.Count; i++)
                    ws.Cell(r, a + i).Value = pbm.TryGetValue(phanBoHeaders[i].IDHeaderKey, out var pb) ? Num(pb) : Blank.Value;

                r++;
            }
        }

        // -------------------------------------------------------
        // Total rows (sau data, trước footer)
        // -------------------------------------------------------

        private static void RenderTotalRow_BOF(IXLWorksheet ws, int r, int lastCol,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows)
        {
            int s = GetPhuLieuStartCol("BOF"); // 6

            ws.Range(r, 1, r, lastCol).Style.Font.Bold = true;

            ws.Range(r, 1, r, 3).Merge();
            ws.Cell(r, 1).Value                      = "Tổng cộng";
            ws.Cell(r, 1).Style.Font.Bold            = true;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 4).Value = (XLCellValue)rows.Sum(x => x.Data?.KLGangLongCCT ?? 0);
            ws.Cell(r, 5).Value = (XLCellValue)rows.Sum(x => (x.Data?.KLThepPhe ?? 0) + (x.Data?.KLThepPheGang ?? 0));

            for (int i = 0; i < headers.Count; i++)
            {
                var hId = headers[i].IDHeaderKey;
                ws.Cell(r, s + i).Value = (XLCellValue)rows.Sum(x =>
                    x.Values.FirstOrDefault(v => v.IDHeaderKey == hId)?.TotalKLPhuGia ?? 0);
            }

            int a = s + headers.Count;
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.O2 ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.N2 ?? 0);
            a++; // Ghi chú — bỏ qua

            for (int i = 0; i < phanBoHeaders.Count; i++)
            {
                var hId = phanBoHeaders[i].IDHeaderKey;
                ws.Cell(r, a + i).Value = (XLCellValue)rows.Sum(x =>
                    x.Values.FirstOrDefault(v => v.IDHeaderKey == hId)?.KLPhanBo ?? 0);
            }

        }

        private static void RenderTotalRow_LF(IXLWorksheet ws, int r, int lastCol,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows)
        {
            int s = GetPhuLieuStartCol("LF"); // 5

            ws.Range(r, 1, r, lastCol).Style.Font.Bold = true;

            ws.Range(r, 1, r, 3).Merge();
            ws.Cell(r, 1).Value                      = "Tổng cộng";
            ws.Cell(r, 1).Style.Font.Bold            = true;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 4).Value = (XLCellValue)rows.Sum(x => x.Data?.KLThepLong ?? 0);

            for (int i = 0; i < headers.Count; i++)
            {
                var hId = headers[i].IDHeaderKey;
                ws.Cell(r, s + i).Value = (XLCellValue)rows.Sum(x =>
                    x.Values.FirstOrDefault(v => v.IDHeaderKey == hId)?.TotalKLPhuGia ?? 0);
            }

            int a = s + headers.Count;
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.AR_LF ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.QueLayMau ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.QueDoNhiet ?? 0);
            a++; // Ghi chú — bỏ qua

            for (int i = 0; i < phanBoHeaders.Count; i++)
            {
                var hId = phanBoHeaders[i].IDHeaderKey;
                ws.Cell(r, a + i).Value = (XLCellValue)rows.Sum(x =>
                    x.Values.FirstOrDefault(v => v.IDHeaderKey == hId)?.KLPhanBo ?? 0);
            }
        }

        private static void RenderTotalRow_RH(IXLWorksheet ws, int r, int lastCol,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows)
        {
            int s = GetPhuLieuStartCol("RH"); // 5

            ws.Range(r, 1, r, lastCol).Style.Font.Bold = true;

            ws.Range(r, 1, r, 3).Merge();
            ws.Cell(r, 1).Value                      = "Tổng cộng";
            ws.Cell(r, 1).Style.Font.Bold            = true;
            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(r, 4).Value = (XLCellValue)rows.Sum(x => x.Data?.KLThepLong ?? 0);

            for (int i = 0; i < headers.Count; i++)
            {
                var hId = headers[i].IDHeaderKey;
                ws.Cell(r, s + i).Value = (XLCellValue)rows.Sum(x =>
                    x.Values.FirstOrDefault(v => v.IDHeaderKey == hId)?.TotalKLPhuGia ?? 0);
            }

            int a = s + headers.Count;
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.AR_RH ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.N2    ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.O2    ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.QueLayMau  ?? 0);
            ws.Cell(r, a++).Value = (XLCellValue)rows.Sum(x => x.Data?.QueDoNhiet ?? 0);
            a++; // Ghi chú — bỏ qua

            for (int i = 0; i < phanBoHeaders.Count; i++)
            {
                var hId = phanBoHeaders[i].IDHeaderKey;
                ws.Cell(r, a + i).Value = (XLCellValue)rows.Sum(x =>
                    x.Values.FirstOrDefault(v => v.IDHeaderKey == hId)?.KLPhanBo ?? 0);
            }
        }

        // -------------------------------------------------------
        // Header cell helpers
        // -------------------------------------------------------

        /// <summary>Merge dọc rows 6-7 cho cột col, set giá trị + style.</summary>
        private static void MergeVertCell(IXLWorksheet ws, int col, string text)
        {
            ws.Range(HeaderParentRow, col, HeaderChildRow, col).Merge();
            ApplyHeaderStyle(ws.Cell(HeaderParentRow, col), text);
        }

        /// <summary>Merge ngang một row khoảng cột [c1, c2], set giá trị + style.</summary>
        private static void MergeHorizCell(IXLWorksheet ws, int row, int c1, int c2, string text)
        {
            ws.Range(row, c1, row, c2).Merge();
            ApplyHeaderStyle(ws.Cell(row, c1), text);
        }

        /// <summary>Set ô header đơn (không merge).</summary>
        private static void HeaderCell(IXLWorksheet ws, int row, int col, string text)
            => ApplyHeaderStyle(ws.Cell(row, col), text);

        private static void ApplyHeaderStyle(IXLCell cell, string text)
        {
            cell.Value                             = text;
            cell.Style.Font.Bold                   = true;
            cell.Style.Alignment.Horizontal        = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical          = XLAlignmentVerticalValues.Center;
            cell.Style.Alignment.WrapText          = true;
            cell.Style.Border.OutsideBorder        = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor   = XLColor.Black;
        }

        // -------------------------------------------------------
        // Footer configs (BOF / RH)
        // -------------------------------------------------------

        private static readonly FooterConfig BofFooterConfig = new();
        private static readonly FooterConfig RhFooterConfig  = new();

        /// <summary>
        /// Render phần footer POST-BODY (sau data + 1 dòng trắng) gồm 3 phần:
        /// 1. Nhãn kíp — Tồn đầu kíp | Nhập trong kíp | Tồn cuối kíp
        /// 2. Khung lượng tồn (merge 4 vùng theo fixedTextCols)
        /// 3. Dòng Trưởng kíp / Người lập
        /// </summary>
        /// <returns>Hàng cuối cùng của khối footer (dòng ký tên).</returns>
        public static int RenderFooter(
            IXLWorksheet ws,
            int startRow,
            int lastCol,
            FooterConfig config,
            List<STD_XUAT_NHAP_TON_HRC2>? footerData = null)
        {
            int N = lastCol;

            // Boundaries cố định 4 cột/nhóm, tính từ cuối (1-based)
            // Đảm bảo g1s >= 2 để luôn có ít nhất 1 cột silo bên trái
            int g1s = Math.Max(2, N - 11); // Tồn đầu kíp: start
            int g1e = N - 8;               // Tồn đầu kíp: end
            int g2s = N - 7;               // Nhập trong kíp: start
            int g2e = N - 4;               // Nhập trong kíp: end
            int g3s = N - 3;               // Tồn cuối kíp: start
            int g3e = N;                   // Tồn cuối kíp: end

            int r = startRow;

            // ── Row header ──────────────────────────────────────────────
            // Tồn trên silo: col 1 → g1s-1
            ws.Range(r, 1,   r, g1s - 1).Merge();
            SetFooterLabelStyle(ws.Cell(r, 1), "Tồn trên silo");
            // 3 nhóm
            ws.Range(r, g1s, r, g1e).Merge();
            SetFooterLabelStyle(ws.Cell(r, g1s), config.LabelTonDauKip);
            ws.Range(r, g2s, r, g2e).Merge();
            SetFooterLabelStyle(ws.Cell(r, g2s), config.LabelNhapTrongKip);
            ws.Range(r, g3s, r, g3e).Merge();
            SetFooterLabelStyle(ws.Cell(r, g3s), config.LabelTonCuoiKip);
            ApplyBorderOnly(ws.Cell(r, 1));
            ApplyBorderOnly(ws.Cell(r, g1s));
            ApplyBorderOnly(ws.Cell(r, g2s));
            ApplyBorderOnly(ws.Cell(r, g3s));
            r++;

            // ── Label rows ────────────────────────────────────────────────
            if (footerData?.Count > 0)
            {
                foreach (var item in footerData)
                {
                    ws.Range(r, 1, r, g1s - 1).Merge();
                    ws.Cell(r, 1).Value                      = item.TenNguyenLieu ?? "";
                    ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    ws.Cell(r, 1).Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                    ApplyBorderOnly(ws.Cell(r, 1));

                    ws.Range(r, g1s, r, g1e).Merge();
                    if (item.TonDauCa.HasValue) ws.Cell(r, g1s).Value = (double)item.TonDauCa.Value;
                    ApplyBorderOnly(ws.Cell(r, g1s));

                    ws.Range(r, g2s, r, g2e).Merge();
                    if (item.NhapVaoTrongCa.HasValue) ws.Cell(r, g2s).Value = (double)item.NhapVaoTrongCa.Value;
                    ApplyBorderOnly(ws.Cell(r, g2s));

                    ws.Range(r, g3s, r, g3e).Merge();
                    if (item.TonCuoiCa.HasValue) ws.Cell(r, g3s).Value = (double)item.TonCuoiCa.Value;
                    ApplyBorderOnly(ws.Cell(r, g3s));
                    r++;
                }
            }
            else
            {
                foreach (var label in config.LuongTonLabels)
                {
                    ws.Range(r, 1,   r, g1s - 1).Merge();
                    ws.Cell(r, 1).Value                      = label;
                    ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    ws.Cell(r, 1).Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                    ApplyBorderOnly(ws.Cell(r, 1));

                    ws.Range(r, g1s, r, g1e).Merge();
                    ApplyBorderOnly(ws.Cell(r, g1s));
                    ws.Range(r, g2s, r, g2e).Merge();
                    ApplyBorderOnly(ws.Cell(r, g2s));
                    ws.Range(r, g3s, r, g3e).Merge();
                    ApplyBorderOnly(ws.Cell(r, g3s));
                    r++;
                }
            }
            // Footer block kết thúc ở dòng ngay trước dòng chữ ký.
            return Math.Max(startRow, r - 1);
        }

        /// <summary>
        /// Render dòng chữ ký ở ngoài khối footer bảng (không nằm trong range apply border chung).
        /// </summary>
        private static void RenderSignatureRow(IXLWorksheet ws, int signRow, int lastCol, FooterConfig config, string key,
            string? truongKipName = null, string? nguoiLapName = null)
        {
            int N = lastCol;
            int g3s = N - 3; // Tồn cuối kíp: start
            int g3e = N;     // Tồn cuối kíp: end
            int leftEnd = g3s - 1;
            string leftLabel = key.Contains("BOF") ? config.LabelTruongKip : "Trưởng/Phó kíp";
            if (leftEnd >= 1)
            {
                ws.Range(signRow, 1, signRow, leftEnd).Merge();
                ws.Cell(signRow, 1).Value = leftLabel;
                ws.Cell(signRow, 1).Style.Font.Bold = true;
                ws.Cell(signRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(signRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            if (g3s <= g3e)
            {
                ws.Range(signRow, g3s, signRow, g3e).Merge();
                ws.Cell(signRow, g3s).Value = config.LabelNguoiLap;
                ws.Cell(signRow, g3s).Style.Font.Bold = true;
                ws.Cell(signRow, g3s).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(signRow, g3s).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }

            if (!string.IsNullOrWhiteSpace(truongKipName) || !string.IsNullOrWhiteSpace(nguoiLapName))
            {
                int nameRow = signRow + 1;
                if (leftEnd >= 1 && !string.IsNullOrWhiteSpace(truongKipName))
                {
                    ws.Range(nameRow, 1, nameRow, leftEnd).Merge();
                    ws.Cell(nameRow, 1).Value = truongKipName;
                    ws.Cell(nameRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(nameRow, 1).Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                }
                if (g3s <= g3e && !string.IsNullOrWhiteSpace(nguoiLapName))
                {
                    ws.Range(nameRow, g3s, nameRow, g3e).Merge();
                    ws.Cell(nameRow, g3s).Value = nguoiLapName;
                    ws.Cell(nameRow, g3s).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(nameRow, g3s).Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                }
            }
        }

        private static void SetFooterLabelStyle(IXLCell cell, string text)
        {
            cell.Value                      = text;
            cell.Style.Font.Bold            = true;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        }

        // -------------------------------------------------------
        // Shared helpers
        // -------------------------------------------------------

        private static XLCellValue Num(double? v)   => v.HasValue ? (XLCellValue)v.Value : Blank.Value;
        private static XLCellValue NumInt(int? v)    => v.HasValue ? (XLCellValue)v.Value : Blank.Value;

        // ===============================================================
        // PDF Export — layout giống Excel (DinkToPdf)
        // ===============================================================

        public async Task<ExportFileResult> ExportPdfDetailAsync(
            DateOnly ngay, int ca, string bieuMau, int scope, Guid idPhieu,
            string gioBatDau = "", string gioKetThuc = "")
        {
            var (headersBOF, headersLF, headersRH, rows) = await GetExportDataAsync(ngay, ca, bieuMau, scope, idPhieu);
            var imageSignsDto = await _pheDuyetService.GetPheDuyetPhieuAsync(idPhieu);

            // Footer HRC2_BB_NauLuyen có 2 vị trí ký:
            // - CapDuyet = 1: Trưởng/Phó kíp
            // - CapDuyet = 0: Người lập
            var truongKip = imageSignsDto?.FirstOrDefault(x => x.CapDuyet == 1);
            var nguoiLap = imageSignsDto?.FirstOrDefault(x => x.CapDuyet == 0);
            string chuKyTruongKipHtml = _pheDuyetService.FormatChuKy(truongKip?.ChuKy);
            string chuKyNguoiLapHtml = _pheDuyetService.FormatChuKy(nguoiLap?.ChuKy);
            string? truongKipName = truongKip?.HoVaTen;
            string? nguoiLapName  = nguoiLap?.HoVaTen;

            // Footer data lấy từ phiếu HRC2_STD_NXT (khác phiếu biên bản) → filter bằng (Ca, NgaySX, STD_Scope)
            int? stdScope = GetStdScope(bieuMau, scope);
            List<STD_XUAT_NHAP_TON_HRC2> footerData;
            if (stdScope.HasValue)
            {
                var ngaySxDt = ngay.ToDateTime(TimeOnly.MinValue);
                footerData = await _context.STD_XUAT_NHAP_TON_HRC2s
                    .Where(x => x.Ca == ca && x.NgaySX.Date == ngaySxDt.Date && x.Scope == stdScope.Value)
                    .OrderBy(x => x.ViTri)
                    .ToListAsync();
            }
            else
            {
                footerData = new List<STD_XUAT_NHAP_TON_HRC2>();
            }

            bool isBof = bieuMau.Equals("BOF", StringComparison.OrdinalIgnoreCase);
            bool isRh = bieuMau.Equals("RH", StringComparison.OrdinalIgnoreCase);
            var headers = isBof ? headersBOF : (isRh ? headersRH : headersLF);
            // Phân bổ đã được gộp vào TotalKLPhuGia — không render cột riêng
            var phanBoHeaders = new List<PhuLieuHeaderTable>();

            string templateName = bieuMau.ToUpperInvariant() switch
            {
                "BOF" => "HRC2_BB_NauLuyen_BOF",
                "LF"  => "HRC2_BB_NauLuyen_LF",
                "RH"  => "HRC2_BB_NauLuyen_RH",
                _     => bieuMau
            };

            var html = await BuildPdfHtmlAsync(
                bieuMau,
                ngay,
                ca,
                scope,
                gioBatDau,
                gioKetThuc,
                headers,
                phanBoHeaders,
                rows,
                chuKyTruongKipHtml,
                chuKyNguoiLapHtml,
                footerData,
                truongKipName,
                nguoiLapName);

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    PaperSize   = PaperKind.A4,
                    Orientation = DinkToPdf.Orientation.Landscape,
                    Margins     = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10, Unit = Unit.Millimeters }
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = html,
                        WebSettings = { DefaultEncoding = "utf-8" }
                    }
                }
            };

            return new ExportFileResult
            {
                Content     = _pdfConverter.Convert(doc),
                FileName    = $"{templateName}_Ca{ca}_{ngay:ddMMyyyy}.pdf",
                ContentType = "application/pdf"
            };
        }

        private async Task<string> BuildPdfHtmlAsync(
            string bieuMau, DateOnly ngay, int ca, int? scope,
            string gioBatDau, string gioKetThuc,
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows,
            string chuKyTruongKipHtml,
            string chuKyNguoiLapHtml,
            List<STD_XUAT_NHAP_TON_HRC2>? footerData = null,
            string? truongKipName = null,
            string? nguoiLapName = null)
        {
            var logoUrl = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";

            string key   = bieuMau.ToUpperInvariant();
            string tenBm = key.Contains("BOF") ? $"BIÊN BẢN TIÊU HAO NẤU LUYỆN LÒ THỔI {scope}"
                         : key.Contains("LF")  ? "BẢNG TIÊU HAO NẤU LUYỆN LÒ TINH LUYỆN LF"
                         : key.Contains("RH")  ? $"BẢNG TIÊU HAO NẤU LUYỆN LÒ TINH LUYỆN RH {scope}"
                         : bieuMau;

            // Ngày/ca cho InfoKip
            string ngayStr = ngay.ToString("dd/MM/yyyy");
            string caStr   = ca.ToString();

            // Nếu tham số gioBatDau/gioKetThuc rỗng thì tự tính như Excel
            string gioBatDauLocal  = string.IsNullOrWhiteSpace(gioBatDau) ? "" : gioBatDau;
            string gioKetThucLocal = string.IsNullOrWhiteSpace(gioKetThuc) ? "" : gioKetThuc;
            string ngayKetThuc     = ngayStr;

            if (string.IsNullOrWhiteSpace(gioBatDauLocal) || string.IsNullOrWhiteSpace(gioKetThucLocal))
            {
                if (ca == 1)
                {
                    gioBatDauLocal  = "08 giờ 00";
                    gioKetThucLocal = "20 giờ 00";
                }
                else if (ca == 2)
                {
                    gioBatDauLocal  = "20 giờ 00";
                    gioKetThucLocal = "08 giờ 00";
                    // tăng ngày +1 cho ca đêm
                    ngayKetThuc = ngay.AddDays(1).ToString("dd/MM/yyyy");
                }
            }

            string infoKip = $"Kíp {caStr}: Từ {gioBatDauLocal} ngày {ngayStr} đến {gioKetThucLocal} ngày {ngayKetThuc}";

            string bmConfigKey = key.Contains("BOF") ? "HRC2_BB_NauLuyen_BOF"
                               : key.Contains("LF")  ? "HRC2_BB_NauLuyen_LF"
                               :                       "HRC2_BB_NauLuyen_RH";
            string bmCode = await _bmConfig.GetBmCodeHtmlAsync(bmConfigKey);

            string thead = key.Contains("BOF") ? PdfThead_BOF(headers, phanBoHeaders)
                         : key.Contains("LF")  ? PdfThead_LF(headers, phanBoHeaders)
                         :                       PdfThead_RH(headers, phanBoHeaders);

            string tbody = key.Contains("BOF") ? PdfTbody_BOF(headers, phanBoHeaders, rows)
                         : key.Contains("LF")  ? PdfTbody_LF(headers, phanBoHeaders, rows)
                         :                       PdfTbody_RH(headers, phanBoHeaders, rows);

            int N = ComputeLastCol(
                key.Contains("BOF") ? "HRC2_BB_NauLuyen_BOF"
              : key.Contains("LF")  ? "HRC2_BB_NauLuyen_LF"
              :                       "HRC2_BB_NauLuyen_RH",
                headers.Count, phanBoHeaders.Count);

            string footer = (key.Contains("BOF") || key.Contains("RH") || key.Contains("LF"))
                ? PdfFooterHtml(
                    N,
                    key.Contains("BOF") ? BofFooterConfig : RhFooterConfig,
                    chuKyTruongKipHtml,
                    chuKyNguoiLapHtml,
                    footerData,
                    key,
                    truongKipName,
                    nguoiLapName)
                : "";

            // Load template và replace placeholder
            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "HRC2_BB_NauLuyen.html");
            var html = await File.ReadAllTextAsync(templatePath);

            return html
                .Replace("{{LogoUrl}}",    logoUrl)
                .Replace("{{BmCode}}",     bmCode)
                .Replace("{{TenBieuMau}}", tenBm)
                .Replace("{{InfoKip}}",    infoKip)
                .Replace("{{TheadRows}}",  thead)
                .Replace("{{TbodyRows}}",  tbody)
                .Replace("{{FooterHtml}}", footer);
        }

        // ── Thead helpers ───────────────────────────────────────────────

        private static string PdfThead_BOF(List<PhuLieuHeaderTable> h, List<PhuLieuHeaderTable> pb)
        {
            var r1 = new StringBuilder();
            var r2 = new StringBuilder();

            r1.Append("<th rowspan=\"2\">STT</th>");
            r1.Append("<th rowspan=\"2\">Mẻ nấu số</th>");
            r1.Append("<th rowspan=\"2\">Mác thép</th>");
            r1.Append("<th colspan=\"2\">Nguyên liệu đầu vào (tấn)</th>");
            r2.Append("<th>Gang lỏng</th><th>Thép phế</th>");

            if (h.Count > 0)
            {
                r1.Append($"<th colspan=\"{h.Count}\">Phụ gia công nghệ (Kg)</th>");
                foreach (var x in h) r2.Append($"<th>{x.TenPhuLieu}</th>");
            }

            r1.Append("<th colspan=\"2\">Nhiên liệu (m³)</th>");
            r2.Append("<th>Oxy</th><th>Nitơ</th>");
            r1.Append("<th rowspan=\"2\">Ghi chú</th>");

            if (pb.Count > 0)
            {
                r1.Append($"<th colspan=\"{pb.Count}\">Phân bổ</th>");
                foreach (var x in pb) r2.Append($"<th>{x.TenPhuLieu}</th>");
            }

            return $"<thead><tr>{r1}</tr><tr>{r2}</tr></thead>";
        }

        /// <summary>Gộp danh sách cột phụ liệu thành 2 khối liền nhau theo Header_Key.LoaiPhieu:
        /// KL (Chất hợp kim hóa) → PG (Phụ gia và chất khử oxy). LF/RH luôn gán đủ KL/PG cho mọi
        /// phụ liệu (không có giá trị khác/NULL trong thực tế) nên không cần bucket dự phòng.
        /// Thứ tự tương đối trong từng khối giữ nguyên theo ThuTu_Excel_*.</summary>
        private static (List<PhuLieuHeaderTable> Kl, List<PhuLieuHeaderTable> Pg) SplitByLoaiPhieuGroup(
            List<PhuLieuHeaderTable> headers)
        {
            var kl = headers.Where(h => h.LoaiPhieu == "KL").ToList();
            var pg = headers.Where(h => h.LoaiPhieu == "PG").ToList();
            return (kl, pg);
        }

        private static string PdfThead_LF(List<PhuLieuHeaderTable> h, List<PhuLieuHeaderTable> pb)
        {
            var r1 = new StringBuilder();
            var r2 = new StringBuilder();

            r1.Append("<th rowspan=\"2\">STT</th>");
            r1.Append("<th rowspan=\"2\">Mẻ nấu số</th>");
            r1.Append("<th rowspan=\"2\">Mác thép</th>");
            r1.Append("<th rowspan=\"2\">Khối lượng thép lỏng (tấn)(Tính cả thùng thép)</th>");

            if (h.Count > 0)
            {
                var (klList, pgList) = SplitByLoaiPhieuGroup(h);
                if (klList.Count > 0)
                {
                    r1.Append($"<th colspan=\"{klList.Count}\">Chất hợp kim hóa</th>");
                    foreach (var x in klList) r2.Append($"<th>{x.TenPhuLieu}</th>");
                }
                if (pgList.Count > 0)
                {
                    r1.Append($"<th colspan=\"{pgList.Count}\">Phụ gia và chất khử oxy</th>");
                    foreach (var x in pgList) r2.Append($"<th>{x.TenPhuLieu}</th>");
                }
            }

            r1.Append("<th>Khí</th>");
            r2.Append("<th>Argon</th>");
            r1.Append("<th rowspan=\"2\">Que lấy mẫu</th>");
            r1.Append("<th rowspan=\"2\">Que đo nhiệt</th>");
            r1.Append("<th rowspan=\"2\">Ghi chú</th>");

            if (pb.Count > 0)
            {
                r1.Append($"<th colspan=\"{pb.Count}\">Phân bổ</th>");
                foreach (var x in pb) r2.Append($"<th>{x.TenPhuLieu}</th>");
            }

            return $"<thead><tr>{r1}</tr><tr>{r2}</tr></thead>";
        }

        private static string PdfThead_RH(List<PhuLieuHeaderTable> h, List<PhuLieuHeaderTable> pb)
        {
            var r1 = new StringBuilder();
            var r2 = new StringBuilder();

            r1.Append("<th rowspan=\"2\">STT</th>");
            r1.Append("<th rowspan=\"2\">Mẻ nấu số</th>");
            r1.Append("<th rowspan=\"2\">Mác thép</th>");
            r1.Append("<th rowspan=\"2\">Khối lượng thép lỏng (tấn)(Tính cả thùng thép)</th>");

            if (h.Count > 0)
            {
                r1.Append($"<th colspan=\"{h.Count}\">Chất hợp kim hóa </th>");
                foreach (var x in h) r2.Append($"<th>{x.TenPhuLieu}</th>");
            }

            r1.Append("<th colspan=\"3\">Khí</th>");
            r2.Append("<th>Argon</th><th>Nito</th><th>Oxi</th>");
            r1.Append("<th rowspan=\"2\">Que lấy mẫu</th>");
            r1.Append("<th rowspan=\"2\">Que đo nhiệt</th>");
            r1.Append("<th rowspan=\"2\">Ghi chú</th>");

            if (pb.Count > 0)
            {
                r1.Append($"<th colspan=\"{pb.Count}\">Phân bổ</th>");
                foreach (var x in pb) r2.Append($"<th>{x.TenPhuLieu}</th>");
            }

            return $"<thead><tr>{r1}</tr><tr>{r2}</tr></thead>";
        }

        // ── Tbody helpers ───────────────────────────────────────────────

        private static string PdfTbody_BOF(
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows)
        {
            var sb = new StringBuilder("<tbody>");
            int stt = 1;
            foreach (var row in rows)
            {
                var d   = row.Data!;
                var vm  = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia);
                var pbm = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.KLPhanBo);
                sb.Append("<tr>");
                sb.Append($"<td>{stt++}</td><td>{d.MeThoi ?? ""}</td><td>{d.MacThep ?? ""}</td>");
                sb.Append($"<td>{PFmt(d.KLGangLongCCT)}</td><td>{PFmt((d.KLThepPhe ?? 0) + (d.KLThepPheGang ?? 0))}</td>");
                foreach (var hx in headers)
                    sb.Append($"<td>{PFmt(vm.TryGetValue(hx.IDHeaderKey, out var kl) ? kl : null)}</td>");
                sb.Append($"<td>{PFmt(d.O2)}</td><td>{PFmt(d.N2)}</td>");
                sb.Append($"<td class=\"td-left\">{d.GhiChu ?? ""}</td>");
                foreach (var pb in phanBoHeaders)
                    sb.Append($"<td>{PFmt(pbm.TryGetValue(pb.IDHeaderKey, out var pv) ? pv : null)}</td>");
                sb.Append("</tr>");
            }
            // Tổng cộng
            sb.Append("<tr class=\"total-row\"><td colspan=\"3\">Tổng cộng</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.KLGangLongCCT ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => (x.Data?.KLThepPhe ?? 0) + (x.Data?.KLThepPheGang ?? 0)))}</td>");
            foreach (var hx in headers) { var id = hx.IDHeaderKey; sb.Append($"<td>{PFmt(rows.Sum(x => x.Values.FirstOrDefault(v => v.IDHeaderKey == id)?.TotalKLPhuGia ?? 0))}</td>"); }
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.O2 ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.N2 ?? 0))}</td>");
            sb.Append("<td></td>");
            foreach (var pb in phanBoHeaders) { var id = pb.IDHeaderKey; sb.Append($"<td>{PFmt(rows.Sum(x => x.Values.FirstOrDefault(v => v.IDHeaderKey == id)?.KLPhanBo ?? 0))}</td>"); }
            sb.Append("</tr></tbody>");
            return sb.ToString();
        }

        private static string PdfTbody_LF(
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows)
        {
            var sb = new StringBuilder("<tbody>");
            int stt = 1;
            foreach (var row in rows)
            {
                var d   = row.Data!;
                var vm  = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia);
                var pbm = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.KLPhanBo);
                sb.Append("<tr>");
                sb.Append($"<td>{stt++}</td><td>{d.MeThoi ?? ""}</td><td>{d.MacThep ?? ""}</td>");
                sb.Append($"<td>{PFmt(d.KLThepLong)}</td>");
                foreach (var hx in headers)
                    sb.Append($"<td>{PFmt(vm.TryGetValue(hx.IDHeaderKey, out var kl) ? kl : null)}</td>");
                sb.Append($"<td>{PFmt(d.AR_LF)}</td>");
                sb.Append($"<td>{PFmt(d.QueLayMau)}</td><td>{PFmt(d.QueDoNhiet)}</td>");
                sb.Append($"<td class=\"td-left\">{d.GhiChu ?? ""}</td>");
                foreach (var pb in phanBoHeaders)
                    sb.Append($"<td>{PFmt(pbm.TryGetValue(pb.IDHeaderKey, out var pv) ? pv : null)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("<tr class=\"total-row\"><td colspan=\"3\">Tổng cộng</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.KLThepLong ?? 0))}</td>");
            foreach (var hx in headers) { var id = hx.IDHeaderKey; sb.Append($"<td>{PFmt(rows.Sum(x => x.Values.FirstOrDefault(v => v.IDHeaderKey == id)?.TotalKLPhuGia ?? 0))}</td>"); }
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.AR_LF     ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.QueLayMau  ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.QueDoNhiet ?? 0))}</td>");
            sb.Append("<td></td>");
            foreach (var pb in phanBoHeaders) { var id = pb.IDHeaderKey; sb.Append($"<td>{PFmt(rows.Sum(x => x.Values.FirstOrDefault(v => v.IDHeaderKey == id)?.KLPhanBo ?? 0))}</td>"); }
            sb.Append("</tr></tbody>");
            return sb.ToString();
        }

        private static string PdfTbody_RH(
            List<PhuLieuHeaderTable> headers, List<PhuLieuHeaderTable> phanBoHeaders,
            List<HRC2ThongKeRow> rows)
        {
            var sb = new StringBuilder("<tbody>");
            int stt = 1;
            foreach (var row in rows)
            {
                var d   = row.Data!;
                var vm  = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia);
                var pbm = row.Values.ToDictionary(v => v.IDHeaderKey, v => v.KLPhanBo);
                sb.Append("<tr>");
                sb.Append($"<td>{stt++}</td><td>{d.MeThoi ?? ""}</td><td>{d.MacThep ?? ""}</td>");
                sb.Append($"<td>{PFmt(d.KLThepLong)}</td>");
                foreach (var hx in headers)
                    sb.Append($"<td>{PFmt(vm.TryGetValue(hx.IDHeaderKey, out var kl) ? kl : null)}</td>");
                sb.Append($"<td>{PFmt(d.AR_RH)}</td><td>{PFmt(d.N2)}</td><td>{PFmt(d.O2)}</td>");
                sb.Append($"<td>{PFmt(d.QueLayMau)}</td><td>{PFmt(d.QueDoNhiet)}</td>");
                sb.Append($"<td class=\"td-left\">{d.GhiChu ?? ""}</td>");
                foreach (var pb in phanBoHeaders)
                    sb.Append($"<td>{PFmt(pbm.TryGetValue(pb.IDHeaderKey, out var pv) ? pv : null)}</td>");
                sb.Append("</tr>");
            }
            sb.Append("<tr class=\"total-row\"><td colspan=\"3\">Tổng cộng</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.KLThepLong ?? 0))}</td>");
            foreach (var hx in headers) { var id = hx.IDHeaderKey; sb.Append($"<td>{PFmt(rows.Sum(x => x.Values.FirstOrDefault(v => v.IDHeaderKey == id)?.TotalKLPhuGia ?? 0))}</td>"); }
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.AR_RH      ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.N2         ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.O2         ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.QueLayMau  ?? 0))}</td>");
            sb.Append($"<td>{PFmt(rows.Sum(x => x.Data?.QueDoNhiet ?? 0))}</td>");
            sb.Append("<td></td>");
            foreach (var pb in phanBoHeaders) { var id = pb.IDHeaderKey; sb.Append($"<td>{PFmt(rows.Sum(x => x.Values.FirstOrDefault(v => v.IDHeaderKey == id)?.KLPhanBo ?? 0))}</td>"); }
            sb.Append("</tr></tbody>");
            return sb.ToString();
        }

        // ── Footer helper ───────────────────────────────────────────────

        private static string PdfFooterHtml(
            int N,
            FooterConfig config,
            string chuKyTruongKipHtml,
            string chuKyNguoiLapHtml,
            List<STD_XUAT_NHAP_TON_HRC2>? footerData = null,
            string key = "",
            string? truongKipName = null,
            string? nguoiLapName = null)
        {
            // Theo spec: 3 nhóm cuối mỗi nhóm 4 cột, silo chiếm phần còn lại
            int g3Span   = 4;
            int g2Span   = 4;
            int g1Span   = 4;
            int siloSpan = Math.Max(1, N - 12);

            var sb = new StringBuilder("<table class=\"footer-tbl\">");

            // Header row
            sb.Append("<tr>");
            sb.Append($"<th colspan=\"{siloSpan}\">Tồn trên silo</th>");
            sb.Append($"<th colspan=\"{g1Span}\">{config.LabelTonDauKip}</th>");
            sb.Append($"<th colspan=\"{g2Span}\">{config.LabelNhapTrongKip}</th>");
            sb.Append($"<th colspan=\"{g3Span}\">{config.LabelTonCuoiKip}</th>");
            sb.Append("</tr>");

            // Label rows — render động từ STD_XUAT_NHAP_TON_HRC2 nếu có, ngược lại dùng config tĩnh
            if (footerData?.Count > 0)
            {
                foreach (var item in footerData)
                {
                    sb.Append("<tr>");
                    sb.Append($"<td colspan=\"{siloSpan}\" class=\"td-left\">{System.Net.WebUtility.HtmlEncode(item.TenNguyenLieu ?? "")}</td>");
                    sb.Append($"<td colspan=\"{g1Span}\">{PFmt(item.TonDauCa)}</td>");
                    sb.Append($"<td colspan=\"{g2Span}\">{PFmt(item.NhapVaoTrongCa)}</td>");
                    sb.Append($"<td colspan=\"{g3Span}\">{PFmt(item.TonCuoiCa)}</td>");
                    sb.Append("</tr>");
                }
            }
            else
            {
                foreach (var label in config.LuongTonLabels)
                {
                    sb.Append("<tr>");
                    sb.Append($"<td colspan=\"{siloSpan}\" class=\"td-left\">{label}</td>");
                    sb.Append($"<td colspan=\"{g1Span}\"></td>");
                    sb.Append($"<td colspan=\"{g2Span}\"></td>");
                    sb.Append($"<td colspan=\"{g3Span}\"></td>");
                    sb.Append("</tr>");
                }
            }

            // Close footer table: sign row render tách riêng để không chịu border ngoài (outer medium) của footer.
            sb.Append("</table>");
            string labelTPChuKy = key.Contains("BOF") ? config.LabelTruongKip : "Trưởng/Phó kíp";
            // Sign row (outside footer table)
            int truongKipSpan = siloSpan + g1Span + g2Span; // col 1 → N-4
            sb.Append($"<table style=\"width:100%;margin-top:20px; border:none; border-collapse:collapse;\">");
            sb.Append("<tr>");
            sb.Append(
                $"<td colspan=\"{truongKipSpan}\" style=\"text-align:center;font-weight:bold;border:none;vertical-align:middle;\">"
                + $"<div style=\"text-align:center;font-weight:bold;\">{labelTPChuKy}</div>"
                + $"{(string.IsNullOrWhiteSpace(chuKyTruongKipHtml) ? "" : chuKyTruongKipHtml)}"
                + $"{(string.IsNullOrWhiteSpace(truongKipName) ? "" : $"<div style=\"text-align:center;\">{truongKipName}</div>")}"
                + $"</td>");
            sb.Append(
                $"<td colspan=\"{g3Span}\" style=\"text-align:center;font-weight:bold;border:none;vertical-align:middle;\">"
                + $"<div style=\"text-align:center;font-weight:bold;\">{config.LabelNguoiLap}</div>"
                + $"{(string.IsNullOrWhiteSpace(chuKyNguoiLapHtml) ? "" : chuKyNguoiLapHtml)}"
                + $"{(string.IsNullOrWhiteSpace(nguoiLapName) ? "" : $"<div style=\"text-align:center;\">{nguoiLapName}</div>")}"
                + $"</td>");
            sb.Append("</tr>");
            sb.Append("</table>");
            return sb.ToString();
        }

        private static string PFmt(double? v)   => v.HasValue ? v.Value.ToString("0.##") : "";
        private static string PFmt(int? v)      => v.HasValue ? v.Value.ToString() : "";
        private static string PFmt(decimal? v)  => v.HasValue ? v.Value.ToString("0.##") : "";

        /// <summary>
        /// Map (bieuMau, phieuScope) → Scope trong STD_XUAT_NHAP_TON_HRC2.
        /// 1=BOF/6, 2=BOF/7, 3=LF/6, 4=RH/1, 5=RH/2
        /// </summary>
        private static int? GetStdScope(string bieuMau, int phieuScope)
        {
            string bm = bieuMau.ToUpperInvariant();
            if (bm.Contains("BOF")) return phieuScope == 6 ? 1 : phieuScope == 7 ? 2 : (int?)null;
            if (bm.Contains("LF"))  return 3;
            if (bm.Contains("RH"))  return phieuScope == 1 ? 4 : phieuScope == 2 ? 5 : (int?)null;
            return null;
        }
    }

    public class FooterConfig
    {
        public List<string> LuongTonLabels { get; set; } = new();
        public string LabelTonDauKip    { get; set; } = "Tồn đầu kíp";
        public string LabelNhapTrongKip { get; set; } = "Nhập trong kíp";
        public string LabelTonCuoiKip   { get; set; } = "Tồn cuối kíp";
        public string LabelTruongKip    { get; set; } = "Trưởng kíp";
        public string LabelNguoiLap     { get; set; } = "Người lập";
    }
}
