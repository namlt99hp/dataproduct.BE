using ClosedXML.Excel;
using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace dataproduct.api.Services
{
    public class Hrc2SlabWorkflowService
    {
        private readonly IHrc2SlabWorkflowRepository _repo;
        private readonly ProductFormContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IConverter _pdfConverter;
        private readonly IConfiguration _config;        

        public Hrc2SlabWorkflowService(
            IHrc2SlabWorkflowRepository repo,
            ProductFormContext context,
            IWebHostEnvironment env,
            IConverter pdfConverter,
            IConfiguration config)
        {
            _repo = repo;
            _context = context;
            _env = env;
            _pdfConverter = pdfConverter;
            _config = config;
        }

        // ── Search / Workflow ────────────────────────────────────────────────

        public Task<(IEnumerable<BkHrc2SlabItem> Data, int TotalCount)> SearchAsync(HrcSlabSearchRequest request)
            => _repo.SearchWithTrangThaiAsync(request);

        public Task<IEnumerable<SlabTongHopItem>> GetTongHopAsync(
            DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip)
            => _repo.GetTongHopAsync(tuNgay, denNgay, ca, kip);

        public Task<IEnumerable<PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca)
            => _repo.GetPhieuBBSLAsync(kip, ca);

        public Task<IEnumerable<SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu)
            => _repo.GetRuotPhieuAsync(idPhieu);

        public Task<IEnumerable<BkHrc2SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu)
            => _repo.GetSlabsByPhieuAsync(idPhieu);

        public Task ChuyenBBSLAsync(ChuyenBBSLRequest req)
            => _repo.ChuyenBBSLAsync(req.IdSlabs, req.IdPhieu, req.NguoiThucHien);

        public Task ThuHoiAsync(ThuHoiRequest req)
            => _repo.ThuHoiAsync(req.IdSlabs, req.NguoiThucHien);

        public Task XacNhanAsync(XacNhanRequest req)
            => _repo.XacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task HuyXacNhanAsync(XacNhanRequest req)
            => _repo.HuyXacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task ChotPhieuAsync(ChotPhieuRequest req)
            => _repo.ChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task HuyChotPhieuAsync(ChotPhieuRequest req)
            => _repo.HuyChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task<SyncStatusItem> SyncAsync(DateOnly? ngayBatDau, DateOnly? ngayKetThuc)
            => _repo.SyncAsync(ngayBatDau, ngayKetThuc);

        public Task<SyncStatusItem?> GetSyncStatusAsync()
            => _repo.GetSyncStatusAsync();

        // ── Chi tiết Excel (BBGN phôi tấm HRC2) ─────────────────────────────

        public async Task<ExportFileResult> ExportChiTietExcelAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuAsync(idPhieu);
            var (soPhieu, ngaySX, _, _) = ExtractPhieuInfo(phieu);

            var trangThais = await GetTrangThaisAsync(idPhieu);

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC2_BBGN_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            const int startRow = 6;
            var rowIndex = startRow;
            var stt = 1;

            foreach (var tt in trangThais)
            {
                var slab = tt.Slab;
                var kt = (slab.ChieuDay != null && slab.ChieuRong != null && slab.ChieuDai != null)
                    ? $"{slab.ChieuDay}x{slab.ChieuRong}x{slab.ChieuDai}"
                    : "";

                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = slab.ShiftName ?? "";
                ws.Cell(rowIndex, 3).Value = slab.IdSlab ?? "";
                ws.Cell(rowIndex, 4).Value = slab.MeThep ?? "";
                ws.Cell(rowIndex, 5).Value = slab.MayDuc.HasValue ? $"Máy {slab.MayDuc}" : "";
                ws.Cell(rowIndex, 6).Value = kt;
                ws.Cell(rowIndex, 7).Value = slab.MacThep ?? "";
                ws.Cell(rowIndex, 8).Value = slab.KhoiLuong.HasValue ? (double)slab.KhoiLuong.Value : 0;
                ws.Cell(rowIndex, 9).Value = slab.ChatLuong ?? "";

                rowIndex++;
                stt++;
            }

            if (rowIndex > startRow)
                SetThinBorders(ws, startRow, rowIndex - 1, 9);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = $"HRC2_BBGN_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        // ── Tổng hợp Excel (BBXNSL phôi tấm HRC2) ───────────────────────────
        // Cột khớp JSON config HRC2_BBGN_PhoiTam.layout[0].columns (27 cột leaf):
        // 1=STT, 2=shiftName, 3=macThep, 4=meThep, 5=kichThuoc,
        // 6-17=phôi nguội (loại I/II/IItp/III/IIItp/ngắndài × so/kl),
        // 18-25=phôi nóng (loại I/II/IItp/IIItp × so/kl),
        // 26=tongSoPhoi, 27=tongKhoiLuong

        public async Task<ExportFileResult> ExportTongHopExcelAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuAsync(idPhieu);
            var (soPhieu, ngaySX, _, _) = ExtractPhieuInfo(phieu);

            var trangThais = await GetTrangThaisAsync(idPhieu);
            var pivotRows = BuildPivotRows(trangThais);

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC2_BBXNSL_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            const int startRow = 6;
            const int lastCol = 27;
            var rowIndex = startRow;
            var stt = 1;

            foreach (var row in pivotRows)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = row.ShiftName;
                ws.Cell(rowIndex, 3).Value = row.MacThep;
                ws.Cell(rowIndex, 4).Value = row.MeThep;
                ws.Cell(rowIndex, 5).Value = row.KichThuoc;
                ws.Cell(rowIndex, 6).Value = row.NguoiLoai1So;
                ws.Cell(rowIndex, 7).Value = (double)row.NguoiLoai1Kl;
                ws.Cell(rowIndex, 8).Value = row.NguoiLoai2So;
                ws.Cell(rowIndex, 9).Value = (double)row.NguoiLoai2Kl;
                ws.Cell(rowIndex, 10).Value = row.NguoiLoai2TpSo;
                ws.Cell(rowIndex, 11).Value = (double)row.NguoiLoai2TpKl;
                ws.Cell(rowIndex, 12).Value = row.NguoiLoai3So;
                ws.Cell(rowIndex, 13).Value = (double)row.NguoiLoai3Kl;
                ws.Cell(rowIndex, 14).Value = row.NguoiLoai3TpSo;
                ws.Cell(rowIndex, 15).Value = (double)row.NguoiLoai3TpKl;
                ws.Cell(rowIndex, 16).Value = row.NguoiNganDaiSo;
                ws.Cell(rowIndex, 17).Value = (double)row.NguoiNganDaiKl;
                ws.Cell(rowIndex, 18).Value = row.NongLoai1So;
                ws.Cell(rowIndex, 19).Value = (double)row.NongLoai1Kl;
                ws.Cell(rowIndex, 20).Value = row.NongLoai2So;
                ws.Cell(rowIndex, 21).Value = (double)row.NongLoai2Kl;
                ws.Cell(rowIndex, 22).Value = row.NongLoai2TpSo;
                ws.Cell(rowIndex, 23).Value = (double)row.NongLoai2TpKl;
                ws.Cell(rowIndex, 24).Value = row.NongLoai3TpSo;
                ws.Cell(rowIndex, 25).Value = (double)row.NongLoai3TpKl;
                ws.Cell(rowIndex, 26).Value = row.TongSoPhoi;
                ws.Cell(rowIndex, 27).Value = (double)row.TongKhoiLuong;

                rowIndex++;
                stt++;
            }

            // Total row
            if (pivotRows.Count > 0)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = "Tổng";
                for (int c = 2; c <= 5; c++) ws.Cell(rowIndex, c).Value = "";
                ws.Cell(rowIndex, 6).Value = pivotRows.Sum(r => r.NguoiLoai1So);
                ws.Cell(rowIndex, 7).Value = (double)pivotRows.Sum(r => r.NguoiLoai1Kl);
                ws.Cell(rowIndex, 8).Value = pivotRows.Sum(r => r.NguoiLoai2So);
                ws.Cell(rowIndex, 9).Value = (double)pivotRows.Sum(r => r.NguoiLoai2Kl);
                ws.Cell(rowIndex, 10).Value = pivotRows.Sum(r => r.NguoiLoai2TpSo);
                ws.Cell(rowIndex, 11).Value = (double)pivotRows.Sum(r => r.NguoiLoai2TpKl);
                ws.Cell(rowIndex, 12).Value = pivotRows.Sum(r => r.NguoiLoai3So);
                ws.Cell(rowIndex, 13).Value = (double)pivotRows.Sum(r => r.NguoiLoai3Kl);
                ws.Cell(rowIndex, 14).Value = pivotRows.Sum(r => r.NguoiLoai3TpSo);
                ws.Cell(rowIndex, 15).Value = (double)pivotRows.Sum(r => r.NguoiLoai3TpKl);
                ws.Cell(rowIndex, 16).Value = pivotRows.Sum(r => r.NguoiNganDaiSo);
                ws.Cell(rowIndex, 17).Value = (double)pivotRows.Sum(r => r.NguoiNganDaiKl);
                ws.Cell(rowIndex, 18).Value = pivotRows.Sum(r => r.NongLoai1So);
                ws.Cell(rowIndex, 19).Value = (double)pivotRows.Sum(r => r.NongLoai1Kl);
                ws.Cell(rowIndex, 20).Value = pivotRows.Sum(r => r.NongLoai2So);
                ws.Cell(rowIndex, 21).Value = (double)pivotRows.Sum(r => r.NongLoai2Kl);
                ws.Cell(rowIndex, 22).Value = pivotRows.Sum(r => r.NongLoai2TpSo);
                ws.Cell(rowIndex, 23).Value = (double)pivotRows.Sum(r => r.NongLoai2TpKl);
                ws.Cell(rowIndex, 24).Value = pivotRows.Sum(r => r.NongLoai3TpSo);
                ws.Cell(rowIndex, 25).Value = (double)pivotRows.Sum(r => r.NongLoai3TpKl);
                ws.Cell(rowIndex, 26).Value = pivotRows.Sum(r => r.TongSoPhoi);
                ws.Cell(rowIndex, 27).Value = (double)pivotRows.Sum(r => r.TongKhoiLuong);
            }

            var lastRow = pivotRows.Count > 0 ? rowIndex : rowIndex - 1;
            if (lastRow >= startRow)
                SetThinBorders(ws, startRow, lastRow, lastCol);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = $"HRC2_BBXNSL_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        // ── Tổng hợp PDF (BBXNSL phôi tấm HRC2) ─────────────────────────────

        public async Task<ExportFileResult> ExportTongHopPdfAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuAsync(idPhieu);
            var (soPhieu, ngaySX, ca, kip) = ExtractPhieuInfo(phieu);

            var trangThais = await GetTrangThaisAsync(idPhieu);
            var pivotRows = BuildPivotRows(trangThais);

            var vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
            var rowsHtml = new StringBuilder();

            static string N0(int v, System.Globalization.CultureInfo c) => v > 0 ? v.ToString("N0", c) : "";
            static string N3(decimal v, System.Globalization.CultureInfo c) => v > 0 ? v.ToString("N3", c) : "";

            void Td2(int so, decimal kl)
            {
                rowsHtml.Append($"<td class=\"num\">{N0(so, vi)}</td><td class=\"num\">{N3(kl, vi)}</td>");
            }

            var stt = 1;
            foreach (var r in pivotRows)
            {
                rowsHtml.Append("<tr>");
                rowsHtml.Append($"<td>{stt++}</td>");
                rowsHtml.Append($"<td>{r.ShiftName}</td>");
                rowsHtml.Append($"<td class=\"txt\">{r.MacThep}</td>");
                rowsHtml.Append($"<td class=\"txt\">{r.MeThep}</td>");
                rowsHtml.Append($"<td>{r.KichThuoc}</td>");
                Td2(r.NguoiLoai1So,   r.NguoiLoai1Kl);
                Td2(r.NguoiLoai2So,   r.NguoiLoai2Kl);
                Td2(r.NguoiLoai2TpSo, r.NguoiLoai2TpKl);
                Td2(r.NguoiLoai3So,   r.NguoiLoai3Kl);
                Td2(r.NguoiLoai3TpSo, r.NguoiLoai3TpKl);
                Td2(r.NguoiNganDaiSo, r.NguoiNganDaiKl);
                Td2(r.NongLoai1So,    r.NongLoai1Kl);
                Td2(r.NongLoai2So,    r.NongLoai2Kl);
                Td2(r.NongLoai2TpSo,  r.NongLoai2TpKl);
                Td2(r.NongLoai3TpSo,  r.NongLoai3TpKl);
                rowsHtml.Append($"<td class=\"num\"><strong>{N0(r.TongSoPhoi, vi)}</strong></td>");
                rowsHtml.Append($"<td class=\"num\"><strong>{N3(r.TongKhoiLuong, vi)}</strong></td>");
                rowsHtml.Append("</tr>");
            }

            // Total row
            void TdTotal2(Func<TongHopPivotRow, int> soFn, Func<TongHopPivotRow, decimal> klFn)
            {
                rowsHtml.Append($"<td class=\"num\">{N0(pivotRows.Sum(soFn), vi)}</td><td class=\"num\">{N3(pivotRows.Sum(klFn), vi)}</td>");
            }

            rowsHtml.Append("<tr class=\"total-row\">");
            rowsHtml.Append("<td colspan=\"5\" style=\"text-align:center\"><strong>Tổng</strong></td>");
            TdTotal2(r => r.NguoiLoai1So,   r => r.NguoiLoai1Kl);
            TdTotal2(r => r.NguoiLoai2So,   r => r.NguoiLoai2Kl);
            TdTotal2(r => r.NguoiLoai2TpSo, r => r.NguoiLoai2TpKl);
            TdTotal2(r => r.NguoiLoai3So,   r => r.NguoiLoai3Kl);
            TdTotal2(r => r.NguoiLoai3TpSo, r => r.NguoiLoai3TpKl);
            TdTotal2(r => r.NguoiNganDaiSo, r => r.NguoiNganDaiKl);
            TdTotal2(r => r.NongLoai1So,    r => r.NongLoai1Kl);
            TdTotal2(r => r.NongLoai2So,    r => r.NongLoai2Kl);
            TdTotal2(r => r.NongLoai2TpSo,  r => r.NongLoai2TpKl);
            TdTotal2(r => r.NongLoai3TpSo,  r => r.NongLoai3TpKl);
            rowsHtml.Append($"<td class=\"num\"><strong>{N0(pivotRows.Sum(r => r.TongSoPhoi), vi)}</strong></td>");
            rowsHtml.Append($"<td class=\"num\"><strong>{N3(pivotRows.Sum(r => r.TongKhoiLuong), vi)}</strong></td>");
            rowsHtml.Append("</tr>");

            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "HRC2_BBXNSL_PhoiTam.html");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy template HTML: {templatePath}");

            var html = await File.ReadAllTextAsync(templatePath);
            var logoUrl = _config.GetValue<string>("AppSettings:LogoUrl")
                          ?? "https://report.hoaphatdungquat.vn/img/logoHP.png";
            
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
                    PaperSize = PaperKind.A3,
                    Orientation = Orientation.Landscape
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
                FileName = $"HRC2_BBXNSL_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.pdf",
                ContentType = "application/pdf"
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void SetThinBorders(IXLWorksheet ws, int fromRow, int toRow, int lastCol)
        {
            var range = ws.Range(fromRow, 1, toRow, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private async Task<BmPhieu?> GetPhieuAsync(Guid idPhieu)
        {
            return await _context.BmPhieus
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == "HRC2_BBGN_PhoiTam");
        }

        private static (string SoPhieu, string NgaySX, string Ca, string Kip) ExtractPhieuInfo(BmPhieu? phieu)
        {
            var soPhieu = phieu?.SoPhieu ?? "";
            string ngaySX = "", ca = "", kip = "";

            if (phieu?.DataJson != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(phieu.DataJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("NgaySX", out var ngProp) && ngProp.ValueKind != JsonValueKind.Null)
                    {
                        var ngStr = ngProp.GetString();
                        if (!string.IsNullOrEmpty(ngStr) && DateTime.TryParse(ngStr, out var dt))
                            ngaySX = dt.ToString("dd/MM/yyyy");
                        else
                            ngaySX = ngStr ?? "";
                    }

                    if (root.TryGetProperty("ca", out var caProp))
                    {
                        int caVal = 0;
                        if (caProp.ValueKind == JsonValueKind.Number)
                            caVal = caProp.GetInt32();
                        else if (caProp.ValueKind == JsonValueKind.String && int.TryParse(caProp.GetString(), out var cv))
                            caVal = cv;
                        ca = caVal == 1 ? "Ca ngày" : caVal == 2 ? "Ca đêm" : "";
                    }

                    if (root.TryGetProperty("kip", out var kipProp) && kipProp.ValueKind != JsonValueKind.Null)
                        kip = kipProp.GetString() ?? "";
                }
                catch { /* ignore parse errors */ }
            }

            return (soPhieu, ngaySX, ca, kip);
        }

        private async Task<List<BkHrc2SlabTrangThai>> GetTrangThaisAsync(Guid idPhieu)
        {
            return await _context.BkHrc2SlabTrangThais
                .AsNoTracking()
                .Include(t => t.Slab)
                .Where(t => t.IdPhieuBBSL == idPhieu && t.TrangThaiKCS == 1)
                .OrderBy(t => t.Slab.BkmisId)
                .ToListAsync();
        }

        // Dịch getColKeys() của FE sang C# — xác định cột pivot từ loaiPhoi + chatLuongTPHH
        private static (string? SoKey, string? KlKey) GetPivotKeys(string? loaiPhoi, string? chatLuong)
        {
            var lp = loaiPhoi ?? "";
            var cl = chatLuong ?? "";
            var isNguoi = Regex.IsMatch(lp, "nguội|nguoi", RegexOptions.IgnoreCase);
            var isNong  = Regex.IsMatch(lp, "nóng|nong",   RegexOptions.IgnoreCase);
            if (!isNguoi && !isNong) return (null, null);
            var p = isNguoi ? "nguoi" : "nong";
            string? mid = null;
            if      (Regex.IsMatch(cl, @"iii.*th[àa]nh|3.*th[àa]nh", RegexOptions.IgnoreCase)) mid = "loai3tp";
            else if (Regex.IsMatch(cl, @"\biii\b|\b3\b",              RegexOptions.IgnoreCase)) mid = isNguoi ? "loai3" : null;
            else if (Regex.IsMatch(cl, @"ii.*th[àa]nh|2.*th[àa]nh",  RegexOptions.IgnoreCase)) mid = "loai2tp";
            else if (Regex.IsMatch(cl, @"\bii\b|\b2\b",               RegexOptions.IgnoreCase)) mid = "loai2";
            else if (Regex.IsMatch(cl, @"\bi\b|\b1\b",                RegexOptions.IgnoreCase)) mid = "loai1";
            else if (isNguoi && Regex.IsMatch(cl, @"ng[aắ]n",         RegexOptions.IgnoreCase)) mid = "nganDai";
            if (mid == null) return (null, null);
            return ($"{p}_{mid}_so", $"{p}_{mid}_kl");
        }

        // Pivot row — nhóm theo (meThep, macThep, kichThuoc), phân loại theo loaiPhoi + chatLuongTPHH
        private class TongHopPivotRow
        {
            public string ShiftName = "";
            public string MacThep   = "";
            public string MeThep    = "";
            public string KichThuoc = "";
            public int NguoiLoai1So;    public decimal NguoiLoai1Kl;
            public int NguoiLoai2So;    public decimal NguoiLoai2Kl;
            public int NguoiLoai2TpSo;  public decimal NguoiLoai2TpKl;
            public int NguoiLoai3So;    public decimal NguoiLoai3Kl;
            public int NguoiLoai3TpSo;  public decimal NguoiLoai3TpKl;
            public int NguoiNganDaiSo;  public decimal NguoiNganDaiKl;
            public int NongLoai1So;     public decimal NongLoai1Kl;
            public int NongLoai2So;     public decimal NongLoai2Kl;
            public int NongLoai2TpSo;   public decimal NongLoai2TpKl;
            public int NongLoai3TpSo;   public decimal NongLoai3TpKl;
            public int TongSoPhoi;      public decimal TongKhoiLuong;
            public HashSet<string> _shiftNames = new();
        }

        private static List<TongHopPivotRow> BuildPivotRows(List<BkHrc2SlabTrangThai> trangThais)
        {
            var map = new Dictionary<string, TongHopPivotRow>();

            foreach (var tt in trangThais)
            {
                var slab = tt.Slab;
                var kt = (slab.ChieuDay != null && slab.ChieuRong != null && slab.ChieuDai != null)
                    ? $"{slab.ChieuDay}x{slab.ChieuRong}x{slab.ChieuDai}"
                    : "";
                var key = $"{slab.MeThep ?? ""}|{slab.MacThep ?? ""}|{kt}";

                if (!map.TryGetValue(key, out var row))
                {
                    row = new TongHopPivotRow
                    {
                        MacThep   = slab.MacThep ?? "",
                        MeThep    = slab.MeThep  ?? "",
                        KichThuoc = kt
                    };
                    map[key] = row;
                }

                if (!string.IsNullOrEmpty(slab.ShiftName))
                    row._shiftNames.Add(slab.ShiftName);

                var kl = slab.KhoiLuong ?? 0;
                var (soKey, _) = GetPivotKeys(slab.LoaiPhoi, slab.ChatLuongTPHH);

                switch (soKey)
                {
                    case "nguoi_loai1_so":   row.NguoiLoai1So++;   row.NguoiLoai1Kl   += kl; break;
                    case "nguoi_loai2_so":   row.NguoiLoai2So++;   row.NguoiLoai2Kl   += kl; break;
                    case "nguoi_loai2tp_so": row.NguoiLoai2TpSo++; row.NguoiLoai2TpKl += kl; break;
                    case "nguoi_loai3_so":   row.NguoiLoai3So++;   row.NguoiLoai3Kl   += kl; break;
                    case "nguoi_loai3tp_so": row.NguoiLoai3TpSo++; row.NguoiLoai3TpKl += kl; break;
                    case "nguoi_nganDai_so": row.NguoiNganDaiSo++; row.NguoiNganDaiKl += kl; break;
                    case "nong_loai1_so":    row.NongLoai1So++;    row.NongLoai1Kl    += kl; break;
                    case "nong_loai2_so":    row.NongLoai2So++;    row.NongLoai2Kl    += kl; break;
                    case "nong_loai2tp_so":  row.NongLoai2TpSo++;  row.NongLoai2TpKl  += kl; break;
                    case "nong_loai3tp_so":  row.NongLoai3TpSo++;  row.NongLoai3TpKl  += kl; break;
                }

                row.TongSoPhoi++;
                row.TongKhoiLuong += kl;
            }

            return map.Values.Select(r =>
            {
                r.ShiftName = string.Join(", ", r._shiftNames);
                return r;
            }).ToList();
        }
    }
}
