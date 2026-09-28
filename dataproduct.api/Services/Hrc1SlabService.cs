using ClosedXML.Excel;
using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;

namespace dataproduct.api.Services
{
    public class Hrc1SlabService
    {
        private readonly IHrc1SlabRepository _repo;
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterCtx;
        private readonly IWebHostEnvironment _env;
        private readonly IConverter _pdfConverter;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _sqlConnStr;
        private readonly BmConfigService _bmConfig;

        public Hrc1SlabService(
            IHrc1SlabRepository repo,
            ProductFormContext context,
            ProductDataMasterDbContext masterCtx,
            IWebHostEnvironment env,
            IConverter pdfConverter,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            BmConfigService bmConfig)
        {
            _repo = repo;
            _context = context;
            _masterCtx = masterCtx;
            _env = env;
            _pdfConverter = pdfConverter;
            _config = config;
            _httpClientFactory = httpClientFactory;
            _sqlConnStr = config.GetConnectionString("DbConnectionString")
                ?? throw new InvalidOperationException("DbConnectionString is not configured.");
            _bmConfig = bmConfig;
        }

        // ── Sync / Search / Workflow ─────────────────────────────────────────

        public async Task<Hrc1SlabSyncResult> SyncAsync(DateOnly ngaySX, int caSX)
        {
            var (fromDate, toDate) = CalculateDateRange(ngaySX, caSX);

            var items = await GetTscSlabDataAsync(fromDate, toDate);

            var result = await _repo.UpsertFromApiAsync(items);

            // Fill MacThep cho các slab cũ chưa có (batch hiện tại đã được fill trong UpsertFromApiAsync)
            var filled = await _repo.FillMacThepAsync();
            result.MacThepFilled += filled;
            if (result.MacThepFilled > 0)
                result.Message += $" | Cập nhật Mác thép: {result.MacThepFilled} slab";

            // Lưu ý: Sync không lưu MaVatTu. MaVatTu chỉ được snapshot khi Đúc xác nhận (XacNhanAsync);
            // trước khi xác nhận, /slabs-by-phieu sẽ tự join bảng MaVatTu theo MacThep để hiển thị.
            return result;
        }

        /// <summary>
        /// Gọi usp_HRC1_GetTscSlabData (Linked Server DATA_TSC_HIS → vw_TSC_DATA_IT).
        /// SP đã lọc sẵn PIECE_ID/LENGTH/GHOST theo @FromDate..@ToDate, nên kết quả trả về
        /// có thể dùng thẳng cho UpsertFromApiAsync, không cần lọc lại ở C#.
        /// </summary>
        private async Task<List<TscSlabItem>> GetTscSlabDataAsync(DateTime fromDate, DateTime toDate)
        {
            var items = new List<TscSlabItem>();

            await using var conn = new SqlConnection(_sqlConnStr);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("usp_HRC1_GetTscSlabData", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 120;
            cmd.Parameters.Add("@FromDate", SqlDbType.DateTime).Value = fromDate;
            cmd.Parameters.Add("@ToDate", SqlDbType.DateTime).Value = toDate;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new TscSlabItem
                {
                    SLAB_ID = reader.IsDBNull(0) ? null : reader.GetString(0),
                    PIECE_ID = reader.IsDBNull(1) ? null : reader.GetString(1),
                    CA = reader.IsDBNull(2) ? null : reader.GetString(2),
                    HEAT_ID = reader.IsDBNull(3) ? null : reader.GetString(3),
                    CUT_DATE = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    THICKNESS = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    LENGTH = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    WEIGHT = reader.IsDBNull(7) ? null : reader.GetDecimal(7),
                    WIDTH_HEAD = reader.IsDBNull(8) ? null : reader.GetDecimal(8),
                    TSC_NO = reader.IsDBNull(9) ? null : reader.GetString(9),
                });
            }

            return items;
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

