using ClosedXML.Excel;
using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text;

namespace dataproduct.api.Services
{
    public class Hrc1SlabService
    {
        private readonly IHrc1SlabRepository _repo;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ProductFormContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConverter _pdfConverter;
        private readonly IConfiguration _config;
        private const string TscApiBase = "http://10.192.49.39:5027/tsc";

        public Hrc1SlabService(
            IHrc1SlabRepository repo,
            IHttpClientFactory httpClientFactory,
            ProductFormContext context,
            IWebHostEnvironment env,
            IConverter pdfConverter,
            IConfiguration config)
        {
            _repo = repo;
            _httpClientFactory = httpClientFactory;
            _context = context;
            _env = env;
            _pdfConverter = pdfConverter;
            _config = config;
        }

        // ── Sync / Search / Workflow ─────────────────────────────────────────

        public async Task<Hrc1SlabSyncResult> SyncAsync(DateOnly ngaySX, int caSX)
        {
            var (fromDate, toDate) = CalculateDateRange(ngaySX, caSX);

            var url = $"{TscApiBase}?fromDate={fromDate:yyyy-MM-ddTHH:mm:ss}&toDate={toDate:yyyy-MM-ddTHH:mm:ss}";

            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

            var response = await client.GetFromJsonAsync<TscApiResponse>(url)
                ?? throw new Exception("TSC API trả về null");

            if (!response.Success)
                throw new Exception($"TSC API báo lỗi (count={response.Count})");

            var items = (response.Data ?? [])
                .Where(x => !string.IsNullOrEmpty(x.PIECE_ID)
                         && (x.LENGTH == null || x.LENGTH >= 16000)
                         && (x.SLAB_ID == null || !x.SLAB_ID.Contains("GHOST", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var result = await _repo.UpsertFromApiAsync(items);

            // Fill MacThep cho các slab cũ chưa có (batch hiện tại đã được fill trong UpsertFromApiAsync)
            var filled = await _repo.FillMacThepAsync();
            result.MacThepFilled += filled;
            if (result.MacThepFilled > 0)
                result.Message += $" | Cập nhật Mác thép: {result.MacThepFilled} slab";

            return result;
        }

        public Task<(IEnumerable<Hrc1SlabItem> Data, int TotalCount)> SearchAsync(Hrc1SlabSearchRequest req)
            => _repo.SearchAsync(req);

        public Task<IEnumerable<Hrc1SlabTongHopItem>> GetTongHopAsync(
            DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip)
            => _repo.GetTongHopAsync(tuNgay, denNgay, ca, kip);

        public Task<IEnumerable<Hrc1PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca)
            => _repo.GetPhieuBBSLAsync(kip, ca);

        public Task<IEnumerable<Hrc1SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu)
            => _repo.GetRuotPhieuAsync(idPhieu);

        public Task<IEnumerable<Hrc1SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu)
            => _repo.GetSlabsByPhieuAsync(idPhieu);

        public Task<int> ChuyenPhoiAsync(Hrc1ChuyenPhoiRequest req)
            => _repo.ChuyenPhoiAsync(req.IdSlabs, req.IdPhieuNguon, req.Huong, req.NguoiChuyen);

        public Task XacNhanAsync(XacNhanRequest req)
            => _repo.XacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task HuyXacNhanAsync(XacNhanRequest req)
            => _repo.HuyXacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task ChotPhieuAsync(ChotPhieuRequest req)
            => _repo.ChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task HuyChotPhieuAsync(ChotPhieuRequest req)
            => _repo.HuyChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task<int> FillMacThepAsync() => _repo.FillMacThepAsync();

        public Task UpdateSlabAsync(int id, Hrc1SlabUpdateRequest req) => _repo.UpdateSlabAsync(id, req);

        public Task<int> BulkUpdateMaVatTuAsync(Hrc1BulkUpdateMaVatTuRequest req) => _repo.BulkUpdateMaVatTuAsync(req);

        public Task<IEnumerable<Hrc1TongHopGhiChuItem>> GetTongHopGhiChuAsync(Guid idPhieu)
            => _repo.GetTongHopGhiChuAsync(idPhieu);

        public Task SaveTongHopGhiChuAsync(Hrc1SaveTongHopGhiChuRequest req)
            => _repo.SaveTongHopGhiChuAsync(req);

        // ── Chi tiết Excel (BBGN phôi tấm) ──────────────────────────────────

        public async Task<ExportFileResult> ExportChiTietExcelAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuForExportAsync(idPhieu);
            var slabs = await GetSlabsForExportAsync(idPhieu);

            var soPhieu = phieu?.SoPhieu ?? "";
            var ngaySX = phieu?.NgaySX?.ToString("dd/MM/yyyy") ?? "";

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC1_BBGN_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            const int startRow = 6;
            var rowIndex = startRow;
            var stt = 1;

