using ClosedXML.Excel;
using dataproduct.api.DTOs.Export;
using dataproduct.api.DTOs.NMTKVV_Dto;
using dataproduct.api.Repositories;
using dataproduct.api.Repositories.NMTKVV;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Text;

namespace dataproduct.api.Services.NMTKVV
{
    public class TKVV_BCSL_ChiPhiService
    {
        private readonly ITKVV_BCSL_ChiPhiRepository _repo;
        private readonly IPhieuRepository _repoPhieu;
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PheDuyetService _pheDuyetService;

        public TKVV_BCSL_ChiPhiService(
            ITKVV_BCSL_ChiPhiRepository repo,
            IPhieuRepository repoPhieu,
            IConverter pdfConverter,
            IWebHostEnvironment env,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            PheDuyetService pheDuyetService)
        {
            _repo = repo;
            _repoPhieu = repoPhieu;
            _pdfConverter = pdfConverter;
            _env = env;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _pheDuyetService = pheDuyetService;
        }

        public Task<List<TKVVGiaTriNVLAutoDto>> GetGiaTriNVLAutoAsync(
            DateTime ngay, int ca, int scope, string maBM)
            => _repo.GetGiaTriNVLAutoAsync(ngay, ca, scope.ToString(), maBM);

        public Task<List<TKVVDuLieuCanDto>> GetDuLieuCanAsync(
            DateTime ngay, int ca, string maBM, string loaiDuLieu, int scope)
            => _repo.GetDuLieuCanAsync(ngay, ca, maBM, loaiDuLieu, scope);

        public Task<LoadDuLieuCanResultDto> LoadAndSaveAsync(LoadDuLieuCanRequestDto request)
            => _repo.LoadAndSaveAsync(request);

        public Task<LoadDuLieuCanResultDto> GetBaoCaoDataAsync(DateOnly ngaySX, string maBM, int scope, int? caSX = null)
            => _repo.GetBaoCaoDataAsync(ngaySX, maBM, scope, caSX);

        public Task<LoadDuLieuCanResultDto> GetByPhieuIdAsync(Guid phieuId)
            => _repo.GetByPhieuIdAsync(phieuId);

        public Task SavePhieuRowsAsync(SaveBcSlPhieuRequestDto request)
            => _repo.SavePhieuRowsAsync(request);

        public Task<int> RefreshBbgnAsync(Guid phieuId, int? currentUserId)
            => _repo.RefreshBbgnAsync(phieuId, currentUserId);

        // ─── Export PDF Báo cáo sản lượng & chi phí (BM.06-QT.05.03) ────────────
        // Cùng cơ chế với TKVV_BBSLService.ExportBienBanPdfAsync (Biên bản sản lượng):
        // HTML template + DinkToPdf. 1 phiếu = 1 ngày (từ 8h00 hôm nay đến 8h00 hôm sau),
        // gồm 2 khối bảng theo Kíp (Ca ngày / Ca đêm) — khớp đúng biểu mẫu giấy gốc.

        public async Task<ExportFileResult> ExportBaoCaoPdfAsync(Guid idPhieu)
        {
            var phieu = await _repoPhieu.GetByIdAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu.");

            var data = await _repo.GetByPhieuIdAsync(idPhieu);

            var ngay = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);
            var ngaySau = ngay.AddDays(1);
            var tenScope = phieu.TenScope
                ?? (phieu.Scope.HasValue ? TKVV_BCSL_ChiPhiRepository.ResolveScopeCode(phieu.Scope.Value) : "");