        public async Task XacNhanAsync(XacNhanRequest req)
        {
            // Luồng C4 (GĐ/PGĐ NM) đã bỏ cho phiếu mới — chỉ phiếu cũ đang "dính" C4 (có ≥1 slab đã được
            // C4 xác nhận) mới còn cho XN C4, và chỉ trên slab thuộc đúng phiếu đó.
            if (req.LoaiXacNhan == "C4")
            {
                if (req.IdPhieu == null)
                    throw new InvalidOperationException("Thiếu phiếu khi xác nhận C4.");
                var (slabIds, dinhC4) = await _repo.GetPhieuC4InfoAsync(req.IdPhieu.Value);
                if (!dinhC4)
                    throw new InvalidOperationException("Phiếu này không còn luồng xác nhận GĐ/PGĐ NM (C4).");
                if (req.IdSlabs.Any(id => !slabIds.Contains(id)))
                    throw new InvalidOperationException("Có slab không thuộc phiếu đang xác nhận.");
            }
            await _repo.XacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);
        }

        public Task HuyXacNhanAsync(XacNhanRequest req)
            => _repo.HuyXacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task ChotPhieuAsync(ChotPhieuRequest req)
            => _repo.ChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task HuyChotPhieuAsync(ChotPhieuRequest req)
            => _repo.HuyChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task<int> FillMacThepAsync() => _repo.FillMacThepAsync();

        public Task UpdateSlabAsync(int id, Hrc1SlabUpdateRequest req) => _repo.UpdateSlabAsync(id, req);

        public Task<Hrc1SlabItem> CreateSlabAsync(Hrc1SlabCreateRequest req) => _repo.CreateSlabAsync(req);

        public Task EditSlabAsync(int id, Hrc1SlabEditRequest req) => _repo.EditSlabAsync(id, req);

        public Task<int> DeleteSlabsAsync(Hrc1SlabDeleteRequest req)
            => _repo.DeleteSlabsAsync(req.IdSlabs, req.NguoiThucHien);

        public Task<int> RestoreSlabsAsync(Hrc1SlabDeleteRequest req)
            => _repo.RestoreSlabsAsync(req.IdSlabs, req.NguoiThucHien);

        public Task<IEnumerable<Hrc1TongHopGhiChuItem>> GetTongHopGhiChuAsync(Guid idPhieu)
            => _repo.GetTongHopGhiChuAsync(idPhieu);

        public Task SaveTongHopGhiChuAsync(Hrc1SaveTongHopGhiChuRequest req)
            => _repo.SaveTongHopGhiChuAsync(req);

        // ── Chi tiết Excel (BBGN phôi tấm) ──────────────────────────────────

        public async Task<ExportFileResult> ExportChiTietExcelAsync(Guid idPhieu)
        {
            var phieu = await GetPhieuForExportAsync(idPhieu);
            var slabs = await GetSlabsForExportAsync(idPhieu);
            var tenVatTuMap = await _repo.GetTenVatTuMapAsync(slabs.Select(s => s.MacThep));

            var soPhieu = phieu?.SoPhieu ?? "";
            var ngaySX = phieu?.NgaySX?.ToString("dd/MM/yyyy") ?? "";

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC1_BBSL_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            ws.Cell(3, 1).Value = BuildInfoKip(phieu?.Ca, phieu?.Kip, phieu?.NgaySX);
            SetThanhPhanCell(ws, 4, await BuildThanhPhanTextAsync(slabs));

            const int startRow = 6;
            var rowIndex = startRow;
            var stt = 1;

            foreach (var slab in slabs)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = slab.MaVatTu ?? "";
                tenVatTuMap.TryGetValue(slab.MacThep ?? "", out var tenVatTu);
                ws.Cell(rowIndex, 3).Value = BuildSanPhamLabel(tenVatTu, slab.MacThep);
                ws.Cell(rowIndex, 4).Value = slab.MaMe ?? "";
                ws.Cell(rowIndex, 5).Value = slab.IDSlab;
                ws.Cell(rowIndex, 6).Value = slab.KhoiLuong.HasValue ? (double)slab.KhoiLuong.Value : 0;
                ws.Cell(rowIndex, 7).Value = slab.GhiChu ?? "";

                rowIndex++;
                stt++;
            }