            foreach (var slab in slabs)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = slab.MaVatTu ?? "";
                ws.Cell(rowIndex, 3).Value = BuildMacPhoi(slab.ChieuDay, slab.ChieuRong, slab.ChieuDai, slab.MacThep);
                ws.Cell(rowIndex, 4).Value = slab.MaMe ?? "";
                ws.Cell(rowIndex, 5).Value = slab.IDSlab;
                ws.Cell(rowIndex, 6).Value = slab.KhoiLuong.HasValue ? (double)slab.KhoiLuong.Value : 0;
                ws.Cell(rowIndex, 7).Value = slab.GhiChu ?? "";

                rowIndex++;
                stt++;
            }

            if (rowIndex > startRow)
                SetThinBorders(ws, startRow, rowIndex - 1, 7);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = $"HRC1_BBGN_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        // ── Tổng hợp Excel (BBXNSL phôi tấm) ────────────────────────────────

        public async Task<ExportFileResult> ExportTongHopExcelAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuForExportAsync(idPhieu);
            var slabs = await GetSlabsForExportAsync(idPhieu);
            var ghiChus = await _context.Hrc1BbslTongHopGhiChus
                .AsNoTracking()
                .Where(x => x.IdPhieuBBSL == idPhieu)
                .ToListAsync();

            var soPhieu = phieu?.SoPhieu ?? "";
            var ngaySX = phieu?.NgaySX?.ToString("dd/MM/yyyy") ?? "";

            var tongHopRows = BuildTongHopRows(slabs, ghiChus);

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC1_BBXNSL_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            const int startRow = 6;
            var rowIndex = startRow;
            var stt = 1;

            foreach (var row in tongHopRows)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                var label = BuildSanPhamLabel(row.MacThep, row.KichThuoc);

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = label;
                ws.Cell(rowIndex, 3).Value = row.SoPhoi;
                ws.Cell(rowIndex, 4).Value = (double)row.TongKL;
                ws.Cell(rowIndex, 5).Value = row.GhiChu ?? "";

