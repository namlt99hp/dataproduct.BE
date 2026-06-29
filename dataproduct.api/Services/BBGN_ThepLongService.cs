using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using DinkToPdf;
using DinkToPdf.Contracts;
using dataproduct.api.Models.MasterData;

namespace dataproduct.api.Services
{
    public class BBGN_ThepLongService
    {
        private readonly IBBGN_ThepLongRepository _repo;
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterContext;
        private readonly SyncPhanLoaiService _syncPhanLoaiService;
        private readonly PheDuyetService _pheDuyetService;
        private readonly IConverter _pdfConverter;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly string _gangLongConnStr;

        public BBGN_ThepLongService(
            IBBGN_ThepLongRepository repo,
            ProductFormContext context,
            ProductDataMasterDbContext masterContext,
            IConfiguration config,
            SyncPhanLoaiService syncPhanLoaiService,
            PheDuyetService pheDuyetService,
            IConverter pdfConverter,
            IWebHostEnvironment env)
        {
            _repo = repo;
            _context = context;
            _masterContext = masterContext;
            _syncPhanLoaiService = syncPhanLoaiService;
            _pheDuyetService = pheDuyetService;
            _pdfConverter = pdfConverter;
            _configuration = config;
            _env = env;
            _gangLongConnStr =
                config.GetConnectionString("GangLongDbConnection")
                ?? config.GetConnectionString("MasterDbConnection")
                ?? throw new InvalidOperationException("GangLongDbConnection/MasterDbConnection connection string is not configured");
        }

        public async Task<List<string>> SaveHRC2BBGNThepLongAsync(JsonElement formData, Guid idPhieu)
        {
            var warnings = new List<string>();

            if (!formData.TryGetProperty("table1", out var table))
                return warnings;
            var bieuMau = GetString(formData, "maBm");
            var ngaySX  = TryParseDateTime(formData, "NgaySX");
            var ca      = TryParseInt(formData, "ca");
            var scope   = TryParseInt(formData, "scope");
            var kip     = GetString(formData, "kip");

            // ===== PRELOAD DATA theo IdPhieu =====
            var existingData = await _context.BBGN_ThepLongs
                .Where(x => x.IdPhieu == idPhieu)
                .ToListAsync();

            var mapById = existingData.ToDictionary(x => x.Id);

            var toInsert = new List<BBGN_ThepLong>();
            var affectedMes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ===== LOOP DATA =====
            foreach (var row in table.EnumerateArray())
            {
                var rowId = TryParseInt(row, "id");
                var me = GetString(row, "me");

                var klLFSauThep = TryParseDecimal(row, "klLFSauThep");
                var klLan1 = TryParseDecimal(row, "klLan1");
                var klLan2 = TryParseDecimal(row, "klLan2");
                var klLan3 = TryParseDecimal(row, "klLan3");
                var klcau1 = TryParseDecimal(row, "klcau1");
                var klcau2 = TryParseDecimal(row, "klcau2");
                var lastIdUserEdit = TryParseInt(row, "lastIdUserEdit");
                var lastNameUserEdit = GetString(row, "lastNameUserEdit");
                var tinhLuyenLenThang = GetString(row, "tinhLuyenLenThang");
                var isIncomingLenThang = string.Equals(tinhLuyenLenThang?.Trim(), "Lên thẳng", StringComparison.OrdinalIgnoreCase);

                if (rowId.HasValue && mapById.TryGetValue(rowId.Value, out var existing))
                {
                    // ===== CHECK SERVER STATE: "Lên thẳng" và klLan1 loại trừ nhau =====
                    var serverHasKlLan1 = existing.KlLan1.HasValue;
                    var serverIsLenThang = string.Equals(existing.TinhLuyenLenThang?.Trim(), "Lên thẳng", StringComparison.OrdinalIgnoreCase);

                    if (isIncomingLenThang && serverHasKlLan1)
                    {
                        // Case 1: FE gửi "Lên thẳng" nhưng server đang có KL lần 1 → giữ giá trị server
                        tinhLuyenLenThang = existing.TinhLuyenLenThang;
                        warnings.Add($"Mẻ {me}: Không thể chọn Lên thẳng vì KL lần 1 đã được nhập");
                    }
                    else if (klLan1.HasValue && serverIsLenThang)
                    {
                        // Case 2: FE gửi KL lần 1 nhưng server đang có "Lên thẳng" → bỏ KL lần 1
                        klLan1 = null;
                        warnings.Add($"Mẻ {me}: Không thể nhập KL lần 1 vì mẻ đã được chọn Lên thẳng");
                    }

                     var klThepLong = TryParseDecimal(row, "klThepLong");

                    // ===== UPDATE =====
                    var oldMe = existing.Me;
                    existing.Me = me;
                    existing.MayDuc = GetString(row, "mayDuc");
                    existing.IdMacThep = TryParseInt(row, "idMacThep");
                    existing.ThungSo = GetString(row, "thungSo");
                    existing.ThoiGian = GetString(row, "thoiGian");
                    existing.KLLFSauThep = klLFSauThep;
                    existing.KlLan1 = klLan1;
                    existing.KlLan2 = klLan2;
                    existing.KlLan3 = klLan3;
                    existing.KlThepLong = klThepLong;
                    existing.GhiChu = GetString(row, "ghiChu");
                    existing.TinhLuyenLenThang = tinhLuyenLenThang;
                    existing.PhanLoai = GetString(row, "phanLoai");
                    existing.IsThuNghiem = TryParseBool(row, "isThuNghiem");
                    existing.MacThepBKMIS = GetString(row, "macThepBKMIS");
                    existing.klcau1 = klcau1;
                    existing.klcau2 = klcau2;
                    if (lastIdUserEdit.HasValue) existing.LastIdUserEdit = lastIdUserEdit;
                    if (!string.IsNullOrWhiteSpace(lastNameUserEdit)) existing.LastNameUserEdit = lastNameUserEdit;
                    if (!string.IsNullOrWhiteSpace(bieuMau)) existing.BieuMau = bieuMau;
                    if (ngaySX.HasValue) existing.NgaySX = ngaySX.Value;
                    if (ca.HasValue) existing.Ca = ca.Value;
                    if (scope.HasValue) existing.Scope = scope.Value;
                    if (!string.IsNullOrWhiteSpace(kip)) existing.Kip = kip;

                    _context.BBGN_ThepLongs.Update(existing);

                    if (!string.IsNullOrWhiteSpace(oldMe))
                        affectedMes.Add(oldMe.Trim());
                    if (!string.IsNullOrWhiteSpace(me))
                        affectedMes.Add(me.Trim());
                }
                else
                {
                    // Dòng mới chưa có id: chỉ insert khi có mẻ
                    if (string.IsNullOrWhiteSpace(me)) continue;

                    // INSERT: payload-level check (không có server state để so sánh)
                    if (isIncomingLenThang)
                        klLan1 = null;

                    var klThepLong = TryParseDecimal(row, "klThepLong") ;

                    // ===== INSERT =====
                    var entity = new BBGN_ThepLong
                    {
                        Me = me,
                        MayDuc = GetString(row, "mayDuc"),
                        IdMacThep = TryParseInt(row, "idMacThep"),
                        ThungSo = GetString(row, "thungSo"),
                        ThoiGian = GetString(row, "thoiGian"),
                        BieuMau = bieuMau,
                        Ca      = ca,
                        NgaySX  = ngaySX,
                        Scope   = scope,
                        Kip     = kip,
                        KLLFSauThep = klLFSauThep,
                        KlLan1 = klLan1,
                        KlLan2 = klLan2,
                        KlLan3 = klLan3,
                        KlThepLong = klThepLong,
                        GhiChu = GetString(row, "ghiChu"),
                        TinhLuyenLenThang = tinhLuyenLenThang,
                        PhanLoai = GetString(row, "phanLoai"),
                        IsThuNghiem = TryParseBool(row, "isThuNghiem"),
                        MacThepBKMIS = GetString(row, "macThepBKMIS"),
                        klcau1 = klcau1,
                        klcau2 = klcau2,
                        LastIdUserEdit = lastIdUserEdit,
                        LastNameUserEdit = lastNameUserEdit,
                        IdPhieu = idPhieu,
                        IsGhost = false
                    };

                    toInsert.Add(entity);
                    affectedMes.Add(me.Trim());
                }
            }

            // ===== SAVE =====
            if (toInsert.Any())
                await _context.BBGN_ThepLongs.AddRangeAsync(toInsert);

            await _context.SaveChangesAsync();

            await UpdateDuplicateFlagsForMesAsync(affectedMes);

            return warnings;
        }
    

