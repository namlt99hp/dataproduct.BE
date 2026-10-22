using dataproduct.api.DTOs.Export;
using dataproduct.api.DTOs.NMTKVV_Dto;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using ClosedXML.Excel;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Text;
using System.Text.Json;

namespace dataproduct.api.Services
{
    public class TKVV_BBSLService
    {
        private readonly ITKVV_BBSLRepository _repo;
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PheDuyetService _pheDuyetService;

        public TKVV_BBSLService(
            ITKVV_BBSLRepository repo,
            IConverter pdfConverter,
            IWebHostEnvironment env,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            PheDuyetService pheDuyetService)
        {
            _repo = repo;
            _pdfConverter = pdfConverter;
            _env = env;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _pheDuyetService = pheDuyetService;
        }

        // ─── Danh mục NVL ────────────────────────────────────────────────────

        public Task<List<TKVVNguyenVatLieuDto>> GetNvlListAsync(string? maBM, string? scope)
            => _repo.GetNvlListAsync(maBM, scope);

        public async Task<TKVVNguyenVatLieuDto?> GetNvlByIdAsync(int id)
        {
            var e = await _repo.GetNvlByIdAsync(id);
            return e == null ? null : MapNvl(e);
        }

        public async Task<TKVVNguyenVatLieuDto> AddNvlAsync(CreateTKVVNguyenVatLieuDto dto)
        {
            var scopeValue = ResolveScopeValue(dto.Scope);
            var entity = new TKVV_NguyenVatLieu
            {
                MaBM = dto.MaBM,
                TenNVL = dto.TenNVL,
                DonViTinh = dto.DonViTinh,
                ThuTu = dto.ThuTu,
                GhiChu = dto.GhiChu,
                TrangThai = true,
                Scope = scopeValue,
                TenScope = dto.TenScope,
            };
            var result = await _repo.AddNvlAsync(entity);
            return MapNvl(result);
        }

        public async Task<TKVVNguyenVatLieuDto?> UpdateNvlAsync(int id, UpdateTKVVNguyenVatLieuDto dto)
        {
            var scopeValue = ResolveScopeValue(dto.Scope);
            var entity = new TKVV_NguyenVatLieu
            {
                MaBM = dto.MaBM,
                TenNVL = dto.TenNVL,
                DonViTinh = dto.DonViTinh,
                ThuTu = dto.ThuTu,
                TrangThai = dto.TrangThai,
                GhiChu = dto.GhiChu,
                Scope = scopeValue,
                TenScope = dto.TenScope,
            };
            var result = await _repo.UpdateNvlAsync(id, entity);
            return result == null ? null : MapNvl(result);
        }

        // Chuyển scope đơn hoặc đa (vd: "1,2,3" hay "TK1,VV2") → chuỗi số "1,2,3"
        private static string? ResolveScopeValue(string? scope)
        {
            if (string.IsNullOrWhiteSpace(scope)) return null;
            var parts = scope.Split(',')
                             .Select(s => s.Trim())
                             .Where(s => s.Length > 0)
                             .Select(p => TKVV_BBSLRepository.ResolveScopeNumber(p)?.ToString() ?? p)
                             .ToList();
            return parts.Count > 0 ? string.Join(",", parts) : null;
        }

        public Task<bool> DeleteNvlAsync(int id) => _repo.DeleteNvlAsync(id);

        private static TKVVNguyenVatLieuDto MapNvl(TKVV_NguyenVatLieu e) => new()
        {
            Id = e.ID,
            MaBM = e.MaBM,
            TenNVL = e.TenNVL,
            DonViTinh = e.DonViTinh,
            ThuTu = e.ThuTu,
            TrangThai = e.TrangThai,
            GhiChu = e.GhiChu,
            Scope = e.Scope,
            TenScope = e.TenScope ?? e.Scope,
        };

        // ─── Dữ liệu PLC thô ─────────────────────────────────────────────────

        public Task<List<TKVVDuLieuRawDto>> GetDataByFilterAsync(
            string? scope, DateTime? ngayBatDau, DateTime? ngayKetThuc)
            => _repo.GetDataByFilterAsync(scope, ngayBatDau, ngayKetThuc);

        // ─── Tổng tự động (PLC) theo Ngay/Ca/Scope toàn cục (1-6) ──────────────