                rowIndex++;
                stt++;
            }

            // Total row
            if (tongHopRows.Count > 0)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = "Tổng";
                ws.Cell(rowIndex, 2).Value = "";
                ws.Cell(rowIndex, 3).Value = tongHopRows.Sum(r => r.SoPhoi);
                ws.Cell(rowIndex, 4).Value = (double)tongHopRows.Sum(r => r.TongKL);
                ws.Cell(rowIndex, 5).Value = "";
            }

            var lastTongHopRow = tongHopRows.Count > 0 ? rowIndex : rowIndex - 1;
            if (lastTongHopRow >= startRow)
                SetThinBorders(ws, startRow, lastTongHopRow, 5);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = $"HRC1_BBXNSL_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        // ── Tổng hợp PDF (BBXNSL phôi tấm) ──────────────────────────────────

        public async Task<ExportFileResult> ExportTongHopPdfAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuForExportAsync(idPhieu);
            var slabs = await GetSlabsForExportAsync(idPhieu);
            var ghiChus = await _context.Hrc1BbslTongHopGhiChus
                .AsNoTracking()
                .Where(x => x.IdPhieuBBSL == idPhieu)
                .ToListAsync();

            var soPhieu = phieu?.SoPhieu ?? "";
            var ngaySX = phieu?.NgaySX?.ToString("dd/MM/yyyy") ?? "";
            var ca = phieu?.Ca == 1 ? "Ca ngày" : phieu?.Ca == 2 ? "Ca đêm" : "";
            var kip = phieu?.Kip ?? "";

            var tongHopRows = BuildTongHopRows(slabs, ghiChus);

            var rowsHtml = new StringBuilder();
            foreach (var row in tongHopRows)
            {
                var label = BuildSanPhamLabel(row.MacThep, row.KichThuoc);
                rowsHtml.Append("<tr>");
                rowsHtml.Append($"<td style=\"text-align:center\">{row.Stt}</td>");
                rowsHtml.Append($"<td>{label}</td>");
                rowsHtml.Append($"<td style=\"text-align:right\">{row.SoPhoi.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</td>");
                rowsHtml.Append($"<td style=\"text-align:right\">{row.TongKL.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</td>");
                rowsHtml.Append($"<td>{row.GhiChu ?? ""}</td>");
                rowsHtml.Append("</tr>");
            }

            // Total row
            var totalSoPhoi = tongHopRows.Sum(r => r.SoPhoi);
            var totalKL = tongHopRows.Sum(r => r.TongKL);
            rowsHtml.Append("<tr>");
            rowsHtml.Append("<td colspan=\"2\" style=\"text-align:center\"><strong>Tổng</strong></td>");
            rowsHtml.Append($"<td style=\"text-align:right\"><strong>{totalSoPhoi.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</strong></td>");
            rowsHtml.Append($"<td style=\"text-align:right\"><strong>{totalKL.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</strong></td>");
            rowsHtml.Append("<td></td>");
            rowsHtml.Append("</tr>");

            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "HRC1_BBXNSL_PhoiTam.html");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy template HTML: {templatePath}");
            var logoUrl = _config.GetValue<string>("AppSettings:LogoUrl")
                          ?? "https://report.hoaphatdungquat.vn/img/logoHP.png";
            var html = await File.ReadAllTextAsync(templatePath);
            html = html
                .Replace("{{LogoUrl}}",       logoUrl)
                .Replace("{{soPhieu}}", soPhieu)
                .Replace("{{ngaySX}}", ngaySX)
                .Replace("{{ca}}", ca)
                .Replace("{{kip}}", kip)
                .Replace("{{TABLE_BODY}}", rowsHtml.ToString());

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    PaperSize = PaperKind.A4,
                    Orientation = Orientation.Portrait
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = html,
                        WebSettings =
                        {
                            DefaultEncoding = "utf-8",
                            LoadImages = true,
                            EnableJavascript = false,
                            PrintMediaType = true
                        },
                        LoadSettings =
                        {
                            BlockLocalFileAccess = false,
                            LoadErrorHandling = ContentErrorHandling.Ignore
                        }
                    }
                }
            };

            var pdfBytes = _pdfConverter.Convert(doc);
            return new ExportFileResult
            {
                Content = pdfBytes,
                FileName = $"HRC1_BBXNSL_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.pdf",
                ContentType = "application/pdf"
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string BuildMacPhoi(decimal? chieuDay, decimal? chieuRong, decimal? chieuDai, string? macThep)
        {
            var hasKt = chieuDay != null && chieuRong != null && chieuDai != null;
            var kt = hasKt ? $"{chieuDay}x{chieuRong}x{chieuDai}" : null;
            if (kt == null && string.IsNullOrWhiteSpace(macThep)) return "-";
            var parts = new[] { "Phôi tấm", kt != null ? $"{kt}mm" : null, macThep }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
            return string.Join(" ", parts);
        }

        private static string BuildSanPhamLabel(string? macThep, string kichThuoc)
        {
            var parts = new[] { "Phôi tấm", !string.IsNullOrEmpty(kichThuoc) ? $"{kichThuoc}mm" : null, macThep }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
            return parts.Length > 0 ? string.Join(" ", parts) : "-";
        }

        private static void SetThinBorders(IXLWorksheet ws, int fromRow, int toRow, int lastCol)
        {
            var range = ws.Range(fromRow, 1, toRow, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private async Task<BmPhieu?> GetPhieuForExportAsync(Guid idPhieu)
        {
            return await _context.BmPhieus
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == "HRC1_BBGN_PhoiTam");
        }

        private async Task<List<Hrc1Slab>> GetSlabsForExportAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == "HRC1_BBGN_PhoiTam");
            if (phieu == null) return [];

            // Reuse repository helper
            var repo = (Hrc1SlabRepository)_repo;
            return await repo.LoadPhieuSlabsAsync(phieu);
        }

        private record TongHopRow(int Stt, string? MacThep, string KichThuoc, int SoPhoi, decimal TongKL, string? GhiChu);

        private static List<TongHopRow> BuildTongHopRows(List<Hrc1Slab> slabs, List<Hrc1BbslTongHopGhiChu> ghiChus)
        {
            var map = new Dictionary<string, (string? MacThep, string KichThuoc, int SoPhoi, decimal TongKL)>();

            foreach (var slab in slabs)
            {
                var hasKt = slab.ChieuDay != null && slab.ChieuRong != null && slab.ChieuDai != null;
                var kt = hasKt ? $"{slab.ChieuDay}x{slab.ChieuRong}x{slab.ChieuDai}" : "";
                var key = $"{slab.MacThep ?? ""}|{kt}";

                if (!map.ContainsKey(key))
                    map[key] = (slab.MacThep, kt, 0, 0);

                var cur = map[key];
                map[key] = (cur.MacThep, cur.KichThuoc, cur.SoPhoi + 1, cur.TongKL + (slab.KhoiLuong ?? 0));
            }

            var ghiChuDict = ghiChus.ToDictionary(
                g => $"{g.MacThep ?? ""}|{g.KichThuoc ?? ""}",
                g => g.GhiChu);

            return map.Select((kvp, i) =>
            {
                var ghiChu = ghiChuDict.TryGetValue(kvp.Key, out var gc) ? gc : null;
                return new TongHopRow(i + 1, kvp.Value.MacThep, kvp.Value.KichThuoc, kvp.Value.SoPhoi, kvp.Value.TongKL, ghiChu);
            }).ToList();
        }

        // Ca 1: 8h–19:59, Ca 2: 20h–7:59 hôm sau
        private static (DateTime from, DateTime to) CalculateDateRange(DateOnly ngaySX, int caSX)
        {
            if (caSX == 1)
                return (ngaySX.ToDateTime(new TimeOnly(8, 0, 0)),
                        ngaySX.ToDateTime(new TimeOnly(19, 59, 59)));

            return (ngaySX.ToDateTime(new TimeOnly(20, 0, 0)),
                    ngaySX.AddDays(1).ToDateTime(new TimeOnly(7, 59, 59)));
        }
    }
}
