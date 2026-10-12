using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace dataproduct.api.Services
{
    public class DLNMHRC2Service
    {
        private readonly IDLNMHRC2Repository _repo;
        private readonly HRC2_NMSyncService _hrc2NMSyncService;
        private readonly ISTD_NXT_HRC2Repository _stdNxtRepo;
        private readonly ProductFormContext _context;

        // Debounce sync: mỗi key (ngaySX_ca_loaiBM_scope) chỉ sync tối đa 1 lần / 2 phút.
        // Dùng static để giữ state qua các request trong cùng process.
        private static readonly ConcurrentDictionary<string, DateTime> _lastSyncTimes = new();
        private static readonly TimeSpan SyncCooldown = TimeSpan.FromMinutes(2);

        private static bool ShouldSync(SyncFromNM_HRC2_Request req)
        {
            var key = $"{req.NgaySX:yyyy-MM-dd}_{req.Ca}_{req.LoaiBM}_{req.Scope}";
            var now = DateTime.UtcNow;
            if (_lastSyncTimes.TryGetValue(key, out var last) && now - last < SyncCooldown)
                return false;
            _lastSyncTimes[key] = now;
            return true;
        }

        // Debounce riêng cho STD_NXT: sync này gộp toàn bộ BOF/LF/RH + mọi Scope cho 1 Ngày/Ca
        // (nặng hơn sync theo từng BM/Scope) nên dùng cooldown dài hơn: tối đa 1 lần / 5 phút.
        private static readonly ConcurrentDictionary<string, DateTime> _lastStdNxtSyncTimes = new();
        private static readonly TimeSpan StdNxtSyncCooldown = TimeSpan.FromMinutes(5);

        private static bool ShouldSyncStdNxt(DateTime ngaySX, int ca)
        {
            var key = $"{ngaySX:yyyy-MM-dd}_{ca}";
            var now = DateTime.UtcNow;
            if (_lastStdNxtSyncTimes.TryGetValue(key, out var last) && now - last < StdNxtSyncCooldown)
                return false;
            _lastStdNxtSyncTimes[key] = now;
            return true;
        }

        public DLNMHRC2Service(
            IDLNMHRC2Repository repo,
            HRC2_NMSyncService hrc2NMSyncService,
            ISTD_NXT_HRC2Repository stdNxtRepo,
            ProductFormContext context)
        {
            _repo = repo;
            _hrc2NMSyncService = hrc2NMSyncService;
            _stdNxtRepo = stdNxtRepo;
            _context = context;
        }

        // =========================================================
        // HRC2: Lưu phụ liệu manual + manual_col từ formData phiếu
        // =========================================================
        public async Task SaveHRC2ManualFromPhieuFormAsync(JsonElement formData)
        {
            var (models, manualColHeaderKeyIds) = await BuildModelToInsert(formData);
            await SaveHRC2ManualDataAsync(models, manualColHeaderKeyIds);
        }

        private double? TryGetDouble(JsonElement row, string key)
        {
            if (row.TryGetProperty(key, out var p))
                return TryConvertNumeric(p);
            return null;
        }

        private double? TryConvertNumeric(JsonElement val)
        {
            if (val.ValueKind == JsonValueKind.Number)
                return val.GetDouble();

            if (val.ValueKind == JsonValueKind.String &&
                double.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;

            return null;
        }

        private int? TryGetInt(JsonElement row, string key)
        {
            var d = TryGetDouble(row, key);
            if (!d.HasValue) return null;
            return (int)d.Value;
        }

        public async Task<(List<HRC2InsertModel> Models, HashSet<int> ManualColHeaderKeyIds)> BuildModelToInsert(JsonElement formData)
        {
            var result = new List<HRC2InsertModel>();

            string bm = formData.GetProperty("maBm").GetString();
            int scope = formData.GetProperty("scope").GetInt32();
            int ca = formData.GetProperty("ca").GetInt32();

            string ngaySXstr = formData.TryGetProperty("NgaySX", out var nsxProp)
                ? nsxProp.GetString() : null;
            DateTime ngaySX = !string.IsNullOrEmpty(ngaySXstr)
                ? DateTime.Parse(ngaySXstr) : DateTime.Now;

            var bmColumnMap = new Dictionary<string, List<string>>
            {
                { "HRC2_BB_NauLuyen_BOF", new List<string> { "BOF_PhuGia", "others", "adjust" } },
                { "HRC2_BB_NauLuyen_LF",  new List<string> { "PG", "KL", "others", "adjust" } },
                { "HRC2_BB_NauLuyen_RH",  new List<string> { "PG", "KL", "others", "adjust" } }
            };

            if (!bmColumnMap.TryGetValue(bm, out var colGroups))
                return (result, new HashSet<int>());

            string loaiBM = bm switch
            {
                "HRC2_BB_NauLuyen_BOF" => "BOF",
                "HRC2_BB_NauLuyen_LF" => "LF",
                _ => "RH"
            };

            var dynamicRoot = formData.GetProperty("table1DynamicColumns");
            var dynamicCols = colGroups
                .Where(g => dynamicRoot.TryGetProperty(g, out _))
                .SelectMany(g => dynamicRoot.GetProperty(g).EnumerateArray())
                .ToList();

            var table1 = formData.GetProperty("table1").EnumerateArray().ToList();

            // -------------------------------------------------------
            // Thu thập manual_col_* từ cả dynamic meta lẫn row payload
            // (FE có thể không lưu meta manual_col_{id} do phân bổ vào json phiếu,
            // nhưng vẫn gửi value trong row khi user sửa để BE lưu.)
            // -------------------------------------------------------
            var manualColIds = new HashSet<int>();

            foreach (var col in dynamicCols)
            {
                var di = col.GetProperty("dataIndex").GetString();
                if (di != null && di.StartsWith("manual_col_"))
                {
                    if (int.TryParse(di.Substring("manual_col_".Length), out var id))
                        manualColIds.Add(id);
                }
            }

            var manualColHeaderKeyIds = new HashSet<int>(manualColIds);

            var headerKeyLabelMap = manualColIds.Count > 0
                ? await _context.Header_Keys
                    .Where(k => manualColIds.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id, k => k.TenHienThi)
                : new Dictionary<int, string>();

            foreach (var row in table1)
            {
                bool isNMRow = !row.TryGetProperty("IsNM", out var flag)
                               || flag.ValueKind != JsonValueKind.False;

                if (!row.TryGetProperty("id", out var idProp) && isNMRow) continue;
                int? rowId = idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : null;
                if (isNMRow && (rowId == null || rowId <= 0)) continue;

                string meThoi = row.TryGetProperty("meThoi", out var mtp) ? mtp.GetString() : null;
                if (isNMRow && string.IsNullOrEmpty(meThoi)) continue;

                // Xử lý dynamic columns — dùng chung cho cả 2 loại row
                var phuLieus = BuildPhuLieus(
                    row, dynamicCols, meThoi, loaiBM,
                    isNMRow, headerKeyLabelMap
                );

                if (isNMRow)
                {
                    var nmKlThepPhe = TryGetDouble(row, "klThepPhe");
                    var nmGhiChu = row.TryGetProperty("ghiChu", out var nmGcProp) && nmGcProp.ValueKind == JsonValueKind.String
                        ? nmGcProp.GetString() : null;
                    var nmMacThep = row.TryGetProperty("macThep", out var mct) ? mct.GetString() : null;

                    // macThep__IsManual do FE tự set (CustomTableHRC.applyAndEmitCellChange) mỗi khi user
                    // sửa ô macThep trên dòng NM — true/false = so sánh thật với baseline, không có nghĩa
                    // là "chưa từng sửa". Field vắng mặt hoàn toàn (null) mới là "FE không gửi thông tin gì"
                    // (BM chưa bật allowEditMacThepOnNMRow, hoặc FE cũ) → không đụng override đã lưu.
                    bool? isManualMacThep = row.TryGetProperty("macThep__IsManual", out var macThepFlagProp)
                        ? macThepFlagProp.ValueKind == JsonValueKind.True
                        : (bool?)null;

                    // nmMacThep hầu như luôn có giá trị (map sẵn từ NM) nên điều kiện này gần như
                    // không bao giờ skip trong thực tế — chỉ để không âm thầm bỏ mất 1 dòng có sửa tay.
                    if (!phuLieus.Any() && nmKlThepPhe == null && nmGhiChu == null && nmMacThep == null) continue;

                    result.Add(new HRC2InsertModel
                    {
                        Id = rowId,
                        Ngay = ngaySX,
                        Ca = ca,
                        BieuMau = loaiBM,
                        Scope = scope,
                        MeThoi = meThoi,
                        MacThep = nmMacThep,
                        IsManualMacThep = isManualMacThep,
                        IsNM = true,
                        IsChuyenCa = false,
                        KLThepPhe = nmKlThepPhe,
                        GhiChu = nmGhiChu,
                        RowKey = Guid.NewGuid(),
                        hRC2_PhuLieus = phuLieus
                    });
                }
                else
                {
                    var model = new HRC2InsertModel
                    {
                        Id = row.TryGetProperty("id", out var idRow) ? idRow.GetInt32() : null,
                        Ngay = ngaySX,
                        Ca = ca,
                        BieuMau = loaiBM,
                        Scope = scope,
                        MeThoi = meThoi,
                        MacThep = row.TryGetProperty("macThep", out var mt) ? mt.GetString() : null,
                        O2 = TryGetDouble(row, "o2"),
                        N2 = TryGetDouble(row, "n2"),
                        AR_RH = TryGetDouble(row, "ar_RH"),
                        AR_LF = TryGetDouble(row, "ar_LF"),
                        AR_BOF = TryGetDouble(row, "ar_BOF"),
                        KLGangLong = TryGetDouble(row, "klGangLong"),
                        KLThepPhe = TryGetDouble(row, "klThepPhe"),
                        KlThepLong = TryGetDouble(row, "klThepLong"),
                        QueLayMau = TryGetInt(row, "queLayMau"),
                        QueDoNhiet = TryGetInt(row, "queDoNhiet"),
                        GhiChu = row.TryGetProperty("ghiChu", out var gcProp) && gcProp.ValueKind == JsonValueKind.String
                            ? gcProp.GetString()
                            : null,
                        IsNM = false,
                        IsChuyenCa = false,
                        hRC2_PhuLieus = phuLieus
                    };
                    result.Add(model);
                }
            }

            return (result, manualColHeaderKeyIds);
        }

        private List<HRC2_PhuLieuInSertModel> BuildPhuLieus(
            JsonElement row,
            List<JsonElement> dynamicCols,
            string meThoi,
            string loaiBM,
            bool isNMRow,
            Dictionary<int, string> headerKeyLabelMap)
        {
            var result = new List<HRC2_PhuLieuInSertModel>();
            var processedManualCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int? rowId = row.TryGetProperty("id", out var rowIdProp) && rowIdProp.ValueKind == JsonValueKind.Number
                ? rowIdProp.GetInt32()
                : null;

            foreach (var col in dynamicCols)
            {
                string dataIndex = col.GetProperty("dataIndex").GetString();

                bool isAdjustColumn = dataIndex.StartsWith("adjust_") && dataIndex.EndsWith("_adjust");
                bool isManualAddedAdjust = dataIndex.StartsWith("manual_col_");

                if (isAdjustColumn && !isManualAddedAdjust) continue;

                double? currentNumeric;
                if (isManualAddedAdjust)
                {
                    currentNumeric = TryResolveManualAdjustValue(row, rowId, meThoi, col, dataIndex);
                }
                else
                {
                    if (!row.TryGetProperty(dataIndex, out var valProp)) continue;
                    currentNumeric = TryConvertNumeric(valProp);
                }

                string label = col.TryGetProperty("label", out var lblProp) ? lblProp.GetString() : null;

                int? headerKeyId = col.TryGetProperty("headerKeyId", out var hkProp)
                                   && hkProp.ValueKind == JsonValueKind.Number
                    ? hkProp.GetInt32() : null;

                if (!headerKeyId.HasValue && isManualAddedAdjust)
                {
                    var suffix = dataIndex.Substring("manual_col_".Length);
                    if (int.TryParse(suffix, out var parsedId))
                        headerKeyId = parsedId;
                }

                // manual_col_*
                if (isManualAddedAdjust)
                {
                    if (!currentNumeric.HasValue) continue;
                    // isNMRow: cho phép 0; isNMRow=false: bỏ qua nếu = 0
                    if (!isNMRow && currentNumeric.Value == 0) continue;

                    if (headerKeyId.HasValue && string.IsNullOrEmpty(label))
                        headerKeyLabelMap.TryGetValue(headerKeyId.Value, out label);

                    result.Add(new HRC2_PhuLieuInSertModel
                    {
                        MeThoi = meThoi,
                        BieuMau = loaiBM,
                        ID_HeaderKey = headerKeyId,
                        TenHienThi = label,
                        // manual_col_* là "điều chỉnh tay", KHÔNG phải "phân bổ"
                        IsPhanBo = false,
                        IsManual = true,
                        KLPhuGia_Manual = currentNumeric,
                        IsAddManual = true
                    });
                    processedManualCols.Add(dataIndex);
                    continue;
                }

                // Cột thường:
                // - Ưu tiên marker FE: {dataIndex}__IsManual (có thể xảy ra trường hợp __orig rỗng do record gốc không tồn tại)
                // - Nếu không có marker hoặc marker không đảm bảo, fallback suy ra dựa trên __orig
                var origKey = $"{dataIndex}__orig";
                bool hasOrig = row.TryGetProperty(origKey, out var origProp);
                var origNumeric = hasOrig ? TryConvertNumeric(origProp) : null;

                var manualFlagKey = $"{dataIndex}__IsManual";
                bool isManualFromFlag = row.TryGetProperty(manualFlagKey, out var manualFlagProp) &&
                                        manualFlagProp.ValueKind == JsonValueKind.True;

                bool isManualFromOrig =
                    hasOrig
                    && origNumeric.HasValue
                    && currentNumeric.HasValue
                    && Math.Abs(currentNumeric.Value - origNumeric.Value) > 0.000001;

                bool isManual = isManualFromFlag || isManualFromOrig;

                // Luồng bạn yêu cầu:
                // - Nếu FE chỉ đánh dấu manual bằng {dataIndex}__IsManual=true nhưng __orig rỗng/không parse được,
                //   thì coi đây giống "manual_col_*": KLPhuGia (tự động) sẽ để null,
                //   chỉ set KLPhuGia_Manual và IsManual.
                // - Nếu manual suy ra từ so sánh __orig (có origNumeric) thì giữ baseline KLPhuGia = origNumeric (như cũ).
                double? klPhuGia;
                double? klPhuGia_Manual;
                if (!isManual)
                {
                    klPhuGia = currentNumeric;
                    klPhuGia_Manual = null;
                }
                else if (isManualFromOrig)
                {
                    klPhuGia = origNumeric; // baseline từ __orig
                    klPhuGia_Manual = currentNumeric;
                }
                else if (origNumeric.HasValue)
                {
                    // TH2: user xóa giá trị (currentNumeric=null), nhưng __orig có dữ liệu
                    // → hoàn tác về gốc: KLPhuGia giữ nguyên, KLPhuGia_Manual = null, IsManual = true
                    klPhuGia = origNumeric;
                    klPhuGia_Manual = null;
                }
                else
                {
                    // manual theo flag nhưng không có __orig → giống manual_col (không có baseline)
                    klPhuGia = null;
                    klPhuGia_Manual = currentNumeric;
                }

                // Chỉ skip khi không phải manual — record manual có thể cần dọn dẹp record cũ trong DB
                if (!isManual
                    && (klPhuGia == null || klPhuGia == 0)
                    && (klPhuGia_Manual == null || klPhuGia_Manual == 0)) continue;

                int? idPhuLieu = null;
                string tenPhuLieu = null;
                if (col.TryGetProperty("mappingPayload", out var mp) && mp.ValueKind != JsonValueKind.Null)
                {
                    if (mp.TryGetProperty("idPhuLieu", out var idp) && idp.ValueKind == JsonValueKind.Number)
                        idPhuLieu = idp.GetInt32();
                    tenPhuLieu = mp.TryGetProperty("tenPhuLieu", out var tnp) ? tnp.GetString() : null;
                }

                result.Add(new HRC2_PhuLieuInSertModel
                {
                    MeThoi = meThoi,
                    BieuMau = loaiBM,
                    ID_PhuLieu = idPhuLieu,
                    TenPhuLieu = tenPhuLieu,
                    KLPhuGia = klPhuGia,
                    ID_HeaderKey = headerKeyId,
                    TenHienThi = label,
                    IsPhanBo = false,
                    IsManual = isManual,
                    KLPhuGia_Manual = klPhuGia_Manual,
                    IsAddManual = false
                });
            }

            return result;
        }

        private double? TryResolveManualAdjustValue(
            JsonElement row,
            int? rowId,
            string meThoi,
            JsonElement dynamicCol,
            string dataIndex)
        {
            // Backward compatibility: FE cũ vẫn có thể gửi manual_col_* trực tiếp trong row.
            if (row.TryGetProperty(dataIndex, out var rowVal))
            {
                var rowNumeric = TryConvertNumeric(rowVal);
                if (rowNumeric.HasValue)
                    return rowNumeric;
            }

            // FE mới: giá trị nằm trong dynamicCol.values theo từng row.
            if (!dynamicCol.TryGetProperty("values", out var valuesProp) ||
                valuesProp.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var item in valuesProp.EnumerateArray())
            {
                var itemRowId = item.TryGetProperty("rowId", out var ridProp) &&
                                ridProp.ValueKind == JsonValueKind.Number
                    ? ridProp.GetInt32()
                    : (int?)null;

                var itemMeThoi = item.TryGetProperty("meThoi", out var mtProp) &&
                                 mtProp.ValueKind == JsonValueKind.String
                    ? mtProp.GetString()
                    : null;

                var matchByRowId = rowId.HasValue && itemRowId.HasValue && rowId.Value == itemRowId.Value;
                var matchByMeThoi = !string.IsNullOrWhiteSpace(meThoi) &&
                                    !string.IsNullOrWhiteSpace(itemMeThoi) &&
                                    string.Equals(meThoi, itemMeThoi, StringComparison.OrdinalIgnoreCase);

                if (!matchByRowId && !matchByMeThoi)
                    continue;

                if (!item.TryGetProperty("value", out var valueProp))
                    return null;

                var val = TryConvertNumeric(valueProp);
                if (val.HasValue)
                    return val;
            }

            return null;
        }

        public async Task SaveHRC2ManualDataAsync(List<HRC2InsertModel> models, HashSet<int> manualColHeaderKeyIds)
        {
            if (models == null || !models.Any()) return;

            var dlnmMap = new Dictionary<Guid, DLNM_HRC2>();

            // Preload tất cả mẻ thoi liên quan 1 lần
            var allMeThois = models.Select(m => m.MeThoi).Distinct().ToList();
            var allBieuMaus = models.Select(m => m.BieuMau).Distinct().ToList();
            var allIds = models.Where(m => m.Id > 0).Select(m => m.Id.Value).ToList();

            var existingDLNMs = await _context.DLNM_HRC2s
                .Where(x => x.IsDelete != true &&
                         (allIds.Contains(x.ID)
                         || (allMeThois.Contains(x.MeThoi) && allBieuMaus.Contains(x.BieuMau))))
                .ToListAsync();

            // Thu thập MeThoi cũ bị thay đổi trước khi loop ghi đè existing.MeThoi
            var oldMeThoiChanges = new HashSet<(string MeThoi, string BieuMau)>();
            foreach (var model in models)
            {
                if (model.Id <= 0) continue;
                var ex = existingDLNMs.FirstOrDefault(x => x.ID == model.Id);
                if (ex == null || ex.IsNM == true) continue;
                if (!string.IsNullOrEmpty(ex.MeThoi)
                    && !string.Equals(ex.MeThoi, model.MeThoi, StringComparison.OrdinalIgnoreCase))
                    oldMeThoiChanges.Add((ex.MeThoi, ex.BieuMau ?? model.BieuMau));
            }

            // -------------------------------------------------------
            // UPSERT DLNM_HRC2
            // -------------------------------------------------------
            foreach (var model in models)
            {
                var existing = model.Id > 0
                    ? existingDLNMs.FirstOrDefault(x => x.ID == model.Id)
                    : null;

                // IsNM=true: chỉ cho phép sửa KLThepPhe, GhiChu và MacThep (qua override), không sửa
                // các field NM khác.
                // MacThep: FE (TaoPhieuLF/TaoPhieuBOF, allowEditMacThepOnNMRow) cho sửa tay trên dòng
                // NM. KHÔNG ghi đè thẳng existing.MacThep (đó là giá trị GỐC đồng bộ từ NM, cần giữ để
                // so sánh/audit) — lưu riêng vào MacThep_Manual + IsManualMacThep, mirror pattern
                // KLPhuGia/KLPhuGia_Manual/IsManual của PhuLieu_HRC2. Các chỗ đọc để hiển thị
                // (PhieuDetailExcelService export Excel/PDF, DLNMHRC2Repository search-thongke) đọc
                // giá trị hiệu lực = MacThep_Manual ?? MacThep.
                if (existing?.IsNM == true)
                {
                    if (model.KLThepPhe.HasValue)
                        existing.KLThepPhe = model.KLThepPhe;
                    if (model.GhiChu != null)
                        existing.GhiChu = model.GhiChu;
                    if (model.IsManualMacThep == true)
                    {
                        existing.MacThep_Manual = model.MacThep;
                        existing.IsManualMacThep = true;
                    }
                    else if (model.IsManualMacThep == false)
                    {
                        // User sửa lại đúng bằng giá trị gốc → bỏ override, không còn lệch gốc/sửa.
                        existing.MacThep_Manual = null;
                        existing.IsManualMacThep = false;
                    }
                    // model.IsManualMacThep == null: FE không gửi flag (BM chưa bật sửa macThep, hoặc
                    // FE cũ) → giữ nguyên override đã lưu trước đó, không đụng vào.
                    if (model.KLThepPhe.HasValue || model.GhiChu != null || model.IsManualMacThep.HasValue)
                        _context.DLNM_HRC2s.Update(existing);
                    // Record đã được con người xem lại qua phiếu (Hiệu chỉnh) → coi như đã xử lý,
                    // không còn bị loại khỏi Thống kê/Phân bổ nữa.
                    existing.IsSyncSauGui = false;
                    dlnmMap[model.RowKey] = existing;
                    continue;
                }

                if (existing == null && model.Id > 0) continue;

                // Check trùng mẻ thoi — dùng preloaded data
                var sameMeThoi = existingDLNMs
                    .Where(x => x.MeThoi == model.MeThoi && x.BieuMau == model.BieuMau)
                    .ToList();

                bool isTrung = existing == null
                    ? sameMeThoi.Any()
                    : sameMeThoi.Any(x => x.ID != existing.ID);

                // Chỉ update flag nếu thay đổi — tránh update thừa
                foreach (var item in sameMeThoi.Where(x => x.IsTrungMeThoi != isTrung))
                {
                    item.IsTrungMeThoi = isTrung;
                    _context.DLNM_HRC2s.Update(item);
                }

                DLNM_HRC2 dlnm;
                if (existing == null)
                {
                    dlnm = new DLNM_HRC2
                    {
                        NgaySx = model.Ngay,
                        Ngay = model.Ngay,
                        Ca = model.Ca,
                        BieuMau = model.BieuMau,
                        Scope = model.Scope,
                        MeThoi = model.MeThoi,
                        MacThep = model.MacThep,
                        O2 = model.O2,
                        N2 = model.N2,
                        AR_RH = model.AR_RH,
                        AR_LF = model.AR_LF,
                        AR_BOF = model.AR_BOF,
                        KLGangLong = model.KLGangLong,
                        KLThepPhe = model.KLThepPhe,
                        KLThepLong = model.KlThepLong,
                        QueLayMau = model.QueLayMau,
                        QueDoNhiet = model.QueDoNhiet,
                        GhiChu = model.GhiChu,
                        IsNM = false,
                        IsChuyenCa = model.IsChuyenCa,
                        IsTrungMeThoi = isTrung,
                        TempKey = Guid.NewGuid()
                    };
                    await _context.DLNM_HRC2s.AddAsync(dlnm);
                }
                else
                {
                    existing.MeThoi = model.MeThoi;
                    existing.MacThep = model.MacThep;
                    existing.O2 = model.O2;
                    existing.N2 = model.N2;
                    existing.AR_RH = model.AR_RH;
                    existing.AR_LF = model.AR_LF;
                    existing.AR_BOF = model.AR_BOF;
                    existing.KLGangLong = model.KLGangLong;
                    existing.KLThepPhe = model.KLThepPhe;
                    existing.KLThepLong = model.KlThepLong;
                    existing.QueLayMau = model.QueLayMau;
                    existing.QueDoNhiet = model.QueDoNhiet;
                    existing.GhiChu = model.GhiChu;
                    existing.IsChuyenCa = model.IsChuyenCa;
                    existing.NgaySx = model.Ngay;
                    existing.IsTrungMeThoi = isTrung;
                    // Record đã được con người xem lại qua phiếu (Hiệu chỉnh) → coi như đã xử lý,
                    // không còn bị loại khỏi Thống kê/Phân bổ nữa.
                    existing.IsSyncSauGui = false;
                    _context.DLNM_HRC2s.Update(existing);
                    dlnm = existing;
                }

                dlnmMap[model.RowKey] = dlnm;
            }

            await _context.SaveChangesAsync();

            // Recheck IsTrungMeThoi cho các MeThoi cũ bị đổi
            // Nếu còn < 2 record không bị xóa → reset IsTrungMeThoi = false
            if (oldMeThoiChanges.Any())
            {
                foreach (var (oldMeThoi, bieuMau) in oldMeThoiChanges)
                {
                    var oldSiblings = await _context.DLNM_HRC2s
                        .Where(x => x.MeThoi == oldMeThoi
                                 && x.BieuMau == bieuMau
                                 && x.IsDelete != true)
                        .ToListAsync();

                    if (oldSiblings.Count < 2)
                    {
                        foreach (var s in oldSiblings.Where(s => s.IsTrungMeThoi == true))
                            s.IsTrungMeThoi = false;
                    }
                }
                await _context.SaveChangesAsync();
            }

            // -------------------------------------------------------
            // UPSERT PhuLieu_HRC2
            // -------------------------------------------------------
            var dlnmIds = dlnmMap.Values.Select(x => x.ID).Distinct().ToList();
            var existingPL = await _context.PhuLieu_HRC2s
                .Where(x => dlnmIds.Contains(x.ID_MeThoi))
                .ToListAsync();

            foreach (var model in models)
            {
                if (!dlnmMap.TryGetValue(model.RowKey, out var dlnm)) continue;

                foreach (var pl in model.hRC2_PhuLieus)
                {
                    // manual_col_* (điều chỉnh tay)
                    var isManualAdjustRow =
                        (pl.IsManual == true) &&
                        (pl.IsAddManual == true) &&
                        (pl.IsPhanBo != true) &&
                        pl.ID_HeaderKey.HasValue &&
                        !pl.ID_PhuLieu.HasValue;

                    // Với điều chỉnh tay: match theo (ID_MeThoi, ID_HeaderKey, ID_PhuLieu=null, IsManual=true), bỏ qua IsPhanBo
                    // để tránh duplicate và migrate các bản ghi cũ (từng lưu sai IsPhanBo=true) về IsPhanBo=false.
                    PhuLieu_HRC2? existing = null;
                    if (isManualAdjustRow)
                    {
                        existing = existingPL
                            .Where(x =>
                                x.ID_MeThoi == dlnm.ID &&
                                x.ID_PhuLieu == null &&
                                x.IsManual == true &&
                                x.IsAddManual == true &&
                                // Match đúng HeaderKey; chỉ fallback bản ghi cũ chưa có HeaderKey.
                                (x.ID_HeaderKey == pl.ID_HeaderKey || x.ID_HeaderKey == null))
                            .OrderBy(x => x.ID_HeaderKey == pl.ID_HeaderKey ? 0 : 1)
                            .ThenBy(x => x.IsPhanBo == true ? 1 : 0)
                            .FirstOrDefault();

                        // Backfill cho dữ liệu cũ: nếu record manual cũ chưa có HeaderKey thì gán lại.
                        if (existing != null && !existing.ID_HeaderKey.HasValue && pl.ID_HeaderKey.HasValue)
                        {
                            existing.ID_HeaderKey = pl.ID_HeaderKey;
                        }
                    }

                    existing ??= existingPL.FirstOrDefault(x =>
                        x.ID_MeThoi == dlnm.ID &&
                        x.ID_HeaderKey == pl.ID_HeaderKey &&
                        x.ID_PhuLieu == pl.ID_PhuLieu &&
                        x.IsPhanBo == (pl.IsPhanBo ?? false));

                    // Với phụ liệu mapped (phuLieu_*) có ID_PhuLieu:
                    // ưu tiên update record gốc theo (ID_MeThoi, ID_PhuLieu, IsPhanBo=false),
                    // không tạo thêm record chỉ vì khác ID_HeaderKey/IsManual.
                    if (existing == null &&
                        pl.ID_PhuLieu.HasValue &&
                        !(pl.IsPhanBo ?? false))
                    {
                        existing = existingPL.FirstOrDefault(x =>
                            x.ID_MeThoi == dlnm.ID &&
                            x.ID_PhuLieu == pl.ID_PhuLieu &&
                            x.IsPhanBo == false);
                    }

                    // Fallback cho NM row: sp_Sync_HRC2_FromNM có thể tạo record không có ID_HeaderKey.
                    // Thử tìm theo ID_PhuLieu (bỏ qua ID_HeaderKey), sau đó backfill ID_HeaderKey.
                    // Hạn chế cho dòng NM gốc: nếu là phụ liệu tự động (IsManual!=true, IsPhanBo=false) và chưa có record,
                    // thì KHÔNG tạo mới mà bỏ qua (chỉ dùng record do stored procedure sync).
                    if (existing == null &&
                        dlnm.IsNM == true &&
                        !(pl.IsPhanBo ?? false) &&
                        pl.IsManual != true)
                    {
                        if (pl.ID_PhuLieu.HasValue)
                        {
                            existing = existingPL.FirstOrDefault(x =>
                                x.ID_MeThoi == dlnm.ID &&
                                x.ID_PhuLieu == pl.ID_PhuLieu &&
                                x.IsPhanBo == false);
                            // Backfill ID_HeaderKey nếu chưa được set bởi stored procedure
                            if (existing != null && existing.ID_HeaderKey == null && pl.ID_HeaderKey.HasValue)
                                existing.ID_HeaderKey = pl.ID_HeaderKey;
                        }
                        // Nếu vẫn không tìm được → không có record gốc từ NM → bỏ qua
                        if (existing == null) continue;
                    }

                    // "Xóa manual về ban đầu": isManual=true nhưng không có giá trị nào để lưu
                    // (KLPhuGia_Manual=null VÀ KLPhuGia=null/0 → orig của record cũng null/0)
                    // → DELETE record nếu tồn tại, bỏ qua nếu chưa có
                    bool isClearManual = pl.IsManual == true
                        && pl.KLPhuGia_Manual == null
                        && (pl.KLPhuGia == null || pl.KLPhuGia == 0);

                    if (isClearManual)
                    {
                        if (existing != null && (existing.KLPhuGia == null || existing.KLPhuGia == 0))
                        {
                            _context.PhuLieu_HRC2s.Remove(existing);
                            existingPL.Remove(existing);
                        }
                        else if (existing != null)
                        {
                            // Record có KLPhuGia thật (từ NM/SP) → chỉ reset manual, không xóa
                            existing.IsManual = true;
                            existing.KLPhuGia_Manual = null;
                        }
                        continue;
                    }

                    if (existing == null)
                    {
                        // Không sinh mới cho phụ liệu mapped khi đang lưu sửa tay;
                        // chỉ update trên record đã có để tránh duplicate.
                        // Tuy nhiên, nếu FE gửi manual marker cho phụ liệu mapped (phuLieu_*),
                        // mà record gốc chưa tồn tại (do UI tổng hợp theo mẻ), thì cần INSERT mới để lưu đúng.
                        if (pl.ID_PhuLieu.HasValue && !(pl.IsPhanBo ?? false) && pl.IsManual != true)
                        {
                            continue;
                        }

                        var newEntity = new PhuLieu_HRC2
                        {
                            REPORT_NO = dlnm.REPORT_NO,
                            BieuMau = model.BieuMau,
                            MeThoi = model.MeThoi,
                            ID_PhuLieu = pl.ID_PhuLieu,
                            TenPhuLieu = pl.TenPhuLieu,
                            KLPhuGia = pl.KLPhuGia,
                            KLPhuGia_Manual = pl.KLPhuGia_Manual,
                            IsManual = pl.IsManual ?? false,
                            IsAddManual = pl.IsAddManual ?? false,
                            ID_HeaderKey = pl.ID_HeaderKey,
                            TenHienThi = pl.TenHienThi,
                            ID_MeThoi = dlnm.ID,
                            IsPhanBo = pl.IsPhanBo ?? false
                        };

                        await _context.PhuLieu_HRC2s.AddAsync(newEntity);
                        // Cập nhật cache in-memory để các item sau trong cùng request không bị add trùng.
                        existingPL.Add(newEntity);
                    }
                    else
                    {
                        // KLPhuGia (orig) KHÔNG BAO GIỜ bị ghi đè trên record đã tồn tại (spec invariant)
                        existing.IsPhanBo = pl.IsPhanBo ?? false;
                        if (pl.ID_HeaderKey.HasValue)
                            existing.ID_HeaderKey = pl.ID_HeaderKey;
                        if (!string.IsNullOrWhiteSpace(pl.TenHienThi))
                            existing.TenHienThi = pl.TenHienThi;

                        if (pl.IsManual == true)
                        {
                            existing.IsManual = true;
                            existing.KLPhuGia_Manual = pl.KLPhuGia_Manual;
                            existing.IsAddManual = pl.IsAddManual ?? false;
                        }
                        else
                        {
                            // Payload không còn manual marker => reset về dữ liệu gốc
                            existing.IsManual = false;
                            existing.KLPhuGia_Manual = null;
                            existing.IsAddManual = false;
                        }

                        // existing được track sẵn từ query hoặc vừa Add trong cùng DbContext.
                        // Không ép Update để tránh lỗi "temporary value" khi entity đang ở state Added.
                    }
                }
            }

            await _context.SaveChangesAsync();

            // -------------------------------------------------------
            // DELETE manual_col_* removed on FE
            // -------------------------------------------------------
            var allowed = manualColHeaderKeyIds ?? new HashSet<int>();
            var toDelete = await _context.PhuLieu_HRC2s
                .Where(x =>
                    dlnmIds.Contains(x.ID_MeThoi) &&
                    // manual_col_* là "điều chỉnh tay" => IsPhanBo = false
                    x.IsPhanBo == false &&
                    x.IsManual == true &&
                    x.IsAddManual == true &&
                    // Chỉ cleanup các record kiểu manual_col_* (không có ID_PhuLieu).
                    // Tránh xóa nhầm manual của cột phụ liệu mapped (phuLieu_*) có ID_PhuLieu.
                    x.ID_PhuLieu == null &&
                    x.ID_HeaderKey.HasValue &&
                    !allowed.Contains(x.ID_HeaderKey.Value))
                .ToListAsync();
            if (toDelete.Count > 0)
            {
                _context.PhuLieu_HRC2s.RemoveRange(toDelete);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> DeleteRowNMAsync(int id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;

            existing.IsDelete = true;
            _context.DLNM_HRC2s.Update(existing);

            // Đếm số record còn lại sau soft-delete (loại chính nó và record đã xóa)
            var countAfterDelete = await _context.DLNM_HRC2s
                .CountAsync(x => x.MeThoi == existing.MeThoi
                             && x.BieuMau == existing.BieuMau
                             && x.ID != existing.ID
                             && x.IsDelete != true);

            // Nếu còn < 2 → reset IsTrungMeThoi = false cho các record còn lại
            if (countAfterDelete < 2)
            {
                var remainItems = await _context.DLNM_HRC2s
                    .Where(x => x.MeThoi == existing.MeThoi
                             && x.BieuMau == existing.BieuMau
                             && x.ID != existing.ID
                             && x.IsDelete != true)
                    .ToListAsync();

                foreach (var x in remainItems)
                    x.IsTrungMeThoi = false;
            }

            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<IEnumerable<DLNM_HRC2>> GetAllAsync(DateTime? Ngay, int? Ca, string? BieuMau, int? Scope)
        {
            return  await _repo.GetAllAsync(Ngay,Ca,BieuMau,Scope);
        }

        public async Task<DLNM_HRC2?> GetByIdAsync(int id)
        {
            var x = await _repo.GetByIdAsync(id);
            if (x == null) return null;
            return x;
            
        }

        public async Task<HRC2DetailByReportNoModel?> GetByReportNoAsync(int reportNo)
        {
            return await _repo.GetByReportNoAsync(reportNo);
        }

        // public async Task<IEnumerable<HRC2DetailByReportNoModel>> FilterAsync(SyncFromNM_HRC2_Request request)
        // {
        //     await _hrc2NMSyncService.SyncHRC2FromNMAsync(request);

        //     var allData = await _repo.GetAllAsync(request.NgaySX , request.Ca, request.LoaiBM, request.Scope);
        //     var reportNos = allData
        //         .Select(x => (int?)x.REPORT_NO)
        //         .Where(x => x.HasValue && x.Value != 0)
        //         .Select(x => x!.Value)
        //         .Distinct()
        //         .ToList();

        //     var result = new List<HRC2DetailByReportNoModel>();
        //     foreach (var reportNo in reportNos)
        //     {
        //         var detail = await _repo.GetByReportNoAsync(reportNo);
        //         if (detail != null)
        //         {
        //             result.Add(detail);
        //         }
        //     }

        //     return result;
        // }

        public async Task<IEnumerable<HRC2GroupedByReportNoModel>> FilterGroupedAsync(SyncFromNM_HRC2_Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Debounce: chỉ sync nếu chưa sync trong vòng SyncCooldown (2 phút) cho cùng key.
            // Gọi /api/DLNMHRC2/force-sync để ép sync ngay lập tức.
            if (ShouldSync(request))
                await _hrc2NMSyncService.SyncHRC2FromNMAsync(request);

            // --- TRƯỚC: sync mỗi request, N+1 query (comment lại để revert nếu cần) ---
            // await _hrc2NMSyncService.SyncHRC2FromNMAsync(request);
            // var allData = await _repo.GetAllAsync(request.NgaySX, request.Ca, request.LoaiBM, request.Scope);
            // var ids = allData
            //     .Select(x => (int?)x.ID)
            //     .Where(x => x.HasValue && x.Value != 0)
            //     .Select(x => x!.Value)
            //     .ToList();
            // var result = new List<HRC2GroupedByReportNoModel>();
            // foreach (var id in ids)
            // {
            //     var detail = await _repo.GetByIdGroupedAsync(id);
            //     if (detail != null) result.Add(detail);
            // }
            // return result;

            // --- SAU: load toàn bộ base records rồi batch trong 4 query ---
            var allData = await _repo.GetAllAsync(request.NgaySX, request.Ca, request.LoaiBM, request.Scope);
            var validRecords = allData.Where(x => x.ID != 0).ToList();
            return await _repo.GetAllGroupedBatchAsync(validRecords);
        }

        /// <summary>
        /// Force sync ngay lập tức, bỏ qua cooldown. Dùng cho nút "Đồng bộ lại" phía FE.
        /// </summary>
        public async Task ForceSyncAsync(SyncFromNM_HRC2_Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            // Reset cooldown để lần filter tiếp theo cũng không bị block
            var key = $"{request.NgaySX:yyyy-MM-dd}_{request.Ca}_{request.LoaiBM}_{request.Scope}";
            _lastSyncTimes[key] = DateTime.MinValue;
            await _hrc2NMSyncService.SyncHRC2FromNMAsync(request);
        }

        /// <summary>
        /// Force sync NM cho Sổ Xuất-Nhập-Tồn (STD_NXT), bỏ qua cooldown. Dùng cho nút
        /// "Đồng bộ lại từ NM" riêng ở FE — khác với "Làm mới" (chỉ đọc DB, xem FilterSTD_NXTAsync).
        /// </summary>
        public async Task ForceSyncStdNxtAsync(FilterSTD_NXTRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            // Reset cooldown để FilterSTD_NXTAsync gọi tiếp theo (nếu rỗng) cũng không bị chặn
            var key = $"{request.NgaySX:yyyy-MM-dd}_{request.Ca}";
            _lastStdNxtSyncTimes[key] = DateTime.MinValue;
            await _hrc2NMSyncService.SyncFromNmStoredProcAsync(request.NgaySX, request.Ca, null, null);
        }

        /// <summary>
        /// Làm mới KLGangLongCCT và KLThepPheGang cho các phiếu BOF được chọn.
        /// Từ idPhieu → tìm slot (Ngay/Ca/Scope) → load DLNM_HRC2 rows → gọi RefreshGangMetricsForRowsAsync.
        /// </summary>
        public async Task<RefreshGangMetricsResult> RefreshGangMetricsByPhieuIdsAsync(List<Guid> phieuIds)
        {
            // Chỉ lấy phiếu BOF, lấy distinct slot
            var slots = await _context.BmPhieus
                .Where(p => phieuIds.Contains(p.Idphieu) && p.MaBm == "HRC2_BB_NauLuyen_BOF")
                .Select(p => new { p.NgaySX, p.Ca, p.Scope })
                .Distinct()
                .ToListAsync();

            int skippedPhieu = phieuIds.Count - slots.Count;

            if (slots.Count == 0)
                return new RefreshGangMetricsResult
                {
                    UpdatedRows = 0,
                    SkippedPhieu = skippedPhieu,
                    Message = "Không có phiếu HRC2_BB_NauLuyen_BOF nào trong danh sách."
                };

            // Load toàn bộ rows cho các slot, deduplicate theo ID
            var allRowsById = new Dictionary<long, DLNM_HRC2>();
            foreach (var slot in slots)
            {
                if (slot.NgaySX == null || slot.Ca == null || slot.Scope == null) continue;
                var ngay = slot.NgaySX.Value.ToDateTime(TimeOnly.MinValue);
                var slotRows = await _context.DLNM_HRC2s
                    .Where(x =>
                        // x.IsNM == true &&
                        x.IsDelete != true &&
                        x.Ngay == ngay &&
                        x.Ca == slot.Ca.Value &&
                        x.Scope == slot.Scope.Value &&
                        x.BieuMau == "BOF" &&
                        x.MeThoi != null)
                    .ToListAsync();
                foreach (var r in slotRows)
                    allRowsById[r.ID] = r;
            }

            var rows = allRowsById.Values.ToList();
            if (rows.Count == 0)
                return new RefreshGangMetricsResult
                {
                    UpdatedRows = 0,
                    SkippedPhieu = skippedPhieu,
                    Message = "Không tìm thấy dữ liệu mẻ NM nào."
                };

            int updated = await _hrc2NMSyncService.RefreshGangMetricsForRowsAsync(rows);
            return new RefreshGangMetricsResult
            {
                UpdatedRows = updated,
                SkippedPhieu = skippedPhieu,
                Message = $"Đã làm mới {updated} mẻ từ {slots.Count} slot ({phieuIds.Count} phiếu)."
            };
        }

        public async Task<DLNM_HRC2> CreateAsync(DLNM_HRC2 entity)
        {
            await _repo.AddAsync(entity);
            return entity;
        }

        public async Task<bool> UpdateAsync(int id, DLNM_HRC2 entity)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;
            entity.ID = id;
            await _repo.UpdateAsync(entity);
            return true;
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;
            await _repo.DeleteAsync(id);
            return true;
        }

        public async Task<PagedResult<DLNM_HRC2>> SearchWithPagingAsync(DateTime? NgaySX, int? Ca, string? LoaiBM, int? Scope, string? searchText, int page, int pageSize)
        {
            var (data, totalCount) = await _repo.SearchWithPagingAsync(NgaySX, Ca, LoaiBM, Scope, searchText, page, pageSize);
            
            return new PagedResult<DLNM_HRC2>
            {
                Data = data.ToList(),
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        // Search grouped theo REPORT_NO (dùng cho Thống kê HRC2 ở UI)
        public async Task<PagedResult<HRC2GroupedByReportNoModel>> SearchGroupedWithPagingAsync(
            DateTime? NgaySX,
            int? Ca,
            string? LoaiBM,
            int? Scope,
            string? searchText,
            int page,
            int pageSize
        )
        {
            // Sync dữ liệu từ NM nếu đủ điều kiện bộ lọc giống API filter
            if (NgaySX.HasValue && Ca.HasValue && Scope.HasValue && !string.IsNullOrWhiteSpace(LoaiBM))
            {
                await _hrc2NMSyncService.SyncHRC2FromNMAsync(new SyncFromNM_HRC2_Request
                {
                    NgaySX = NgaySX.Value,
                    Ca = Ca.Value,
                    LoaiBM = LoaiBM!,
                    Scope = Scope.Value
                });
            }

            var (baseList, totalCount) = await _repo.SearchWithPagingAsync(NgaySX, Ca, LoaiBM, Scope, searchText, page, pageSize);
            var result = new List<HRC2GroupedByReportNoModel>();

            foreach (var item in baseList)
            {
                if (item == null) continue;
                if (item.ID == 0) continue;
                var detail = await _repo.GetByIdGroupedAsync((int)item.ID);
                if (detail != null)
                {
                    result.Add(detail);
                }
            }

            return new PagedResult<HRC2GroupedByReportNoModel>
            {
                Data = result,
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<SearchThongKeApiResponse> SearchThongKeAsync(SearchThongKe dto)
        {
            return await _repo.SearchThongKeApiAsync(dto);
        }

        public async Task<List<ThongKeSumItem>> GetThongKeSumAsync(SearchThongKe dto)
        {
            return await _repo.GetThongKeSumAsync(dto);
        }

        public async Task<bool> ChuyenMeThoiAsync(ChuyenMeThoiRequest request)
        {
            return await _repo.ChuyenMeThoiAsync(request);
        }

        public async Task<IEnumerable<FilterSTD_NXTResponse>> FilterSTD_NXTAsync(FilterSTD_NXTRequest request)
        {
            // Không còn sync NM ngay từ đầu (nặng, làm nút "Làm mới" chậm mỗi lần bấm).
            // Dữ liệu bình thường được giữ tươi bởi job đồng bộ chạy định kỳ ngoài app
            // (xem sp_Sync_HRC2_FromNM_Now trong hrc2_syncTieuHao_now.sql) — ở đây chỉ đọc thẳng DB.
            var result = (await _repo.GetHRC2GroupedByMaterialAsync(request.NgaySX, request.Ca)).ToList();

            // Fallback: chỉ khi DB hoàn toàn chưa có dữ liệu cho đúng Ngày/Ca này (job chưa từng
            // chạm tới slot này — job downtime, NM trễ, hoặc slot quá cũ từ trước khi bật job) mới
            // đồng bộ trực tiếp từ NM (nặng) rồi đọc lại — trường hợp hiếm nên chấp nhận chậm 1 lần.
            // Vẫn debounce (5 phút/lần cho cùng Ngày/Ca) để tránh spam SP nếu slot thực sự không có
            // dữ liệu (vd chọn nhầm ngày tương lai) hoặc nhiều tab cùng bấm "Làm mới" một lúc.
            if (result.Count == 0 && ShouldSyncStdNxt(request.NgaySX, request.Ca))
            {
                await _hrc2NMSyncService.SyncFromNmStoredProcAsync(request.NgaySX, request.Ca, null, null);
                result = (await _repo.GetHRC2GroupedByMaterialAsync(request.NgaySX, request.Ca)).ToList();
            }

            if (request.IdPhieu.HasValue && request.IdPhieu.Value != Guid.Empty)
            {
                // Ưu tiên dùng danh sách HeaderKeyIds từ FE (phản ánh đúng bảng đang hiển thị, kể cả dòng mới chưa lưu)
                var headerKeys = (request.HeaderKeyIds != null && request.HeaderKeyIds.Count > 0)
                    ? request.HeaderKeyIds
                        .Where(id => id > 0)
                        .Distinct()
                        .Select(id => new IdHeaderKeyModel { Id_HeaderKey = id })
                        .ToList()
                    : result
                        .Where(x => x.HeaderKeyId.HasValue && x.HeaderKeyId.Value > 0)
                        .Select(x => new IdHeaderKeyModel { Id_HeaderKey = x.HeaderKeyId!.Value })
                        .DistinctBy(x => x.Id_HeaderKey)
                        .ToList();
                if (headerKeys.Count > 0)
                {
                    await _stdNxtRepo.GetHRC2FilterInitAsync(new InitXuatNhapTonHRC2Request
                    {
                        NgaySX = request.NgaySX,
                        Ca = request.Ca,
                        IdPhieu = request.IdPhieu.Value,
                        HeaderKeys = headerKeys
                    });
                }
            }
            return result;
        }

        public async Task<ExportFileResult> ExportThongKeExcelAsync(SearchThongKe dto)
        {
            dto.Page = null;
            dto.PageSize = null;

            var result = await SearchThongKeAsync(dto);

            if (!result.Data.Any())
                throw new InvalidOperationException("Không có dữ liệu phù hợp với điều kiện lọc để xuất Excel.");

            var headers = result.PhuLieuHeaderTables;

            var loaiBmKey = (dto.LoaiBM ?? "").Trim().ToUpperInvariant();
            var templateFileName = loaiBmKey == "BOF" ? "PKH_BOF.xlsx" : "PKH_LFRH.xlsx";

            var templatePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "templates",
                templateFileName);

            if (!File.Exists(templatePath))
                throw new FileNotFoundException($"Không tìm thấy file mẫu Excel: {templatePath}");

            using var fs = new FileStream(
                templatePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);

            using var workbook = new XLWorkbook(fs);
            var ws = workbook.Worksheet(1);

            var scope = dto.Scope;

            string title = "";
            if(loaiBmKey == "BOF")
            {
                title = $"BIÊN BẢN TIÊU HAO NẤU LUYỆN LÒ THỔI {scope}".Trim();
            }
            else
            {
                if(loaiBmKey == "LF")
                    title = $"BẢNG TIÊU HAO NẤU LUYỆN LÒ TINH LUYỆN LF {scope}".Trim();
                else
                    title = $"BẢNG TIÊU HAO NẤU LUYỆN LÒ TINH LUYỆN RH {scope}".Trim();
            }
            // merge dòng 4 (ví dụ 27 cột theo layout hiện tại)
            ws.Range(4, 1, 4, 27).Merge();

            ws.Cell(4, 1).Value = title;
            ws.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(4, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            ws.Cell(4, 1).Style.Font.Bold = true;
            ws.Cell(4, 1).Style.Font.FontSize = 16;

            // ===== HEADER PHỤ LIỆU ĐỘNG + CÁC CỘT CỐ ĐỊNH THEO MẪU =====
            // BOF: phụ liệu từ cột 6, LF/RH: phụ liệu từ cột 5 (dòng 7)
            int headerRow = 7;
            int headerStartCol = loaiBmKey == "BOF" ? 9 : 8;

            // Dòng 6: tiêu đề chung cho vùng phụ liệu
            if (headers.Count > 0)
            {
                int headerEndCol = headerStartCol + headers.Count - 1;
                var phuGiaRange = ws.Range(6, headerStartCol, 6, headerEndCol);
                phuGiaRange.Merge();
                phuGiaRange.Value = "Phụ gia công nghệ (Kg)";
                phuGiaRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                phuGiaRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                phuGiaRange.Style.Alignment.WrapText = true;
                phuGiaRange.Style.Font.Bold = true;
            }

            // Dòng 7: tên từng phụ liệu
            int headerCol = headerStartCol;
            foreach (var h in headers)
            {
                ws.Cell(headerRow, headerCol).Value = h.TenPhuLieu;
                headerCol++;
            }

            // Sau các phụ liệu, bổ sung các cột cố định tùy theo loại BM
            int extraStartCol = headerStartCol + headers.Count;
            int? fuelStartCol = null;
            int? noteCol = null;
            int? scrapCol = null;
            int? gasCol = null;
            int? queLayMauCol = null;
            int? queDoNhietCol = null;
            int? noteColLfRh = null;

            if (loaiBmKey == "BOF")
            {
                // Hình 2: Nhiên liệu (m3) / Oxy / Nitơ / Ghi chú / KL thép phế trong thùng gang (tấn)
                fuelStartCol = extraStartCol;

                // Dòng 6: merge 2 cột cho "Nhiên liệu (m3)"
                ws.Range(6, fuelStartCol.Value, 6, fuelStartCol.Value + 1).Merge();
                var fuelHeader = ws.Range(6, fuelStartCol.Value, 6, fuelStartCol.Value + 1);
                fuelHeader.Merge();
                fuelHeader.Value = "Nhiên liệu (m³)";
                fuelHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                fuelHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                fuelHeader.Style.Alignment.WrapText = true;
                fuelHeader.Style.Font.Bold = true;

                // Dòng 7: "Oxy" và "Nitơ"
                ws.Cell(7, fuelStartCol.Value).Value = "Oxy";
                ws.Cell(7, fuelStartCol.Value + 1).Value = "Nitơ";

                // "Ghi chú" – merge 2 dòng 6-7
                noteCol = fuelStartCol.Value + 2;
                ws.Range(6, noteCol.Value, 7, noteCol.Value).Merge();
                var noteHeader = ws.Range(6, noteCol.Value, 7, noteCol.Value);
                noteHeader.Merge();
                noteHeader.Value = "Ghi chú";
                noteHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                noteHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                noteHeader.Style.Alignment.WrapText = true;
                noteHeader.Style.Font.Bold = true;

                // "KL thép phế trong thùng gang (tấn)" – merge 2 dòng 6-7
                scrapCol = fuelStartCol.Value + 3;
                var scrapHeader = ws.Range(6, scrapCol.Value, 7, scrapCol.Value);
                scrapHeader.Merge();
                scrapHeader.Value = "KL thép phế trong thùng gang (tấn)";
                scrapHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                scrapHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                scrapHeader.Style.Alignment.WrapText = true;
                scrapHeader.Style.Font.Bold = true;
            }
            else
            {
                // LF/RH – tương tự hình 1
                gasCol = extraStartCol;

                // Cột "Khí" (dòng 6) + "Argon (m3)" (dòng 7)
                ws.Cell(6, gasCol.Value).Value = "Khí";
                ws.Cell(7, gasCol.Value).Value = "Argon (m³)";
                ws.Range(6, gasCol.Value, 7, gasCol.Value).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(6, gasCol.Value, 7, gasCol.Value).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Range(6, gasCol.Value, 7, gasCol.Value).Style.Alignment.WrapText = true;
                ws.Range(6, gasCol.Value, 7, gasCol.Value).Style.Font.Bold = true;

                // "Que lấy mẫu (Cái)" – merge 2 dòng 6-7
                queLayMauCol = gasCol.Value + 1;
                var queLayMauHeader = ws.Range(6, queLayMauCol.Value, 7, queLayMauCol.Value);
                queLayMauHeader.Merge();
                queLayMauHeader.Value = "Que lấy mẫu (Cái)";
                queLayMauHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                queLayMauHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                queLayMauHeader.Style.Alignment.WrapText = true;
                queLayMauHeader.Style.Font.Bold = true;

                // "Que đo nhiệt (Cái)" – merge 2 dòng 6-7
                queDoNhietCol = gasCol.Value + 2;
                var queDoNhietHeader = ws.Range(6, queDoNhietCol.Value, 7, queDoNhietCol.Value);
                queDoNhietHeader.Merge();
                queDoNhietHeader.Value = "Que đo nhiệt (Cái)";
                queDoNhietHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                queDoNhietHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                queDoNhietHeader.Style.Alignment.WrapText = true;
                queDoNhietHeader.Style.Font.Bold = true;

                // "Ghi chú" – merge 2 dòng 6-7
                noteColLfRh = gasCol.Value + 3;
                var noteLfRhHeader = ws.Range(6, noteColLfRh.Value, 7, noteColLfRh.Value);
                noteLfRhHeader.Merge();
                noteLfRhHeader.Value = "Ghi chú";
                noteLfRhHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                noteLfRhHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                noteLfRhHeader.Style.Alignment.WrapText = true;
                noteLfRhHeader.Style.Font.Bold = true;
            }

            int startRow = 8;
            int currentRow = startRow;

            foreach (var row in result.Data)
            {
                var d = row.Data;
                if (d == null) continue;

                ws.Cell(currentRow, 1).Value = currentRow - startRow + 1;

                ws.Cell(currentRow, 2).Value = d.Ngay.HasValue ? d.Ngay.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(currentRow, 3).Value = d.Ca == 1 ? "Ca ngày" : d.Ca == 2 ? "Ca đêm" : "";
                ws.Cell(currentRow, 4).Value = "";

                ws.Cell(currentRow, 5).Value = d.MeThoi;
                ws.Cell(currentRow, 6).Value = d.MacThep;
                ws.Cell(currentRow, 7).Value = d.KLGangLongCCT;
                // BOF: cột KL thép phế = thép phế NM + thép phế từ gang
                ws.Cell(currentRow, 8).Value = loaiBmKey == "BOF"
                    ? (d.KLThepPhe ?? 0) + (d.KLThepPheGang ?? 0)
                    : d.KLThepPhe;

                // Values đã được align theo thứ tự headers từ SearchThongKeApiAsync
                var valueByHeaderKeyId = row.Values
                    .Where(v => v.TotalKLPhuGia.HasValue)
                    .ToDictionary(v => v.IDHeaderKey, v => v.TotalKLPhuGia!.Value);

                int colIndex = headerStartCol;

                foreach (var h in headers)
                {
                    if (valueByHeaderKeyId.TryGetValue(h.IDHeaderKey, out var value) && value != 0)
                        ws.Cell(currentRow, colIndex).Value = value;

                    colIndex++;
                }

                // ===== set value cho các cột extra sau vùng phụ liệu =====
                if (loaiBmKey == "BOF")
                {
                    // Nhiên liệu: Oxy / Nitơ
                    if (fuelStartCol.HasValue)
                    {
                        if (d.O2.HasValue) ws.Cell(currentRow, fuelStartCol.Value).Value = d.O2.Value;
                        if (d.N2.HasValue) ws.Cell(currentRow, fuelStartCol.Value + 1).Value = d.N2.Value;
                    }

                    // Ghi chú: hiện chưa có field → để trống
                    if (noteCol.HasValue)
                    {
                        // ws.Cell(currentRow, noteCol.Value).Value = d.GhiChu; // nếu sau này có field
                    }

                    // KL thép phế trong thùng gang (tấn) = thép phế NM + thép phế từ gang
                    if (scrapCol.HasValue)
                    {
                        var sumThepPhe = (d.KLThepPhe ?? 0) + (d.KLThepPheGang ?? 0);
                        if (sumThepPhe != 0)
                            ws.Cell(currentRow, scrapCol.Value).Value = sumThepPhe;
                    }
                }
                else
                {
                    // Khí Argon (m3): LF dùng AR_LF, RH dùng AR_RH
                    if (gasCol.HasValue)
                    {
                        var ar = loaiBmKey == "LF" ? d.AR_LF : d.AR_RH;
                        if (ar.HasValue) ws.Cell(currentRow, gasCol.Value).Value = ar.Value;
                    }

                    // Que lấy mẫu / Que đo nhiệt / Ghi chú: map theo model nếu có field tương ứng
                    // Ví dụ: nếu sau này DLNM_HRC2/DLNM_HRC2_ResponseModels có:
                    //   public double? QueLayMau { get; set; }
                    //   public double? QueDoNhiet { get; set; }
                    //   public string? GhiChu { get; set; }
                    // thì các dòng dưới sẽ tự ghi giá trị, còn hiện tại field chưa có nên Excel để trống.
                    //
                    if (queLayMauCol.HasValue && d.QueLayMau.HasValue)
                    {
                        ws.Cell(currentRow, queLayMauCol.Value).Value = d.QueLayMau.Value;
                    }

                    if (queDoNhietCol.HasValue && d.QueDoNhiet.HasValue)
                    {
                        ws.Cell(currentRow, queDoNhietCol.Value).Value = d.QueDoNhiet.Value;
                    }

                    if (noteColLfRh.HasValue && !string.IsNullOrWhiteSpace(d.GhiChu))
                    {
                        ws.Cell(currentRow, noteColLfRh.Value).Value = d.GhiChu;
                    }
                }

                // tăng chiều cao dòng dữ liệu cho dễ đọc
                ws.Row(currentRow).Height = 18;

                currentRow++;
            }


            // ===== DÒNG TỔNG =====
            currentRow += 1;
            int totalRow = currentRow;

            ws.Range(totalRow, 1, totalRow, 6).Merge();
            ws.Cell(totalRow, 1).Value = "Tổng";
            ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(totalRow, 1).Style.Font.Bold = true;

            int lastDataRow = currentRow - 1;
            ws.Range(startRow, 1, lastDataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(totalRow, 7).FormulaA1 = $"SUM(G{startRow}:G{lastDataRow})";
            ws.Cell(totalRow, 8).FormulaA1 = $"SUM(H{startRow}:H{lastDataRow})";

            int col = headerStartCol;

            foreach (var h in headers)
            {
                var colLetter = ws.Cell(1, col).Address.ColumnLetter;
                ws.Cell(totalRow, col).FormulaA1 = $"SUM({colLetter}{startRow}:{colLetter}{lastDataRow})";
                col++;
            }

            currentRow += 2;

            // ===== HEADER FOOTER =====
            // ws.Range(currentRow, 16, currentRow, 19).Merge();
            // ws.Range(currentRow, 20, currentRow, 23).Merge();
            // ws.Range(currentRow, 24, currentRow, 27).Merge();

            // ws.Cell(currentRow, 16).Value = "Tồn đầu kíp";
            // ws.Cell(currentRow, 20).Value = "Nhập trong kíp";
            // ws.Cell(currentRow, 24).Value = "Tồn cuối kíp";

            // ws.Row(currentRow).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            // ws.Row(currentRow).Style.Font.Bold = true;

            // List<string> materials;

            // if (loaiBmKey.Contains("BOF"))
            // {
            //     materials = new List<string>
            //     {
            //         "Lượng Vôi",
            //         "Lượng Dolomite",
            //         "Lượng Quặng",
            //         "Lượng FeSi",
            //         "Lượng SiMn",
            //         "Lượng FeMn",
            //         "Lượng Than",
            //         "Lượng AL",
            //         "FeCrHC",
            //         "Lượng Chất tăng cacbon",
            //         "Lượng HC-FeMn75",
            //         "Nguyên liệu khác: Coke",
            //         "FeP",
            //         "Lượng FeCr LC",
            //         "Bauxite",
            //         "LDSF",
            //         "Chất cải tính xỉ"
            //     };
            // }
            // else
            // {
            //     materials = new List<string>
            //     {
            //         "Tồn trên silo",
            //         "Lượng SiMn (kg)",
            //         "Lượng FeSi (kg)",
            //         "Lượng vôi (kg)",
            //         "Lượng than (kg)",
            //         "Lượng FeMn (kg)",
            //         "Lượng Huỳnh thạch (kg)",
            //         "Lượng Nhôm (kg)",
            //         "Khác"
            //     };
            // }

            // int r = currentRow + 1;

            // foreach (var m in materials)
            // {
            //     ws.Range(r, 1, r, 15).Merge();
            //     ws.Cell(r, 1).Value = m;

            //     ws.Range(r, 16, r, 19).Merge();
            //     ws.Range(r, 20, r, 23).Merge();
            //     ws.Range(r, 24, r, 27).Merge();

            //     r++;
            // }

            int lastFooterRow =currentRow;

            // ===== BORDER TOÀN BỘ =====

            // Xác định số cột cuối cùng dựa trên nội dung thực tế của sheet (template + cột động)
            var lastUsedColumn = ws.LastColumnUsed();
            int lastColumn = lastUsedColumn != null ? lastUsedColumn.ColumnNumber() : 34;

            // border header (dòng 6-7) + căn giữa + wrap text toàn vùng header
            var headerBorderRange = ws.Range(6, 1, 7, lastColumn);
            headerBorderRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            headerBorderRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            headerBorderRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            headerBorderRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            headerBorderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            headerBorderRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerBorderRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerBorderRange.Style.Alignment.WrapText = true;

            // border bảng dữ liệu + dòng tổng
            var dataRange = ws.Range(startRow, 1, totalRow, lastColumn);
            dataRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // border footer
            var footerRange = ws.Range(currentRow, 1, lastFooterRow, lastColumn);
            footerRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            footerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            footerRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            footerRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            footerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            var fileName = $"ThongKe_HRC2_{loaiBmKey}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            return new ExportFileResult
            {
                Content = stream.ToArray(),
                FileName = fileName,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };

        }

    }
}