        public async Task<TKVVTongTuDongDto> GetTongTuDongAsync(DateTime ngay, int ca, int scope)
        {
            var hasData = await _repo.HasDuLieuByNgayCaScopeAsync(ngay, ca, scope);

            if (!hasData)
            {
                return new TKVVTongTuDongDto
                {
                    TongTuDong = 0,
                    HasData = false,
                    Message = $"Chưa có dữ liệu của ngày {ngay:dd/MM/yyyy}, ca {ca}, xưởng {scope}."
                };
            }

            var result = await _repo.GetTongTuDongAsync(ngay, ca, scope);
            result.HasData = true;
            result.Message = null;
            return result;
        }


        // ─── Chi tiết sản lượng theo phiếu ─────────────────────────────────────

        public Task<List<TKVVChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu)
            => _repo.GetChiTietByPhieuAsync(idPhieu);

        // ─── Export PDF / Excel biên bản xác nhận sản lượng (BM.01-QT.05.03) ───
        // Cùng cơ chế với NapLieuLoCao (LGNLService.ExportNapLieuPdfAsync/ExportNapLieuExcelAsync):
        // HTML template + DinkToPdf cho PDF, ClosedXML cho Excel. Dữ liệu lấy trực tiếp từ
        // TKVV_SanLuongChiTiet — layout khớp đúng biểu mẫu giấy gốc (STT, Lô(Batch), 1 dòng
        // Tổng theo từng cột, thứ tự chữ ký P.QLCL trước NM.TKVV).

        private async Task<(BmPhieu Phieu, List<TKVVChiTietDto> ChiTiet, Dictionary<int, string?> DonViTinhById, List<DTOs.CTD_Dto.PheDuyetDto> PheDuyets)> LoadExportDataAsync(Guid idPhieu)
        {
            var phieu = await _repo.GetPhieuByIdAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu.");

            var chiTiet = (await _repo.GetChiTietByPhieuAsync(idPhieu))
                .OrderBy(x => x.ThuTuDong)
                .ToList();

            var nvlList = await _repo.GetNvlListAsync(null, null);
            var donViTinhById = nvlList.ToDictionary(n => n.Id, n => n.DonViTinh);

            var pheDuyets = await _pheDuyetService.GetPheDuyetPhieuAsync(idPhieu);

            return (phieu, chiTiet, donViTinhById, pheDuyets);
        }

        public async Task<ExportFileResult> ExportBienBanPdfAsync(Guid idPhieu)
        {
            var (phieu, chiTiet, donViTinhById, pheDuyets) = await LoadExportDataAsync(idPhieu);

            var ngay = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);
            var kip = phieu.Kip;
            var ca = phieu.Ca ?? 0;
            var tenScope = phieu.TenScope
                ?? (phieu.Scope.HasValue ? TKVV_BBSLRepository.ResolveScopeCode(phieu.Scope.Value) : "");
            var caKip = $"{ca}{kip}";