        private decimal? SumValues(params decimal?[] values)
        {
            decimal total = 0;
            bool hasValue = false;

            foreach (var v in values)
            {
                if (v.HasValue)
                {
                    total += v.Value;
                    hasValue = true;
                }
            }

            return hasValue ? total : null;
        }
        /// <summary>Đọc chuỗi từ JSON; chấp nhận cả Number (FE thường gửi thungSo dạng số).</summary>
        private string? GetString(JsonElement row, string key)
        {
            if (!row.TryGetProperty(key, out var val) || val.ValueKind == JsonValueKind.Null)
                return null;

            if (val.ValueKind == JsonValueKind.String)
                return val.GetString();

            if (val.ValueKind == JsonValueKind.Number)
                return val.GetRawText();

            return null;
        }
        
        private decimal? TryParseDecimal(JsonElement row, string key)
        {
            if (!row.TryGetProperty(key, out var val)) return null;

            if (val.ValueKind == JsonValueKind.Number)
                return val.GetDecimal();

            if (val.ValueKind == JsonValueKind.String &&
                decimal.TryParse(val.GetString(), out var d))
                return d;

            return null;
        }

        private DateTime? TryParseDateTime(JsonElement row, string key)
        {
            if (!row.TryGetProperty(key, out var val)) return null;

            if (val.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(val.GetString(), out var dt))
                return dt;

            return null;
        }