            var pheDuyets = await _pheDuyetService.GetPheDuyetPhieuAsync(idPhieu);
            var nguoiGiaoKip = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);
            var nguoiNhanKip = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1);

            var caInPhieu = data.Table.FirstOrDefault()?.Ca ?? 1;
            var rowsNgay = caInPhieu == 1 ? BuildRows(data.Table, 1, out var tongNgay) : "";
            var rowsDem = caInPhieu == 2 ? BuildRows(data.Table, 2, out var tongDem) : "";

            var logoBase64 = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            var signGiaoKip = await FormatChuKyBase64Async(nguoiGiaoKip?.ChuKy, nguoiGiaoKip?.TinhTrang == 1);
            var signNhanKip = await FormatChuKyBase64Async(nguoiNhanKip?.ChuKy, nguoiNhanKip?.TinhTrang == 1);

            var templatePath = Path.Combine(
                _env.WebRootPath, "template_html", "BM.06-QT.05.03_Bao_cao_san_luong_chi_phi.html");
            var html = await File.ReadAllTextAsync(templatePath);

            html = html
                .Replace("{{LogoUrl}}", logoBase64)
                .Replace("{{Xuong}}", System.Net.WebUtility.HtmlEncode(tenScope))
                .Replace("{{NgayBatDau}}", ngay.Day.ToString("00"))
                .Replace("{{ThangBatDau}}", ngay.Month.ToString("00"))
                .Replace("{{NgayKetThuc}}", ngaySau.Day.ToString("00"))
                .Replace("{{ThangKetThuc}}", ngaySau.Month.ToString("00"))
                .Replace("{{Nam}}", ngaySau.Year.ToString())
                // .Replace("{{RowsNgay}}", caInPhieu == 1 ? rowsNgay + BuildTongRow(tongNgay) : "")
                // .Replace("{{RowsDem}}", caInPhieu == 2 ? rowsDem + BuildTongRow(tongDem) : "")
                .Replace("{{Sign_GiaoKip}}", signGiaoKip)
                .Replace("{{Sign_NhanKip}}", signNhanKip)
                .Replace("{{Name_GiaoKip}}", System.Net.WebUtility.HtmlEncode(nguoiGiaoKip?.HoVaTen ?? ""))
                .Replace("{{Name_NhanKip}}", System.Net.WebUtility.HtmlEncode(nguoiNhanKip?.HoVaTen ?? ""));

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    PaperSize = PaperKind.A4,
                    Orientation = Orientation.Landscape,
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
                            PrintMediaType = true,
                        },
                        LoadSettings =
                        {
                            BlockLocalFileAccess = false,
                            LoadErrorHandling = ContentErrorHandling.Ignore,
                        }
                    }
                }
            };

            var pdfBytes = _pdfConverter.Convert(doc);
            var fileName = $"BaoCaoSanLuongChiPhi_{phieu.SoPhieu ?? idPhieu.ToString("N")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return new ExportFileResult
            {
                Content = pdfBytes,
                FileName = fileName,
                ContentType = "application/pdf",
            };
        }

        // Cột "Kíp" = Ca (1/2) + Kíp chữ (A/B/C) do KTV nhập khi lưu phiếu, khớp đúng biểu mẫu
        // giấy gốc ("kíp ca + kíp": Ca 1/2, Kíp A/B/C) — gộp theo chiều dọc (rowspan) cho cả khối.
        private static string BuildRows(List<TKVVBaoCaoSanLuongChiPhiDto> items, int ca, out (decimal KLAm, decimal QuyKho, decimal L1, decimal L2, decimal L3) tong)
        {
            var kipChu = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Kip))?.Kip;
            var kipLabel = string.IsNullOrWhiteSpace(kipChu) ? $"Ca {ca}" : $"Ca {ca} - Kíp {kipChu}";

            var rows = new StringBuilder();
            decimal klAm = 0, quyKho = 0, l1 = 0, l2 = 0, l3 = 0;
            var rowspan = Math.Max(items.Count, 1);
            var first = true;

            if (items.Count == 0)
            {
                rows.Append($"<tr class=\"data-row\"><td class=\"text-center\">{System.Net.WebUtility.HtmlEncode(kipLabel)}</td><td class=\"text-left\"></td><td></td><td></td><td></td><td></td><td></td><td></td></tr>");
            }

            foreach (var r in items)
            {
                rows.Append("<tr class=\"data-row\">");
                if (first)
                {
                    rows.Append($"<td class=\"text-center\" rowspan=\"{rowspan}\">{System.Net.WebUtility.HtmlEncode(kipLabel)}</td>");
                    first = false;
                }
                rows.Append($"<td class=\"text-left\">{System.Net.WebUtility.HtmlEncode(r.TenNVL ?? "")}</td>");
                rows.Append($"<td class=\"text-right\">{(r.KLAm.HasValue ? r.KLAm.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(r.DoAm.HasValue ? r.DoAm.Value.ToString("#,##0.##") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(r.QuyKho.HasValue ? r.QuyKho.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(r.ThanhPhamL1.HasValue ? r.ThanhPhamL1.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(r.ThanhPhamL2.HasValue ? r.ThanhPhamL2.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(r.ThanhPhamL3.HasValue ? r.ThanhPhamL3.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append("</tr>");

                klAm += r.KLAm ?? 0;
                quyKho += r.QuyKho ?? 0;
                l1 += r.ThanhPhamL1 ?? 0;
                l2 += r.ThanhPhamL2 ?? 0;
                l3 += r.ThanhPhamL3 ?? 0;
            }

            tong = (klAm, quyKho, l1, l2, l3);
            return rows.ToString();
        }

        private static string BuildTongRow((decimal KLAm, decimal QuyKho, decimal L1, decimal L2, decimal L3) tong)
            => $@"
                <tr class=""total-row"">
                    <td colspan=""2"">Tổng kíp</td>
                    <td class=""text-right"">{tong.KLAm:#,##0.###}</td>
                    <td></td>
                    <td class=""text-right"">{tong.QuyKho:#,##0.###}</td>
                    <td class=""text-right"">{tong.L1:#,##0.###}</td>
                    <td class=""text-right"">{tong.L2:#,##0.###}</td>
                    <td class=""text-right"">{tong.L3:#,##0.###}</td>
                </tr>";

        // ─── Export Excel Báo cáo sản lượng & chi phí ───────────────────────────
        public async Task<ExportFileResult> ExportBaoCaoExcelAsync(Guid idPhieu)
        {
            var phieu = await _repoPhieu.GetByIdAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu.");

            var data = await _repo.GetByPhieuIdAsync(idPhieu);
            var rows = data.Table.OrderBy(x => x.ThuTu).ToList();

            var ngay = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);
            var ngaySau = ngay.AddDays(1);
            var ca = rows.FirstOrDefault()?.Ca ?? phieu.Ca ?? 0;
            var caLabel = ca == 1 ? "Ca ngày" : ca == 2 ? "Ca đêm" : $"Ca {ca}";
            var kipChu = rows.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Kip))?.Kip;
            var kipLabel = string.IsNullOrWhiteSpace(kipChu) ? caLabel : $"{caLabel} - Kíp {kipChu}";
            var tenScope = phieu.TenScope
                ?? (phieu.Scope.HasValue ? TKVV_BCSL_ChiPhiRepository.ResolveScopeCode(phieu.Scope.Value) : "");

            var pheDuyets = await _pheDuyetService.GetPheDuyetPhieuAsync(idPhieu);
            var nguoiGiaoKip = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);
            var nguoiNhanKip = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1);

            const int totalCols = 10;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("BaoCaoSanLuongChiPhi");
            ws.Style.Font.FontName = "Times New Roman";
            ws.Style.Font.FontSize = 11;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;

            // ── Header ────────────────────────────────────────────────────────
            ws.Cell(1, 1).Value = "CÔNG TY CỔ PHẦN THÉP HÒA PHÁT DUNG QUẤT";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(1, 1, 1, totalCols).Merge();

            ws.Cell(2, 1).Value =
                $"BM.02/QT.05.03     Từ 8h00 ngày {ngay:dd/MM/yyyy} đến 8h00 ngày {ngaySau:dd/MM/yyyy}     {kipLabel}     Xưởng: {tenScope}";
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(2, 1, 2, totalCols).Merge();

            ws.Cell(3, 1).Value = "BÁO CÁO SẢN LƯỢNG & CHI PHÍ SẢN XUẤT HÀNG NGÀY";
            ws.Cell(3, 1).Style.Font.Bold = true;
            ws.Cell(3, 1).Style.Font.FontSize = 14;
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(3, 1, 3, totalCols).Merge();

            ws.Cell(4, 1).Value =
                $"Người giao kíp: {nguoiGiaoKip?.HoVaTen ?? ""}    Chức vụ: {nguoiGiaoKip?.TenViTri ?? ""}    Bộ phận: {nguoiGiaoKip?.TenPhongBan ?? ""}";
            ws.Range(4, 1, 4, totalCols).Merge();

            ws.Cell(5, 1).Value =
                $"Người nhận kíp: {nguoiNhanKip?.HoVaTen ?? ""}    Chức vụ: {nguoiNhanKip?.TenViTri ?? ""}    Bộ phận: {nguoiNhanKip?.TenPhongBan ?? ""}";
            ws.Range(5, 1, 5, totalCols).Merge();

            // ── Tiêu đề cột ───────────────────────────────────────────────────
            string[] headers = {
                "STT", "Nguyên nhiên vật liệu đầu vào",
                "KL ẩm (Tấn)", "Độ ẩm (%)", "Quy khô (Tấn)",
                "Vận chuyển", "Thành phẩm L1 (Tấn)", "Thành phẩm L2 (Tấn)", "Thành phẩm L3 (Tấn)",
                "Ghi chú"
            };
            int headerRow = 6;
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = true;
            }
            ws.Range(headerRow, 1, headerRow, totalCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, headerRow, totalCols).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Row(headerRow).Height = 36;

            // ── Dữ liệu ──────────────────────────────────────────────────────
            int dataRow = headerRow + 1;
            int stt = 0;
            decimal tongKLAm = 0, tongQuyKho = 0, tongL1 = 0, tongL2 = 0, tongL3 = 0;

            foreach (var r in rows)
            {
                stt++;
                ws.Cell(dataRow, 1).Value = stt;
                ws.Cell(dataRow, 2).Value = r.TenNVL ?? "";
                if (r.KLAm.HasValue) ws.Cell(dataRow, 3).Value = (double)r.KLAm.Value;
                if (r.DoAm.HasValue) ws.Cell(dataRow, 4).Value = (double)r.DoAm.Value;
                if (r.QuyKho.HasValue) ws.Cell(dataRow, 5).Value = (double)r.QuyKho.Value;
                ws.Cell(dataRow, 6).Value = r.ThanhPham_Note ?? "";
                if (r.ThanhPhamL1.HasValue) ws.Cell(dataRow, 7).Value = (double)r.ThanhPhamL1.Value;
                if (r.ThanhPhamL2.HasValue) ws.Cell(dataRow, 8).Value = (double)r.ThanhPhamL2.Value;
                if (r.ThanhPhamL3.HasValue) ws.Cell(dataRow, 9).Value = (double)r.ThanhPhamL3.Value;
                ws.Cell(dataRow, 10).Value = r.GhiChu ?? "";

                ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                for (int c = 3; c <= 9; c++)
                    ws.Cell(dataRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(dataRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                tongKLAm += r.KLAm ?? 0;
                tongQuyKho += r.QuyKho ?? 0;
                tongL1 += r.ThanhPhamL1 ?? 0;
                tongL2 += r.ThanhPhamL2 ?? 0;
                tongL3 += r.ThanhPhamL3 ?? 0;
                dataRow++;
            }

            // ── Dòng tổng ─────────────────────────────────────────────────────
            ws.Cell(dataRow, 1).Value = "Tổng";
            ws.Range(dataRow, 1, dataRow, 2).Merge();
            ws.Cell(dataRow, 1).Style.Font.Bold = true;
            ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(dataRow, 3).Value = (double)tongKLAm;
            ws.Cell(dataRow, 5).Value = (double)tongQuyKho;
            ws.Cell(dataRow, 7).Value = (double)tongL1;
            ws.Cell(dataRow, 8).Value = (double)tongL2;
            ws.Cell(dataRow, 9).Value = (double)tongL3;
            foreach (int c in new[] { 3, 5, 7, 8, 9 })
            {
                ws.Cell(dataRow, c).Style.Font.Bold = true;
                ws.Cell(dataRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
            for (int c = 1; c <= totalCols; c++)
                ws.Cell(dataRow, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow += 2;

            // ── Chữ ký ────────────────────────────────────────────────────────
            ws.Cell(dataRow, 1).Value = "Người giao kíp";
            ws.Cell(dataRow, 1).Style.Font.Bold = true;
            ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(dataRow, 1, dataRow, 5).Merge();
            ws.Cell(dataRow, 6).Value = "Người nhận kíp";
            ws.Cell(dataRow, 6).Style.Font.Bold = true;
            ws.Cell(dataRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(dataRow, 6, dataRow, totalCols).Merge();
            dataRow += 3;
            ws.Cell(dataRow, 1).Value = nguoiGiaoKip?.HoVaTen ?? "";
            ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(dataRow, 1, dataRow, 5).Merge();
            ws.Cell(dataRow, 6).Value = nguoiNhanKip?.HoVaTen ?? "";
            ws.Cell(dataRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(dataRow, 6, dataRow, totalCols).Merge();

            // ── Column widths ─────────────────────────────────────────────────
            ws.Column(1).Width = 6;
            ws.Column(2).Width = 28;
            ws.Column(3).Width = 13;
            ws.Column(4).Width = 11;
            ws.Column(5).Width = 13;
            ws.Column(6).Width = 14;
            ws.Column(7).Width = 14;
            ws.Column(8).Width = 14;
            ws.Column(9).Width = 14;
            ws.Column(10).Width = 20;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"BaoCaoSanLuongChiPhi_{phieu.SoPhieu ?? idPhieu.ToString("N")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = fileName,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            };
        }

        private async Task<string> ConvertImageUrlToBase64Async(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return "";

            if (imageUrl.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return imageUrl;

            try
            {
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                var base64 = Convert.ToBase64String(imageBytes);

                var extension = Path.GetExtension(imageUrl).ToLower();
                var mimeType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".svg" => "image/svg+xml",
                    _ => "image/png"
                };

                return $"data:{mimeType};base64,{base64}";
            }
            catch
            {
                return "";
            }
        }

        private async Task<string> FormatChuKyBase64Async(string? chuKy, bool daKy = false)
        {
            if (string.IsNullOrWhiteSpace(chuKy))
                return daKy ? "<div style='font-style:italic;color:red'>Đã ký</div>" : "";

            if (chuKy.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return $"<img src=\"{chuKy}\" style=\"max-width:150px;max-height:80px;\" />";

            if (chuKy.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var base64 = await ConvertImageUrlToBase64Async(chuKy);
                if (!string.IsNullOrEmpty(base64))
                    return $"<img src=\"{base64}\" style=\"max-width:150px;max-height:80px;\" />";
            }
            else if (chuKy.StartsWith("/"))
            {
                var domain = _configuration.GetValue<string>("AppSettings:Domain") ?? "https://report.hoaphatdungquat.vn";
                var fullUrl = domain.TrimEnd('/') + chuKy;
                var base64 = await ConvertImageUrlToBase64Async(fullUrl);
                if (!string.IsNullOrEmpty(base64))
                    return $"<img src=\"{base64}\" style=\"max-width:150px;max-height:80px;\" />";
            }

            return daKy ? "<div style='font-style:italic;color:red'>Đã ký</div>" : "";
        }
    }
}
