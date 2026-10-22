using ClosedXML.Excel;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel.Drawings;
using dataproduct.api.Utils;

namespace dataproduct.api.Services
{
    public class CTDSoTheoDoiService
    {
        private readonly ICtdSoTheoDoiRepository _repo;
        private readonly IPhieuRepository _repoPhieu;
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly PheDuyetService _pheDuyetService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ProductFormContext _context;
        private readonly BmConfigService _bmConfig;

        public CTDSoTheoDoiService(
            ICtdSoTheoDoiRepository repo,
            IPhieuRepository repoPhieu,
            IConverter pdfConverter,
            IWebHostEnvironment env,
            IConfiguration configuration,
            PheDuyetService pheDuyetService,
            IHttpClientFactory httpClientFactory,
            ProductFormContext context,
            BmConfigService bmConfig)
        {
            _repo = repo;
            _repoPhieu = repoPhieu;
            _pdfConverter = pdfConverter;
            _env = env;
            _configuration = configuration;
            _pheDuyetService = pheDuyetService;
            _httpClientFactory = httpClientFactory;
            _context = context;
            _bmConfig = bmConfig;
        }

        public async Task<(int soTheoDoiCount, int dienBienCount)> InsertFromPhieuJsonAsync(BmPhieu phieu)
        {
            if (phieu == null)
                throw new ArgumentNullException(nameof(phieu));

            var soTheoDoiEntities = new List<CtdSoTheoDoi>();
            var dienBienEntities = new List<CtdStdDienBien>();

            if (!string.IsNullOrWhiteSpace(phieu.DataJson))
            {
                using var jsonDoc = JsonDocument.Parse(phieu.DataJson);
                var root = jsonDoc.RootElement;

                if (TryGetSoTheoDoiRows(root, out var soTheoDoiRows))
                {
                    var index = 1;
                    foreach (var row in soTheoDoiRows.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object)
                            continue;

                        soTheoDoiEntities.Add(new CtdSoTheoDoi
                        {
                            Idphieu = phieu.Idphieu,
                            LoaiMacPhoi = TryGetInt(row, "MacPhoiLoai", "LoaiMacPhoi") ?? index,
                            TenMacPhoi = TryGetString(row, "macPhoi", "tenMacPhoi", "MacPhoi"),
                            KichThuoc = TryGetString(row, "kichThuoc", "KichThuoc"),
                            PhoiRaLo = TryGetInt(row, "soPhoiRaKhoiLo", "PhoiRaKhoiLo", "PhoiRaLo"),
                            PhoiHoiLo = TryGetInt(row, "soPhoiHoiLo", "PhoiHoiLo"),
                            PhoiRaSan = TryGetInt(row, "soPhoiCanRaSanPham", "phoiCanRaSan", "PhoiRaSan"),
                            PhoiPheCn = TryGetInt(row, "soPhoiPheCongNghe", "phoiPheCn", "PhoiPheCN"),
                            LoaiPhoi = TryGetInt(row, "loaiPhoi", "LoaiPhoi"),
                            LoaiSp = TryGetString(row, "loaiSanPham", "loaiSp", "LoaiSP"),
                            MacThep = TryGetString(row, "macThep", "macPhoi", "MacThep"),
                            LenhSanXuat = TryGetString(row, "lenhSanXuat", "LenhSanXuat"),
                            IdDonTrongPhoi = TryGetInt(row, "idDonTrongPhoi", "IdDonTrongPhoi")
                        });

                        index++;
                    }
                }

                if (TryGetDienBienRows(root, out var dienBienRows))
                {
                    foreach (var row in dienBienRows.EnumerateArray())
                    {
                        if (row.ValueKind != JsonValueKind.Object)
                            continue;

                        dienBienEntities.Add(new CtdStdDienBien
                        {
                            Idphieu = phieu.Idphieu,
                            TuGio = TryGetTime(row, "tuGio", "TuGio"),
                            DenGio = TryGetTime(row, "denGio", "DenGio"),
                            ThietBi = TryGetString(row, "thietBi", "ThietBi"),
                            MoTa = TryGetString(row, "moTaSuCo", "MoTa", "suCo", "SuCo"),
                            LoaiSuCo = TryGetString(row, "loaiSuCo", "LoaiSuCo"),
                            PheCongNghe = TryGetString(row, "pheCongNghe", "PheCongNghe")
                        });
                    }
                }
            }

            await _repo.DeleteDienBienByPhieuIdAsync(phieu.Idphieu);
            await _repo.DeleteSoTheoDoiByPhieuIdAsync(phieu.Idphieu);

            await _repo.AddSoTheoDoiListAsync(soTheoDoiEntities);
            await _repo.AddDienBienListAsync(dienBienEntities);

            return (soTheoDoiEntities.Count, dienBienEntities.Count);
        }

        public async Task<ExportFileResult> ExportPdfAsync(Guid phieuId)
        {
            var phieu = await _repoPhieu.GetByIdAsync(phieuId);
            if (phieu is null)
                throw new Exception("Không tìm thấy phiếu");

            var soTheoDoiList = await _repo.GetSoTheoDoiByPhieuIdAsync(phieuId);
            var dienBienList = await _repo.GetDienBienByPhieuIdAsync(phieuId);
            var pheDuyets = await _pheDuyetService.GetPheDuyetPhieuAsync(phieuId);

            var ca = phieu.Ca ?? 1;
            var kip = phieu.Kip ?? "";
            var ngaySX = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);

