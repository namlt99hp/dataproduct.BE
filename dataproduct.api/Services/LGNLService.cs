using ClosedXML.Excel;
using dataproduct.api.DTOs.CTD_Dto;
using dataproduct.api.DTOs.Export;
using dataproduct.api.DTOs.NMLG_Dto;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace dataproduct.api.Services
{
    public class LGNLService
    {
        private readonly ILGNLRepository _repo;
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private static readonly ConcurrentDictionary<Guid, PhieuLockEntry> _phieuLocks = new();

        private sealed class PhieuLockEntry
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public int RefCount;
        }

        public LGNLService(
            ILGNLRepository repo,
            IConverter pdfConverter,
            IWebHostEnvironment env,
            IConfiguration configuration)
        {
            _repo = repo;
            _pdfConverter = pdfConverter;
            _env = env;
            _configuration = configuration;
        }
        // ─── TS Mapping lookup ───────────────────────────────────────────────

        public Task<List<LGNLTsMappingDto>> GetTsMappingListAsync()
            => _repo.GetTsMappingListAsync();

        // ─── SiLo Master ─────────────────────────────────────────────────────

        public async Task<List<LGNLSiLoMasterDto>> GetSiLoMasterListAsync(int? idLoCao)
        {
            var list = await _repo.GetSiLoMasterListAsync(idLoCao);
            return list.Select(MapSiLoMaster).ToList();
        }

        public async Task<LGNLSiLoMasterDto?> GetSiLoMasterByIdAsync(int id)
        {
            var e = await _repo.GetSiLoMasterByIdAsync(id);
            return e == null ? null : MapSiLoMaster(e);
        }

        public async Task<LGNLSiLoMasterDto> AddSiLoMasterAsync(CreateLGNLSiLoMasterDto dto)
        {
            var entity = new LG_NL_SiLo
            {
                IDLoCao = dto.IdLoCao,
                TenSiLo = dto.TenSiLo,
                ThuTu   = dto.ThuTu,
                TagKey  = dto.TagKey
            };
            var result = await _repo.AddSiLoMasterAsync(entity);
            return MapSiLoMaster(result);
        }

        public async Task<LGNLSiLoMasterDto?> UpdateSiLoMasterAsync(int id, UpdateLGNLSiLoMasterDto dto)
        {
            var entity = new LG_NL_SiLo
            {
                IDLoCao = dto.IdLoCao,
                TenSiLo = dto.TenSiLo,
                ThuTu   = dto.ThuTu,
                TagKey  = dto.TagKey,
                IsDelete = dto.IsDelete,
            };
            var result = await _repo.UpdateSiLoMasterAsync(id, entity);
            return result == null ? null : MapSiLoMaster(result);
        }

        public Task<bool> DeleteSiLoMasterAsync(int id) => _repo.DeleteSiLoMasterAsync(id);

        // ─── Mapping ─────────────────────────────────────────────────────────

        public Task<List<LGNLMappingDto>> GetMappingListAsync(DateTime? ngay, int? idCa, int? idLoCao)
            => _repo.GetMappingListAsync(ngay, idCa, idLoCao);

        public async Task<LGNLMappingDto?> GetMappingByIdAsync(int id)
        {
            var e = await _repo.GetMappingByIdAsync(id);
            return e == null ? null : MapMapping(e);
        }

        public async Task<LGNLMappingDto> AddMappingAsync(CreateLGNLMappingDto dto)
        {
            var entity = new LG_NL_Mapping
            {
                Ngay    = dto.Ngay,
                IDCa    = dto.IdCa,
                IDLoCao = dto.IdLoCao,
                IDSiLo  = dto.IdSiLo,
                IDNVL   = dto.IdNVL,
                GhiChu  = dto.GhiChu
            };
            var result = await _repo.AddMappingAsync(entity);
            return MapMapping(result);
        }

        public async Task<LGNLMappingDto?> UpdateMappingAsync(int id, UpdateLGNLMappingDto dto)
        {
            var entity = new LG_NL_Mapping
            {
                Ngay    = dto.Ngay,
                IDCa    = dto.IdCa,
                IDLoCao = dto.IdLoCao,
                IDSiLo  = dto.IdSiLo,
                IDNVL   = dto.IdNVL,
                GhiChu  = dto.GhiChu
            };
            var result = await _repo.UpdateMappingAsync(id, entity);
            return result == null ? null : MapMapping(result);
        }

        public Task<bool> DeleteMappingAsync(int id) => _repo.DeleteMappingAsync(id);

        // ─── Nhóm NVL ─────────────────────────────────────────────────────────

        public async Task<List<LGNLNhomNvlDto>> GetNhomNvlListAsync(int? idLoCao)
        {
            var list = await _repo.GetNhomNvlListAsync(idLoCao);
            return list.Select(MapNhomNvl).ToList();
        }

        public async Task<LGNLNhomNvlDto?> GetNhomNvlByIdAsync(int id)
        {
            var e = await _repo.GetNhomNvlByIdAsync(id);
            return e == null ? null : MapNhomNvl(e);
        }

        public async Task<LGNLNhomNvlDto> AddNhomNvlAsync(CreateLGNLNhomNvlDto dto)
        {
            var entity = new LG_NL_NhomNVL
            {
                TenNhom = dto.TenNhom,
                ThuTu   = dto.ThuTu,
                GhiChu  = dto.GhiChu
            };
            var result = await _repo.AddNhomNvlAsync(entity);
            return MapNhomNvl(result);
        }

        public async Task<LGNLNhomNvlDto?> UpdateNhomNvlAsync(int id, UpdateLGNLNhomNvlDto dto)
        {
            var entity = new LG_NL_NhomNVL
            {
                TenNhom = dto.TenNhom,
                ThuTu   = dto.ThuTu,
                GhiChu  = dto.GhiChu
            };
            var result = await _repo.UpdateNhomNvlAsync(id, entity);
            return result == null ? null : MapNhomNvl(result);
        }

        public Task<bool> DeleteNhomNvlAsync(int id) => _repo.DeleteNhomNvlAsync(id);

        // ─── NVL ─────────────────────────────────────────────────────────────

        public Task<List<LGNLNvlDto>> GetNvlListAsync(int? idLoCao)
            => _repo.GetNvlListAsync(idLoCao);

        public async Task<LGNLNvlDto?> GetNvlByIdAsync(int id)
        {
            var e = await _repo.GetNvlByIdAsync(id);
            return e == null ? null : MapNvlEntity(e);
        }

        public async Task<LGNLNvlDto> AddNvlAsync(CreateLGNLNvlDto dto)
        {
            var entity = new LG_NL_NVL
            {
                IDLoCao     = dto.IdLoCao,
                IDNhomNVL   = dto.IdNhomNVL,
                TenNVL_NM   = dto.TenNVL_NM,
                ThuTu       = dto.ThuTu,
                GhiChu      = dto.GhiChu,
            };
            var result = await _repo.AddNvlAsync(entity);
            return MapNvlEntity(result);
        }

        public async Task<LGNLNvlDto?> UpdateNvlAsync(int id, UpdateLGNLNvlDto dto)
        {
            var entity = new LG_NL_NVL
            {
                IDLoCao     = dto.IdLoCao,
                IDNhomNVL   = dto.IdNhomNVL,
                TenNVL_NM   = dto.TenNVL_NM,
                TenNVL_TK = dto.TenNVL_TK,
                XacNhan = dto.XacNhan,
                ThuTu       = dto.ThuTu,
                GhiChu      = dto.GhiChu,
            };
            var result = await _repo.UpdateNvlAsync(id, entity);
            return result == null ? null : MapNvlEntity(result);
        }

        public Task<bool> DeleteNvlAsync(int id) => _repo.DeleteNvlAsync(id);

        public async Task<bool> UpdateXacNhanAsync(UpdateXacNhanDto dto)
        {
            var entity = await _repo.GetNvlByIdAsync(dto.Id);
            if (entity == null) return false;

            entity.XacNhan = dto.XacNhan;
            entity.NgayXacNhan = DateTime.Now;

            await _repo.UpdateNvlAsync(dto.Id, entity);
            return true;
        }
        private static LGNLSiLoMasterDto MapSiLoMaster(LG_NL_SiLo e) => new()
        {
            Id       = e.ID,
            IdLoCao  = e.IDLoCao,
            TenSiLo  = e.TenSiLo,
            ThuTu    = e.ThuTu,
            NgayTao  = e.NgayTao,
            TagKey   = e.TagKey,
            IsDelete = e.IsDelete,
        };

        private static LGNLMappingDto MapMapping(LG_NL_Mapping e) => new()
        {
            Id          = e.ID,
            Ngay        = e.Ngay,
            IdCa        = e.IDCa,
            IdLoCao     = e.IDLoCao,
            IdSiLo      = e.IDSiLo,
            IdNVL       = e.IDNVL,
            ThoiDiemBD  = e.ThoiDiemBD,
            NgayHetHL   = e.NgayHetHL,
            IdCaHetHL   = e.IDCaHetHL,
            GhiChu      = e.GhiChu,
            NgayTao     = e.NgayTao
        };

        private static LGNLNhomNvlDto MapNhomNvl(LG_NL_NhomNVL e) => new()
        {
            Id      = e.ID,
            TenNhom = e.TenNhom,
            ThuTu   = e.ThuTu,
            GhiChu  = e.GhiChu,
        };

        private static LGNLNvlDto MapNvlEntity(LG_NL_NVL e) => new()
        {
            Id          = e.ID,
            IdLoCao     = e.IDLoCao,
            IdNhomNVL   = e.IDNhomNVL,
            TenNVL_NM   = e.TenNVL_NM,
            ThuTu       = e.ThuTu,
            GhiChu      = e.GhiChu,
            NgayTao     = e.NgayTao,
        };

        // ─── Dữ liệu theo LoCao, Ngày ───────────────────────────────

        public async Task<List<LGNLDuLieuScadaDto>> GetDataByFilterAsync(
            int? idLoCao, DateTime? ngayBatDau, DateTime? ngayKetThuc)
        {
            return await _repo.GetDataByFilterAsync(idLoCao, ngayBatDau, ngayKetThuc);
        }

        // ─── Pivot dữ liệu nạp liệu theo Silo mapping (LO 1-4) ───────

        public async Task<LGNLDuLieuSiLoResult> GetDuLieuSiloPivotAsync(
            DateTime ngay, int idCa, int idLoCao)
        {
            return await _repo.GetDuLieuSiloPivotAsync(ngay, idCa, idLoCao);
        }


        // ─── Snapshot trạng thái Silo ──────────────────────────────

        public Task<List<LGNLSiloSnapshotDto>> GetSiloSnapshotAsync(
            int idLoCao, DateTime ngay, int idCa)
            => _repo.GetSiloSnapshotAsync(idLoCao, ngay, idCa);

        // ─── Sao chép mapping từ ca gần nhất có dữ liệu ─────────────

        public Task<CopyMappingFromPreviousShiftResultDto> CopyMappingFromPreviousShiftAsync(
            int idLoCao, DateTime ngay, int idCa)
            => _repo.CopyMappingFromPreviousShiftAsync(idLoCao, ngay, idCa);

        // ─── Đổi NVL cho silo tại thời điểm cụ thể trong ca ─────────

        public async Task<LG_NL_Mapping> ChangeSiLoNVLAsync(
            int idLoCao, DateTime ngay, int idCa, int idSiLo, int idNVLMoi,
            DateTime thoiDiem, string? ghiChu)
        {
            return await _repo.ChangeSiLoNVLAsync(
                idLoCao, ngay, idCa, idSiLo, idNVLMoi, thoiDiem, ghiChu);
        }

        public Task<int> UndoChangeSiLoNVLAsync(int idLoCao, DateTime ngay, int idCa, int idSiLo)
            => _repo.UndoChangeSiLoNVLAsync(idLoCao, ngay, idCa, idSiLo);

        // ─── Chi tiết nạp liệu theo phiếu ────────────────────────────────────────

        public Task<List<LGNLChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu)
            => _repo.GetChiTietByPhieuAsync(idPhieu);

        // ─── InsertFromPhieu ──────────────────────────────────────────────────────

        public async Task<int> InsertFromPhieuJsonAsync(BmPhieu phieu)
        {
            if (phieu == null || string.IsNullOrWhiteSpace(phieu.DataJson))
                return 0;
            try
            {
                using var jsonDoc = JsonDocument.Parse(phieu.DataJson);
                var root = jsonDoc.RootElement;

                var ngayStr = TryGetString(root, "NgaySX", "ngaySX", "ngay");
                var idLoCao = TryGetInt(root, "scope", "Scope", "idLoCao", "IDLoCao");
                var idCa = TryGetInt(root, "ca", "Ca");

                if (idLoCao == null || idCa == null || string.IsNullOrWhiteSpace(ngayStr))
                    return 0;

                if (!DateTime.TryParse(ngayStr, out var ngay))
                    return 0;

                if (!TryGetArray(root, "table1", out var table1))
                    return 0;

                // Parse độ ẩm % per IDNVL từ root: { "doAm": { "123": 5.0, "456": 3.5 } }
                var doAmByNvl = new Dictionary<int, decimal>();
                if (root.TryGetProperty("doAm", out var doAmEl) && doAmEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var kv in doAmEl.EnumerateObject())
                    {
                        if (int.TryParse(kv.Name, out var nvlId) && kv.Value.TryGetDecimal(out var da))
                            doAmByNvl[nvlId] = da;
                    }
                }

                // Group-first: load nhóm → NVL từ DB để duyệt theo thứ tự nhóm,
                // chỉ xử lý NVL đã được cấu hình (tránh key lạ trong JSON).
                var nvlGroupOrdered = (await _repo.GetNvlListAsync(idLoCao.Value))
                    .OrderBy(n => n.ThuTuNhom ?? 999)
                    .ThenBy(n => n.ThuTu ?? 999)
                    .ToList();

                var items = new List<LG_NL_ChiTiet>();
                int thuTu = 0;

                foreach (var row in table1.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Object)
                        continue;

                    thuTu++;

                    var soMe            = TryGetDecimal(row, "soMe");
                    var meGio           = TryGetString(row, "meGio");
                    var thoiGianNapLieu = TryGetString(row, "thoiGianNapLieu");
                    var cheDo           = TryGetString(row, "cheDoNapLieu");
                    var thuocThamLieu1  = TryGetDecimal(row, "thuocThamLieu1");
                    var thuocThamLieu2  = TryGetDecimal(row, "thuocThamLieu2");
                    var ghiChu          = TryGetString(row, "ghiChu");

                    // Unpivot group-first: duyệt theo nhóm → NVL → lấy giá trị từ JSON row
                    foreach (var nvl in nvlGroupOrdered)
                    {
                        var propName = nvl.Id.ToString();
                        if (!row.TryGetProperty(propName, out var propVal)) continue;
                        if (propVal.ValueKind == JsonValueKind.Null) continue;
                        if (!TryParseDecimalElement(propVal, out var giaTri)) continue;

                        var isManual = row.TryGetProperty($"_manual_{nvl.Id}", out var manualEl)
                                       && manualEl.ValueKind == JsonValueKind.True;
                        decimal? giaTri_Goc = null;
                        if (row.TryGetProperty($"_goc_{nvl.Id}", out var gocEl)
                            && gocEl.ValueKind != JsonValueKind.Null
                            && TryParseDecimalElement(gocEl, out var gocVal))
                            giaTri_Goc = gocVal;

                        var doAm = doAmByNvl.TryGetValue(nvl.Id, out var da) ? da : (decimal?)null;

                        items.Add(new LG_NL_ChiTiet
                        {
                            IDPhieu         = phieu.Idphieu,
                            IDLoCao         = idLoCao.Value,
                            Ngay            = ngay,
                            IDCa            = idCa.Value,
                            ThoiGianNapLieu = thoiGianNapLieu,
                            SoMe            = soMe,
                            MeGio           = meGio,
                            CheDo           = cheDo,
                            ThuocThamLieu1  = thuocThamLieu1,
                            ThuocThamLieu2  = thuocThamLieu2,
                            GhiChu          = ghiChu,
                            IDNVL           = nvl.Id,
                            GiaTri          = giaTri,
                            ThuTu           = thuTu,
                            NgayTao         = DateTime.Now,
                            ManualGiaTri    = isManual,
                            GiaTri_Goc      = giaTri_Goc,
                            DoAm            = doAm,
                        });
                    }
                }

                items = items
                    .GroupBy(x => new { x.IDPhieu, x.ThuTu, x.IDNVL })
                    .Select(g => g.First())
                    .ToList();

                // ── Merge với dữ liệu đã lưu trong DB ──────────────────────────
                // Khoá so khớp: (SoMe, ThoiGianNapLieu, IDNVL).
                // Nếu bản ghi cũ có ManualGiaTri = true → giữ GiaTri nhập tay,
                // cập nhật GiaTri_Goc = giá trị API mới để người dùng so sánh.
                var savedRecords = await _repo.GetChiTietByPhieuAsync(phieu.Idphieu);
                var manualMap = savedRecords
                    .Where(r => r.ManualGiaTri)
                    .ToDictionary(r => (r.SoMe, r.ThoiGianNapLieu, r.IdNVL));

                //foreach (var item in items)
                //{
                //    var key = (item.SoMe, item.ThoiGianNapLieu, item.IDNVL);
                //    if (manualMap.TryGetValue(key, out var saved))
                //    {
                //        // GiaTri_Goc = giá trị SCADA (tự động), không bao giờ là giá trị nhập tay.
                //        // Ưu tiên: _goc_ từ JSON (đã set khi lần đầu chuyển sang manual),
                //        // fallback về GiaTri_Goc đã lưu trong DB.
                //        // KHÔNG dùng item.GiaTri vì đó là giá trị nhập tay từ JSON.
                //        item.GiaTri_Goc   = item.GiaTri_Goc ?? saved.GiaTri_Goc;
                //        item.GiaTri       = saved.GiaTri;  // giữ nguyên giá trị nhập tay
                //        item.ManualGiaTri = true;
                //    }
                //}
                foreach (var item in items)
                {
                    var key = (item.SoMe, item.ThoiGianNapLieu, item.IDNVL);

                    if (manualMap.TryGetValue(key, out var saved))
                    {
                        item.ManualGiaTri =
                            item.ManualGiaTri || saved.ManualGiaTri;

                        // Giữ nguyên giá trị gốc đầu tiên
                        item.GiaTri_Goc =
                            item.GiaTri_Goc ?? saved.GiaTri_Goc;

                        // KHÔNG ghi đè GiaTri
                        // item.GiaTri = saved.GiaTri;
                    }
                }
                // Tính QuyKho server-side: sum(GiaTri) per IDNVL * (1 - DoAm/100)
                var totalByNvl = items
                    .GroupBy(x => x.IDNVL)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.GiaTri ?? 0));

                foreach (var item in items)
                {
                    if (doAmByNvl.TryGetValue(item.IDNVL, out var pct) && totalByNvl.TryGetValue(item.IDNVL, out var total))
                        item.QuyKho = total * (100 - pct) / 100;
                }

                // Replace mode: xóa dữ liệu cũ rồi ghi mới (đã merge) — trong 1 transaction
                await _repo.ReplaceChiTietAsync(phieu.Idphieu, items);

                return items.Count;
            }

            catch (Exception ex)
            {
                // TODO: log error
                Console.WriteLine(ex);
                return 0;
            }
        }

        // ─── Sync chi tiết từ SCADA khi người dùng bấm "Tải dữ liệu" ─────────────
        // Lấy dữ liệu SCADA mới nhất → merge với bản ghi đã lưu (giữ ManualGiaTri) → lưu DB → trả về pivot để hiển thị.

        public async Task<LGNLDuLieuSiLoResult> SyncChiTietFromScadaAsync(Guid idPhieu)
        {
            // 1. Đọc ngày/ca/lò cao trực tiếp từ cột BmPhieu — không parse DataJson
            var phieu = await _repo.GetPhieuByIdAsync(idPhieu)
                ?? throw new InvalidOperationException($"Không tìm thấy phiếu {idPhieu}.");

            if (phieu.NgaySX is null || phieu.Ca is null || phieu.Scope is null)
                throw new InvalidOperationException("Phiếu thiếu thông tin ngày/ca/lò cao (NgaySX, Ca, Scope).");

            var ngay    = phieu.NgaySX.Value.ToDateTime(TimeOnly.MinValue);
            var idCa    = phieu.Ca.Value;
            var idLoCao = phieu.Scope.Value;

            // 2. Lấy dữ liệu SCADA mới nhất (pivot)
            var pivot = await _repo.GetDuLieuSiloPivotAsync(ngay, idCa, idLoCao);

            // 3. Load bản ghi đã lưu để lấy doAm và giá trị nhập tay
            var savedRecords = await _repo.GetChiTietByPhieuAsync(idPhieu);

            var doAmByNvl = savedRecords
                .Where(r => r.DoAm.HasValue)
                .GroupBy(r => r.IdNVL)
                .ToDictionary(g => g.Key, g => g.First().DoAm!.Value);

            // SoMe (số mẻ từ tag TS0) là định danh duy nhất cho việc khớp/gộp dữ liệu nhập tay
            // qua các lần Sync — có ở cả 6 lò cao. Không còn dùng ThuTu để khớp vì ThuTu chỉ là
            // vị trí dòng, được đánh số lại mỗi lần Sync nên không ổn định giữa các lần.
            var manualBySoMe = savedRecords
                .Where(r => r.ManualGiaTri && r.SoMe.HasValue)
                .GroupBy(r => (r.SoMe, r.IdNVL))
                .ToDictionary(g => g.Key, g => g.First());

            // 4. Convert pivot rows → LG_NL_ChiTiet
            // Group-first: iterate pivot.Columns (nhóm → NVL) to drive item mapping,
            // so items are created in group/NVL order matching the UI column structure.
            var items = new List<LG_NL_ChiTiet>();
            int thuTu = 0;

            // SoMe → ThuTu của dòng SCADA hiện tại — kể cả dòng "trắng" (mẻ không có NVL nào
            // báo trọng lượng), để bản ghi nhập tay ở bước 5b tìm đúng dòng cần gộp vào thay vì
            // luôn tách thành dòng mới nối đuôi bảng.
            var soMeThuTuMap = new Dictionary<decimal, int>();

            // SoMe → giờ của dòng SCADA hiện tại: bản ghi bảo toàn gộp vào mẻ này phải dùng giờ này,
            // không giữ giờ cũ đã lưu, để 1 số mẻ chỉ có đúng 1 khung giờ trong DB.
            var soMeThoiGianMap = new Dictionary<decimal, string>();

            foreach (var row in pivot.Rows)
            {
                thuTu++;

                var soMe     = ObjToDecimal(row.GetValueOrDefault("soMe"));
                var timeStr  = row.GetValueOrDefault("time")?.ToString();
                var thoiGian = DateTime.TryParse(timeStr, out var dt)
                                 ? dt.ToString("HH:mm:ss")
                                 : "";

                if (soMe.HasValue)
                {
                    soMeThuTuMap[soMe.Value] = thuTu;
                    soMeThoiGianMap[soMe.Value] = thoiGian;
                }

                // Map group → NVL → item (group-first)
                foreach (var groupCol in pivot.Columns)
                {
                    if (groupCol.Children == null) continue;

                    foreach (var nvlCol in groupCol.Children)
                    {
                        if (string.IsNullOrEmpty(nvlCol.DataIndex)) continue;
                        if (!int.TryParse(nvlCol.DataIndex, out var idNvl)) continue;
                        if (!row.TryGetValue(nvlCol.DataIndex, out var val) || val is null) continue;

                        var giaTri = ObjToDecimal(val);
                        if (!giaTri.HasValue) continue;

                        var doAm = doAmByNvl.TryGetValue(idNvl, out var da) ? da : (decimal?)null;

                        items.Add(new LG_NL_ChiTiet
                        {
                            IDPhieu         = idPhieu,
                            IDLoCao         = idLoCao,
                            Ngay            = ngay,
                            IDCa            = idCa,
                            ThoiGianNapLieu = thoiGian,
                            SoMe            = soMe,
                            IDNVL           = idNvl,
                            GiaTri          = giaTri.Value,
                            ThuTu           = thuTu,
                            NgayTao         = DateTime.Now,
                            ManualGiaTri    = false,
                            DoAm            = doAm,
                        });
                    }
                }
            }

            // 5. Merge: giữ giá trị nhập tay, cập nhật GiaTri_Goc = giá trị SCADA mới
            foreach (var item in items)
            {
                if (item.SoMe.HasValue && manualBySoMe.TryGetValue((item.SoMe, item.IDNVL), out var saved))
                {
                    item.GiaTri_Goc   = item.GiaTri;
                    item.GiaTri       = saved.GiaTri;
                    item.ManualGiaTri = true;
                    item.DoAm         = saved.DoAm ?? item.DoAm;
                }
            }

            // 5b. Giữ lại bản ghi cần bảo toàn qua lần Sync này — gồm 2 trường hợp:
            //  (a) NVL hoàn toàn không có SCADA data (silo không có tagKey) — giữ mọi bản ghi
            //      đã lưu (như cũ), vì đây là NVL nhập tay hoàn toàn.
            //  (b) Bản ghi nhập tay (ManualGiaTri) cho 1 SoMe mà mẻ đó KHÔNG còn xuất hiện trong
            //      SCADA hiện tại dưới bất kỳ NVL nào — đây là dòng người dùng tự bấm "+ Thêm
            //      dòng" thêm hẳn, SCADA không có mẻ này.
            //      Nếu SoMe đó VẪN còn trong SCADA hiện tại (chỉ đổi sang NVL khác do mapping
            //      "Đổi NVL giữa ca" thay đổi) thì bản ghi cũ coi như lỗi thời — phải loại bỏ,
            //      nếu không sẽ bị lặp lại vô hạn mỗi lần Sync (trước đây so theo (SoMe, IDNVL)
            //      nên không phát hiện được trường hợp NVL đã đổi cho cùng 1 SoMe).
            var scadaNvlIds = items.Select(x => x.IDNVL).ToHashSet();
            var scadaSoMeSlots = items.Where(x => x.SoMe.HasValue).Select(x => x.SoMe!.Value).ToHashSet();
            var preservedRecords = savedRecords
                .Where(r => !scadaNvlIds.Contains(r.IdNVL)
                    || (r.ManualGiaTri && r.SoMe.HasValue && !scadaSoMeSlots.Contains(r.SoMe.Value)))
                .GroupBy(r => new { r.SoMe, r.IdNVL })
                .Select(g => g.First())
                .ToList();

            LG_NL_ChiTiet BuildPreservedItem(LGNLChiTietDto r, int targetThuTu) => new()
            {
                IDPhieu         = idPhieu,
                IDLoCao         = idLoCao,
                Ngay            = ngay,
                IDCa            = idCa,
                ThoiGianNapLieu = r.SoMe.HasValue && soMeThoiGianMap.TryGetValue(r.SoMe.Value, out var gioScada)
                                    ? gioScada
                                    : r.ThoiGianNapLieu,
                SoMe            = r.SoMe,
                MeGio           = r.MeGio,
                CheDo           = r.CheDo,
                ThuocThamLieu1  = r.ThuocThamLieu1,
                ThuocThamLieu2  = r.ThuocThamLieu2,
                GhiChu          = r.GhiChu,
                IDNVL           = r.IdNVL,
                GiaTri          = r.GiaTri,
                ThuTu           = targetThuTu,
                NgayTao         = DateTime.Now,
                ManualGiaTri    = true,
                GiaTri_Goc      = r.GiaTri_Goc,
                DoAm            = r.DoAm,
            };

            // Với mỗi bản ghi cần bảo toàn: nếu SoMe của nó TRÙNG với 1 dòng SCADA vừa build ở
            // bước 4 (cùng 1 mẻ thực tế, ví dụ NVL hoàn toàn nhập tay nhưng vẫn cùng mẻ với các
            // NVL khác đang có SCADA) → gộp vào ĐÚNG ThuTu của dòng đó, tránh 1 mẻ bị tách thành
            // 2 dòng riêng biệt trên bảng. Chỉ khi mẻ đó hoàn toàn không có trong dữ liệu SCADA
            // hiện tại mới đánh ThuTu mới, nối tiếp sau các dòng SCADA (group theo SoMe GỐC để
            // các cột NVL cùng 1 mẻ vẫn được gộp chung 1 dòng mới).
            var noScadaItems = new List<LG_NL_ChiTiet>();
            var toAppend = new List<LGNLChiTietDto>();

            foreach (var r in preservedRecords)
            {
                if (r.SoMe.HasValue && soMeThuTuMap.TryGetValue(r.SoMe.Value, out var matchedThuTu))
                    noScadaItems.Add(BuildPreservedItem(r, matchedThuTu));
                else
                    toAppend.Add(r);
            }

            foreach (var group in toAppend.GroupBy(r => r.SoMe))
            {
                thuTu++;
                foreach (var r in group)
                    noScadaItems.Add(BuildPreservedItem(r, thuTu));
            }

            items.AddRange(noScadaItems);

            // 6. Tính QuyKho
            var totalByNvl = items
                .GroupBy(x => x.IDNVL)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.GiaTri ?? 0));

            foreach (var item in items)
            {
                if (doAmByNvl.TryGetValue(item.IDNVL, out var pct) && totalByNvl.TryGetValue(item.IDNVL, out var total))
                    item.QuyKho = total * (100 - pct) / 100;
            }

            // 7. Xóa cũ → ghi mới (đã merge) — trong 1 transaction
            await _repo.ReplaceChiTietAsync(idPhieu, items);

            return pivot;
        }

        private static decimal? ObjToDecimal(object? value)
        {
            if (value is null) return null;
            try { return Convert.ToDecimal(value); }
            catch { return null; }
        }

        // ─── Export PDF ──────────────────────────────────────────────────────────

        public async Task<ExportFileResult> ExportNapLieuPdfAsync(Guid idPhieu, List<PheDuyetDto> pheDuyets, bool useKeHoachName = false)
        {
            var phieu = await _repo.GetPhieuByIdAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu.");

            // Lấy chi tiết và NVL
            var chiTiet = await _repo.GetChiTietByPhieuAsync(idPhieu);
            var firstRow = chiTiet.FirstOrDefault();

            var ngay    = firstRow?.Ngay ?? DateTime.MinValue;
            var ca      = firstRow?.IdCa ?? 0;
            var scope   = firstRow?.IdLoCao ?? 0;
            var kip = phieu.Kip?.Trim().ToUpper() ?? "";

            var ngayDisplay = ngay != DateTime.MinValue ? ngay.ToString("dd/MM/yyyy") : "";
            //   var caLabel     = ca == 1 ? "1" : ca == 2 ? "2" : $"{ca + kip}" ;
            var caLabel = $"{ca}{kip}";
            var loCao       = scope > 0 ? scope.ToString() : "";

            // Ca 1: 8h -> 20h cùng ngày; Ca 2: 20h -> 8h ngày hôm sau
            var gioBatDau   = ca == 2 ? 20 : 8;
            var gioKetThuc  = ca == 2 ? 8 : 20;
            var ngayBatDauDisplay  = ngayDisplay;
            var ngayKetThucDisplay = ngay != DateTime.MinValue
                ? (ca == 2 ? ngay.AddDays(1) : ngay).ToString("dd/MM/yyyy")
                : "";

            var nvlList = await _repo.GetNvlListAsync(scope > 0 ? scope : null);

            // Chỉ giữ NVL có ít nhất 1 dòng đã nhập giá trị trong phiếu này — loại bỏ NVL không dùng (toàn null)
            var usedNvlIds = chiTiet.Where(x => x.GiaTri.HasValue).Select(x => x.IdNVL).ToHashSet();
            nvlList = nvlList.Where(n => usedNvlIds.Contains(n.Id)).ToList();

            // Hàm lấy tên hiển thị NVL theo quyền: P.KH dùng TenNVL_TK (nếu đã xác nhận), còn lại dùng TenNVL_NM
            string NvlLabel(LGNLNvlDto n) =>
                useKeHoachName && n.XacNhan == true && !string.IsNullOrWhiteSpace(n.TenNVL_TK)
                    ? n.TenNVL_TK!
                    : n.TenNVL_NM ?? "";

            // Luôn lấy đủ 6 nhóm NVL; NVL được sắp xếp theo nhóm rồi theo thứ tự NVL
            var nhomList = (await _repo.GetNhomNvlListAsync(scope > 0 ? scope : null))
                .Select(MapNhomNvl)
                .OrderBy(nh => nh.ThuTu ?? 999)
                .ToList();
            var nvlByNhomId = nvlList
                .GroupBy(n => n.IdNhomNVL ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(n => n.ThuTu ?? 999).ToList());

            // Số slot tối thiểu per nhóm (khớp với GetDuLieuSiloPivotAsync)
            var slotConfig = new Dictionary<int, int>
            {
                { 1, 2 }, { 2, 2 }, { 3, 2 }, { 4, 2 }, { 5, 1 }, { 6, 2 },
            };

            // colSlots: NVL thực, sau đó pad null-slot đến min
            var colSlots = new List<(LGNLNhomNvlDto Nhom, LGNLNvlDto? Nvl)>();
            foreach (var nhom in nhomList)
            {
                var nvls = nvlByNhomId.TryGetValue(nhom.Id, out var list) ? list : new List<LGNLNvlDto>();
                foreach (var n in nvls) colSlots.Add((nhom, n));
                var minSlots = slotConfig.GetValueOrDefault(nhom.ThuTu ?? 99, 1);
                var current  = colSlots.Count(s => s.Nhom.Id == nhom.Id);
                while (current++ < minSlots)
                    colSlots.Add((nhom, null));
            }

            // Tính width cột NVL: chia đều phần còn lại sau khi trừ fixed cols
            // A4 landscape ~994px usable (297mm @ 96dpi − 17mm×2 margins, no body padding)
            const int pageWidthPx   = 994;
            const int fixedPrefixPx = 45 + 55 + 80 + 75 + 55 + 55; // 365px
            const int ghiChuPx      = 80;
            int nvlColWidth = colSlots.Count > 0
                ? Math.Max(44, (pageWidthPx - fixedPrefixPx - ghiChuPx) / colSlots.Count)
                : 60;

            // Build colgroup — xác định width cố định mỗi cột, tránh cột trống bị co
            var colGroup = new StringBuilder("<colgroup>");
            colGroup.Append($@"<col style=""width:45px""/><col style=""width:55px""/><col style=""width:80px""/><col style=""width:75px""/><col style=""width:55px""/><col style=""width:55px""/>");
            foreach (var _ in colSlots)
                colGroup.Append($@"<col style=""width:{nvlColWidth}px""/>");
            colGroup.Append($@"<col style=""width:{ghiChuPx}px""/>");
            colGroup.Append("</colgroup>");

            // Build thead
            var th1 = new StringBuilder();
            var th2 = new StringBuilder();
            th1.Append(@"<th rowspan=""2"">Số mẻ</th>");
            th1.Append(@"<th rowspan=""2"">Mê/giờ</th>");
            th1.Append(@"<th rowspan=""2"">Thời gian nạp liệu</th>");
            th1.Append(@"<th rowspan=""2"">Chế độ nạp liệu</th>");
            th1.Append(@"<th rowspan=""2"">Thuốc thăm liệu 1 (m)</th>");
            th1.Append(@"<th rowspan=""2"">Thuốc thăm liệu 2 (m)</th>");

            foreach (var nhom in nhomList)
            {
                var slotsForNhom = colSlots.Where(s => s.Nhom.Id == nhom.Id).ToList();
                var realCount    = slotsForNhom.Count(s => s.Nvl != null);
                if (realCount == 0)
                {
                    // Toàn placeholder → tên nhóm span tất cả slot, rowspan=2
                    th1.Append($@"<th rowspan=""2"" colspan=""{slotsForNhom.Count}"">{System.Net.WebUtility.HtmlEncode(nhom.TenNhom ?? "")}</th>");
                }
                else
                {
                    th1.Append($@"<th colspan=""{slotsForNhom.Count}"">{System.Net.WebUtility.HtmlEncode(nhom.TenNhom ?? "")}</th>");
                    foreach (var (_, nvl) in slotsForNhom)
                        th2.Append($@"<th>{(nvl != null ? System.Net.WebUtility.HtmlEncode(NvlLabel(nvl)) : "")}</th>");
                }
            }

            th1.Append(@"<th rowspan=""2"">Ghi chú</th>");

            // Build data rows grouped by ThuTu
            var rowsByThuTu = chiTiet
                .GroupBy(x => x.ThuTu ?? 0)
                .OrderBy(g => g.Key)
                .ToList();

            var dataRows = new StringBuilder();
            foreach (var grp in rowsByThuTu)
            {
                var sample = grp.First();
                var nvlValues = grp.ToDictionary(x => x.IdNVL, x => x.GiaTri);

                dataRows.Append("<tr>");
                dataRows.Append($"<td class=\"text-center\">{sample.SoMe?.ToString("#,##0.###") ?? ""}</td>");
                dataRows.Append($"<td class=\"text-center\">{System.Net.WebUtility.HtmlEncode(sample.MeGio ?? "")}</td>");
                dataRows.Append($"<td class=\"text-center\">{System.Net.WebUtility.HtmlEncode(sample.ThoiGianNapLieu ?? "")}</td>");
                dataRows.Append($"<td class=\"text-center\">{System.Net.WebUtility.HtmlEncode(sample.CheDo ?? "")}</td>");
                dataRows.Append($"<td class=\"text-right\">{(sample.ThuocThamLieu1.HasValue ? sample.ThuocThamLieu1.Value.ToString("N2") : "")}</td>");
                dataRows.Append($"<td class=\"text-right\">{(sample.ThuocThamLieu2.HasValue ? sample.ThuocThamLieu2.Value.ToString("N2") : "")}</td>");

                foreach (var (_, nvl) in colSlots)
                {
                    if (nvl == null) { dataRows.Append("<td></td>"); continue; }
                    var val = nvlValues.TryGetValue(nvl.Id, out var v) ? v : null;
                    dataRows.Append($"<td class=\"text-right\">{(val.HasValue ? val.Value.ToString("#,##0.###") : "")}</td>");
                }

                dataRows.Append($"<td>{System.Net.WebUtility.HtmlEncode(sample.GhiChu ?? "")}</td>");
                dataRows.Append("</tr>");
            }

            // Tổng cộng row
            var tongRow = new StringBuilder();
            tongRow.Append(@"<tr class=""tfoot-row""><td colspan=""6"" class=""text-center""><b>TỔNG CỘNG</b></td>");
            foreach (var (_, nvl) in colSlots)
            {
                if (nvl == null) { tongRow.Append("<td></td>"); continue; }
                var total = chiTiet.Where(x => x.IdNVL == nvl.Id).Sum(x => x.GiaTri ?? 0);
                tongRow.Append($"<td class=\"text-right\"><b>{total.ToString("#,##0.###")}</b></td>");
            }
            tongRow.Append("<td></td></tr>");

            // Độ ẩm row
            var doAmRow = new StringBuilder();
            doAmRow.Append(@"<tr class=""doam-row""><td colspan=""6"" class=""text-center"">Độ ẩm (%)</td>");
            foreach (var (_, nvl) in colSlots)
            {
                if (nvl == null) { doAmRow.Append("<td></td>"); continue; }
                var da = chiTiet.FirstOrDefault(x => x.IdNVL == nvl.Id && x.DoAm.HasValue)?.DoAm;
                doAmRow.Append($"<td class=\"text-center\">{(da.HasValue ? da.Value.ToString("N2") : "")}</td>");
            }
            doAmRow.Append("<td></td></tr>");

            // Quy khô row
            var quyKhoRow = new StringBuilder();
            quyKhoRow.Append(@"<tr class=""quykho-row""><td colspan=""6"" class=""text-center"">Quy khô</td>");
            foreach (var (_, nvl) in colSlots)
            {
                if (nvl == null) { quyKhoRow.Append("<td></td>"); continue; }
                var qk = chiTiet.FirstOrDefault(x => x.IdNVL == nvl.Id && x.QuyKho.HasValue)?.QuyKho;
                quyKhoRow.Append($"<td class=\"text-right\">{(qk.HasValue ? qk.Value.ToString("#,##0.###") : "")}</td>");
            }
            quyKhoRow.Append("<td></td></tr>");

            // Signatures
            var nguoiTheoDoi = pheDuyets.FirstOrDefault(x => x.CapDuyet == 0);

            var logoBase64 = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            var signTheoDoi = await FormatChuKyBase64Async(nguoiTheoDoi?.ChuKy, nguoiTheoDoi?.TinhTrang == 1);

            var templatePath = Path.Combine(
                _env.WebRootPath,
                "template_html",
                "BM.05-QT.05.09_So_theo_doi_nap_lieu_lo_cao.html");

            var html = await File.ReadAllTextAsync(templatePath);
            html = html
                .Replace("{{LogoUrl}}", logoBase64)
                .Replace("{{LoCao}}", loCao)
                .Replace("{{CaLabel}}", caLabel)
                .Replace("{{NgaySX}}", ngayDisplay)
                .Replace("{{GioBatDau}}", gioBatDau.ToString())
                .Replace("{{NgayBatDau}}", ngayBatDauDisplay)
                .Replace("{{GioKetThuc}}", gioKetThuc.ToString())
                .Replace("{{NgayKetThuc}}", ngayKetThucDisplay)
                .Replace("{{ColGroup}}", colGroup.ToString())
                .Replace("{{TheadRow1}}", th1.ToString())
                .Replace("{{TheadRow2}}", th2.ToString())
                .Replace("{{DataRows}}", dataRows.ToString())
                .Replace("{{TongRow}}", tongRow.ToString())
                .Replace("{{DoAmRow}}", doAmRow.ToString())
                .Replace("{{QuyKhoRow}}", quyKhoRow.ToString())
                .Replace("{{Sign_NguoiTheoDoi}}", signTheoDoi)
                .Replace("{{Ten_NguoiTheoDoi}}", nguoiTheoDoi?.HoVaTen ?? "");

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    PaperSize   = PaperKind.A4,
                    Orientation = Orientation.Landscape,
                },
                Objects =
                {
                    new ObjectSettings
                    {
                        HtmlContent = html,
                        WebSettings =
                        {
                            DefaultEncoding  = "utf-8",
                            LoadImages       = true,
                            EnableJavascript = false,
                            PrintMediaType   = true,
                        },
                        LoadSettings =
                        {
                            BlockLocalFileAccess = false,
                            LoadErrorHandling    = ContentErrorHandling.Ignore,
                        }
                    }
                }
            };

            var pdfBytes = _pdfConverter.Convert(doc);
            var fileName = $"NapLieuLoCao_{phieu.SoPhieu ?? idPhieu.ToString("N")}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            return new ExportFileResult
            {
                Content     = pdfBytes,
                FileName    = fileName,
                ContentType = "application/pdf",
            };
        }

        // ─── Export Excel ─────────────────────────────────────────────────────────

        public async Task<ExportFileResult> ExportNapLieuExcelAsync(Guid idPhieu)
        {
            var phieu = await _repo.GetPhieuByIdAsync(idPhieu)
                ?? throw new Exception("Không tìm thấy phiếu.");

            var chiTiet = await _repo.GetChiTietByPhieuAsync(idPhieu);
            var firstRow = chiTiet.FirstOrDefault();

            var ngay = firstRow?.Ngay ?? DateTime.MinValue;
            var ca = firstRow?.IdCa ?? 0;
            var scope = firstRow?.IdLoCao ?? 0;

            var ngayDisplay = ngay != DateTime.MinValue ? ngay.ToString("dd/MM/yyyy") : "";
            var caLabel = ca == 1 ? "Ca 1" : ca == 2 ? "Ca 2" : $"Ca {ca}";
            var loCao = scope > 0 ? scope.ToString() : "";

            var nvlList = await _repo.GetNvlListAsync(scope > 0 ? scope : null);

            // Chỉ giữ NVL có ít nhất 1 dòng đã nhập giá trị trong phiếu này — loại bỏ NVL không dùng (toàn null)
            var usedNvlIds = chiTiet.Where(x => x.GiaTri.HasValue).Select(x => x.IdNVL).ToHashSet();
            nvlList = nvlList.Where(n => usedNvlIds.Contains(n.Id)).ToList();

            string NvlLabel(LGNLNvlDto n) => n.TenNVL_NM ?? "";

            // Luôn lấy đủ 6 nhóm NVL; NVL sắp xếp theo nhóm rồi theo thứ tự NVL
            var nhomList = (await _repo.GetNhomNvlListAsync(scope > 0 ? scope : null))
                .Select(MapNhomNvl)
                .OrderBy(nh => nh.ThuTu ?? 999)
                .ToList();
            var nvlByNhomId = nvlList
                .GroupBy(n => n.IdNhomNVL ?? 0)
                .ToDictionary(g => g.Key, g => g.OrderBy(n => n.ThuTu ?? 999).ToList());

            // colSlots: 1 entry per NVL; nhóm không có NVL → 1 slot placeholder (Nvl = null)
            var colSlots = new List<(LGNLNhomNvlDto Nhom, LGNLNvlDto? Nvl)>();
            foreach (var nhom in nhomList)
            {
                if (nvlByNhomId.TryGetValue(nhom.Id, out var nvls) && nvls.Count > 0)
                    foreach (var n in nvls) colSlots.Add((nhom, n));
                else
                    colSlots.Add((nhom, null));
            }

            // Fixed prefix columns (6) + NVL/placeholder columns + GhiChu
            int fixedCols = 6;
            int nvlColCount = colSlots.Count;
            int totalCols = fixedCols + nvlColCount + 1; // +1 for GhiChu

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("NapLieu");

            // ── Row 1: Company header ──
            ws.Cell(1, 1).Value = "CÔNG TY CỔ PHẦN THÉP HÒA PHÁT DUNG QUẤT";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Range(1, 1, 1, totalCols).Merge();

            // ── Row 2: Date/Ca ──
            ws.Cell(2, 1).Value = $"Ngày: {ngayDisplay}    {caLabel}    Lò cao: {loCao}";
            ws.Range(2, 1, 2, totalCols).Merge();

            // ── Row 3: Title ──
            ws.Cell(3, 1).Value = $"SỔ THEO DÕI NẠP LIỆU LÒ CAO {loCao}";
            ws.Cell(3, 1).Style.Font.Bold = true;
            ws.Cell(3, 1).Style.Font.FontSize = 14;
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Range(3, 1, 3, totalCols).Merge();

            // ── Rows 4-5: Multi-level column headers ──
            string[] fixedHdrs = { "Số mê", "Mê/giờ", "Thời gian nạp liệu", "Chế độ nạp liệu", "Thuốc thăm liệu 1 (m)", "Thuốc thăm liệu 2 (m)" };
            for (int i = 0; i < fixedCols; i++)
            {
                ws.Range(4, i + 1, 5, i + 1).Merge();
                ws.Cell(4, i + 1).Value = fixedHdrs[i];
                ws.Cell(4, i + 1).Style.Font.Bold = true;
                ws.Cell(4, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
                ws.Cell(4, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(4, i + 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Cell(4, i + 1).Style.Alignment.WrapText = true;
            }

            // NVL columns: luôn xuất đủ 6 nhóm, tên nhóm ở row 4, tên NVL ở row 5
            int col = fixedCols + 1;
            foreach (var nhom in nhomList)
            {
                var nvlsInNhom = nvlByNhomId.TryGetValue(nhom.Id, out var nlist) ? nlist : new List<LGNLNvlDto>();
                if (nvlsInNhom.Count == 0)
                {
                    // Nhóm chưa có NVL → 1 cột placeholder, rowspan=2
                    ws.Range(4, col, 5, col).Merge();
                    ws.Cell(4, col).Value = nhom.TenNhom ?? "";
                    ws.Cell(4, col).Style.Font.Bold = true;
                    ws.Cell(4, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
                    ws.Cell(4, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(4, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Cell(4, col).Style.Alignment.WrapText = true;
                    col++;
                }
                else
                {
                    // Luôn hiển thị tên nhóm ở row 4 (colspan=N), tên NVL ở row 5 (dù chỉ có 1 NVL)
                    if (nvlsInNhom.Count > 1)
                        ws.Range(4, col, 4, col + nvlsInNhom.Count - 1).Merge();
                    ws.Cell(4, col).Value = nhom.TenNhom ?? "";
                    ws.Cell(4, col).Style.Font.Bold = true;
                    ws.Cell(4, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
                    ws.Cell(4, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(4, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Cell(4, col).Style.Alignment.WrapText = true;
                    foreach (var n in nvlsInNhom)
                    {
                        ws.Cell(5, col).Value = NvlLabel(n);
                        ws.Cell(5, col).Style.Font.Bold = true;
                        ws.Cell(5, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
                        ws.Cell(5, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        col++;
                    }
                }
            }

            // GhiChu header (rowspan=2)
            ws.Range(4, col, 5, col).Merge();
            ws.Cell(4, col).Value = "Ghi chú";
            ws.Cell(4, col).Style.Font.Bold = true;
            ws.Cell(4, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#dce6f1");
            ws.Cell(4, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(4, col).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            // Apply borders to header area
            ws.Range(4, 1, 5, totalCols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(4, 1, 5, totalCols).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // ── Data rows ──
            var rowsByThuTu = chiTiet
                .GroupBy(x => x.ThuTu ?? 0)
                .OrderBy(g => g.Key)
                .ToList();

            int dataRow = 6;
            foreach (var grp in rowsByThuTu)
            {
                var sample = grp.First();
                var nvlValues = grp.ToDictionary(x => x.IdNVL, x => x.GiaTri);

                ws.Cell(dataRow, 1).Value = sample.SoMe.HasValue ? (double)sample.SoMe.Value : (double?)null;
                ws.Cell(dataRow, 2).Value = sample.MeGio ?? "";
                ws.Cell(dataRow, 3).Value = sample.ThoiGianNapLieu ?? "";
                ws.Cell(dataRow, 4).Value = sample.CheDo ?? "";
                ws.Cell(dataRow, 5).Value = sample.ThuocThamLieu1.HasValue ? (double)sample.ThuocThamLieu1.Value : (double?)null;
                ws.Cell(dataRow, 6).Value = sample.ThuocThamLieu2.HasValue ? (double)sample.ThuocThamLieu2.Value : (double?)null;

                int c2 = fixedCols + 1;
                foreach (var (_, nvl) in colSlots)
                {
                    if (nvl != null)
                    {
                        var val = nvlValues.TryGetValue(nvl.Id, out var v) ? v : null;
                        if (val.HasValue) ws.Cell(dataRow, c2).Value = (double)val.Value;
                        ws.Cell(dataRow, c2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    }
                    c2++;
                }
                ws.Cell(dataRow, c2).Value = sample.GhiChu ?? "";

                for (int c3 = 1; c3 <= totalCols; c3++)
                    ws.Cell(dataRow, c3).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                dataRow++;
            }

            // ── Tổng cộng ──
            ws.Cell(dataRow, 1).Value = "TỔNG CỘNG";
            ws.Range(dataRow, 1, dataRow, fixedCols).Merge();
            ws.Cell(dataRow, 1).Style.Font.Bold = true;
            ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            int c4 = fixedCols + 1;
            foreach (var (_, nvl) in colSlots)
            {
                if (nvl != null)
                {
                    var total = chiTiet.Where(x => x.IdNVL == nvl.Id).Sum(x => x.GiaTri ?? 0);
                    ws.Cell(dataRow, c4).Value = (double)total;
                    ws.Cell(dataRow, c4).Style.Font.Bold = true;
                    ws.Cell(dataRow, c4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                c4++;
            }
            for (int c5 = 1; c5 <= totalCols; c5++)
                ws.Cell(dataRow, c5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow++;

            // ── Độ ẩm ──
            ws.Cell(dataRow, 1).Value = "Độ ẩm (%)";
            ws.Range(dataRow, 1, dataRow, fixedCols).Merge();
            ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            int c6 = fixedCols + 1;
            foreach (var (_, nvl) in colSlots)
            {
                if (nvl != null)
                {
                    var da = chiTiet.FirstOrDefault(x => x.IdNVL == nvl.Id && x.DoAm.HasValue)?.DoAm;
                    if (da.HasValue) ws.Cell(dataRow, c6).Value = (double)da.Value;
                    ws.Cell(dataRow, c6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                c6++;
            }
            for (int c7 = 1; c7 <= totalCols; c7++)
                ws.Cell(dataRow, c7).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            dataRow++;

            // ── Quy khô ──
            ws.Cell(dataRow, 1).Value = "Quy khô";
            ws.Range(dataRow, 1, dataRow, fixedCols).Merge();
            ws.Cell(dataRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            int c8 = fixedCols + 1;
            foreach (var (_, nvl) in colSlots)
            {
                if (nvl != null)
                {
                    var qk = chiTiet.FirstOrDefault(x => x.IdNVL == nvl.Id && x.QuyKho.HasValue)?.QuyKho;
                    if (qk.HasValue) ws.Cell(dataRow, c8).Value = (double)qk.Value;
                    ws.Cell(dataRow, c8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }
                c8++;
            }
            for (int c9 = 1; c9 <= totalCols; c9++)
                ws.Cell(dataRow, c9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            // ── Column widths ──
            ws.Column(1).Width = 8;
            ws.Column(2).Width = 10;
            ws.Column(3).Width = 14;
            ws.Column(4).Width = 14;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 12;
            for (int c10 = fixedCols + 1; c10 <= fixedCols + nvlColCount; c10++)
                ws.Column(c10).Width = 12;
            ws.Column(totalCols).Width = 20;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            var fileName = $"NapLieuLoCao_{phieu.SoPhieu ?? idPhieu.ToString("N")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return new ExportFileResult
            {
                Content = ms.ToArray(),
                FileName = fileName,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            };
        }

        private async Task<string> ConvertImageUrlToBase64Async(string imageUrl)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var bytes = await client.GetByteArrayAsync(imageUrl);
                var ext   = Path.GetExtension(imageUrl).TrimStart('.').ToLower();
                var mime  = ext == "png" ? "image/png" : "image/jpeg";
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch
            {
                return imageUrl;
            }
        }

        private async Task<string> FormatChuKyBase64Async(string? chuKy, bool daKy = false)
        {
            if (string.IsNullOrWhiteSpace(chuKy))
            {
                if (daKy)
                    return @"<div style='text-align:center'>
                        <div style='font-style:italic;color:red'>Đã ký</div>
                        <div style='font-size:11px;color:red'>(Chưa cập nhật chữ ký)</div>
                    </div>";
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
            else if (chuKy.StartsWith("/"))
            {
                var domain  = _configuration.GetValue<string>("AppSettings:Domain") ?? "https://report.hoaphatdungquat.vn";
                var fullUrl = domain.TrimEnd('/') + chuKy;
                var base64  = await ConvertImageUrlToBase64Async(fullUrl);
                if (!string.IsNullOrEmpty(base64))
                    return $"<img src=\"{base64}\" style=\"max-width:150px;max-height:80px;\" />";
            }

            return @"<div style='text-align:center'>
                <div style='font-style:italic;color:red'>Đã ký</div>
                <div style='font-size:11px;color:red'>(Chưa cập nhật chữ ký)</div>
            </div>";
        }

        // ─── JSON helpers ─────────────────────────────────────────────────────────

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

        private static decimal? TryGetDecimal(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!obj.TryGetProperty(key, out var val) || val.ValueKind == JsonValueKind.Null)
                    continue;

                if (val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out var d))
                    return d;

                if (val.ValueKind == JsonValueKind.String
                    && decimal.TryParse(val.GetString(), out var s))
                    return s;
            }
            return null;
        }

        // Xử lý giá trị có thể là Number hoặc String (frontend đôi khi gửi số dạng chuỗi)
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