        private int? TryParseInt(JsonElement row, string key)
        {
            if (!row.TryGetProperty(key, out var val)) return null;
            if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var n))
                return n;
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var s))
                return s;
            return null;
        }

        private bool? TryParseBool(JsonElement row, string key)
        {
            if (!row.TryGetProperty(key, out var val)) return null;
            if (val.ValueKind == JsonValueKind.True) return true;
            if (val.ValueKind == JsonValueKind.False) return false;
            if (val.ValueKind == JsonValueKind.String && bool.TryParse(val.GetString(), out var b)) return b;
            return null;
        }

        private decimal? Sum(JsonElement row, params string[] keys)
        {
            decimal total = 0;
            bool hasValue = false;

            foreach (var key in keys)
            {
                var val = TryParseDecimal(row, key);
                if (val.HasValue)
                {
                    total += val.Value;
                    hasValue = true;
                }
            }

            return hasValue ? total : null;
        }

        public async Task<List<string>> GetDistinctPhanLoaiNhomAsync(string bieuMau, string? search = null, int limit = 30)
        {
            var query = _context.NhomPhanLoaiMacTheps.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.TenNhom.Contains(search.Trim()));

            return await query
                .Select(x => x.TenNhom)
                .Distinct()
                .OrderBy(x => x)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<string>> FetchMeThoiAsync(FetchMeThoiRequest request)
        {
            return await ExecuteGetMeThoiAsync("usp_GetMeThoi_ByNgayCaNhaMay", request.NgaySX, request.Ca, request.NhaMay);
        }

        public async Task<List<string>> FetchMeThoiHRC1Async(HRC1_FetchMeThoiRequest request)
        {
            return await ExecuteGetMeThoiAsync(
                "usp_GetMeThoi_ByNgayCaLoThoi",
                request.NgaySX, request.Ca, nhaMay: 1, idLoThoi: request.IdLoThoi);
        }

        public async Task<List<string>> SearchMeThoi(int nhaMay, string? searchStr, List<int>? ID_LoThois)
        {
            List<int> toHopLoThoi;
            if (nhaMay == 1 && ID_LoThois != null && ID_LoThois.Count > 0)
                toHopLoThoi = ID_LoThois;
            else
                toHopLoThoi = nhaMay == 1
                    ? new List<int> { 1, 2, 3, 4, 5 }
                    : new List<int> { 6, 7 };

            var query = _masterContext.Tbl_MeThoi
                .Where(x => x.Is_Delete != true
                         && toHopLoThoi.Contains(x.ID_LoThoi));

            if (!string.IsNullOrWhiteSpace(searchStr))
                query = query.Where(x => x.MaMeThoi.Contains(searchStr.Trim()));

            return await query
                .OrderByDescending(x => x.ID)
                .Select(x => x.MaMeThoi)
                .ToListAsync();
        }

        public async Task<List<BBGN_ThepLong>> LoadAsync(LoadBBGNThepLongRequest request)
        {
            var rows = await _context.BBGN_ThepLongs
                .Where(x => x.IdPhieu == request.IdPhieu && x.IsGhost != true)
                .ToListAsync();

            var maMes = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.Me) && (x.PhanLoai == null || x.MacThepBKMIS == null))
                .Select(x => x.Me!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (maMes.Count > 0)
            {
                var (bieuMau, _) = ParseBieuMauForThepLong(request.BieuMau);
                await _syncPhanLoaiService.SyncAsync(new SyncPhanLoaiRequest
                {
                    BieuMau = bieuMau,
                    MaMes = maMes
                });

                rows = await _context.BBGN_ThepLongs
                    .Where(x => x.IdPhieu == request.IdPhieu && x.IsGhost != true)
                    .ToListAsync();
            }

            await ResolveMacThepNamesAsync(rows);
            return rows;
        }

        public async Task<bool> DeleteRowAsync(int id)
        {
            var row = await _context.BBGN_ThepLongs.FirstOrDefaultAsync(x => x.Id == id);
            if (row == null) return false;

            var idPhieu = row.IdPhieu;
            var deletedMe = row.Me;

            _context.BBGN_ThepLongs.Remove(row);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(deletedMe))
            {
                await UpdateDuplicateFlagsForMesAsync(new[] { deletedMe });
            }

            return true;
        }

        private async Task UpdateDuplicateFlagsForMesAsync(IEnumerable<string> rawMes)
        {
            var mes = rawMes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (mes.Count == 0) return;

            var rows = await _context.BBGN_ThepLongs
                .Where(x => x.IsGhost != true && x.Me != null && mes.Contains(x.Me))
                .ToListAsync();

            bool changed = false;
            var groups = rows.GroupBy(x => x.Me!.Trim(), StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                bool? expected = group.Count() >= 2 ? true : null;
                foreach (var row in group)
                {
                    if (row.IsTrungMeThoi != expected)
                    {
                        row.IsTrungMeThoi = expected;
                        _context.BBGN_ThepLongs.Update(row);
                        changed = true;
                    }
                }
            }

            if (changed)
                await _context.SaveChangesAsync();
        }

        private static (string CanonicalBieuMau, int NhaMayValue) ParseBieuMauForThepLong(string? raw)
        {
            var s = raw?.Trim() ?? "";
            if (s.Length == 0)
                throw new ArgumentException("BieuMau là bắt buộc khi load dữ liệu.");

            if (s.Equals("HRC1_BBGN_ThepLong", StringComparison.OrdinalIgnoreCase))
                return ("HRC1_BBGN_ThepLong", (int)NhaMay.HRC1);
            if (s.Equals("HRC2_BBGN_ThepLong", StringComparison.OrdinalIgnoreCase))
                return ("HRC2_BBGN_ThepLong", (int)NhaMay.HRC2);

            throw new ArgumentException(
                $"BieuMau không hợp lệ: '{raw}'. Chỉ chấp nhận HRC1_BBGN_ThepLong hoặc HRC2_BBGN_ThepLong.");
        }

        public async Task<List<BBGN_ThepLong>> LoadByIdPhieuAsync(Guid idPhieu)
        {
            var rows = await _context.BBGN_ThepLongs
                .Where(x => x.IdPhieu == idPhieu && x.IsGhost != true)
                .OrderBy(x => x.ThoiGian)
                .ThenBy(x => x.Id)
                .ToListAsync();
            await ResolveMacThepNamesAsync(rows);
            return rows;
        }

        public async Task<PagedResult<BBGN_ThepLong>> SearchThongKeAsync(SearchThongKeBBGNThepLongRequest request)
        {
            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
            if (pageSize > 500) pageSize = 500;

            var query = BuildThongKeQuery(request);

            var total = await query.CountAsync();
            var data = await query
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            await ResolveMacThepNamesAsync(data);

            return new PagedResult<BBGN_ThepLong>
            {
                Data = data,
                TotalRecords = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<SumThongKeBBGNThepLongResponse> SumThongKeAsync(SearchThongKeBBGNThepLongRequest request)
        {
            var query = BuildThongKeQuery(request);
            var totalRows = await query.CountAsync();
            var totalKl = await query.SumAsync(x => x.KlThepLong);

            return new SumThongKeBBGNThepLongResponse
            {
                TotalRows = totalRows,
                TotalKlThepLong = totalKl
            };
        }

        public async Task<TongHopBBGNThepLongResponse> TongHopAsync(SearchThongKeBBGNThepLongRequest request)
        {
            var baseQuery = BuildThongKeQuery(request);

            var phanLoai = await baseQuery
                .Where(x => !string.IsNullOrEmpty(x.PhanLoai))
                .GroupBy(x => x.PhanLoai!)
                .Select(g => new TongHopItem { Label = g.Key, SoMe = g.Count() })
                .OrderBy(x => x.Label)
                .ToListAsync();

            var caRaw = await baseQuery
                .Where(x => x.Ca.HasValue)
                .GroupBy(x => x.Ca!.Value)
                .Select(g => new { Ca = g.Key, SoMe = g.Count() })
                .OrderBy(x => x.Ca)
                .ToListAsync();
            var ca = caRaw.Select(x => new TongHopItem
            {
                Label = x.Ca == 1 ? "Ca ngày" : x.Ca == 2 ? "Ca đêm" : $"Ca {x.Ca}",
                SoMe  = x.SoMe
            }).ToList();

            var kip = await baseQuery
                .Where(x => !string.IsNullOrEmpty(x.Kip))
                .GroupBy(x => x.Kip!)
                .Select(g => new TongHopItem { Label = g.Key, SoMe = g.Count() })
                .OrderBy(x => x.Label)
                .ToListAsync();

            var tinhLuyen = await baseQuery
                .Where(x => !string.IsNullOrEmpty(x.TinhLuyenLenThang))
                .GroupBy(x => x.TinhLuyenLenThang!)
                .Select(g => new TongHopItem { Label = g.Key, SoMe = g.Count() })
                .OrderBy(x => x.Label)
                .ToListAsync();

            var ducVuong = await (
                from row in baseQuery
                where row.Scope.HasValue && !string.IsNullOrEmpty(row.MayDuc)
                join md in _context.MayDucs on row.Scope.Value equals md.Id
                where md.LoaiMayDuc == "DV"
                group row by row.MayDuc! into g
                orderby g.Key
                select new TongHopItem { Label = g.Key, SoMe = g.Count() }
            ).ToListAsync();

            var ducTam = await (
                from row in baseQuery
                where row.Scope.HasValue && !string.IsNullOrEmpty(row.MayDuc)
                join md in _context.MayDucs on row.Scope.Value equals md.Id
                where md.LoaiMayDuc == "DT"
                group row by row.MayDuc! into g
                orderby g.Key
                select new TongHopItem { Label = g.Key, SoMe = g.Count() }
            ).ToListAsync();

            var nhomPhanLoai = await (
                from row in baseQuery
                where row.IdMacThep.HasValue
                join mt in _context.MacTheps on row.IdMacThep!.Value equals mt.Id
                where mt.Id_NhomPhanLoaiMacThep.HasValue
                join nhom in _context.NhomPhanLoaiMacTheps on mt.Id_NhomPhanLoaiMacThep!.Value equals nhom.Id
                group row by nhom.TenNhom into g
                orderby g.Key
                select new TongHopItem { Label = g.Key, SoMe = g.Count() }
            ).ToListAsync();

            return new TongHopBBGNThepLongResponse
            {
                PhanLoai            = phanLoai,
                Ca                  = ca,
                Kip                 = kip,
                TinhLuyenLenThang   = tinhLuyen,
                DucVuong            = ducVuong,
                DucTam              = ducTam,
                NhomPhanLoaiMacThep = nhomPhanLoai
            };
        }

        public async Task<ExportFileResult> ExportThongKeExcelAsync(SearchThongKeBBGNThepLongRequest request, string templatePath)
        {
            var query = BuildThongKeQuery(request);
            var data = await query
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .ThenByDescending(x => x.Id)
                .ToListAsync();

            await ResolveMacThepNamesAsync(data);

            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu Excel: {templatePath}");

            using var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = new XLWorkbook(fs);
            var ws = workbook.Worksheet(1);

            bool isHRC2 = (request.BieuMau ?? "") == "HRC2_BBGN_ThepLong";

            // Column positions (1-based)
            int colKLLF       = isHRC2 ? 0  : 10; // 0 = not present
            int colLan1       = isHRC2 ? 10 : 11;
            int colLan2       = isHRC2 ? 11 : 12;
            int colLan3       = isHRC2 ? 12 : 13;
            int colKlThepLong = isHRC2 ? 13 : 14;
            int colCau1       = isHRC2 ? 19 : 20;
            int colCau2       = isHRC2 ? 20 : 21;
            int totalCols     = isHRC2 ? 20 : 21;

            int startRow = 5;
            int stt = 1;

            foreach (var row in data)
            {
                int col = 1;
                ws.Cell(startRow, col++).Value = stt++;
                ws.Cell(startRow, col++).Value = row.NgaySX.HasValue ? row.NgaySX.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(startRow, col++).Value = row.Ca.HasValue ? (XLCellValue)row.Ca.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.MayDuc ?? "";
                var colMeThoi = ws.Cell(startRow, col++);
                colMeThoi.Value = row.Me ?? "";
                if (row.IsTrungMeThoi == true)
                    colMeThoi.Style.Fill.BackgroundColor = XLColor.Red;
                ws.Cell(startRow, col++).Value = row.MacThep ?? "";
                ws.Cell(startRow, col++).Value = row.TenNhomPhanLoaiMacThep ?? "";
                ws.Cell(startRow, col++).Value = row.ThungSo ?? "";
                ws.Cell(startRow, col++).Value = row.ThoiGian ?? "";
                if (!isHRC2)
                    ws.Cell(startRow, col++).Value = row.KLLFSauThep.HasValue ? (XLCellValue)row.KLLFSauThep.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.KlLan1.HasValue ? (XLCellValue)row.KlLan1.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.KlLan2.HasValue ? (XLCellValue)row.KlLan2.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.KlLan3.HasValue ? (XLCellValue)row.KlLan3.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.KlThepLong.HasValue ? (XLCellValue)row.KlThepLong.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.GhiChu ?? "";
                ws.Cell(startRow, col++).Value = row.TinhLuyenLenThang ?? "";
                ws.Cell(startRow, col++).Value = row.PhanLoai ?? "";
                ws.Cell(startRow, col++).Value = row.MacThepBKMIS ?? "";
                ws.Cell(startRow, col++).Value = row.IsThuNghiem == true ? "X" : "";
                ws.Cell(startRow, col++).Value = row.klcau1.HasValue ? (XLCellValue)row.klcau1.Value : Blank.Value;
                ws.Cell(startRow, col++).Value = row.klcau2.HasValue ? (XLCellValue)row.klcau2.Value : Blank.Value;

                startRow++;
            }

            // ===== TOTALS ROW =====
            int totalRow = startRow;

            var labelRange = ws.Range(totalRow, 1, totalRow, 9);
            labelRange.Merge();
            labelRange.Value = "Tổng";
            labelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            labelRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            labelRange.Style.Font.Bold = true;

            if (colKLLF > 0)
                ws.Cell(totalRow, colKLLF).Value = (XLCellValue)(double)data.Sum(x => (double)(x.KLLFSauThep ?? 0));
            ws.Cell(totalRow, colLan1).Value       = (XLCellValue)(double)data.Sum(x => (double)(x.KlLan1 ?? 0));
            ws.Cell(totalRow, colLan2).Value       = (XLCellValue)(double)data.Sum(x => (double)(x.KlLan2 ?? 0));
            ws.Cell(totalRow, colLan3).Value       = (XLCellValue)(double)data.Sum(x => (double)(x.KlLan3 ?? 0));
            ws.Cell(totalRow, colKlThepLong).Value = (XLCellValue)(double)data.Sum(x => (double)(x.KlThepLong ?? 0));
            ws.Cell(totalRow, colCau1).Value       = (XLCellValue)(double)data.Sum(x => (double)(x.klcau1 ?? 0));
            ws.Cell(totalRow, colCau2).Value       = (XLCellValue)(double)data.Sum(x => (double)(x.klcau2 ?? 0));

            // ===== BORDER =====
            var tableRange = ws.Range(5, 1, totalRow, totalCols);
            tableRange.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
            tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var fileName = $"ThongKe_BBGN_ThepLong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return new ExportFileResult
            {
                Content     = ms.ToArray(),
                FileName    = fileName,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        private IQueryable<BBGN_ThepLong> BuildThongKeQuery(SearchThongKeBBGNThepLongRequest request)
        {
            var query = _context.BBGN_ThepLongs
                .AsNoTracking()
                .Where(x => x.IsGhost != true && x.BieuMau == request.BieuMau);

            if (request.TuNgay.HasValue)
            {
                var fromDate = request.TuNgay.Value.Date;
                query = query.Where(x => x.NgaySX.HasValue && x.NgaySX.Value.Date >= fromDate);
            }

            if (request.DenNgay.HasValue)
            {
                var toDate = request.DenNgay.Value.Date;
                query = query.Where(x => x.NgaySX.HasValue && x.NgaySX.Value.Date <= toDate);
            }

            if (request.Ca.HasValue)
                query = query.Where(x => x.Ca == request.Ca.Value);

            if (!string.IsNullOrWhiteSpace(request.Kip))
            {
                var kip = request.Kip.Trim();
                var phieuQuery = _context.BmPhieus
                    .AsNoTracking()
                    .Where(x =>
                        x.IsDelete != 1 &&
                        x.IsLock != 1 &&
                        x.Kip == kip &&
                        x.MaBm == request.BieuMau);

                if (request.Ca.HasValue)
                    phieuQuery = phieuQuery.Where(x => x.Ca == request.Ca.Value);

                var idPhieuList = phieuQuery.Select(x => x.Idphieu);
                query = query.Where(x => idPhieuList.Contains(x.IdPhieu));
            }

            var scope = request.Scope ?? request.MayDuc;
            if (scope.HasValue)
                query = query.Where(x => x.Scope == scope.Value);

            if (!string.IsNullOrWhiteSpace(request.SearchString))
            {
                var keyword = request.SearchString.Trim();
                query = query.Where(x =>
                    (x.Me ?? string.Empty).Contains(keyword) ||
                    (x.MacThep ?? string.Empty).Contains(keyword) ||
                    _context.MacTheps.Any(m => m.Id == x.IdMacThep && m.TenMacThep.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(request.ThungSo))
            {
                var thungSo = request.ThungSo.Trim();
                query = query.Where(x => (x.ThungSo ?? string.Empty).Contains(thungSo));
            }

            if (!string.IsNullOrWhiteSpace(request.TinhLuyenLenThang))
            {
                var tinhLuyen = request.TinhLuyenLenThang.Trim();
                query = query.Where(x => (x.TinhLuyenLenThang ?? string.Empty).Contains(tinhLuyen));
            }

            if (!string.IsNullOrWhiteSpace(request.PhanLoai))
            {
                var phanLoai = request.PhanLoai.Trim();
                query = query.Where(x => (x.PhanLoai ?? string.Empty).Contains(phanLoai));
            }

            if (!string.IsNullOrWhiteSpace(request.PhanLoaiNhom))
            {
                var phanLoaiNhom = request.PhanLoaiNhom.Trim();
                var matchingMacThepIds = _context.MacTheps
                    .Where(m => m.Id_NhomPhanLoaiMacThep.HasValue &&
                        _context.NhomPhanLoaiMacTheps
                            .Any(n => n.Id == m.Id_NhomPhanLoaiMacThep.Value && n.TenNhom.Contains(phanLoaiNhom)))
                    .Select(m => m.Id);
                query = query.Where(x => x.IdMacThep.HasValue && matchingMacThepIds.Contains(x.IdMacThep.Value));
            }

            if (request.IsTrungMeThoi.HasValue)
                query = query.Where(x => x.IsTrungMeThoi == request.IsTrungMeThoi.Value);

            if (request.IsThuNghiem.HasValue)
                query = query.Where(x => x.IsThuNghiem == request.IsThuNghiem.Value);

            return query;
        }

        private async Task ResolveMacThepNamesAsync(List<BBGN_ThepLong> rows)
        {
            var ids = rows.Where(x => x.IdMacThep.HasValue).Select(x => x.IdMacThep!.Value).Distinct().ToList();
            if (ids.Count == 0) return;

            var macTheps = await _context.MacTheps
                .Where(x => ids.Contains(x.Id))
                .ToListAsync();

            var nhomIds = macTheps
                .Where(x => x.Id_NhomPhanLoaiMacThep.HasValue)
                .Select(x => x.Id_NhomPhanLoaiMacThep!.Value)
                .Distinct()
                .ToList();

            var nhomMap = nhomIds.Count > 0
                ? await _context.NhomPhanLoaiMacTheps
                    .Where(x => nhomIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => x.TenNhom)
                : new Dictionary<int, string>();

            var macThepMap = macTheps.ToDictionary(x => x.Id);

            foreach (var row in rows.Where(x => x.IdMacThep.HasValue))
            {
                if (!macThepMap.TryGetValue(row.IdMacThep!.Value, out var mt)) continue;
                row.MacThep = mt.TenMacThep;
                if (mt.Id_NhomPhanLoaiMacThep.HasValue &&
                    nhomMap.TryGetValue(mt.Id_NhomPhanLoaiMacThep.Value, out var tenNhom))
                    row.TenNhomPhanLoaiMacThep = tenNhom;
            }
        }

        public async Task<BmPhieu?> GetBmPhieuByIdAsync(Guid idPhieu)
        {
            return await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == idPhieu);
        }

        public async Task<ExportFileResult> ExportPdfAsync(Guid idPhieu)
        {
            var rows  = await LoadByIdPhieuAsync(idPhieu);
            var phieu = await GetBmPhieuByIdAsync(idPhieu);

            var ca  = phieu?.Ca ?? 1;
            var kip = phieu?.Kip ?? "";
            var ngay = phieu?.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);

            string tuGio, denGio, tuNgay, denNgay;
            if (ca == 1)
            {
                tuGio   = "08"; denGio  = "20";
                tuNgay  = ngay.ToString("dd/MM/yyyy");
                denNgay = ngay.ToString("dd/MM/yyyy");
            }
            else
            {
                tuGio   = "20"; denGio  = "08";
                tuNgay  = ngay.ToString("dd/MM/yyyy");
                denNgay = ngay.AddDays(1).ToString("dd/MM/yyyy");
            }

            // Build table rows
            var sb  = new StringBuilder();
            decimal sumKl = 0;
            int stt = 1;
            foreach (var r in rows)
            {
                var isTrung = r.IsTrungMeThoi == true;
                var meStyle = isTrung ? " style=\"color:red\"" : "";
                sb.Append($@"
                <tr>
                  <td>{stt++}</td>
                  <td>{r.MayDuc ?? ""}</td>
                  <td{meStyle}>{r.Me ?? ""}</td>
                  <td>{r.MacThep ?? ""}</td>
                  <td>{r.ThungSo ?? ""}</td>
                  <td>{r.ThoiGian ?? ""}</td>
                  <td>{(r.KlLan1.HasValue ? r.KlLan1.Value.ToString("0.##") : "")}</td>
                  <td>{(r.KlLan2.HasValue ? r.KlLan2.Value.ToString("0.##") : "")}</td>
                  <td>{(r.KlLan3.HasValue ? r.KlLan3.Value.ToString("0.##") : "")}</td>
                  <td>{(r.KlThepLong.HasValue ? r.KlThepLong.Value.ToString("0.##") : "")}</td>
                  <td>{r.GhiChu ?? ""}</td>
                  <td>{r.TinhLuyenLenThang ?? ""}</td>
                </tr>");
                if (r.KlThepLong.HasValue) sumKl += r.KlThepLong.Value;
            }

            // Phê duyệt
            var pheDuyets   = await _pheDuyetService.GetPheDuyetPhieuAsync(idPhieu);
            var benGiaoList = pheDuyets.Where(x => x.CapDuyet == 0).ToList();
            var benNhanList = pheDuyets.Where(x => x.CapDuyet != 0).ToList();

            var benGiaoHtml = BuildSignatureHtml(benGiaoList);
            var benNhanHtml = BuildSignatureHtml(benNhanList);

            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "BBGN_ThepLong.html");
            var html = await File.ReadAllTextAsync(templatePath);

            var logoUrl = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            html = html
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{Ca}}", ca.ToString())
                .Replace("{{Kip}}", kip)
                .Replace("{{TuGio}}", tuGio)
                .Replace("{{TuNgay}}", tuNgay)
                .Replace("{{DenGio}}", denGio)
                .Replace("{{DenNgay}}", denNgay)
                .Replace("{{Rows}}", sb.ToString())
                .Replace("{{TongKl}}", sumKl.ToString("0.##"))
                .Replace("{{TenBenGiao}}", benGiaoList.FirstOrDefault()?.HoVaTen ?? "")
                .Replace("{{ChucVuBenGiao}}", benGiaoList.FirstOrDefault()?.TenViTri ?? "")
                .Replace("{{TenBenNhan}}", benNhanList.FirstOrDefault()?.HoVaTen ?? "")
                .Replace("{{ChucVuBenNhan}}", benNhanList.FirstOrDefault()?.TenViTri ?? "")
                .Replace("{{NhaMayBenGiao}}", benGiaoList.FirstOrDefault()?.TenPhongBan ?? "")
                .Replace("{{NhaMayBenNhan}}", benNhanList.FirstOrDefault()?.TenPhongBan ?? "")
                .Replace("{{BenGiaoHtml}}", benGiaoHtml)
                .Replace("{{BenNhanHtml}}", benNhanHtml);

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    PaperSize   = PaperKind.A4,
                    Orientation = DinkToPdf.Orientation.Landscape
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

            var pdfBytes = _pdfConverter.Convert(doc);

            return new ExportFileResult
            {
                Content     = pdfBytes,
                FileName    = $"BBGN_ThepLong_{ngay:ddMMyyyy}_Ca{ca}.pdf",
                ContentType = "application/pdf"
            };
        }

        public void RenderBBGNThepLongExcel(ClosedXML.Excel.IXLWorksheet ws, List<BBGN_ThepLong> rows, BmPhieu? phieu = null)
        {
            const int startRow = 7;
            const int totalCols = 13;

            // ===== DÒNG 5: Ca kíp =====
            if (phieu != null)
            {
                var ca     = phieu.Ca ?? 1;
                var kip    = phieu.Kip ?? "";
                var ngay   = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today);

                string startTime, endTime, startDate, endDate;
                if (ca == 1)
                {
                    startTime = "08 giờ 00";
                    endTime   = "20 giờ 00";
                    startDate = ngay.ToString("dd/MM/yyyy");
                    endDate   = ngay.ToString("dd/MM/yyyy");
                }
                else // Ca 2
                {
                    startTime = "20 giờ 00";
                    endTime   = "08 giờ 00";
                    startDate = ngay.ToString("dd/MM/yyyy");
                    endDate   = ngay.AddDays(1).ToString("dd/MM/yyyy");
                }

                ws.Cell(5, 1).Value = $"Ca {ca}{kip} Từ {startTime}  ngày {startDate} đến {endTime}  ngày {endDate}";
                ws.Cell(5, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
            }

            decimal sumKlThepLong = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int r = startRow + i;
            
                ws.Cell(r, 1).Value = i + 1;         
                ws.Cell(r, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                                                                 // A - STT
                ws.Cell(r, 2).Value = row.MayDuc ?? "";       
                ws.Cell(r, 2).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                                                        // B - Máy đúc
                ws.Cell(r, 3).Value = row.Me ?? "";                                                   // C - Mẻ
                ws.Cell(r, 3).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                ws.Cell(r, 4).Value = row.MacThep ?? "";                                              // D - Mác thép
                ws.Cell(r, 5).Value = row.ThungSo ?? "";                                              // E - Thùng số
                ws.Cell(r, 5).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                ws.Cell(r, 6).Value = row.ThoiGian ?? "";                                             // F - Thời gian
                ws.Cell(r, 6).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                if (row.KlLan1.HasValue)    ws.Cell(r, 7).Value  = (double)row.KlLan1.Value;         // G - KL lần 1
                ws.Cell(r, 7).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                if (row.KlLan2.HasValue)    ws.Cell(r, 8).Value  = (double)row.KlLan2.Value;         // H - KL lần 2
                ws.Cell(r, 8).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                if (row.KlLan3.HasValue)    ws.Cell(r, 9).Value  = (double)row.KlLan3.Value;  
                ws.Cell(r, 9).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                // I - KL lần 3
                if (row.KlThepLong.HasValue) ws.Cell(r, 10).Value = (double)row.KlThepLong.Value;    // J - KL thép lỏng
                ws.Cell(r, 10).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                ws.Cell(r, 11).Value = row.GhiChu ?? "";                                              // K - Ghi chú
                ws.Cell(r, 12).Value = row.TinhLuyenLenThang ?? "";                                   // L - Tinh luyện lên thang
                ws.Cell(r, 13).Value = row.PhanLoai ?? "";                                            // M - Phân loại

                if (row.KlThepLong.HasValue)
                    sumKlThepLong += row.KlThepLong.Value;

                // Border toàn bộ ô trong dòng
                var dataRange = ws.Range(r, 1, r, totalCols);
                dataRange.Style.Border.TopBorder    = ClosedXML.Excel.XLBorderStyleValues.Thin;
                dataRange.Style.Border.BottomBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                dataRange.Style.Border.LeftBorder   = ClosedXML.Excel.XLBorderStyleValues.Thin;
                dataRange.Style.Border.RightBorder  = ClosedXML.Excel.XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

                // Mẻ trùng → màu chữ đỏ cột C (Mẻ)
                if (row.IsTrungMeThoi == true)
                    ws.Cell(r, 3).Style.Font.FontColor = ClosedXML.Excel.XLColor.Red;
            }

            // ===== DÒNG TỔNG =====
            int sumRow = startRow + rows.Count;

            // Merge A-J (cột 1-10): nhãn "Tổng" in đậm
            var labelRange = ws.Range(sumRow, 1, sumRow, 9);
            labelRange.Merge();
            labelRange.FirstCell().Value = "Tổng";
            labelRange.Style.Font.Bold   = true;
            labelRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

            var sumRange = ws.Range(sumRow, 10, sumRow, 10);
            sumRange.Merge();
            sumRange.FirstCell().Value = (double)sumKlThepLong;
            sumRange.Style.Font.Bold   = true;
            sumRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;

            // Merge K-M (cột 11-13): giá trị tổng KL thép lỏng
            var tmpRange = ws.Range(sumRow, 11, sumRow, 13);
            tmpRange.Merge();
            tmpRange.FirstCell().Value = "";
            tmpRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
        }

        private string BuildSignatureHtml(List<dataproduct.api.DTOs.CTD_Dto.PheDuyetDto> list)
        {
            if (list.Count == 0)
                return "<div style=\"min-height:80px\"></div>";

            var sb = new StringBuilder();
            foreach (var p in list)
            {
                var chuKyHtml = _pheDuyetService.FormatChuKy(p.ChuKy);
                sb.Append($@"
                <div style=""display:inline-block;text-align:center;min-width:120px;margin:0 16px;"">
                  <div style=""font-weight:bold;"">{p.HoVaTen ?? ""}</div>
                  {(string.IsNullOrWhiteSpace(chuKyHtml) ? "<div style=\"min-height:60px\"></div>" : chuKyHtml)}
                </div>");
            }
            return sb.ToString();
        }

        private async Task<List<string>> ExecuteGetMeThoiAsync(
            string procedureName,
            DateOnly ngaySX,
            int ca,
            int nhaMay,
            int? idLoThoi = null)
        {
            var result = new List<string>();

            await using var connection = new SqlConnection(_gangLongConnStr);
            await connection.OpenAsync();

            try
            {
                using var command = new SqlCommand(procedureName, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;

                command.Parameters.Add("@Ngay", SqlDbType.Date).Value = ngaySX.ToDateTime(TimeOnly.MinValue);
                command.Parameters.Add("@Ca", SqlDbType.Int).Value = ca;
                command.Parameters.Add("@NhaMay", SqlDbType.Int).Value = nhaMay;
                if (idLoThoi.HasValue)
                    command.Parameters.Add("@ID_LoThoi", SqlDbType.Int).Value = idLoThoi.Value;

                await using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        result.Add(reader.GetString(0)); 
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error executing {procedureName}: {ex.Message}", ex);
            }
        }
    }
}