            // Thứ tự khớp biểu mẫu giấy gốc: P.QLCL ("1. Ông/bà...", chữ ký bên trái) trước NM.TKVV.
            var nguoiQLCL = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1);
            var nguoiTKVV = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);

            var rows = new StringBuilder();
            decimal tong1 = 0, tong2 = 0, tong3 = 0, tongPP = 0;
            int stt = 0;
            foreach (var ct in chiTiet)
            {
                stt++;
                var dvt = donViTinhById.TryGetValue(ct.NguyenVatLieuID, out var d) ? d : null;
                rows.Append("<tr class=\"data-row\">");
                rows.Append($"<td>{stt}</td>");
                rows.Append($"<td>{System.Net.WebUtility.HtmlEncode(ct.ThoiGian?.ToString("HH:mm") ?? "")}</td>");
                rows.Append($"<td class=\"text-left\">{System.Net.WebUtility.HtmlEncode(ct.TenNVL ?? "")}</td>");
                rows.Append($"<td>{System.Net.WebUtility.HtmlEncode(dvt ?? "")}</td>");
                rows.Append("<td></td>"); // Lô (Batch) — chưa có dữ liệu trong hệ thống
                rows.Append($"<td class=\"text-right\">{(ct.Loai1.HasValue ? ct.Loai1.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(ct.Loai2.HasValue ? ct.Loai2.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(ct.Loai3.HasValue ? ct.Loai3.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td class=\"text-right\">{(ct.PhePham.HasValue ? ct.PhePham.Value.ToString("#,##0.###") : "")}</td>");
                rows.Append($"<td>{System.Net.WebUtility.HtmlEncode(ct.GhiChu ?? "")}</td>");
                rows.Append("</tr>");

                tong1 += ct.Loai1 ?? 0;
                tong2 += ct.Loai2 ?? 0;
                tong3 += ct.Loai3 ?? 0;
                tongPP += ct.PhePham ?? 0;
            }

            rows.Append($@"
                <tr class=""total-row"">
                    <td colspan=""5"">Tổng</td>
                    <td class=""text-right"">{tong1:#,##0.###}</td>
                    <td class=""text-right"">{tong2:#,##0.###}</td>
                    <td class=""text-right"">{tong3:#,##0.###}</td>
                    <td class=""text-right"">{tongPP:#,##0.###}</td>
                    <td></td>
                </tr>");

            var logoBase64 = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            var signTKVV = await FormatChuKyBase64Async(nguoiTKVV?.ChuKy, nguoiTKVV?.TinhTrang == 1);
            var signQLCL = await FormatChuKyBase64Async(nguoiQLCL?.ChuKy, nguoiQLCL?.TinhTrang == 1);

            var templatePath = Path.Combine(
                _env.WebRootPath, "template_html", "BM.01-QT.05.03_Bien_ban_xac_nhan_san_luong.html");
            var html = await File.ReadAllTextAsync(templatePath);

            html = html
                    .Replace("{{LogoUrl}}", logoBase64)

                    // Thông tin biên bản
                    .Replace(
                        "{{Xuong}}",
                        System.Net.WebUtility.HtmlEncode(tenScope)
                    )
                    .Replace(
                        "{{CaKip}}",
                        System.Net.WebUtility.HtmlEncode(caKip)
                    )
                    .Replace(
                        "{{Ngay}}",
                        ngay.Day.ToString("00")
                    )
                    .Replace(
                        "{{Thang}}",
                        ngay.Month.ToString("00")
                    )
                    .Replace(
                        "{{Nam}}",
                        ngay.Year.ToString()
                    )

                    // Bảng dữ liệu
                    .Replace("{{Rows}}", rows.ToString())

                    // Chữ ký
                    .Replace("{{Sign_TKVV}}", signTKVV)
                    .Replace("{{Sign_QLCL}}", signQLCL)

                    .Replace(
                        "{{Name_TKVV}}",
                        System.Net.WebUtility.HtmlEncode(
                            nguoiTKVV?.HoVaTen ?? ""
                        )
                    )
                    .Replace(
                        "{{Name_QLCL}}",
                        System.Net.WebUtility.HtmlEncode(
                            nguoiQLCL?.HoVaTen ?? ""
                        )
                    )

                    .Replace(
                        "{{ChucVu_TKVV}}",
                        System.Net.WebUtility.HtmlEncode(
                            nguoiTKVV?.TenViTri ?? ""
                        )
                    )
                    .Replace(
                        "{{ChucVu_QLCL}}",
                        System.Net.WebUtility.HtmlEncode(
                            nguoiQLCL?.TenViTri ?? ""
                        )
                    )

                    .Replace(
                        "{{BoPhan_TKVV}}",
                        System.Net.WebUtility.HtmlEncode(
                            nguoiTKVV?.TenPhongBan ?? ""
                        )
                    )
                    .Replace(
                        "{{BoPhan_QLCL}}",
                        System.Net.WebUtility.HtmlEncode(
                            nguoiQLCL?.TenPhongBan ?? ""
                        )
                    );

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
            var fileName = $"BienBanSanLuong_{phieu.SoPhieu ?? idPhieu.ToString("N")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return new ExportFileResult
            {
                Content = pdfBytes,
                FileName = fileName,
                ContentType = "application/pdf",
            };
        }

        public async Task<ExportFileResult> ExportBienBanExcelAsync(Guid idPhieu)
        {
            var (phieu, chiTiet, donViTinhById, pheDuyets) = await LoadExportDataAsync(idPhieu);

            var ngay = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);
            var ca = phieu.Ca ?? 0;
            var caLabel = ca == 1 ? "Ca ngày" : ca == 2 ? "Ca đêm" : $"Ca {ca}";
            var tenScope = phieu.TenScope
                ?? (phieu.Scope.HasValue ? TKVV_BBSLRepository.ResolveScopeCode(phieu.Scope.Value) : "");

            // Thứ tự khớp biểu mẫu giấy gốc: P.QLCL trước NM.TKVV.
            var nguoiQLCL = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1);
            var nguoiTKVV = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);

            const int totalCols = 10; // STT, Thời gian, Sản lượng, ĐVT, Lô(Batch), Loại1-3, Phế phẩm, Ghi chú

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("BienBanSanLuong");
            ws.Style.Font.FontName = "Times New Roman";
            ws.Style.Font.FontSize = 11;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;

            ws.Cell(1, 1).Value = "CÔNG TY CỔ PHẦN THÉP HÒA PHÁT DUNG QUẤT";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Range(1, 1, 1, totalCols).Merge();

            ws.Cell(2, 1).Value = "BIÊN BẢN XÁC NHẬN SẢN LƯỢNG";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 14;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(2, 1, 2, totalCols).Merge();

            ws.Cell(3, 1).Value = $"Kíp {caLabel}    Ngày {ngay:dd} tháng {ngay:MM} năm {ngay:yyyy}    Áp dụng cho {tenScope}";
            ws.Range(3, 1, 3, totalCols).Merge();

            ws.Cell(4, 1).Value = $"1. Ông/bà: {nguoiQLCL?.HoVaTen ?? ""}    Chức vụ: {nguoiQLCL?.TenViTri ?? ""}    BP: {nguoiQLCL?.TenPhongBan ?? ""}";
            ws.Range(4, 1, 4, totalCols).Merge();

            ws.Cell(5, 1).Value = $"2. Ông/bà: {nguoiTKVV?.HoVaTen ?? ""}    Chức vụ: {nguoiTKVV?.TenViTri ?? ""}    BP: {nguoiTKVV?.TenPhongBan ?? ""}";
            ws.Range(5, 1, 5, totalCols).Merge();

            string[] headers = { "STT", "Thời gian", "Sản lượng", "ĐVT", "Lô (Batch)", "Loại 1", "Loại 2", "Loại 3", "Phế phẩm", "Ghi chú" };
            int headerRow = 6;
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(headerRow, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            ws.Range(headerRow, 1, headerRow, totalCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(headerRow, 1, headerRow, totalCols).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = headerRow + 1;
            int stt = 0;
            decimal tong1 = 0, tong2 = 0, tong3 = 0, tongPP = 0;
            foreach (var ct in chiTiet)
            {
                stt++;
                var dvt = donViTinhById.TryGetValue(ct.NguyenVatLieuID, out var d) ? d : null;

                ws.Cell(row, 1).Value = stt;
                ws.Cell(row, 2).Value = ct.ThoiGian?.ToString("HH:mm") ?? "";
                ws.Cell(row, 3).Value = ct.TenNVL ?? "";
                ws.Cell(row, 4).Value = dvt ?? "";
                // Cột 5 (Lô/Batch): chưa có dữ liệu trong hệ thống, để trống
                if (ct.Loai1.HasValue) ws.Cell(row, 6).Value = (double)ct.Loai1.Value;
                if (ct.Loai2.HasValue) ws.Cell(row, 7).Value = (double)ct.Loai2.Value;
                if (ct.Loai3.HasValue) ws.Cell(row, 8).Value = (double)ct.Loai3.Value;
                if (ct.PhePham.HasValue) ws.Cell(row, 9).Value = (double)ct.PhePham.Value;
                ws.Cell(row, 10).Value = ct.GhiChu ?? "";

                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                for (int c = 6; c <= 9; c++)
                    ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                tong1 += ct.Loai1 ?? 0;
                tong2 += ct.Loai2 ?? 0;
                tong3 += ct.Loai3 ?? 0;
                tongPP += ct.PhePham ?? 0;
                row++;
            }

            ws.Cell(row, 1).Value = "Tổng";
            ws.Range(row, 1, row, 5).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 6).Value = (double)tong1;
            ws.Cell(row, 7).Value = (double)tong2;
            ws.Cell(row, 8).Value = (double)tong3;
            ws.Cell(row, 9).Value = (double)tongPP;
            for (int c = 6; c <= 9; c++)
            {
                ws.Cell(row, c).Style.Font.Bold = true;
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            }
            for (int c = 1; c <= totalCols; c++)
                ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            row += 2;

            ws.Cell(row, 1).Value = "P.QLCL";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(row, 1, row, 4).Merge();
            ws.Cell(row, 6).Value = "NM.TKVV";
            ws.Cell(row, 6).Style.Font.Bold = true;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(row, 6, row, 9).Merge();
            row += 2;
            ws.Cell(row, 1).Value = nguoiQLCL?.HoVaTen ?? "";
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(row, 1, row, 4).Merge();
            ws.Cell(row, 6).Value = nguoiTKVV?.HoVaTen ?? "";
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(row, 6, row, 9).Merge();

            ws.Column(1).Width = 6;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 22;
            ws.Column(4).Width = 10;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 12;
            ws.Column(7).Width = 12;
            ws.Column(8).Width = 12;
            ws.Column(9).Width = 12;
            ws.Column(10).Width = 22;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"BienBanSanLuong_{phieu.SoPhieu ?? idPhieu.ToString("N")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
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

        // ─── Ghi chi tiết từ JSON của phiếu (hook post-save từ PhieuService) ──

        public async Task<(int ItemCount, int SkippedRows)> InsertFromPhieuJsonAsync(BmPhieu phieu)
        {
            if (phieu == null || string.IsNullOrWhiteSpace(phieu.DataJson))
                return (0, 0);

            try
            {
                using var jsonDoc = JsonDocument.Parse(phieu.DataJson);
                var root = jsonDoc.RootElement;

                var ngayStr = TryGetString(root, "NgaySX", "ngaySX", "ngay");
                var scope = TryGetInt(root, "scope", "Scope");
                var ca = TryGetInt(root, "ca", "Ca");

                if (scope == null || ca == null || string.IsNullOrWhiteSpace(ngayStr))
                    return (0, 0);
                if (!DateTime.TryParse(ngayStr, out var ngay))
                    return (0, 0);
                if (!TryGetArray(root, "table1", out var table1))
                    return (0, 0);

                var items = new List<TKVV_SanLuongChiTiet>();
                int thuTu = 0;
                int skippedRows = 0;

                foreach (var row in table1.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object) continue;
                    thuTu++;

                    var nguyenVatLieuID = TryGetInt(row, "nguyenVatLieuID") ?? 0;
                    if (nguyenVatLieuID == 0)
                    {
                        if (RowHasAnyPhanLoaiValue(row)) skippedRows++;
                        continue;
                    }

                    var thoiGianStr = TryGetString(row, "thoiGian");
                    TimeOnly? thoiGian = TimeOnly.TryParse(thoiGianStr, out var tg) ? tg : null;
                    var ghiChu = TryGetString(row, "ghiChu");

                    var loai1 = TryGetDecimalProperty(row, "1");
                    var loai2 = TryGetDecimalProperty(row, "2");
                    var loai3 = TryGetDecimalProperty(row, "3");
                    var phePham = TryGetDecimalProperty(row, "4");

                    if (loai1 == null && loai2 == null && loai3 == null && phePham == null)
                        continue;

                    items.Add(new TKVV_SanLuongChiTiet
                    {
                        IDPhieu = phieu.Idphieu,
                        Scope = TKVV_BBSLRepository.ResolveScopeCode(scope.Value),
                        Ngay = DateOnly.FromDateTime(ngay),
                        Ca = (byte)ca.Value,
                        NguyenVatLieuID = nguyenVatLieuID,
                        ThuTuDong = thuTu,
                        ThoiGian = thoiGian,
                        Loai1 = loai1,
                        Loai2 = loai2,
                        Loai3 = loai3,
                        PhePham = phePham,
                        IsEdited = true,
                        NguoiSuaID = phieu.NguoiTaoId,
                        NgaySua = DateTime.Now,
                        GhiChu = ghiChu,
                        NgayTao = DateTime.Now,
                    });
                }

                await _repo.ReplaceChiTietAsync(phieu.Idphieu, items);
                return (items.Count, skippedRows);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return (0, 0);
            }
        }

        private static bool RowHasAnyPhanLoaiValue(JsonElement row)
        {
            for (byte phanLoai = 1; phanLoai <= 4; phanLoai++)
                if (row.TryGetProperty(phanLoai.ToString(), out var v) && v.ValueKind != JsonValueKind.Null)
                    return true;
            return false;
        }

        private static decimal? TryGetDecimalProperty(JsonElement row, string propName)
        {
            if (!row.TryGetProperty(propName, out var propVal)) return null;
            if (propVal.ValueKind == JsonValueKind.Null) return null;
            return TryParseDecimalElement(propVal, out var giaTri) ? giaTri : null;
        }

        // ─── JSON helpers ─────────────────────────────────────────────────────

        private static bool TryGetArray(JsonElement obj, string key, out JsonElement array)
        {
            array = default;
            if (obj.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Array)
            {
                array = el;
                return true;
            }
            return false;
        }

        private static string? TryGetString(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (obj.TryGetProperty(key, out var val) && val.ValueKind != JsonValueKind.Null)
                    return val.ValueKind == JsonValueKind.String ? val.GetString() : val.ToString();
            }
            return null;
        }

        private static int? TryGetInt(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!obj.TryGetProperty(key, out var val) || val.ValueKind == JsonValueKind.Null)
                    continue;
                if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var n))
                    return n;
                if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var s))
                    return s;
            }
            return null;
        }

        private static bool TryParseDecimalElement(JsonElement el, out decimal result)
        {
            if (el.ValueKind == JsonValueKind.Number)
                return el.TryGetDecimal(out result);

            if (el.ValueKind == JsonValueKind.String)
                return decimal.TryParse(
                    el.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out result);

            result = 0;
            return false;
        }
    }
}