            // Total row
            if (slabs.Count > 0)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = "Tổng";
                ws.Cell(rowIndex, 2).Value = "";
                ws.Cell(rowIndex, 3).Value = "";
                ws.Cell(rowIndex, 4).Value = "";
                ws.Cell(rowIndex, 5).Value = "";
                ws.Cell(rowIndex, 6).Value =   (double)slabs.Sum(r => r.KhoiLuong.HasValue ? r.KhoiLuong.Value : 0);
                ws.Cell(rowIndex, 7).Value = "";
            }

            if (rowIndex > startRow)
                SetThinBorders(ws, startRow, rowIndex - 1, 7);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = $"HRC1_BBSL_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.xlsx",
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

            var tenVatTuMap = await _repo.GetTenVatTuMapAsync(slabs.Select(s => s.MacThep));
            var tongHopRows = BuildTongHopRows(slabs, ghiChus, tenVatTuMap);

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC1_BBXNSL_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            ws.Cell(3, 1).Value = BuildInfoKip(phieu?.Ca, phieu?.Kip, phieu?.NgaySX);
            SetThanhPhanCell(ws, 4, await BuildThanhPhanTextAsync(slabs));

            const int startRow = 6;
            var rowIndex = startRow;
            var stt = 1;

            foreach (var row in tongHopRows)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                var label = BuildSanPhamLabel(row.TenVatTu, row.MacThep);

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
            var ngaySX = phieu?.NgaySX?.ToString("dd 'tháng' MM 'năm' yyyy") ?? "";
            // Ca ngày = 1, Ca đêm = 2 — giữ nguyên số để ghép "Kíp: {ca}{kip}" (vd "1A")
            var ca = phieu?.Ca?.ToString() ?? "";
            var kip = phieu?.Kip ?? "";

            var tenVatTuMap = await _repo.GetTenVatTuMapAsync(slabs.Select(s => s.MacThep));
            var tongHopRows = BuildTongHopRows(slabs, ghiChus, tenVatTuMap);

            var rowsHtml = new StringBuilder();
            foreach (var row in tongHopRows)
            {
                var label = BuildSanPhamLabel(row.TenVatTu, row.MacThep);
                rowsHtml.Append("<tr>");
                rowsHtml.Append($"<td style=\"text-align:center\">{row.Stt}</td>");
                rowsHtml.Append($"<td colspan=\"2\">{label}</td>");
                rowsHtml.Append($"<td style=\"text-align:right\">{row.SoPhoi.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</td>");
                rowsHtml.Append($"<td style=\"text-align:right\">{row.TongKL.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</td>");
                rowsHtml.Append($"<td>{row.GhiChu ?? ""}</td>");
                rowsHtml.Append("</tr>");
            }

            // Total row
            var totalSoPhoi = tongHopRows.Sum(r => r.SoPhoi);
            var totalKL = tongHopRows.Sum(r => r.TongKL);
            rowsHtml.Append("<tr>");
            rowsHtml.Append("<td colspan=\"3\" style=\"text-align:center\"><strong>Tổng sản lượng: </strong></td>");
            rowsHtml.Append($"<td style=\"text-align:right\"><strong>{totalSoPhoi.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</strong></td>");
            rowsHtml.Append($"<td style=\"text-align:right\"><strong>{totalKL.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"))}</strong></td>");
            rowsHtml.Append("<td></td>");
            rowsHtml.Append("</tr>");

            var (ducUserId, canUserId, c4UserId) = await GetPhieuSignersAsync(slabs.Select(s => s.Id).ToList());
            var signerIds = new[] { ducUserId, canUserId, c4UserId }
                .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
            var userMap = signerIds.Count > 0
                ? await _masterCtx.Tbl_TaiKhoan
                    .Include(t => t.ViTri)
                    .Include(t => t.PhongBan)
                    .Where(t => signerIds.Contains(t.ID_TaiKhoan))
                    .ToDictionaryAsync(t => t.ID_TaiKhoan)
                : new Dictionary<int, TaiKhoan>();

            var (ducSigImg, ducSigTen) = await BuildSigPartsAsync(ducUserId, userMap);
            var (canSigImg, canSigTen) = await BuildSigPartsAsync(canUserId, userMap);
            // Luồng C4 đã bỏ cho phiếu mới — chỉ phiếu cũ có người C4 xác nhận mới in ô ký + dòng thành phần C4
            var c4SigCellHtml = "";
            if (c4UserId != null)
            {
                var (c4SigImg, c4SigTen) = await BuildSigPartsAsync(c4UserId, userMap);
                c4SigCellHtml =
                    "<div class=\"sig-cell\">" +
                    "<div class=\"sig-title\">PGĐ/GĐ NM</div>" +
                    "<div class=\"sig-note\">(Ký, ghi rõ họ tên)</div>" +
                    $"<div class=\"sig-space\">{c4SigImg}</div>" +
                    $"<div class=\"sig-name\">{c4SigTen}</div>" +
                    "</div>";
            }

            var thanhPhanHtml = string.Concat(
                BuildThanhPhanLine(1, ducUserId, userMap),
                BuildThanhPhanLine(2, canUserId, userMap),
                c4UserId != null ? BuildThanhPhanLine(3, c4UserId, userMap) : "");

            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "HRC1_BBXNSL_PhoiTam.html");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy template HTML: {templatePath}");
            var logoUrl = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            var html = await File.ReadAllTextAsync(templatePath);
            var bmCode = await _bmConfig.GetBmCodeHtmlAsync("HRC1_BBSL_PhoiTam");
            html = html
                .Replace("{{LogoUrl}}",       logoUrl)
                .Replace("{{BmCode}}", bmCode)
                .Replace("{{soPhieu}}", soPhieu)
                .Replace("{{ngaySX}}", ngaySX)
                .Replace("{{ca}}", ca)
                .Replace("{{kip}}", kip)
                .Replace("{{TABLE_BODY}}", rowsHtml.ToString())
                .Replace("{{ThanhPhanHtml}}", thanhPhanHtml)
                .Replace("{{DucKyImgHtml}}", ducSigImg)
                .Replace("{{DucKyTenHtml}}", ducSigTen)
                .Replace("{{CanKyImgHtml}}", canSigImg)
                .Replace("{{CanKyTenHtml}}", canSigTen)
                .Replace("{{C4SigCellHtml}}", c4SigCellHtml);

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

        private static string BuildSanPhamLabel(string? tenVatTu, string? macThep)
        {
            if (!string.IsNullOrWhiteSpace(tenVatTu)) return tenVatTu!;
            if (!string.IsNullOrWhiteSpace(macThep)) return macThep!;
            return "-";
        }

        private static string BuildInfoKip(int? ca, string? kip, DateOnly? ngaySX)
        {
            var caVal = ca ?? 1;
            var ngay = ngaySX ?? DateOnly.FromDateTime(DateTime.Today);

            var (gioBatDau, gioKetThuc, ngayKetThuc) = caVal == 2
                ? ("20 giờ 00", "08 giờ 00", ngay.AddDays(1))
                : ("08 giờ 00", "20 giờ 00", ngay);

            return $"Kíp {caVal}{kip}   Từ {gioBatDau} ngày {ngay:dd/MM/yyyy}   đến {gioKetThuc} ngày {ngayKetThuc:dd/MM/yyyy}";
        }

        // Dòng "Thành phần" (Đúc/Cán/GĐ-PGĐ NM xác nhận gần nhất) — cùng nguồn dữ liệu với
        // DucKyTenHtml/CanKyTenHtml/C4KyTenHtml ở ExportTongHopPdfAsync, nhưng render dạng text
        // thuần theo format "THÀNH PHẦN" của BBGN_ThepLong.html thay vì HTML chữ ký.
        private async Task<string> BuildThanhPhanTextAsync(List<Hrc1Slab> slabs)
        {
            var (ducUserId, canUserId, c4UserId) = await GetPhieuSignersAsync(slabs.Select(s => s.Id).ToList());
            var signerIds = new[] { ducUserId, canUserId, c4UserId }
                .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
            var userMap = signerIds.Count > 0
                ? await _masterCtx.Tbl_TaiKhoan
                    .Include(t => t.ViTri)
                    .Where(t => signerIds.Contains(t.ID_TaiKhoan))
                    .ToDictionaryAsync(t => t.ID_TaiKhoan)
                : new Dictionary<int, TaiKhoan>();

            string Line(string label, int? userId)
            {
                if (userId == null || !userMap.TryGetValue(userId.Value, out var u))
                    return $"{label}: Ông/Bà: -";
                var chucVu = u.ViTri?.TenViTri;
                return string.IsNullOrWhiteSpace(chucVu)
                    ? $"{label}: Ông/Bà: {u.HoVaTen}"
                    : $"{label}: Ông/Bà: {u.HoVaTen}   Chức vụ: {chucVu}";
            }

            var lines = new List<string>
            {
                "Chúng tôi gồm:",
                Line("1. Đúc", ducUserId),
                Line("2. Cán", canUserId),
            };
            // Luồng C4 đã bỏ cho phiếu mới — chỉ in dòng GĐ/PGĐ NM khi phiếu cũ có người C4 xác nhận
            if (c4UserId != null)
                lines.Add(Line("3. GĐ/PGĐ NM", c4UserId));
            return string.Join("\n", lines);
        }

        private static void SetThanhPhanCell(IXLWorksheet ws, int row, string text)
        {
            var cell = ws.Cell(row, 1);
            cell.Value = text;
            cell.Style.Alignment.SetWrapText(true);
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
            cell.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
            ws.Row(row).Height = 60;
        }

        private static void SetThinBorders(IXLWorksheet ws, int fromRow, int toRow, int lastCol)
        {
            var range = ws.Range(fromRow, 1, toRow, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        // Với mỗi vai trò (Đúc/Cán/C4), lấy người xác nhận GẦN NHẤT trong số các slab thuộc phiếu
        // (thông thường xác nhận là hành động hàng loạt nên cùng 1 người; trường hợp lệch người giữa
        // các đợt xác nhận thì ưu tiên người xác nhận sau cùng làm đại diện chữ ký).
        private async Task<(int? Duc, int? Can, int? C4)> GetPhieuSignersAsync(List<int> slabIds)
        {
            if (slabIds.Count == 0) return (null, null, null);

            var trangThais = await _context.Hrc1SlabTrangThais
                .AsNoTracking()
                .Where(t => slabIds.Contains(t.IdSlab))
                .ToListAsync();

            int? ducUserId = trangThais
                .Where(t => t.NguoiXacNhanDuc != null)
                .OrderByDescending(t => t.NgayXacNhanDuc)
                .Select(t => t.NguoiXacNhanDuc)
                .FirstOrDefault();

            int? canUserId = trangThais
                .Where(t => t.NguoiXacNhanCan != null)
                .OrderByDescending(t => t.NgayXacNhanCan)
                .Select(t => t.NguoiXacNhanCan)
                .FirstOrDefault();

            int? c4UserId = trangThais
                .Where(t => t.NguoiXacNhanC4 != null)
                .OrderByDescending(t => t.NgayXacNhanC4)
                .Select(t => t.NguoiXacNhanC4)
                .FirstOrDefault();

            return (ducUserId, canUserId, c4UserId);
        }

        // Dòng "Thành phần" trong biên bản PDF — cùng nguồn dữ liệu với chữ ký (Duc/Can/C4),
        // format: "N. Ông/Bà: Tên   Chức vụ: ...   BP: ..."
        private static string BuildThanhPhanLine(int idx, int? userId, Dictionary<int, TaiKhoan> userMap)
        {
            var u = userId != null && userMap.TryGetValue(userId.Value, out var uu) ? uu : null;
            if (u == null)
                return $"<div class=\"info-row\">{idx}. Ông/Bà: -</div>";

            var ten = System.Net.WebUtility.HtmlEncode(u.HoVaTen ?? "");
            var chucVu = System.Net.WebUtility.HtmlEncode(u.ViTri?.TenViTri ?? "");
            var bp = System.Net.WebUtility.HtmlEncode(u.PhongBan?.TenNgan ?? "");

            var body = $"Ông/Bà: <strong>{ten}</strong>";
            if (!string.IsNullOrEmpty(chucVu)) body += $"&nbsp;&nbsp;&nbsp;&nbsp;Chức vụ: {chucVu}";
            if (!string.IsNullOrEmpty(bp)) body += $"&nbsp;&nbsp;&nbsp;&nbsp;BP: {bp}";
            return $"<div class=\"info-row\">{idx}. {body}</div>";
        }

        private async Task<(string ImgHtml, string TenHtml)> BuildSigPartsAsync(int? userId, Dictionary<int, TaiKhoan> userMap)
        {
            if (userId == null || !userMap.TryGetValue(userId.Value, out var u))
                return ("", "");

            var imgTag = await FormatChuKyImgAsync(u.ChuKy);
            var name = System.Net.WebUtility.HtmlEncode(u.HoVaTen ?? "");
            return (imgTag, name);
        }

        private async Task<string> FormatChuKyImgAsync(string? chuKy)
        {
            if (string.IsNullOrWhiteSpace(chuKy)) return "";

            if (chuKy.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return $"<img src=\"{chuKy}\" style=\"max-width:120px;max-height:50px;\" />";

            string url = chuKy.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? chuKy
                : (_config.GetValue<string>("AppSettings:Domain") ?? "https://report.hoaphatdungquat.vn").TrimEnd('/') + chuKy;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var bytes = await client.GetByteArrayAsync(url);
                var mime = url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                return $"<img src=\"data:{mime};base64,{Convert.ToBase64String(bytes)}\" style=\"max-width:120px;max-height:50px;\" />";
            }
            catch { return ""; }
        }

        private async Task<BmPhieu?> GetPhieuForExportAsync(Guid idPhieu)
        {
            return await _context.BmPhieus
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == "HRC1_BBSL_PhoiTam");
        }

        private async Task<List<Hrc1Slab>> GetSlabsForExportAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == "HRC1_BBSL_PhoiTam");
            if (phieu == null) return [];

            // Reuse repository helper
            var repo = (Hrc1SlabRepository)_repo;
            return await repo.LoadPhieuSlabsAsync(phieu);
        }

        private record TongHopRow(int Stt, string? MacThep, string? MaVatTu, string? TenVatTu, int SoPhoi, decimal TongKL, string? GhiChu);

        private static List<TongHopRow> BuildTongHopRows(List<Hrc1Slab> slabs, List<Hrc1BbslTongHopGhiChu> ghiChus, Dictionary<string, string> tenVatTuMap)
        {
            var map = new Dictionary<string, (string? MacThep, string? MaVatTu, int SoPhoi, decimal TongKL)>();

            foreach (var slab in slabs)
            {
                var key = $"{slab.MacThep ?? ""}|{slab.MaVatTu ?? ""}";

                if (!map.ContainsKey(key))
                    map[key] = (slab.MacThep, slab.MaVatTu, 0, 0);

                var cur = map[key];
                map[key] = (cur.MacThep, cur.MaVatTu, cur.SoPhoi + 1, cur.TongKL + (slab.KhoiLuong ?? 0));
            }

            var ghiChuDict = ghiChus.ToDictionary(
                g => $"{g.MacThep ?? ""}|{g.MaVatTu ?? ""}",
                g => g.GhiChu);

            return map.Select((kvp, i) =>
            {
                var ghiChu = ghiChuDict.TryGetValue(kvp.Key, out var gc) ? gc : null;
                tenVatTuMap.TryGetValue(kvp.Value.MacThep ?? "", out var tenVatTu);
                return new TongHopRow(i + 1, kvp.Value.MacThep, kvp.Value.MaVatTu, tenVatTu, kvp.Value.SoPhoi, kvp.Value.TongKL, ghiChu);
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