            // Tính ca và ngày
            string tuGio, denGio;
            DateOnly ngayBatDau = ngaySX;
            DateOnly ngayKetThuc = ngaySX;

            switch (ca)
            {
                case 1:
                    tuGio = "08h00";
                    denGio = "20h00";
                    break;
                case 2:
                    tuGio = "20h00";
                    denGio = "08h00";
                    ngayKetThuc = ngaySX.AddDays(1);
                    break;
                default:
                    tuGio = "";
                    denGio = "";
                    break;
            }

            var tuNgay = ngayBatDau.ToString("dd/MM/yyyy");
            var denNgay = ngayKetThuc.ToString("dd/MM/yyyy");
            var kipCa = $"{ca}{kip}";

            // Lấy người ký
            var truongKip = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1);
            var nvVanHanh = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);

            // Logo
            var logoBase64 = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";

            // Chữ ký
            var signTruongKip = await FormatChuKyBase64Async(truongKip?.ChuKy, truongKip?.TinhTrang == 1);
            var signNvVanHanh = await FormatChuKyBase64Async(nvVanHanh?.ChuKy, nvVanHanh?.TinhTrang == 1);

            // Dữ liệu loại I, II, III (hỗ trợ nhiều dòng cho cùng một loại)
            var loaiIList = soTheoDoiList.Where(x => x.LoaiMacPhoi == 1).ToList();
            var loaiIIList = soTheoDoiList.Where(x => x.LoaiMacPhoi == 2).ToList();
            var loaiIIIList = soTheoDoiList.Where(x => x.LoaiMacPhoi == 3).ToList();

            var loaiI = loaiIList.FirstOrDefault() ?? new CtdSoTheoDoi();
            var loaiII = loaiIIList.FirstOrDefault() ?? new CtdSoTheoDoi();
            var loaiIII = loaiIIIList.FirstOrDefault() ?? new CtdSoTheoDoi();

            var loaiIExtraRows = BuildSoTheoDoiExtraRows(loaiIList.Skip(1), "I");
            var loaiIIExtraRows = BuildSoTheoDoiExtraRows(loaiIIList.Skip(1), "II");
            var loaiIIIExtraRows = BuildSoTheoDoiExtraRows(loaiIIIList.Skip(1), "III");

            // Rows diễn biến
            var dienBienRows = new StringBuilder();
            foreach (var row in dienBienList)
            {
                dienBienRows.Append($@"
                <tr>
                    <td>{row.TuGio?.ToString("HH:mm") ?? ""}</td>
                    <td>{row.DenGio?.ToString("HH:mm") ?? ""}</td>
                    <td>{row.ThietBi ?? ""}</td>
                    <td class=""text-left"">{row.MoTa ?? ""}</td>
                    <td>{row.LoaiSuCo ?? ""}</td>
                    <td>{row.PheCongNghe ?? ""}</td>
                </tr>");
            }

            // Đảm bảo có ít nhất 5 dòng trống nếu không có dữ liệu
            if (dienBienList.Count == 0)
            {
                for (int i = 0; i < 5; i++)
                    dienBienRows.Append("<tr><td>&nbsp;</td><td></td><td></td><td></td><td></td><td></td></tr>");
            }

            // Load body template
            var templatePath = Path.Combine(
                _env.WebRootPath,
                "template_html",
                "BM.09-QT.05.13_So_theo_doi_san_xuat_hang_ngay.html"
            );
            var html = await _bmConfig.LoadTemplateAsync(templatePath);

            // Load header template and render dynamic values.
            var headerTemplatePath = Path.Combine(
                _env.WebRootPath,
                "template_html",
                "BM.09-QT.05.13_So_theo_doi_san_xuat_hang_ngay_header.html"
            );
            var headerHtml = await _bmConfig.LoadTemplateAsync(headerTemplatePath);

            headerHtml = headerHtml
                .Replace("{{LogoUrl}}", logoBase64)
                .Replace("{{KipCa}}", kipCa)
                .Replace("{{TuGio}}", tuGio)
                .Replace("{{TuNgay}}", tuNgay)
                .Replace("{{DenGio}}", denGio)
                .Replace("{{DenNgay}}", denNgay);

            html = html
                // Loại I
                .Replace("{{LoaiI_MacPhoi}}", loaiI.TenMacPhoi + (loaiI.LoaiPhoi == 1 ? "- Phôi nóng" : loaiI.LoaiPhoi == 2 ? "- Phôi nguội" : "") ?? "")
                .Replace("{{LoaiI_KichThuoc}}", loaiI.KichThuoc ?? "")
                .Replace("{{LoaiI_PhoiRaLo}}", loaiI.PhoiRaLo?.ToString() ?? "")
                .Replace("{{LoaiI_PhoiHoiLo}}", loaiI.PhoiHoiLo?.ToString() ?? "")
                .Replace("{{LoaiI_PhoiRaSan}}", loaiI.PhoiRaSan?.ToString() ?? "")
                .Replace("{{LoaiI_PhoiPheCn}}", loaiI.PhoiPheCn?.ToString() ?? "")
                .Replace("{{LoaiI_LoaiSp}}", loaiI.LoaiSp ?? "")
                .Replace("{{LoaiI_MacThep}}", loaiI.MacThep ?? "")
                .Replace("{{LoaiI_LenhSanXuat}}", loaiI.LenhSanXuat ?? "")
                .Replace("{{LoaiI_ExtraRows}}", loaiIExtraRows)
                // Loại II
                .Replace("{{LoaiII_MacPhoi}}", loaiII.TenMacPhoi + (loaiII.LoaiPhoi == 1 ? "- Phôi nóng" : loaiII.LoaiPhoi == 2 ? "- Phôi nguội" : "") ?? "")
                .Replace("{{LoaiII_KichThuoc}}", loaiII.KichThuoc ?? "")
                .Replace("{{LoaiII_PhoiRaLo}}", loaiII.PhoiRaLo?.ToString() ?? "")
                .Replace("{{LoaiII_PhoiHoiLo}}", loaiII.PhoiHoiLo?.ToString() ?? "")
                .Replace("{{LoaiII_PhoiRaSan}}", loaiII.PhoiRaSan?.ToString() ?? "")
                .Replace("{{LoaiII_PhoiPheCn}}", loaiII.PhoiPheCn?.ToString() ?? "")
                .Replace("{{LoaiII_LoaiSp}}", loaiII.LoaiSp ?? "")
                .Replace("{{LoaiII_MacThep}}", loaiII.MacThep ?? "")
                .Replace("{{LoaiII_LenhSanXuat}}", loaiII.LenhSanXuat ?? "")
                .Replace("{{LoaiII_ExtraRows}}", loaiIIExtraRows)
                // Loại III
                .Replace("{{LoaiIII_MacPhoi}}", loaiIII.TenMacPhoi + (loaiIII.LoaiPhoi == 1 ? "- Phôi nóng" : loaiIII.LoaiPhoi == 2 ? "- Phôi nguội" : "") ?? "")
                .Replace("{{LoaiIII_KichThuoc}}", loaiIII.KichThuoc ?? "")
                .Replace("{{LoaiIII_PhoiRaLo}}", loaiIII.PhoiRaLo?.ToString() ?? "")
                .Replace("{{LoaiIII_PhoiHoiLo}}", loaiIII.PhoiHoiLo?.ToString() ?? "")
                .Replace("{{LoaiIII_PhoiRaSan}}", loaiIII.PhoiRaSan?.ToString() ?? "")
                .Replace("{{LoaiIII_PhoiPheCn}}", loaiIII.PhoiPheCn?.ToString() ?? "")
                .Replace("{{LoaiIII_LoaiSp}}", loaiIII.LoaiSp ?? "")
                .Replace("{{LoaiIII_MacThep}}", loaiIII.MacThep ?? "")
                .Replace("{{LoaiIII_LenhSanXuat}}", loaiIII.LenhSanXuat ?? "")
                .Replace("{{LoaiIII_ExtraRows}}", loaiIIIExtraRows)
                // Diễn biến
                .Replace("{{DienBienRows}}", dienBienRows.ToString())
                // Chữ ký
                .Replace("{{ChuKyTruongKip}}", signTruongKip)
                .Replace("{{TenTruongKip}}", truongKip?.HoVaTen ?? "")
                .Replace("{{ChuKyNvVanHanh}}", signNvVanHanh)
                .Replace("{{TenNvVanHanh}}", nvVanHanh?.HoVaTen ?? "");

            var tempHeaderPath = Path.Combine(Path.GetTempPath(), $"BM09_header_{Guid.NewGuid():N}.html");
            await File.WriteAllTextAsync(tempHeaderPath, headerHtml, Encoding.UTF8);

            var headerUri = new Uri(tempHeaderPath).AbsoluteUri;

            try
            {
                var doc = new HtmlToPdfDocument
                {
                    GlobalSettings =
                    {
                        PaperSize = PaperKind.A4,
                        Orientation = Orientation.Portrait,
                        Margins = new MarginSettings
                        {
                            Top = 52,
                            Bottom = 15,
                            Left = 15,
                            Right = 15
                        }
                    },
                    Objects =
                    {
                        new ObjectSettings
                        {
                            HtmlContent = html,
                            HeaderSettings = new HeaderSettings
                            {
                                HtmUrl = headerUri,
                                Spacing = 4
                            },
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
                    FileName = $"BM.09-QT.05.13_So_theo_doi_{ngaySX:yyyyMMdd}_Ca{ca}{kip}_{DateTime.Now:HHmmss}.pdf",
                    ContentType = "application/pdf"
                };
            }
            finally
            {
                if (File.Exists(tempHeaderPath))
                    File.Delete(tempHeaderPath);
            }
        }

        public async Task<ExportFileResult> ExportTongHopExcelAsync(DateOnly? fromDate, DateOnly? toDate)
        {
            var maBmList = new[]
            {
                "CTD_STD_Sanxuat", "CTD_SoTheoDoi",
                "BM.09-QT.05.13", "BM.09/QT.05.13",
                "BM09-QT.05.13",  "BM09/QT.05.13"
            };

            var phieuQuery = _context.BmPhieus
                .AsNoTracking()
                .Where(x => x.IsDelete != 1 && x.IsLock != 1 && maBmList.Contains(x.MaBm));

            if (fromDate.HasValue)
                phieuQuery = phieuQuery.Where(x => x.NgaySX >= fromDate.Value);
            if (toDate.HasValue)
                phieuQuery = phieuQuery.Where(x => x.NgaySX <= toDate.Value);

            var phieus = await phieuQuery
                .Select(x => new { x.Idphieu, x.SoPhieu, x.NgaySX, x.Ca, x.Kip, x.TinhTrang, x.Scope })
                .OrderBy(x => x.NgaySX).ThenBy(x => x.Ca).ThenBy(x => x.Kip)
                .ToListAsync();

            var phieuIds = phieus.Select(x => x.Idphieu).ToList();

            var soTheoDoiList = await _context.CtdSoTheoDois
                .AsNoTracking()
                .Where(x => x.Idphieu.HasValue && phieuIds.Contains(x.Idphieu.Value))
                .ToListAsync();

            var dienBienList = await _context.CtdStdDienBiens
                .AsNoTracking()
                .Where(x => x.Idphieu.HasValue && phieuIds.Contains(x.Idphieu.Value))
                .ToListAsync();

            var donTrongIds = soTheoDoiList
                .Where(x => x.IdDonTrongPhoi.HasValue)
                .Select(x => x.IdDonTrongPhoi!.Value)
                .Distinct()
                .ToList();
            var donTrongLookup = donTrongIds.Count > 0
                ? await _context.DonTrongPhois
                    .Where(x => donTrongIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x.MaVatTu)
                : new Dictionary<int, string?>();

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "BM_TongHopSoTheoDoiSanXuat.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu Excel: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);

            // ── Sheet 1: Nguyên liệu & sản phẩm ──────────────────────────────
            var ws1 = workbook.Worksheet(1);
            ws1.Cell("C3").Value = $"Từ ngày: {fromDate:dd/MM/yyyy} đến ngày: {toDate:dd/MM/yyyy}";

            // Thêm cột "Mã vật tư" vào cuối (col 17)
            ws1.Cell(5, 17).CopyFrom(ws1.Cell(5, 16));
            ws1.Cell(5, 17).Value = "Mã vật tư";

            var stdRows = (from p in phieus
                           join d in soTheoDoiList on p.Idphieu equals d.Idphieu
                           orderby p.NgaySX, p.Ca, p.Kip, p.SoPhieu, d.LoaiMacPhoi
                           select new { p, d }).ToList();

            const int startRow1 = 6;
            for (int i = 0; i < stdRows.Count; i++)
            {
                var row = stdRows[i];
                var r = startRow1 + i;
                if (i > 0)
                    ws1.Row(startRow1).CopyTo(ws1.Row(r));

                var maVatTu = row.d.IdDonTrongPhoi.HasValue
                    && donTrongLookup.TryGetValue(row.d.IdDonTrongPhoi.Value, out var mv) ? mv : null;

                ws1.Cell(r, 1).Value = row.p.NgaySX?.ToDateTime(TimeOnly.MinValue);
                ws1.Cell(r, 2).Value = row.p.Ca;
                ws1.Cell(r, 3).Value = row.p.Kip;
                ws1.Cell(r, 4).Value = "Xưởng cán" + row.p.Scope;
                ws1.Cell(r, 5).Value = row.d.TenMacPhoi;
                ws1.Cell(r, 6).Value = row.d.LoaiPhoi switch
                {
                    1 => "Phôi nóng",
                    2 => "Phôi nguội",
                    3 => "Khác",
                    _ => row.d.LoaiPhoi?.ToString()
                };
                ws1.Cell(r, 7).Value = row.d.KichThuoc;
                ws1.Cell(r, 8).Value = row.d.PhoiRaLo;
                ws1.Cell(r, 9).Value = row.d.PhoiHoiLo;
                ws1.Cell(r, 10).Value = row.d.PhoiRaSan;
                ws1.Cell(r, 11).Value = row.d.PhoiPheCn;
                ws1.Cell(r, 12).Value = row.d.LoaiSp;
                ws1.Cell(r, 13).Value = row.d.MacThep;
                ws1.Cell(r, 14).Value = row.d.LenhSanXuat;
                ws1.Cell(r, 15).Value = row.p.SoPhieu;
                ws1.Cell(r, 16).Value = TinhTrangToText(row.p.TinhTrang);
                ws1.Cell(r, 17).Value = maVatTu;        // Mã vật tư (cuối)
            }

            if (stdRows.Count > 0)
                ws1.Range(startRow1, 1, startRow1 + stdRows.Count - 1, 1)
                   .Style.DateFormat.Format = "dd/MM/yyyy";

            // ── Sheet 2: Diễn biến ───────────────────────────────────────────
            if (workbook.Worksheets.Count >= 2)
            {
                var ws2 = workbook.Worksheet(2);
                ws2.Cell("C3").Value = $"Từ ngày: {fromDate:dd/MM/yyyy} đến ngày: {toDate:dd/MM/yyyy}";

                var dbRows = (from p in phieus
                              join d in dienBienList on p.Idphieu equals d.Idphieu
                              orderby p.NgaySX, p.Ca, p.Kip, p.SoPhieu, d.TuGio
                              select new { p, d }).ToList();

                const int startRow2 = 6;
                for (int i = 0; i < dbRows.Count; i++)
                {
                    var row = dbRows[i];
                    var r = startRow2 + i;
                    if (i > 0)
                        ws2.Row(startRow2).CopyTo(ws2.Row(r));

                    ws2.Cell(r, 1).Value = row.p.NgaySX?.ToDateTime(TimeOnly.MinValue);
                    ws2.Cell(r, 2).Value = row.p.Ca;
                    ws2.Cell(r, 3).Value = row.p.Kip;
                    ws2.Cell(r, 4).Value = row.p.SoPhieu;
                    ws2.Cell(r, 5).Value = row.d.TuGio?.ToString("HH:mm");
                    ws2.Cell(r, 6).Value = row.d.DenGio?.ToString("HH:mm");
                    ws2.Cell(r, 7).Value = row.d.ThietBi;
                    ws2.Cell(r, 8).Value = row.d.MoTa;
                    ws2.Cell(r, 9).Value = row.d.LoaiSuCo;
                    ws2.Cell(r, 10).Value = row.d.PheCongNghe;
                }

                if (dbRows.Count > 0)
                    ws2.Range(startRow2, 1, startRow2 + dbRows.Count - 1, 1)
                       .Style.DateFormat.Format = "dd/MM/yyyy";
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return new ExportFileResult
            {
                Content = stream.ToArray(),
                FileName = $"TongHopSoTheoDoiSanXuat_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        public async Task<ExportFileResult> ExportChiTietExcelAsync(Guid phieuId)
        {
            var phieu = await _repoPhieu.GetByIdAsync(phieuId);
            if (phieu is null)
                throw new Exception("Không tìm thấy phiếu");

            var soTheoDoiList = await _repo.GetSoTheoDoiByPhieuIdAsync(phieuId);
            var dienBienList = await _repo.GetDienBienByPhieuIdAsync(phieuId);
            var pheDuyets = await _pheDuyetService.GetPheDuyetPhieuAsync(phieuId);

            var ca = phieu.Ca ?? 1;
            var kip = phieu.Kip ?? "";
            var ngaySX = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);

            string tuGio, denGio;
            var ngayKetThuc = ngaySX;
            switch (ca)
            {
                case 1: tuGio = "08h00"; denGio = "20h00"; break;
                case 2: tuGio = "20h00"; denGio = "08h00"; ngayKetThuc = ngaySX.AddDays(1); break;
                default: tuGio = ""; denGio = ""; break;
            }

            var truongKip = pheDuyets.FirstOrDefault(x => x.CapDuyet == 1);
            var nvVanHanh = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);

            // Logo
            var _logoPath = Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png");
            byte[]? logoBytes = File.Exists(_logoPath) ? await File.ReadAllBytesAsync(_logoPath) : null;

            var htmlPath09 = Path.Combine(_env.WebRootPath, "template_html", "BM.09-QT.05.13_So_theo_doi_san_xuat_hang_ngay.html");
            var bmHeaderText = await HtmlTemplateHelper.GetBmHeaderTextAsync(htmlPath09);

            var loaiIList = soTheoDoiList.Where(x => x.LoaiMacPhoi == 1).ToList();
            var loaiIIList = soTheoDoiList.Where(x => x.LoaiMacPhoi == 2).ToList();
            var loaiIIIList = soTheoDoiList.Where(x => x.LoaiMacPhoi == 3).ToList();

            var loaiI = loaiIList.FirstOrDefault() ?? new CtdSoTheoDoi();
            var loaiII = loaiIIList.FirstOrDefault() ?? new CtdSoTheoDoi();
            var loaiIII = loaiIIIList.FirstOrDefault() ?? new CtdSoTheoDoi();

            var chiTietDonTrongIds = soTheoDoiList
                .Where(x => x.IdDonTrongPhoi.HasValue)
                .Select(x => x.IdDonTrongPhoi!.Value)
                .Distinct()
                .ToList();
            var chiTietDonTrongLookup = chiTietDonTrongIds.Count > 0
                ? await _context.DonTrongPhois
                    .Where(x => chiTietDonTrongIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x.MaVatTu)
                : new Dictionary<int, string?>();

            string? GetMaVatTu(CtdSoTheoDoi d) =>
                d.IdDonTrongPhoi.HasValue && chiTietDonTrongLookup.TryGetValue(d.IdDonTrongPhoi.Value, out var mv) ? mv : null;

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Sổ theo dõi");

            // Toàn bộ 1 sheet, 12 cột:
            // Loại I   : col 1-4  (label 1-3, value 4)
            // Loại II  : col 5-8  (label 5-7, value 8)
            // Loại III : col 9-12 (label 9-11, value 12)
            // Diễn biến: Từ(1) Đến(2) Thiết bị(3-4) Mô tả(5-8) Loại SC(9-10) Phế CN(11-12)
            const int COLS = 12;

            ws.Style.Font.FontName = "Times New Roman";
            ws.Style.Font.FontSize = 10;

            ws.Column(1).Width = 8;
            ws.Column(2).Width = 8;
            ws.Column(3).Width = 20;
            ws.Column(4).Width = 8;
            ws.Column(5).Width = 8;
            ws.Column(6).Width = 8;
            ws.Column(7).Width = 20;
            ws.Column(8).Width = 8;
            ws.Column(9).Width = 8;
            ws.Column(10).Width = 8;
            ws.Column(11).Width = 20;
            ws.Column(12).Width = 8;

            int row = 1;

            // ── HEADER: như HTML template ─────────────────────────────────────
            // Row 1: Logo (trái) | BM code (phải, merge 2 rows)
            // Row 2: Tên công ty (trái) |  "  "
            ws.Row(row).Height = 38;

            if (logoBytes != null)
            {
                var fmt = XLPictureFormat.Png;
                using var logoMs = new MemoryStream(logoBytes);
                ws.AddPicture(logoMs, fmt)
                    .MoveTo(ws.Cell(row, 1))
                    .WithSize(100, 30);
            }

            // BM code — bên phải, merge cả 2 rows header
            ws.Cell(row, 9).Value = bmHeaderText;
            ws.Range(row, 9, row + 1, COLS).Merge().Style
                .Font.SetFontSize(10)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText(true);

            row++;

            // Row 2: Tên công ty bên dưới logo (giống HTML: <img/><br/>CÔNG TY...)
            ws.Row(row).Height = 18;
            ws.Cell(row, 1).Value = "CÔNG TY CỔ PHẦN THÉP  \n  HÒA PHÁT DUNG QUẤT";
            ws.Range(row, 1, row, 7).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(10)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText(true);

            row++;

            // ── TITLES ────────────────────────────────────────────────────────
            ws.Cell(row, 1).Value = "SỔ THEO DÕI";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(15)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 22;
            row++;

            ws.Cell(row, 1).Value = "SẢN XUẤT HÀNG NGÀY";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(15)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 22;
            row++;

            ws.Cell(row, 1).Value = $"Kíp {ca}{kip} từ {tuGio} ngày {ngaySX:dd/MM/yyyy} đến {denGio} ngày {ngayKetThuc:dd/MM/yyyy}";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetItalic(true).Font.SetFontSize(10)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 15;
            row++;

            ws.Cell(row, 1).Value = "(Do nhân viên vận hành pupit 1 theo dõi)";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetItalic(true).Font.SetFontSize(10)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(row).Height = 14;
            row++;

            // ── SECTION 1: 3 cột song song Loại I / II / III ─────────────────
            ws.Cell(row, 1).Value = "1. Nguyên liệu và sản phẩm:";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(11);
            ws.Row(row).Height = 16;
            row++;

            // Ghi từng loại vào nhóm 4 cột: label(c1-c3 merged) | value(c4)
            void WriteLoaiBlock(int startRow, int c1, CtdSoTheoDoi d, string titleLabel, string? maVatTu = null)
            {
                int c3 = c1 + 2, c4 = c1 + 3;
                var loaiPhoi = d.LoaiPhoi switch { 1 => " - Phôi nóng", 2 => " - Phôi nguội", 3 => " - Khác", _ => "" };

                // Title row (bold, full 4 cols)
                ws.Cell(startRow, c1).Value = $"{titleLabel}: {d.TenMacPhoi}{loaiPhoi}";
                ws.Range(startRow, c1, startRow, c4).Merge().Style
                    .Font.SetBold(true).Alignment.SetWrapText(true);

                // Info rows: label (c1-c3) | value (c4, bold where required)
                (string Label, string? Value, bool BoldValue)[] fields =
                [
                    ("- Kích thước:",                  d.KichThuoc is not null ? d.KichThuoc + " mm" : "", false),
                    ("- Mã vật tư:",                   maVatTu ?? "",                                       false),
                    ("- Số phôi ra khỏi lò:",          d.PhoiRaLo?.ToString(),   true),
                    ("- Số phôi hồi lò:",              d.PhoiHoiLo?.ToString(),  true),
                    ("- Số phôi cần ra sản (T/P):",    d.PhoiRaSan?.ToString(),  false),
                    ("- Số phôi phế công nghệ:",       d.PhoiPheCn?.ToString(),  true),
                    ($"- Loại sản phẩm: {d.LoaiSp ?? ""}  Mác thép: {d.MacThep ?? ""}", null, false),
                    ($"- Lệnh sản xuất: {d.LenhSanXuat ?? ""}",                   null, false),
                ];

                for (int i = 0; i < fields.Length; i++)
                {
                    var r = startRow + 1 + i;
                    var (label, value, boldVal) = fields[i];

                    if (value is null)
                    {
                        ws.Cell(r, c1).Value = label;
                        ws.Range(r, c1, r, c4).Merge();
                    }
                    else
                    {
                        ws.Cell(r, c1).Value = label;
                        ws.Range(r, c1, r, c3).Merge();
                        ws.Cell(r, c4).Value = value;
                        ws.Cell(r, c4).Style
                            .Font.SetBold(boldVal)
                            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                    }
                }
            }

            const int LOAI_ROWS = 9; // 1 title + 8 field rows (kichThuoc, maVatTu, 6 số liệu)
            int secStart = row;

            WriteLoaiBlock(secStart, 1, loaiI, "a. Mác phôi loại I", GetMaVatTu(loaiI));
            WriteLoaiBlock(secStart, 5, loaiII, "b. Mác phôi loại II", GetMaVatTu(loaiII));
            WriteLoaiBlock(secStart, 9, loaiIII, "c. Mác phôi loại III", GetMaVatTu(loaiIII));

            for (int r = secStart; r < secStart + LOAI_ROWS; r++)
                ws.Row(r).Height = 15;

            // Thêm extra items (loại có nhiều hơn 1 dòng) phía dưới section
            int extraStart = secStart + LOAI_ROWS;
            int maxExtra = Math.Max(Math.Max(loaiIList.Count - 1, loaiIIList.Count - 1), loaiIIIList.Count - 1);
            for (int ei = 0; ei < maxExtra; ei++)
            {
                var eI = loaiIList.Count > ei + 1 ? loaiIList[ei + 1] : null;
                var eII = loaiIIList.Count > ei + 1 ? loaiIIList[ei + 1] : null;
                var eIII = loaiIIIList.Count > ei + 1 ? loaiIIIList[ei + 1] : null;

                if (eI is not null) WriteLoaiBlock(extraStart, 1, eI, "a. (tiếp theo)", GetMaVatTu(eI));
                if (eII is not null) WriteLoaiBlock(extraStart, 5, eII, "b. (tiếp theo)", GetMaVatTu(eII));
                if (eIII is not null) WriteLoaiBlock(extraStart, 9, eIII, "c. (tiếp theo)", GetMaVatTu(eIII));

                for (int r = extraStart; r < extraStart + LOAI_ROWS; r++)
                    ws.Row(r).Height = 15;

                extraStart += LOAI_ROWS;
            }

            row = extraStart;

            // ── SECTION 2: DIỄN BIẾN (bảng có merged headers) ────────────────
            // "2. Diễn biến" — section title row
            ws.Cell(row, 1).Value = "2. Diễn biến";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(11)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#f0f0f0"))
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            ws.Row(row).Height = 17;
            row++;

            // Header row 1:
            //   col 1-2  : "Thời gian đình trễ"
            //   col 3-4  : "Thiết bị"      (rowspan 2 → merge với row+1)
            //   col 5-8  : "Mô tả sự cố/dừng sản xuất" (rowspan 2)
            //   col 9-10 : "Loại sự cố"    (rowspan 2)
            //   col 11-12: "Phế công nghệ" (rowspan 2)
            int dbH = row;

            ws.Cell(dbH, 1).Value = "Thời gian đình trễ";
            ws.Range(dbH, 1, dbH, 2).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(dbH, 3).Value = "Thiết bị";
            ws.Range(dbH, 3, dbH + 1, 4).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(dbH, 5).Value = "Mô tả sự cố/\ndừng sản xuất";
            ws.Range(dbH, 5, dbH + 1, 8).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetWrapText(true)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(dbH, 9).Value = "Loại sự cố";
            ws.Range(dbH, 9, dbH + 1, 10).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(dbH, 11).Value = "Phế công nghệ";
            ws.Range(dbH, 11, dbH + 1, COLS).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Row(dbH).Height = 18;
            row++;

            // Header row 2: chỉ "Từ giờ" và "Đến giờ" (các cột còn lại đã merge lên trên)
            ws.Cell(row, 1).Value = "Từ giờ";
            ws.Cell(row, 1).Style.Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Cell(row, 2).Value = "Đến giờ";
            ws.Cell(row, 2).Style.Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                .Border.SetOutsideBorder(XLBorderStyleValues.Thin);

            ws.Row(row).Height = 18;
            row++;

            // Data rows (tối thiểu 5 dòng như HTML template)
            var dbItems = dienBienList.OrderBy(x => x.TuGio).ToList();
            int dbCount = Math.Max(dbItems.Count, 5);
            for (int i = 0; i < dbCount; i++)
            {
                var db = i < dbItems.Count ? dbItems[i] : null;

                ws.Cell(row, 1).Value = db?.TuGio?.ToString("HH:mm") ?? "";
                ws.Cell(row, 2).Value = db?.DenGio?.ToString("HH:mm") ?? "";

                ws.Range(row, 3, row, 4).Merge();
                ws.Cell(row, 3).Value = db?.ThietBi ?? "";

                ws.Range(row, 5, row, 8).Merge();
                ws.Cell(row, 5).Value = db?.MoTa ?? "";
                ws.Cell(row, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);

                ws.Range(row, 9, row, 10).Merge();
                ws.Cell(row, 9).Value = db?.LoaiSuCo ?? "";

                ws.Range(row, 11, row, COLS).Merge();
                ws.Cell(row, 11).Value = db?.PheCongNghe ?? "";

                ws.Range(row, 1, row, COLS).Style
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                    .Border.SetInsideBorder(XLBorderStyleValues.Thin);

                ws.Row(row).Height = 18;
                row++;
            }

            // ── GHI CHÚ ──────────────────────────────────────────────────────
            row++;
            ws.Cell(row, 1).Value = "Ghi chú: - Loại sự cố ghi rõ: Cơ khí/điện/công nghệ/nguyên liệu/con người hoặc khách quan.";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetItalic(true).Font.SetFontSize(9);
            ws.Row(row).Height = 13;
            row++;

            ws.Cell(row, 1).Value = "           - Phế công nghệ: Khi xảy ra sự cố ở vị trí nào thì ghi cụ thể bao nhiêu cây thép trong lần sự cố đó.";
            ws.Range(row, 1, row, COLS).Merge().Style
                .Font.SetItalic(true).Font.SetFontSize(9);
            ws.Row(row).Height = 13;
            row += 2;

            // ── CHỮ KÝ ───────────────────────────────────────────────────────
            ws.Cell(row, 2).Value = "Trưởng kíp";
            ws.Range(row, 2, row, 5).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(row, 8).Value = "NV vận hành";
            ws.Range(row, 8, row, 11).Merge().Style
                .Font.SetBold(true)
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Row(row).Height = 16;
            row++;

            ws.Row(row).Height = 60; // vùng chữ ký
            row++;

            ws.Cell(row, 2).Value = truongKip?.HoVaTen ?? "";
            ws.Range(row, 2, row, 5).Merge().Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(row, 8).Value = nvVanHanh?.HoVaTen ?? "";
            ws.Range(row, 8, row, 11).Merge().Style
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            // Page setup A4 portrait
            ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.Margins.Left = 0.6;
            ws.PageSetup.Margins.Right = 0.6;
            ws.PageSetup.Margins.Top = 0.8;
            ws.PageSetup.Margins.Bottom = 0.6;

            using var stream = new MemoryStream();
            wb.SaveAs(stream);

            return new ExportFileResult
            {
                Content = stream.ToArray(),
                FileName = $"SoTheoDoiSanXuat_{ngaySX:yyyyMMdd}_Ca{ca}{kip}_{DateTime.Now:HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        private static string TinhTrangToText(int? tinhTrang) => tinhTrang switch
        {
            0 => "Đang lưu",
            1 => "Đã gửi",
            2 => "Hoàn thành",
            3 => "Đã thu hồi",
            4 => "Không xác nhận",
            5 => "Đã chốt",
            6 => "Đang phê duyệt",
            7 => "Hiệu chỉnh",
            _ => "Không xác định"
        };

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
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting image URL to base64: {imageUrl} - {ex.Message}");
                return "";
            }
        }

        private async Task<string> FormatChuKyBase64Async(string? chuKy, bool daKy = false)
        {
            if (string.IsNullOrWhiteSpace(chuKy))
            {
                if (daKy)
                {
                    return @"
                        <div style='text-align:center'>
                            <div style='font-style:italic;color:red'>Đã ký</div>
                            <div style='font-size:11px;color:red'>(Chưa cập nhật chữ ký)</div>
                        </div>";
                }
                return "";
            }

            if (chuKy.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return $"<img src=\"{chuKy}\" style=\"max-width:150px;max-height:80px;\" />";

            if (chuKy.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                var base64 = await ConvertImageUrlToBase64Async(chuKy);
                if (!string.IsNullOrEmpty(base64))
                    return $"<img src=\"{base64}\" style=\"max-width:150px;max-height:80px;\" />";
            }
            else if (chuKy.StartsWith('/'))
            {
                var domain = _configuration.GetValue<string>("AppSettings:Domain") ?? "https://report.hoaphatdungquat.vn";
                var fullUrl = domain.TrimEnd('/') + chuKy;
                var base64 = await ConvertImageUrlToBase64Async(fullUrl);
                if (!string.IsNullOrEmpty(base64))
                    return $"<img src=\"{base64}\" style=\"max-width:150px;max-height:80px;\" />";
            }

            return @"
                    <div style='text-align:center'>
                        <div style='font-style:italic;color:red'>Đã ký</div>
                        <div style='font-size:11px;color:red'>(Chưa cập nhật chữ ký)</div>
                    </div>";
        }

        private bool TryGetSoTheoDoiRows(JsonElement root, out JsonElement rows)
        {
            rows = default;
            return TryGetArray(root, "table1", out rows)
                || TryGetArray(root, "soTheoDoiRows", out rows)
                || TryGetArray(root, "soTheoDoi", out rows)
                || TryGetArray(root, "nguyenLieuSanPham", out rows)
                || TryGetArray(root, "section1", out rows);
        }

        private static string BuildSoTheoDoiExtraRows(IEnumerable<CtdSoTheoDoi> rows, string loai)
        {
            var sb = new StringBuilder();
            var index = 2;

            foreach (var row in rows)
            {
                sb.Append($@"
                    <div class=""item-block extra-item"">
                        <div class=""info-row""><b>- Mác phôi loại {Encode(loai)} :</b> {Encode(row.TenMacPhoi)} {Encode(row.LoaiPhoi == 1 ? " - Phôi nóng" : row.LoaiPhoi == 2 ? " - Phôi nguội" : "")}</div>
                        <div class=""info-row"">- Kích thước: {Encode(row.KichThuoc)} mm</div>
                        <div class=""info-row"">- Số phôi ra khỏi lò: &nbsp;<b>{Encode(row.PhoiRaLo)}</b></div>
                        <div class=""info-row"">- Số phôi hồi lò: &nbsp;<b>{Encode(row.PhoiHoiLo)}</b></div>
                        <div class=""info-row"">- Số phôi cần ra sản (T/P): {Encode(row.PhoiRaSan)}</div>
                        <div class=""info-row"">- Số phôi phế công nghệ: &nbsp;<b>{Encode(row.PhoiPheCn)}</b></div>
                        <div class=""info-row"">- Loại sản phẩm: <b>{Encode(row.LoaiSp)}</b> Mác thép: <b>{Encode(row.MacThep)}</b></div>
                        <div class=""info-row"">- Lệnh sản xuất: <b>{Encode(row.LenhSanXuat)}</b></div>
                    </div>");

                index++;
            }

            return sb.ToString();
        }

        private static string Encode(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? "");

        private bool TryGetDienBienRows(JsonElement root, out JsonElement rows)
        {
            rows = default;
            return TryGetArray(root, "table2", out rows)
                || TryGetArray(root, "dienBienRows", out rows)
                || TryGetArray(root, "dienBien", out rows)
                || TryGetArray(root, "section2", out rows);
        }

        private bool TryGetArray(JsonElement obj, string key, out JsonElement array)
        {
            array = default;
            if (obj.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.Array)
            {
                array = element;
                return true;
            }

            return false;
        }

        private string? TryGetString(JsonElement row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (row.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    if (value.ValueKind == JsonValueKind.String)
                        return value.GetString();

                    return value.ToString();
                }
            }

            return null;
        }

        private int? TryGetInt(JsonElement row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (row.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
                        return n;

                    if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
                        return parsed;
                }
            }

            return null;
        }

        private TimeOnly? TryGetTime(JsonElement row, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!row.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null)
                    continue;

                if (value.ValueKind == JsonValueKind.String)
                {
                    var raw = value.GetString();
                    if (string.IsNullOrWhiteSpace(raw))
                        continue;

                    var normalized = raw.Replace("h", ":", StringComparison.OrdinalIgnoreCase).Trim();

                    if (TimeOnly.TryParseExact(normalized, new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                        return t;

                    if (DateTime.TryParse(raw, out var dt))
                        return TimeOnly.FromDateTime(dt);
                }
            }

            return null;
        }
    }
}
