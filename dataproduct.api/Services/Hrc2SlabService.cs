using ClosedXML.Excel;
using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace dataproduct.api.Services
{
    public class Hrc2SlabService
    {
        private readonly IHrc2SlabRepository _repo;
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterCtx;
        private readonly IWebHostEnvironment _env;
        private readonly IConverter _pdfConverter;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BmConfigService _bmConfig;

        public Hrc2SlabService(
            IHrc2SlabRepository repo,
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
            _bmConfig = bmConfig;
        }

        public Task<(IEnumerable<Hrc2SlabItem> Data, int TotalCount)> SearchAsync(Hrc2SlabSearchRequest req)
            => _repo.SearchAsync(req);

        public Task<IEnumerable<Hrc2SlabTongHopItem>> GetTongHopAsync(
            string? tuNgay, string? denNgay, string? ca, string? kip)
            => _repo.GetTongHopAsync(tuNgay, denNgay, ca, kip);

        public Task<IEnumerable<Hrc2PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca, string? tuNgay = null, string? denNgay = null)
            => _repo.GetPhieuBBSLAsync(kip, ca, tuNgay, denNgay);

        public Task<IEnumerable<Hrc2SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu)
            => _repo.GetRuotPhieuAsync(idPhieu);

        public Task<IEnumerable<Hrc2SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu, int? currentUserId = null)
            => _repo.GetSlabsByPhieuAsync(idPhieu, currentUserId);

        public Task<IEnumerable<Hrc2ThongKeSlabRow>> GetThongKeSlabAsync(Hrc2ThongKeSlabRequest req)
            => _repo.GetThongKeSlabAsync(req);

        public Task CheckAsync(Hrc2SlabCheckRequest req)
            => _repo.CheckAsync(req.IdSlabs, req.NguoiThucHien);

        public Task UnCheckAsync(Hrc2SlabCheckRequest req)
            => _repo.UnCheckAsync(req.IdSlabs, req.NguoiThucHien);

        public Task XacNhanAsync(Hrc2XacNhanRequest req)
            => _repo.XacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task HuyXacNhanAsync(Hrc2XacNhanRequest req)
            => _repo.HuyXacNhanAsync(req.IdSlabs, req.LoaiXacNhan, req.NguoiThucHien);

        public Task ChotPhieuAsync(Hrc2ChotPhieuRequest req)
            => _repo.ChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public Task HuyChotPhieuAsync(Hrc2ChotPhieuRequest req)
            => _repo.HuyChotPhieuAsync(req.IdPhieu, req.NguoiThucHien);

        public async Task<int> ChuyenBbslAsync(Hrc2ChuyenBbslRequest req)
            => await _repo.ChuyenBbslAsync(req.IdSlabs, req.IdPhieu, req.NguoiThucHien, req.ThoiDiemThaoTac);

        public async Task<int> ThuHoiAsync(Hrc2ChuyenBbslRequest req)
            => await _repo.ThuHoiAsync(req.IdSlabs, req.NguoiThucHien);

        // Sync status từ BK_SyncHRC2SlabControl
        public async Task<object?> GetSyncStatusAsync()
        {
            var latest = await _context.BkSyncHrc2SlabControls
                .AsNoTracking()
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (latest == null) return null;

            return new
            {
                id          = latest.Id,
                trangThai   = latest.TrangThai,
                batDauLuc   = latest.BatDauLuc,
                ketThucLuc  = latest.KetThucLuc,
                ghiChu      = latest.GhiChu,
            };
        }
        public Task<SyncStatusItem> SyncAsync(DateOnly? ngayBatDau, DateOnly? ngayKetThuc)
            => _repo.SyncAsync(ngayBatDau, ngayKetThuc);

        // ── Chi tiết Excel (BBGN phôi tấm HRC2) ─────────────────────────────

        public async Task<ExportFileResult> ExportChiTietExcelAsync(Guid idPhieu, int? currentUserId = null)
        {
            var phieu = await GetPhieuAsync(idPhieu);
            var (soPhieu, ngaySX, _, _) = ExtractPhieuInfo(phieu);

            // "Ca BBSL" = Ca + Ngày SX của chính phiếu BBSL đang chứa các slab này (khác "Ca SX" của slab).
            var caBbsl = phieu?.Ca.HasValue == true && phieu.NgaySX.HasValue
                ? $"Ca {phieu.Ca}{phieu.Kip} - {phieu.NgaySX:dd/MM/yyyy}"
                : "";

            var trangThais = await GetTrangThaisAsync(idPhieu);

            // "Đã check" chỉ tính riêng cho user hiện tại — độc lập với workflow xác nhận ở trên.
            HashSet<int> checkedSlabIds = [];
            if (currentUserId.HasValue && trangThais.Count > 0)
            {
                var slabIds = trangThais.Select(t => t.IdSlab).ToList();
                checkedSlabIds = (await _context.BkHrc2Slab_UserChecks
                    .AsNoTracking()
                    .Where(c => c.IdUser == currentUserId.Value && slabIds.Contains(c.IdSlab))
                    .Select(c => c.IdSlab)
                    .ToListAsync())
                    .ToHashSet();
            }

            var templatePath = Path.Combine(_env.WebRootPath, "templates", "HRC2_BBSL_PhoiTam.xlsx");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu: {templatePath}");

            using var workbook = new XLWorkbook(templatePath);
            var ws = workbook.Worksheet(1);

            const int startRow = 6;
            const int checkCol = 18;
            var rowIndex = startRow;
            var stt = 1;

            void SetTrangThaiCell(int r, int c, int trangThai)
            {
                var cell = ws.Cell(r, c);
                cell.Value = trangThai == 1 ? "Đã xác nhận" : "Chưa xác nhận";
                cell.Style.Fill.BackgroundColor = trangThai == 1
                    ? XLColor.FromHtml("#C6EFCE")
                    : XLColor.FromHtml("#D9D9D9");
            }

            void SetCheckCell(int r, int c, bool daCheck)
            {
                var cell = ws.Cell(r, c);
                cell.Value = daCheck ? "Đã check" : "";
                if (daCheck)
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC000");
            }

            // Header cột mới — copy style từ header "TT PKH" (cột 17) để đồng bộ giao diện với template có sẵn.
            var headerRow = startRow - 1;
            var checkHeaderCell = ws.Cell(headerRow, checkCol);
            checkHeaderCell.Value = "Đã check";
            checkHeaderCell.Style = ws.Cell(headerRow, 17).Style;
            ws.Column(checkCol).Width = 14;

            foreach (var tt in trangThais)
            {
                var slab = tt.Slab;
                var kt = (slab.ChieuDay != null && slab.ChieuRong != null && slab.ChieuDai != null)
                    ? $"{FmtKichThuoc(slab.ChieuDay.Value)}x{FmtKichThuoc(slab.ChieuRong.Value)}x{FmtKichThuoc(slab.ChieuDai.Value)}"
                    : "";

                // Phôi nguội/nóng xác định theo tiền tố OrderId ("203" = nguội, khác = nóng) — quy ước riêng
                // cho 2 cột này, khác GetPivotKeys() vốn dựa trên LoaiPhoi cho báo cáo tổng hợp.
                var kl = slab.KhoiLuong.HasValue ? (double)slab.KhoiLuong.Value : 0;
                var laPhoiNguoi = (slab.OrderId ?? "").StartsWith("203");

                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = caBbsl;
                ws.Cell(rowIndex, 3).Value = slab.ShiftName ?? "";
                ws.Cell(rowIndex, 4).Value = laPhoiNguoi ? kl : 0;
                ws.Cell(rowIndex, 5).Value = laPhoiNguoi ? 0 : kl;
                ws.Cell(rowIndex, 6).Value = slab.IdSlab ?? "";
                ws.Cell(rowIndex, 7).Value = slab.OrderId ?? "";
                ws.Cell(rowIndex, 8).Value = slab.MeThep ?? "";
                ws.Cell(rowIndex, 9).Value = slab.MayDuc.HasValue ? $"Máy {slab.MayDuc}" : "";
                ws.Cell(rowIndex, 10).Value = kt;
                ws.Cell(rowIndex, 11).Value = slab.MacThep ?? "";
                ws.Cell(rowIndex, 12).Value = kl;
                ws.Cell(rowIndex, 13).Value = slab.ChatLuong ?? "";
                ws.Cell(rowIndex, 14).Value = GetPhanLoaiLabel(slab.PhanLoai);
                SetTrangThaiCell(rowIndex, 15, tt.TrangThaiDuc);
                SetTrangThaiCell(rowIndex, 16, tt.TrangThaiKho);
                SetTrangThaiCell(rowIndex, 17, tt.TrangThaiPKH);
                SetCheckCell(rowIndex, checkCol, checkedSlabIds.Contains(tt.IdSlab));

                rowIndex++;
                stt++;
            }

            if (rowIndex > startRow)
                SetThinBorders(ws, startRow, rowIndex - 1, checkCol);

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;

            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = $"HRC2_BBSL_PhoiTam_{soPhieu}_{ngaySX.Replace("/", "")}_{DateTime.Now:HHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        // ── Tổng hợp Excel (BBXNSL phôi tấm HRC2) ───────────────────────────
        // Layout ISO cố định của biểu mẫu BM.36/QT.05.15 (27 cột leaf), KHÔNG được đổi cấu trúc dù
        // dữ liệu "Phôi nóng" hiện chưa có (LoaiPhoi trong DB hiện luôn là "Phôi nguội") — các cột
        // nóng vẫn phải tồn tại và chỉ render trống/0 khi không có dữ liệu (SetSo/SetKl tự Clear khi
        // v<=0), không được bỏ cột. Xem GetPivotKeys() để biết cách phân loại nguội/nóng × loại.
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

            void SetSo(int r, int c, int v)
            {
                if (v > 0) ws.Cell(r, c).Value = v;
                else ws.Cell(r, c).Clear(XLClearOptions.Contents);
            }

            void SetKl(int r, int c, decimal v)
            {
                if (v > 0) ws.Cell(r, c).Value = (double)v;
                else ws.Cell(r, c).Clear(XLClearOptions.Contents);
            }

            foreach (var row in pivotRows)
            {
                if (rowIndex > startRow)
                    ws.Row(startRow).CopyTo(ws.Row(rowIndex));

                ws.Cell(rowIndex, 1).Value = stt;
                ws.Cell(rowIndex, 2).Value = row.ShiftName;
                ws.Cell(rowIndex, 3).Value = row.MacThep;
                ws.Cell(rowIndex, 4).Value = row.MeThep;
                ws.Cell(rowIndex, 5).Value = row.KichThuoc;
                SetSo(rowIndex, 6,  row.NguoiLoai1So);   SetKl(rowIndex, 7,  row.NguoiLoai1Kl);
                SetSo(rowIndex, 8,  row.NguoiLoai2So);   SetKl(rowIndex, 9,  row.NguoiLoai2Kl);
                SetSo(rowIndex, 10, row.NguoiLoai2TpSo); SetKl(rowIndex, 11, row.NguoiLoai2TpKl);
                SetSo(rowIndex, 12, row.NguoiLoai3So);   SetKl(rowIndex, 13, row.NguoiLoai3Kl);
                SetSo(rowIndex, 14, row.NguoiLoai3TpSo); SetKl(rowIndex, 15, row.NguoiLoai3TpKl);
                SetSo(rowIndex, 16, row.NguoiNganDaiSo); SetKl(rowIndex, 17, row.NguoiNganDaiKl);
                SetSo(rowIndex, 18, row.NongLoai1So);    SetKl(rowIndex, 19, row.NongLoai1Kl);
                SetSo(rowIndex, 20, row.NongLoai2So);    SetKl(rowIndex, 21, row.NongLoai2Kl);
                SetSo(rowIndex, 22, row.NongLoai2TpSo);  SetKl(rowIndex, 23, row.NongLoai2TpKl);
                SetSo(rowIndex, 24, row.NongLoai3TpSo);  SetKl(rowIndex, 25, row.NongLoai3TpKl);
                SetSo(rowIndex, 26, row.TongSoPhoi);     SetKl(rowIndex, 27, row.TongKhoiLuong);

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
                SetSo(rowIndex, 6,  pivotRows.Sum(r => r.NguoiLoai1So));   SetKl(rowIndex, 7,  pivotRows.Sum(r => r.NguoiLoai1Kl));
                SetSo(rowIndex, 8,  pivotRows.Sum(r => r.NguoiLoai2So));   SetKl(rowIndex, 9,  pivotRows.Sum(r => r.NguoiLoai2Kl));
                SetSo(rowIndex, 10, pivotRows.Sum(r => r.NguoiLoai2TpSo)); SetKl(rowIndex, 11, pivotRows.Sum(r => r.NguoiLoai2TpKl));
                SetSo(rowIndex, 12, pivotRows.Sum(r => r.NguoiLoai3So));   SetKl(rowIndex, 13, pivotRows.Sum(r => r.NguoiLoai3Kl));
                SetSo(rowIndex, 14, pivotRows.Sum(r => r.NguoiLoai3TpSo)); SetKl(rowIndex, 15, pivotRows.Sum(r => r.NguoiLoai3TpKl));
                SetSo(rowIndex, 16, pivotRows.Sum(r => r.NguoiNganDaiSo)); SetKl(rowIndex, 17, pivotRows.Sum(r => r.NguoiNganDaiKl));
                SetSo(rowIndex, 18, pivotRows.Sum(r => r.NongLoai1So));    SetKl(rowIndex, 19, pivotRows.Sum(r => r.NongLoai1Kl));
                SetSo(rowIndex, 20, pivotRows.Sum(r => r.NongLoai2So));    SetKl(rowIndex, 21, pivotRows.Sum(r => r.NongLoai2Kl));
                SetSo(rowIndex, 22, pivotRows.Sum(r => r.NongLoai2TpSo));  SetKl(rowIndex, 23, pivotRows.Sum(r => r.NongLoai2TpKl));
                SetSo(rowIndex, 24, pivotRows.Sum(r => r.NongLoai3TpSo));  SetKl(rowIndex, 25, pivotRows.Sum(r => r.NongLoai3TpKl));
                SetSo(rowIndex, 26, pivotRows.Sum(r => r.TongSoPhoi));     SetKl(rowIndex, 27, pivotRows.Sum(r => r.TongKhoiLuong));
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
            static string NKl(decimal v, System.Globalization.CultureInfo c) => v > 0 ? v.ToString("N0", c) : "";

            void Td2(int so, decimal kl)
            {
                rowsHtml.Append($"<td class=\"num\">{N0(so, vi)}</td><td class=\"num\">{NKl(kl, vi)}</td>");
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
                rowsHtml.Append($"<td class=\"num\"><strong>{NKl(r.TongKhoiLuong, vi)}</strong></td>");
                rowsHtml.Append("</tr>");
            }

            // Total row
            void TdTotal2(Func<TongHopPivotRow, int> soFn, Func<TongHopPivotRow, decimal> klFn)
            {
                rowsHtml.Append($"<td class=\"num\">{N0(pivotRows.Sum(soFn), vi)}</td><td class=\"num\">{NKl(pivotRows.Sum(klFn), vi)}</td>");
            }

            rowsHtml.Append("<tr class=\"total-row\">");
            rowsHtml.Append("<td colspan=\"5\" style=\"text-align:center\"><strong>Tổng cộng: </strong></td>");
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
            rowsHtml.Append($"<td class=\"num\"><strong>{NKl(pivotRows.Sum(r => r.TongKhoiLuong), vi)}</strong></td>");
            rowsHtml.Append("</tr>");

            var (ducUserId, khoUserId, qlclUserId) = await GetPhieuSignersAsync(idPhieu);
            var signerIds = new[] { ducUserId, khoUserId, qlclUserId }
                .Where(id => id != null).Select(id => id!.Value).Distinct().ToList();
            var userMap = signerIds.Count > 0
                ? await _masterCtx.Tbl_TaiKhoan
                    .Include(t => t.ViTri)
                    .Include(t => t.PhongBan)
                    .Where(t => signerIds.Contains(t.ID_TaiKhoan))
                    .ToDictionaryAsync(t => t.ID_TaiKhoan)
                : new Dictionary<int, TaiKhoan>();

            var (ducSigImg, ducSigTen) = await BuildSigPartsAsync(ducUserId, userMap);
            var (khoSigImg, khoSigTen) = await BuildSigPartsAsync(khoUserId, userMap);
            var (qlclSigImg, qlclSigTen) = await BuildSigPartsAsync(qlclUserId, userMap);

            var thanhPhanHtml = string.Concat(
                BuildThanhPhanLine(1, qlclUserId, userMap),
                BuildThanhPhanLine(2, ducUserId, userMap),
                BuildThanhPhanLine(3, khoUserId, userMap));

            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "HRC2_BBXNSL_PhoiTam.html");
            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy template HTML: {templatePath}");

            var html = await File.ReadAllTextAsync(templatePath);
            var logoUrl = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            var bmCode = await _bmConfig.GetBmCodeHtmlAsync("HRC2_BBSL_PhoiTam");

            html = html
                .Replace("{{LogoUrl}}",       logoUrl)
                .Replace("{{BmCode}}", bmCode)
                .Replace("{{soPhieu}}", soPhieu)
                .Replace("{{ngaySX}}", ngaySX)
                .Replace("{{mayduc}}", phieu?.MayDuc.HasValue == true ? $"Máy {phieu.MayDuc}" : "")
                .Replace("{{ca}}", ca)
                .Replace("{{kip}}", kip)
                .Replace("{{TABLE_BODY}}", rowsHtml.ToString())
                .Replace("{{ThanhPhanHtml}}", thanhPhanHtml)
                .Replace("{{DucKyImgHtml}}", ducSigImg)
                .Replace("{{DucKyTenHtml}}", ducSigTen)
                .Replace("{{KhoKyImgHtml}}", khoSigImg)
                .Replace("{{KhoKyTenHtml}}", khoSigTen)
                .Replace("{{QLCLKyImgHtml}}", qlclSigImg)
                .Replace("{{QLCLKyTenHtml}}", qlclSigTen);

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
                .FirstOrDefaultAsync(p => p.Idphieu == idPhieu && p.MaBm == "HRC2_BBSL_PhoiTam");
        }

        private static (string SoPhieu, string NgaySX, string Ca, string Kip) ExtractPhieuInfo(BmPhieu? phieu)
        {
            var soPhieu = phieu?.SoPhieu ?? "";
            string ngaySX = "", ca = "", kip = "";
            kip = phieu?.Kip ?? "";
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
                        // Ca ngày = 1, Ca đêm = 2 — giữ nguyên số để ghép "Kíp: {ca}{kip}" (vd "1A")
                        ca = caVal > 0 ? caVal.ToString() : "";
                    }
                    
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

        // Người ký cho biên bản: X.ĐPT → Đúc, Kho phôi → Kho, P.QLCL → KCS
        private async Task<(int? Duc, int? Kho, int? Qlcl)> GetPhieuSignersAsync(Guid idPhieu)
        {
            var trangThais = await _context.BkHrc2SlabTrangThais
                .AsNoTracking()
                .Where(t => t.IdPhieuBBSL == idPhieu)
                .ToListAsync();

            int? ducUserId = trangThais
                .Where(t => t.NguoiXacNhanDuc != null)
                .OrderByDescending(t => t.NgayXacNhanDuc)
                .Select(t => t.NguoiXacNhanDuc)
                .FirstOrDefault();

            int? khoUserId = trangThais
                .Where(t => t.NguoiXacNhanKho != null)
                .OrderByDescending(t => t.NgayXacNhanKho)
                .Select(t => t.NguoiXacNhanKho)
                .FirstOrDefault();

            int? qlclUserId = trangThais
                .Where(t => t.NguoiChuyenKCS != null)
                .OrderByDescending(t => t.NgayChuyenKCS)
                .Select(t => t.NguoiChuyenKCS)
                .FirstOrDefault();

            return (ducUserId, khoUserId, qlclUserId);
        }

        // Dòng "Thành phần" trong biên bản PDF — cùng nguồn dữ liệu với chữ ký (Duc/Kho/Qlcl),
        // format text theo kiểu BBGN_ThepLong.html: "N. Nhãn: Ông/Bà: Tên   Chức vụ: ..."
        // Dòng "Thành phần" trong biên bản PDF — cùng nguồn dữ liệu với chữ ký (Duc/Kho/Qlcl),
        // format: "N. Ông/Bà: Tên   Chức vụ: ...   BP: ..."
        private static string BuildThanhPhanLine(int idx, int? userId, Dictionary<int, TaiKhoan> userMap)
        {
            var u = userId != null && userMap.TryGetValue(userId.Value, out var uu) ? uu : null;
            if (u == null)
                return $"<div class=\"info-row\"><span class=\"info-idx\">{idx}.</span><span class=\"info-field info-name\">Ông/Bà: -</span></div>";

            var ten = System.Net.WebUtility.HtmlEncode(u.HoVaTen ?? "");
            var chucVu = System.Net.WebUtility.HtmlEncode(u.ViTri?.TenViTri ?? "");
            var bp = System.Net.WebUtility.HtmlEncode(u.PhongBan?.TenNgan ?? "");

            return $"<div class=\"info-row\">" +
                   $"<span class=\"info-idx\">{idx}.</span>" +
                   $"<span class=\"info-field info-name\">Ông/Bà: <strong>{ten}</strong></span>" +
                   $"<span class=\"info-field info-chucvu\">Chức vụ: {chucVu}</span>" +
                   $"<span class=\"info-field info-bp\">BP: {bp}</span>" +
                   $"</div>";
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

        // Nhãn hiển thị của PhanLoai (L1/L2/L2TP/L3/L3TP/ND/PP) — đồng bộ với HRC2_PHAN_LOAI_LABEL
        // ở dataproduct.UI/src/utils/enums/Hrc2PhanLoaiEnum.ts, phải sửa cả hai nơi khi đổi nhãn.
        private static readonly Dictionary<string, string> Hrc2PhanLoaiLabel = new()
        {
            ["L1"]   = "Loại 1",
            ["L2"]   = "Loại 2",
            ["L2TP"] = "Loại 2 TP",
            ["L3"]   = "Loại 3",
            ["L3TP"] = "Loại 3 TP",
            ["ND"]   = "Ngắn dài",
            ["PP"]   = "Phế phẩm",
        };

        private static string GetPhanLoaiLabel(string? phanLoai)
            => string.IsNullOrEmpty(phanLoai) ? "" : Hrc2PhanLoaiLabel.GetValueOrDefault(phanLoai, phanLoai);

        // Xác định cột pivot (nguội/nóng × loại) cho biểu mẫu ISO cố định (BM.36/QT.05.15 — layout
        // 10 nhóm, KHÔNG được đổi: nguội có 6 nhóm loại1/2/2TP/3/3TP/ngắn-dài, nóng chỉ có 4 nhóm
        // loại1/2/2TP/3TP, không có "loại 3" hay "ngắn dài" riêng cho nóng).
        // nguội/nóng lấy từ LoaiPhoi (field thô từ BKMIS, đáng tin cho việc này). Nhóm con (loại
        // mấy) lấy từ PhanLoai (L1/L2/L2TP/L3/L3TP/ND/PP — field đã tính đúng, đồng bộ qua
        // usp_SyncBK_HRC2_Slab) thay vì ChatLuongTPHH cũ (chỉ có text "Loại 1/2/3", không đủ chi
        // tiết để tách L2TP/L3TP/ND). PP (phế phẩm) không có cột trong biểu mẫu ISO → không match,
        // vẫn được cộng vào TongSoPhoi/TongKhoiLuong nhưng không lên cột phân loại nào.
        private static (string? SoKey, string? KlKey) GetPivotKeys(string? loaiPhoi, string? phanLoai)
        {
            var lp = loaiPhoi ?? "";
            var isNguoi = Regex.IsMatch(lp, "nguội|nguoi", RegexOptions.IgnoreCase);
            var isNong  = Regex.IsMatch(lp, "nóng|nong",   RegexOptions.IgnoreCase);
            if (!isNguoi && !isNong) return (null, null);
            var p = isNguoi ? "nguoi" : "nong";

            string? mid = phanLoai switch
            {
                "L1"   => "loai1",
                "L2"   => "loai2",
                "L2TP" => "loai2tp",
                "L3"   => isNguoi ? "loai3" : null,   // nóng không có cột "Loại III" riêng
                "L3TP" => "loai3tp",
                "ND"   => isNguoi ? "nganDai" : null, // nóng không có cột "Ngắn dài"
                _      => null,                       // PP hoặc PhanLoai chưa xác định
            };
            if (mid == null) return (null, null);
            return ($"{p}_{mid}_so", $"{p}_{mid}_kl");
        }

        // Pivot row — nhóm theo (meThep, macThep, kichThuoc), phân loại theo nguội/nóng (LoaiPhoi) ×
        // loại 1/2/2TP/3/3TP/ngắn dài (PhanLoai) — đúng layout ISO cố định của biểu mẫu.
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

        private static string FmtKichThuoc(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("0");

        private static List<TongHopPivotRow> BuildPivotRows(List<BkHrc2SlabTrangThai> trangThais)
        {
            var map = new Dictionary<string, TongHopPivotRow>();

            foreach (var tt in trangThais)
            {
                var slab = tt.Slab;
                var kt = (slab.ChieuDay != null && slab.ChieuRong != null && slab.ChieuDai != null)
                    ? $"{FmtKichThuoc(slab.ChieuDay.Value)}x{FmtKichThuoc(slab.ChieuRong.Value)}x{FmtKichThuoc(slab.ChieuDai.Value)}"
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
                var (soKey, _) = GetPivotKeys(slab.LoaiPhoi, slab.PhanLoai);

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
