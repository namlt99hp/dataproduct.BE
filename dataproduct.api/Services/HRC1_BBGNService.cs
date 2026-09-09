using ClosedXML.Excel;
using dataproduct.api.DTOs;
using dataproduct.api.DTOs.Export;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace dataproduct.api.Services
{
    public class HRC1_BBGNService
    {
        private readonly IHRC1_BBGNRepository _repo;
        private readonly BBGN_ThepLongService _bbgnSvc;
        private readonly PheDuyetService _pheDuyetSvc;
        private readonly IConverter _pdfConverter;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ProductDataMasterDbContext _masterCtx;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly BmConfigService _bmConfig;

        public HRC1_BBGNService(
            IHRC1_BBGNRepository repo,
            BBGN_ThepLongService bbgnSvc,
            PheDuyetService pheDuyetSvc,
            IConverter pdfConverter,
            IWebHostEnvironment env,
            IConfiguration config,
            ProductDataMasterDbContext masterCtx,
            IHttpClientFactory httpClientFactory,
            BmConfigService bmConfig)
        {
            _repo = repo;
            _bbgnSvc = bbgnSvc;
            _pheDuyetSvc = pheDuyetSvc;
            _pdfConverter = pdfConverter;
            _env = env;
            _config = config;
            _masterCtx = masterCtx;
            _httpClientFactory = httpClientFactory;
            _bmConfig = bmConfig;
        }

        // -------------------------------------------------------
        // JSON partial-patch helpers — phân biệt "field không gửi lên" (giữ nguyên)
        // với "field gửi giá trị null" (user xóa input, phải lưu null). Deserialize thẳng
        // sang DTO bằng "??" không phân biệt được 2 trường hợp này vì cả 2 đều ra null.
        // -------------------------------------------------------
        private static readonly JsonSerializerOptions _jsonPatchOpts = new() { PropertyNameCaseInsensitive = true };

        private static HashSet<string> ProvidedKeys(JsonElement body) =>
            body.ValueKind == JsonValueKind.Object
                ? new HashSet<string>(body.EnumerateObject().Select(p => p.Name), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>();

        // -------------------------------------------------------
        // GET — phiếu bất kỳ công đoạn (lo_thoi | tinh_luyen | duc)
        // loSo: lọc mẻ theo lò thổi (cho lo_thoi phiếu); scopePhieu: lọc theo TL scope (tinh_luyen); idMayDuc: override scope cho duc
        // -------------------------------------------------------
        public async Task<HRC1_PhieuDataVm> GetPhieuAsync(Guid idPhieu, int? loSo = null, int? scopePhieu = null, int? idMayDuc = null)
        {
            var phieu = await _repo.GetBmPhieuAsync(idPhieu)
                ?? throw new KeyNotFoundException($"Không tìm thấy phiếu {idPhieu}");

            var congDoan = phieu.MaBm switch
            {
                "HRC1_LoThoi" => "lo_thoi",
                "HRC1_TinhLuyen" => "tinh_luyen",
                "HRC1_BBGN_ThepLong" => "duc",
                _ => phieu.MaBm ?? string.Empty
            };

            var mayDucs = await _repo.GetMayDucsHRC1Async();

            List<HRC1_MeThepVm> danhSachMe;
            List<HRC1_MeThep> rawMes;

            if (congDoan == "duc")
            {
                var effectiveMayDuc = idMayDuc ?? phieu.Scope;
                rawMes = phieu.NgaySX.HasValue && phieu.Ca.HasValue && effectiveMayDuc.HasValue
                    ? await _repo.GetMeThepsByMayDucAsync(phieu.NgaySX.Value, phieu.Ca.Value, effectiveMayDuc.Value)
                    : new List<HRC1_MeThep>();

                danhSachMe = rawMes
                    .Select(m => MapToVm(m, new HRC1_MePhanCong { MeId = m.Id, IdPhieu = idPhieu, CongDoan = "duc" }, mayDucs))
                    .ToList();

                // Load TL nào đã nhận mẻ + chuyenVe info cho DucPanel
                if (danhSachMe.Count > 0)
                {
                    var ducMeIds = danhSachMe.Select(m => m.Id).ToList();

                    var tlScopes = await _repo.GetTLScopesByMeIdsAsync(ducMeIds);
                    foreach (var meVm in danhSachMe)
                        if (tlScopes.TryGetValue(meVm.Id, out var scope))
                            meVm.SoTinhLuyenNhan = scope;

                    var chuyenVeByMeId = await _repo.GetChuyenVeByMeIdsAsync(ducMeIds);
                    foreach (var meVm in danhSachMe)
                    {
                        if (chuyenVeByMeId.TryGetValue(meVm.Id, out var cv))
                        {
                            meVm.ChuyenVeMaMe = cv.MaMe;
                            meVm.TenMayDucChuyen = cv.IdMayDucDich.HasValue
                                ? mayDucs.FirstOrDefault(d => d.Id == cv.IdMayDucDich.Value)?.TenMayDuc
                                : null;
                        }
                    }
                }
            }
            else
            {
                // lo_thoi: lọc theo loSo; tinh_luyen: lọc theo scopePhieu; null = trả tất cả
                int? effectiveScope = congDoan == "lo_thoi" ? loSo : scopePhieu;
                var phanCongs = await _repo.GetMePhanCongsByPhieuAsync(idPhieu, congDoan, effectiveScope);
                var meIds = phanCongs.Select(pc => pc.MeId).Distinct().ToList();
                rawMes = await _repo.GetMeThepsByIdsAsync(meIds);
                var meDict = rawMes.ToDictionary(m => m.Id);

                // Tải mẻ đích ChuyenVeMe để tra tên máy đúc (tinh_luyen: chuyển TL; lo_thoi: chuyển mẻ lên thẳng)
                Dictionary<int, HRC1_MeThep> chuyenVeDict = new();
                if (congDoan == "tinh_luyen" || congDoan == "lo_thoi")
                {
                    var chuyenVeIds = phanCongs
                        .Where(pc => pc.ChuyenVeMeId.HasValue)
                        .Select(pc => pc.ChuyenVeMeId!.Value)
                        .Distinct().ToList();
                    if (chuyenVeIds.Count > 0)
                        chuyenVeDict = (await _repo.GetMeThepsByIdsAsync(chuyenVeIds))
                            .ToDictionary(m => m.Id);
                }

                danhSachMe = phanCongs.Select(pc =>
                {
                    meDict.TryGetValue(pc.MeId, out var m);
                    return MapToVm(m, pc, mayDucs, chuyenVeDict);
                }).ToList();

                if (congDoan == "lo_thoi" && meIds.Count > 0)
                {
                    var tlScopes = await _repo.GetTLScopesByMeIdsAsync(meIds);
                    foreach (var meVm in danhSachMe)
                        if (tlScopes.TryGetValue(meVm.Id, out var scope))
                            meVm.SoTinhLuyenNhan = scope;
                }
            }

            // Tra cứu tên người sửa cuối theo công đoạn
            var rawMeDict = rawMes.ToDictionary(m => m.Id);
            var auditIds = danhSachMe
                .Select(vm => congDoan switch
                {
                    "lo_thoi" => rawMeDict.GetValueOrDefault(vm.Id)?.CapNhatBoi,
                    "tinh_luyen" => rawMeDict.GetValueOrDefault(vm.Id)?.CapNhatBoiTL,
                    "duc" => rawMeDict.GetValueOrDefault(vm.Id)?.CapNhatBoiDuc,
                    _ => null
                })
                .Where(id => id.HasValue).Select(id => id!.Value)
                .Distinct().ToList();

            var userNames = auditIds.Count > 0
                ? await _repo.GetUserNamesByIdsAsync(auditIds)
                : new Dictionary<int, string?>();

            foreach (var meVm in danhSachMe)
            {
                var raw = rawMeDict.GetValueOrDefault(meVm.Id);
                int? uid = congDoan switch
                {
                    "lo_thoi" => raw?.CapNhatBoi,
                    "tinh_luyen" => raw?.CapNhatBoiTL,
                    "duc" => raw?.CapNhatBoiDuc,
                    _ => null
                };
                if (uid.HasValue && userNames.TryGetValue(uid.Value, out var name))
                    meVm.TenCapNhatBoi = name;
            }

            // Tính KlThepLongChot theo cùng logic với thong-ke
            var klChotMeIds = danhSachMe.Where(vm => vm.Id > 0).Select(vm => vm.Id).ToList();
            if (klChotMeIds.Count > 0)
            {
                var klChotMap = await _repo.ComputeKlThepLongChotAsync(klChotMeIds);
                foreach (var meVm in danhSachMe)
                    if (klChotMap.TryGetValue(meVm.Id, out var klChot))
                        meVm.KlThepLongChot = klChot;
            }

            var vm = new HRC1_PhieuDataVm
            {
                IdPhieu = phieu.Idphieu,
                SoPhieu = phieu.SoPhieu,
                MaBm = phieu.MaBm,
                CongDoan = congDoan,
                Scope = phieu.Scope,
                NgaySX = phieu.NgaySX,
                Ca = phieu.Ca,
                Kip = phieu.Kip,
                TinhTrang = phieu.TinhTrang,
                DanhSachMe = danhSachMe,
                DanhSachMayDuc = await BuildDanhSachMayDucAsync(congDoan, phieu.NgaySX, phieu.Ca, mayDucs)
            };

            if (congDoan == "tinh_luyen")
                vm.ChoNhan = (await _repo.GetChoNhanAsync())
                    .Select(m => new HRC1_ChoNhanMeVm
                    {
                        MeId = m.Id,
                        MaMe = m.MaMe,
                        ThungSo = m.ThungSo,
                        LoSo = m.LoSo,
                        ThoiGian = m.ThoiGian,
                        KlThepLong = m.KlThepLong,
                        TLDichSo = m.TLDichSo
                    }).ToList();

            return vm;
        }

        // -------------------------------------------------------
        // GET — danh sách mẻ chờ TL nhận có filter + phân trang
        // -------------------------------------------------------
        public Task<HRC1_PagedResult<HRC1_ChoNhanMeVm>> GetMeChoNhanPagedAsync(HRC1_GetMeChoNhanQuery q) =>
            _repo.GetMeChoNhanPagedAsync(q.TuNgay, q.DenNgay, q.Ca, q.MaMe, q.ThungSo, q.LoSo, q.Page, q.PageSize);

        // -------------------------------------------------------
        // GET — danh sách mẻ chờ TL nhận (dùng riêng nếu cần)
        // -------------------------------------------------------
        public async Task<List<HRC1_ChoNhanMeVm>> GetChoNhanAsync() =>
            (await _repo.GetChoNhanAsync())
                .Select(m => new HRC1_ChoNhanMeVm
                {
                    MeId = m.Id,
                    MaMe = m.MaMe,
                    ThungSo = m.ThungSo,
                    LoSo = m.LoSo,
                    ThoiGian = m.ThoiGian,
                    KlThepLong = m.KlThepLong,
                    TLDichSo = m.TLDichSo
                }).ToList();

        // -------------------------------------------------------
        // LÒ THỔI
        // -------------------------------------------------------
        public async Task UpdateMeAsync(int meId, JsonElement body, int userId)
        {
            var req = body.Deserialize<HRC1_LoThoiUpdateRequest>(_jsonPatchOpts) ?? new HRC1_LoThoiUpdateRequest();
            var provided = ProvidedKeys(body);
            bool P(string name) => provided.Contains(name);

            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");
            if (me.TrangThaiLo >= 1)
                throw new InvalidOperationException("Mẻ đã xác nhận, không thể chỉnh sửa.");
            // Đúc "xác nhận" chỉ là trạng thái tạm (còn có thể "Hủy xác nhận") — không chặn lò thổi sửa/lưu ở
            // bước này. Chỉ khi mẻ đã CHỐT (IsChot, khóa vĩnh viễn) mới thực sự cấm nhập và lưu.
            if (me.IsChot == true)
                throw new InvalidOperationException("Mẻ đã chốt, không thể chỉnh sửa.");

            // Nếu TL đã nhận mẻ thì lò thổi không được chuyển sang lên thẳng nữa
            if (req.DichChuyen == "len_thang" && (me.TrangThaiTL ?? 0) >= 1)
                throw new InvalidOperationException("Tinh luyện đã nhận mẻ này. Chỉ có thể chọn tinh luyện để tham khảo, không thể chuyển sang lên thẳng.");

            // Ngược lại: nếu Đúc đã xác nhận mẻ lên thẳng thì không được chuyển sang tinh luyện nữa.
            // Nhánh previouslyLenThang bên dưới sẽ reset IdMayDucDich (thuộc về TL) — nếu vẫn cho đổi
            // trong khi TrangThaiDuc = 1, mẻ mất hẳn liên kết với máy đúc đã xác nhận (Đúc xác định mẻ
            // của mình bằng IdMayDucDich, không qua bảng phân công) nhưng vẫn mang trạng thái "đã xác
            // nhận" — kẹt vĩnh viễn: không máy đúc nào còn thấy để Bỏ xác nhận, TL cũng không sửa được
            // (UpdateMePhanCongAsync chặn khi TrangThaiDuc >= 1). Phải Bỏ xác nhận đúc trước.
            if (req.DichChuyen == "tinh_luyen" && me.DichChuyen == "len_thang" && (me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Máy đúc đã xác nhận mẻ này. Cần Bỏ xác nhận đúc trước khi chuyển sang tinh luyện.");

            // Đã có xác nhận/không xác nhận PCN ở máy đúc — phải Reset xác nhận PCN về null trước khi đổi Thử nghiệm
            if (req.IsThuNghiem.HasValue && req.IsThuNghiem != me.IsThuNghiem && me.TrangThaiPCN != null)
                throw new InvalidOperationException("Mẻ đã có xác nhận/không xác nhận PCN. Cần Reset xác nhận PCN trước khi đổi Thử nghiệm.");

            var oldDich = me.DichChuyen;
            var old = Snapshot(me);
            var loThoiPc = await _repo.GetLoThoiMePhanCongByMeIdAsync(meId);

            // Dùng P("field") thay cho "req.Field ?? me.Field": field không có trong JSON (không sửa) → giữ
            // nguyên; field có trong JSON dù giá trị là null (user xóa input) → phải lưu null. "??" không phân
            // biệt được 2 trường hợp này (cả 2 đều là null sau khi deserialize) nên trước đây không xóa được.
            me.ThungSo = P("thungSo") ? req.ThungSo : me.ThungSo;
            me.KLLFSauThep = P("kllfSauThep") ? req.KLLFSauThep : me.KLLFSauThep;
            me.KlLan3 = P("klLan3") ? req.KlLan3 : me.KlLan3;
            if (P("dichChuyen") && req.DichChuyen is not null)
            {
                var previouslyLenThang = me.DichChuyen == "len_thang";
                me.DichChuyen = req.DichChuyen;

                if (req.DichChuyen == "len_thang")
                {
                    // Lên thẳng: lò thổi chỉ định máy đúc trực tiếp; xóa TL đích
                    me.IdMayDucDich = req.IdMayDucDich;
                    me.TLDichSo = null;
                }
                else // "tinh_luyen"
                {
                    me.TLDichSo = req.TLDichSo;
                    // IdMayDucDich thuộc về tinh luyện — không ghi đè.
                    // Ngoại lệ: nếu trước đó là len_thang thì reset để TL tự chọn lại.
                    if (previouslyLenThang)
                    {
                        me.IdMayDucDich = null;
                        // Không còn lên thẳng nữa → xóa chuyển mẻ cũ (không còn ý nghĩa)
                        if (loThoiPc != null)
                            loThoiPc.ChuyenVeMeId = null;
                        // ThoiGian/KlLan2/KlThepLong là dữ liệu lò thổi tự nhập khi lên thẳng (không qua TL).
                        // Chỉ xóa khi THỰC SỰ chuyển từ lên thẳng về tinh luyện — nếu mẻ đã ở tinh luyện từ
                        // trước thì đây chính là các cột TL đang dùng để lưu dữ liệu đã nhập, không được xóa
                        // mỗi lần lò thổi lưu lại (bug: TL nhập xong bị mất khi lò thổi sửa dữ liệu khác).
                        me.ThoiGian = null;
                        me.KlLan2 = null;
                        me.KlThepLong = null;
                    }
                }
            }
            else if (!P("dichChuyen"))
            {
                me.TLDichSo = P("tlDichSo") ? req.TLDichSo : me.TLDichSo;
                me.IdMayDucDich = P("idMayDucDich") ? req.IdMayDucDich : me.IdMayDucDich;
            }
            // Lò thổi tự nhập ThoiGian, KlLan2 & KlThepLong khi mẻ đi thẳng lên máy đúc (không qua TL)
            if (me.DichChuyen == "len_thang")
            {
                me.ThoiGian = P("thoiGian") ? req.ThoiGian : me.ThoiGian;
                me.KlLan2 = P("klLan2") ? req.KlLan2 : me.KlLan2;
                me.KlThepLong = P("klThepLong") ? req.KlThepLong : me.KlThepLong;
                // Chuyển mẻ (gộp vào máy đúc của mẻ khác) — lưu trên MePhanCong "lo_thoi", luôn ghi đè để cho phép xóa (set null)
                if (loThoiPc != null)
                    loThoiPc.ChuyenVeMeId = req.ChuyenVeMeId;
            }
            me.IsThuNghiem = P("isThuNghiem") ? req.IsThuNghiem : me.IsThuNghiem;
            me.IsTrungMeThoi = P("isTrungMeThoi") ? req.IsTrungMeThoi : me.IsTrungMeThoi;
            me.GhiChuLo = P("ghiChuLo") ? req.GhiChuLo : me.GhiChuLo;
            me.KLThepLongPhanBo = P("klThepLongPhanBo") ? req.KlThepLongPhanBo : me.KLThepLongPhanBo;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "chinh_sua",
                DuLieuCu = old,
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();

            // Nếu trạng thái len_thang thay đổi (thêm vào hoặc bỏ ra), recalc IsTrungMeThoi
            bool lenThangChanged = (oldDich == "len_thang") != (me.DichChuyen == "len_thang");
            if (lenThangChanged && !string.IsNullOrEmpty(me.MaMe))
            {
                await RecalcTrungMeByMaMeAsync(me.MaMe);
                await _repo.SaveChangesAsync();
            }
        }

        public async Task XacNhanLoThoiAsync(int meId, int userId)
        {
            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");
            if (me.TrangThaiLo >= 1)
                throw new InvalidOperationException("Mẻ đã xác nhận.");

            me.TrangThaiLo = 1;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "xac_nhan",
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();
        }

        public async Task BoXacNhanLoThoiAsync(int meId, int userId)
        {
            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");
            if (me.TrangThaiLo != 1)
                throw new InvalidOperationException("Mẻ chưa ở trạng thái xác nhận.");
            if (me.TrangThaiTL >= 1)
                throw new InvalidOperationException("TL đã nhận mẻ, không thể bỏ xác nhận.");

            me.TrangThaiLo = 0;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "bo_xac_nhan",
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();
        }

        public async Task LamMoiAsync(int meId, int userId)
        {
            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");
            if (me.TrangThaiTL >= 1 || me.TrangThaiDuc >= 1)
                throw new InvalidOperationException("TL hoặc đúc đã xác nhận, không thể làm mới.");

            var old = Snapshot(me);

            me.KLLFSauThep = null;
            me.KlLan3 = null;
            me.DichChuyen = null;
            me.TLDichSo = null;
            me.IdMayDucDich = null;
            me.IsThuNghiem = null;
            me.IsTrungMeThoi = null;
            me.GhiChuLo = null;
            me.TrangThaiLo = null;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "lam_moi",
                DuLieuCu = old,
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // GET — mẻ BBGN thép lỏng (công đoạn đúc) lọc theo khoảng ThoiGian thực tế
        // Lọc phiếu BM_Phieu (MaBm=HRC1_BBGN_ThepLong) có ngày/ca giao với [fromDate,toDate],
        // rồi dùng đúng logic lấy mẻ của GetPhieuAsync (nhánh "duc"): GetMeThepsByMayDucAsync
        // theo phieu.NgaySX/Ca/Scope. ThoiGian trong HRC1_MeThep chỉ là chuỗi HH:mm không có
        // ngày nên phải quy theo ca của phiếu (ca 1: 08h-20h cùng ngày NgaySX; ca 2: 20h NgaySX
        // -> 08h ngày kế) mới tính được ngày thực, sau đó lọc lại đúng khoảng fromDate-toDate.
        // -------------------------------------------------------
        public async Task<List<HRC1_MeTheoThoiGianVm>> GetMeTheoThoiGianAsync(DateTime fromDate, DateTime toDate)
        {
            if (toDate < fromDate)
                throw new InvalidOperationException("toDate phải lớn hơn hoặc bằng fromDate.");

            var shifts = new HashSet<(DateOnly Ngay, int Ca)>();
            var day = DateOnly.FromDateTime(fromDate).AddDays(-1);
            var lastDay = DateOnly.FromDateTime(toDate);
            while (day <= lastDay)
            {
                var ca1Start = day.ToDateTime(new TimeOnly(8, 0));
                var ca1End = day.ToDateTime(new TimeOnly(20, 0));
                var ca2Start = ca1End;
                var ca2End = day.AddDays(1).ToDateTime(new TimeOnly(8, 0));

                if (ca1Start < toDate && ca1End > fromDate)
                    shifts.Add((day, 1));
                if (ca2Start < toDate && ca2End > fromDate)
                    shifts.Add((day, 2));

                day = day.AddDays(1);
            }

            var ngays = shifts.Select(s => s.Ngay).Distinct().ToList();
            var phieus = (await _repo.GetPhieuThepLongByNgaysAsync(ngays))
                .Where(p => p.NgaySX.HasValue && p.Ca.HasValue && p.Scope.HasValue
                         && shifts.Contains((p.NgaySX.Value, p.Ca.Value)))
                .ToList();

            var mayDucDict = (await _repo.GetMayDucsHRC1Async())
                .ToDictionary(m => m.Id, m => m.TenMayDuc);

            var raw = new List<(DateTime Actual, string? MaMe, decimal? KlThepLong, string? TenMayDuc,
                string? ThungSo, decimal? KLLFSauThep, decimal? KlLan1, decimal? KlLan2, decimal? KlLan3,
                bool? IsThuNghiem, string? PhanLoai, string? MacThepBKMIS, string? TrangThai)>();
            var seenMeIds = new HashSet<int>();

            foreach (var phieu in phieus)
            {
                var ngay = phieu.NgaySX!.Value;
                var ca = phieu.Ca!.Value;
                var idMayDuc = phieu.Scope!.Value;

                var mes = await _repo.GetMeThepsByMayDucAsync(ngay, ca, idMayDuc);
                foreach (var m in mes)
                {
                    if (!seenMeIds.Add(m.Id)) continue;
                    // Chưa nhập ThoiGian thì bỏ qua
                    if (string.IsNullOrWhiteSpace(m.ThoiGian)) continue;
                    if (!TimeOnly.TryParseExact(m.ThoiGian, "HH:mm",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var gio))
                        continue;

                    // Ca 1: toàn bộ giờ thuộc cùng ngày NgaySX.
                    // Ca 2: 20h-23h59 thuộc NgaySX, 00h-07h59 thuộc ngày kế tiếp.
                    var ngayThucTe = ca == 1 || gio.Hour >= 20
                        ? ngay
                        : ngay.AddDays(1);

                    var actual = ngayThucTe.ToDateTime(gio);
                    if (actual < fromDate || actual > toDate) continue;

                    mayDucDict.TryGetValue(idMayDuc, out var tenMayDuc);
                    raw.Add((actual, m.MaMe, m.KlThepLong, tenMayDuc,
                        m.ThungSo, m.KLLFSauThep, m.KlLan1, m.KlLan2, m.KlLan3,
                        m.IsThuNghiem, m.PhanLoai, m.MacThepBKMIS, m.TrangThaiDuc  == 1 ? "Đã xác nhận" : "Chưa xác nhận"));
                }
            }

            return raw
                .OrderBy(r => r.Actual)
                .Select(r => new HRC1_MeTheoThoiGianVm
                {
                    MeThoi = r.MaMe,
                    KLThepLong = r.KlThepLong,
                    ThoiGian = $"{r.Actual:dd/MM/yyyy} {r.Actual.Hour}h{r.Actual.Minute:D2}",
                    MayDuc = r.TenMayDuc,
                    ThungSo = r.ThungSo,
                    KLLFSauThep = r.KLLFSauThep,
                    KLLan1 = r.KlLan1,
                    KLLan2 = r.KlLan2,
                    KLLan3 = r.KlLan3,
                    IsThuNghiem = r.IsThuNghiem,
                    PhanLoai = r.PhanLoai,
                    MacThepBKMIS = r.MacThepBKMIS,
                    TrangThai = r.TrangThai
                }).ToList();
        }

        // -------------------------------------------------------
        // TINH LUYỆN
        // -------------------------------------------------------
        public async Task NhanMeAsync(HRC1_NhanMeRequest req, int userId)
        {
            var me = await _repo.GetMeByIdAsync(req.MeId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {req.MeId}");

            if (me.DichChuyen == "len_thang")
                throw new InvalidOperationException("Lò thổi đã chỉ định mẻ này lên thẳng máy đúc, không thể nhận vào tinh luyện.");

            // Phòng hờ mẻ vừa được lò thổi chuyển len_thang → tinh_luyen trong khi Đúc đã xác nhận
            // (đáng lẽ đã bị chặn ở UpdateMeAsync) — không cho TL nhận đè lên trạng thái Đúc còn treo,
            // tránh lặp lại đúng lỗi "mẻ vô định" (TrangThaiDuc=1 nhưng mất IdMayDucDich).
            if ((me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Máy đúc đã xác nhận mẻ này, không thể nhận vào tinh luyện.");

            // Mẻ chưa được lò thổi chọn đích (DichChuyen = null) → tự động gán tinh_luyen khi TL nhận
            if (string.IsNullOrEmpty(me.DichChuyen))
                me.DichChuyen = "tinh_luyen";

            if (await _repo.ExistsMePhanCongAsync(req.MeId, "tinh_luyen"))
                throw new InvalidOperationException("Mẻ đã được nhận vào tinh luyện.");

            var phieuTL = await _repo.GetBmPhieuAsync(req.IdPhieu);
            if (phieuTL?.NgaySX.HasValue == true && phieuTL.Ca.HasValue)
            {
                me.NgayNhanTL = phieuTL.NgaySX.Value.ToDateTime(TimeOnly.MinValue);
                me.CaTinhLuyen = phieuTL.Ca;
                me.KipTinhLuyen = phieuTL.Kip;
            }

            _repo.AddMePhanCong(new HRC1_MePhanCong
            {
                MeId = req.MeId,
                IdPhieu = req.IdPhieu,
                CongDoan = "tinh_luyen",
                ThuTuTL = null,
                ScopePhieu = req.ScopePhieu
            });

            me.TrangThaiTL = 1;
            me.CapNhatLuc = DateTime.Now;
            me.CapNhatBoiTL = userId;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "nhan_me",
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();

            // Liên kết sang Tiêu hao LF: 1 mẻ TL nhận = 1 dòng Hrc1TieuHao(BieuMau=LF), cùng
            // Ngày/Ca (đúng công đoạn tinh luyện, KHÔNG phải Ca đúc đã chuyển) + Scope=TL số.
            if (phieuTL?.NgaySX.HasValue == true && phieuTL.Ca.HasValue)
                await _repo.UpsertLfTieuHaoFromMeAsync(me, req.ScopePhieu, phieuTL.NgaySX, phieuTL.Ca);

            // Recalc trùng: mẻ vừa được TL nhận có thể conflict với len_thang hoặc TL khác
            await RecalcTrungMeByMaMeAsync(me.MaMe);
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // TINH LUYỆN — Chuyển ca Đúc (routing sang phiếu Đúc ca trước/sau, không đổi Ca của chính TL)
        // Ví dụ: TL luyện xong mẻ lúc 19:59 vẫn thuộc ca đầu (đúng cho TL/Tiêu hao LF), nhưng đem
        // đúc thì thực tế đã rơi vào ca sau (hoặc ngược lại: luyện đầu ca sau nhưng đúc kịp từ cuối
        // ca trước). Bấm "Chuyển ca" chỉ đẩy routing sang phiếu Đúc ca trước/sau, NgayNhanTL/CaTinhLuyen
        // (và dòng Tiêu hao LF liên kết) giữ nguyên không đổi.
        // huong: "truoc" | "sau"
        // -------------------------------------------------------
        public async Task ChuyenCaDucAsync(int meId, string huong, int userId)
        {
            if (huong != "truoc" && huong != "sau")
                throw new InvalidOperationException("Hướng chuyển ca không hợp lệ.");

            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");

            if ((me.TrangThaiTL ?? 0) < 1)
                throw new InvalidOperationException("Mẻ chưa được nhận vào tinh luyện.");
            if ((me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Máy đúc đã xác nhận mẻ này, không thể chuyển ca.");
            if (!me.IdMayDucDich.HasValue)
                throw new InvalidOperationException("Chưa chọn máy đúc đích, không thể chuyển ca.");
            if (!me.NgayNhanTL.HasValue || !me.CaTinhLuyen.HasValue)
                throw new InvalidOperationException("Mẻ chưa xác định được ngày/ca tinh luyện.");

            var ngayHienTai = me.NgaySXDucChuyen ?? DateOnly.FromDateTime(me.NgayNhanTL.Value);
            var caHienTai = me.CaDucChuyen ?? me.CaTinhLuyen.Value;

            DateOnly ngayMoi; int caMoi;
            if (huong == "truoc")
                (ngayMoi, caMoi) = caHienTai == 1
                    ? (ngayHienTai.AddDays(-1), 2)
                    : (ngayHienTai, 1);
            else
                (ngayMoi, caMoi) = caHienTai == 2
                    ? (ngayHienTai.AddDays(1), 1)
                    : (ngayHienTai, 2);

            // Nếu phiếu đúc ca đích đã tồn tại và đã chốt (TinhTrang=5) thì không cho chuyển vào nữa
            var phieuDich = await _repo.GetPhieuDucAsync(ngayMoi, caMoi, me.IdMayDucDich.Value);
            if (phieuDich?.TinhTrang == 5)
                throw new InvalidOperationException("Phiếu đúc ca đích đã chốt, không thể chuyển mẻ sang ca đó.");

            // Kíp trực của ca đích — khác kíp TL đã nhận mẻ (KipTinhLuyen) vì đây là ca/ngày Đúc thực tế
            var kipMoi = await _masterCtx.Tbl_Kip.AsNoTracking()
                .Where(k => k.NgayLamViec == ngayMoi && k.TenCa == caMoi.ToString())
                .Select(k => k.TenKip)
                .FirstOrDefaultAsync();

            var old = Snapshot(me);
            me.NgaySXDucChuyen = ngayMoi;
            me.CaDucChuyen = caMoi;
            me.KipDucChuyen = kipMoi;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;
            me.CapNhatBoiTL = userId;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "chuyen_ca_duc",
                DuLieuCu = old,
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();
        }

        public async Task HuyChuyenCaDucAsync(int meId, int userId)
        {
            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");

            if (!me.NgaySXDucChuyen.HasValue && !me.CaDucChuyen.HasValue)
                throw new InvalidOperationException("Mẻ này chưa được chuyển ca.");
            if ((me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Máy đúc đã xác nhận mẻ này, không thể hủy chuyển ca.");

            var old = Snapshot(me);
            me.NgaySXDucChuyen = null;
            me.CaDucChuyen = null;
            me.KipDucChuyen = null;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;
            me.CapNhatBoiTL = userId;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "huy_chuyen_ca_duc",
                DuLieuCu = old,
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();
        }

        public async Task UpdateMePhanCongAsync(int mePhanCongId, JsonElement body, int userId)
        {
            var req = body.Deserialize<HRC1_TinhLuyenUpdateRequest>(_jsonPatchOpts) ?? new HRC1_TinhLuyenUpdateRequest();
            var provided = ProvidedKeys(body);
            bool P(string name) => provided.Contains(name);

            var pc = await _repo.GetMePhanCongByIdAsync(mePhanCongId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ phân công {mePhanCongId}");

            var me = await _repo.GetMeByIdAsync(pc.MeId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {pc.MeId}");
            if (me.TrangThaiDuc >= 1)
                throw new InvalidOperationException("Mẻ đã được máy đúc xác nhận, không thể chỉnh sửa.");

            var old = Snapshot(me);

            // P("field") thay cho "req.Field ?? me.Field" — cho phép TL xóa input (lưu null) mà không bị
            // "??" fallback về giá trị cũ (field không gửi lên và field gửi null đều ra null sau deserialize).
            me.ThoiGian = P("thoiGian") ? req.ThoiGian : me.ThoiGian;
            me.KlLan1 = P("klLan1") ? req.KlLan1 : me.KlLan1;
            me.KlLan2 = P("klLan2") ? req.KlLan2 : me.KlLan2;
            me.KlLan3 = P("klLan3") ? req.KlLan3 : me.KlLan3;
            me.KlThepLong = P("klThepLong") ? req.KlThepLong : me.KlThepLong;
            me.IdMayDucDich = P("idMayDucDich") ? req.IdMayDucDich : me.IdMayDucDich;
            me.PhanLoai = P("phanLoai") ? req.PhanLoai : me.PhanLoai;
            me.MacThep = P("macThep") ? req.MacThep : me.MacThep;
            me.MacThepBKMIS = P("macThepBKMIS") ? req.MacThepBKMIS : me.MacThepBKMIS;
            me.IdMacThep = P("idMacThep") ? req.IdMacThep : me.IdMacThep;
            me.GhiChuTL = P("ghiChuTL") ? req.GhiChuTL : me.GhiChuTL;
            // Chỉ mẻ IsManualTL mới được TinhLuyen ghi các field thường do LoThoi nhập
            if (me.IsManualTL == true)
            {
                me.ThungSo = P("thungSo") ? req.ThungSo : me.ThungSo;
                me.KLLFSauThep = P("kllfSauThep") ? req.KllfSauThep : me.KLLFSauThep;
            }
            me.CapNhatLuc = DateTime.Now;
            me.CapNhatBoiTL = userId;

            // ChuyenVeMeId lưu trên MePhanCong — luôn ghi đè để cho phép xóa (set null)
            pc.ChuyenVeMeId = req.ChuyenVeMeId;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "chinh_sua",
                DuLieuCu = old,
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();

            // Đồng bộ MacThep/KlThepLong mới nhất sang dòng Tiêu hao LF liên kết (Ngày/Ca giữ theo
            // đúng công đoạn tinh luyện — NgayNhanTL/CaTinhLuyen, không phải Ca đúc đã chuyển).
            if (me.NgayNhanTL.HasValue && me.CaTinhLuyen.HasValue)
                await _repo.UpsertLfTieuHaoFromMeAsync(me, pc.ScopePhieu, DateOnly.FromDateTime(me.NgayNhanTL.Value), me.CaTinhLuyen);
        }

        public async Task ThemDongAsync(HRC1_ThemDongTLRequest req, int userId)
        {
            var me = await _repo.GetMeByIdAsync(req.MeId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {req.MeId}");
            if ((me.TrangThaiTL ?? 0) < 1)
                throw new InvalidOperationException("Mẻ phải được nhận vào tinh luyện trước khi thêm dòng.");

            var phieu = await _repo.GetBmPhieuAsync(req.IdPhieu)
                ?? throw new KeyNotFoundException($"Không tìm thấy phiếu {req.IdPhieu}");

            int nextThuTu = await _repo.GetMaxThuTuTLAsync(req.MeId) + 1;

            _repo.AddMePhanCong(new HRC1_MePhanCong
            {
                MeId = req.MeId,
                IdPhieu = req.IdPhieu,
                CongDoan = "tinh_luyen",
                ThuTuTL = nextThuTu
            });

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "chinh_sua",
                DuLieuMoi = $"{{\"them_dong_tl\":{nextThuTu}}}",
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();
        }

        public async Task HuyNhanMeAsync(HRC1_HuyNhanMeRequest req, int userId)
        {
            var me = await _repo.GetMeByIdAsync(req.MeId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {req.MeId}");

            if (me.IsManualTL == true)
                throw new InvalidOperationException("Dùng chức năng xóa mẻ tay để xóa mẻ thêm thủ công.");

            if ((me.TrangThaiTL ?? 0) < 1)
                throw new InvalidOperationException("Mẻ chưa được nhận vào tinh luyện.");

            if ((me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Máy đúc đã xác nhận mẻ này, không thể hủy nhận.");

            var old = Snapshot(me);

            var pcs = await _repo.GetTLPhanCongsByMePhieuAsync(req.MeId, req.IdPhieu, req.ScopePhieu);
            if (!pcs.Any())
                throw new InvalidOperationException("Không tìm thấy dòng nhận nào để hủy.");
            _repo.RemoveMePhanCongs(pcs);

            me.TrangThaiTL = null;
            me.NgayNhanTL = null;
            me.CaTinhLuyen = null;
            me.KipTinhLuyen = null;
            me.ThoiGian = null;
            me.KlLan1 = null;
            me.KlLan2 = null;
            me.KlThepLong = null;
            me.PhanLoai = null;
            me.MacThep = null;
            me.MacThepBKMIS = null;
            me.IdMacThep = null;
            me.GhiChuTL = null;
            me.IsTrungMeThoi = null;   // mẻ này rời khỏi TL — không còn là bên trùng
                                       // if (me.DichChuyen == "tinh_luyen")
            me.IdMayDucDich = null;
            me.NgaySXDucChuyen = null; // hủy nhận thì override routing đúc (nếu có) cũng không còn ý nghĩa
            me.CaDucChuyen = null;
            me.CapNhatBoi = userId;
            me.CapNhatLuc = DateTime.Now;
            me.CapNhatBoiTL = userId;

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = me.Id,
                TaiKhoanId = userId,
                HanhDong = "huy_nhan_me",
                DuLieuCu = old,
                DuLieuMoi = Snapshot(me),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();

            // Mẻ rời khỏi TL — xóa mềm dòng Tiêu hao LF liên kết vô điều kiện (kể cả đã sửa tay);
            // nhận lại mẻ sẽ tự tạo lại dòng mới nên không cần giữ lại
            await _repo.SoftDeleteLfTieuHaoByMeThoiAsync(me.Id, me.MaMe);

            // Recalc trùng cho các mẻ cùng MaMe còn lại (bao gồm cả len_thang)
            await RecalcTrungMeByMaMeAsync(me.MaMe);
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // MÁY ĐÚC
        // -------------------------------------------------------

        // Điều kiện đủ để xác nhận đúc — PHẢI khớp với checkDucReady ở FE (TaoPhieuGN.tsx).
        // Re-check ngay tại thời điểm ghi DB (không tin dữ liệu FE gửi lên/đã load trước đó) để chống race
        // condition: LT/TL có thể vừa xóa 1 field bắt buộc trong lúc Đúc đang bấm xác nhận với data cũ trên UI.
        private static List<string> CheckDucReady(HRC1_MeThep me)
        {
            var missing = new List<string>();
            if (string.IsNullOrEmpty(me.MaMe))                       missing.Add("Mã mẻ");
            if (string.IsNullOrEmpty(me.ThungSo))                    missing.Add("Thùng số");
            if (string.IsNullOrEmpty(me.ThoiGian))                   missing.Add("Thời gian");
            if (me.KLLFSauThep == null)                              missing.Add("KL thùng LF sau khi ra thép");
            if (me.DichChuyen != "len_thang" && me.KlLan1 == null)   missing.Add("KL thùng&thép lỏng vào bệ xoay - Lần 1 (tấn)");
            if (me.KlLan2 == null)                                   missing.Add("KL bì - Lần 2 (tấn)");
            if (me.KlThepLong == null)                               missing.Add("KL thép lỏng");
            if (me.IdMayDucDich == null)                             missing.Add("Máy đúc");
            return missing;
        }

        public async Task<HRC1_DucXacNhanResult> XacNhanDucAsync(HRC1_DucXacNhanRequest req, int userId)
        {
            var result = new HRC1_DucXacNhanResult();
            if (req.MeIds.Count == 0) return result;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.TrangThaiDuc >= 1) { result.ThanhCong.Add(me.Id); continue; }

                var missing = CheckDucReady(me);
                if (missing.Count > 0)
                {
                    result.ThatBai.Add(new HRC1_DucXacNhanThatBai { MeId = me.Id, MaMe = me.MaMe ?? "", LyDo = missing });
                    continue;
                }

                me.TrangThaiDuc = 1;
                me.CapNhatLuc = now;
                me.CapNhatBoiDuc = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "xac_nhan",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
                result.ThanhCong.Add(me.Id);
            }
            await _repo.SaveChangesAsync();
            return result;
        }

        public async Task BoXacNhanDucAsync(HRC1_DucBoXacNhanRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            if (meTheps.Any(m => m.IsChot == true))
                throw new InvalidOperationException("Mẻ đã được chốt, phải bỏ chốt trước khi bỏ xác nhận.");
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.TrangThaiDuc != 1) continue;

                me.TrangThaiDuc = 0;
                me.CapNhatLuc = now;
                me.CapNhatBoiDuc = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "bo_xac_nhan",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // MÁY ĐÚC — XÁC NHẬN PCN (chỉ áp dụng mẻ IsThuNghiem=true)
        // -------------------------------------------------------
        public async Task XacNhanPCNAsync(HRC1_DucXacNhanPCNRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            if(meTheps.Any(me => me.IsChot == true))
                throw new InvalidOperationException("Mẻ đã được chốt, không thể xác nhận.");
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.IsThuNghiem != true) continue;
                if (me.TrangThaiChotPCN == true) continue; // đã chốt PCN — khóa vĩnh viễn
                if (me.TrangThaiPCN == true) continue;

                me.TrangThaiPCN = true;
                me.CapNhatLuc = now;
                me.CapNhatBoiPCN = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "xac_nhan_pcn",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
        }

        public async Task KhongXacNhanPCNAsync(HRC1_DucKhongXacNhanPCNRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.IsThuNghiem != true) continue;
                if (me.TrangThaiChotPCN == true) continue; // đã chốt PCN — khóa vĩnh viễn
                if (me.TrangThaiPCN == false) continue;

                me.TrangThaiPCN = false;
                me.CapNhatLuc = now;
                me.CapNhatBoiPCN = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "khong_xac_nhan_pcn",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
        }

        // Reset về null (chưa xử lý) — undo cả xác nhận lẫn không xác nhận
        public async Task ResetXacNhanPCNAsync(HRC1_DucResetXacNhanPCNRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.TrangThaiChotPCN == true) continue; // đã chốt PCN — khóa vĩnh viễn
                if (me.TrangThaiPCN == null) continue;

                me.TrangThaiPCN = null;
                me.CapNhatLuc = now;
                me.CapNhatBoiPCN = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "reset_xac_nhan_pcn",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // CHỐT / BỎ CHỐT PCN (P.KH, từ trang Thống kê) — khóa vĩnh viễn TrangThaiPCN,
        // chỉ áp dụng mẻ đã IsThuNghiem=true && TrangThaiPCN=true
        // -------------------------------------------------------
        public async Task ChotPCNAsync(HRC1_ChotPCNRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.IsThuNghiem != true) continue;
                if (me.TrangThaiPCN != true) continue;
                if (me.TrangThaiChotPCN == true) continue;

                me.TrangThaiChotPCN = true;
                me.CapNhatLuc = now;
                me.CapNhatChotPCNBoi = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "chot_pcn",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
        }

        public async Task BoChotPCNAsync(HRC1_BoChotPCNRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;

            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.TrangThaiChotPCN != true) continue;

                me.TrangThaiChotPCN = false;
                me.CapNhatLuc = now;
                me.CapNhatChotPCNBoi = userId;

                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "bo_chot_pcn",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // Đồng bộ mẻ thổi từ gang lỏng — chỉ dành cho phiếu lò thổi
        // loSo: lò thổi số cần đồng bộ (1–5)
        // -------------------------------------------------------
        public async Task<HRC1_PhieuDataVm> SyncMeThoiLoThoiAsync(Guid idPhieu, int loSo)
        {
            var phieu = await _repo.GetBmPhieuAsync(idPhieu)
                ?? throw new KeyNotFoundException($"Không tìm thấy phiếu {idPhieu}");
            if (phieu.MaBm != "HRC1_LoThoi")
                throw new InvalidOperationException("Chỉ đồng bộ được cho phiếu lò thổi.");

            // Lấy danh sách mẻ từ gang lỏng (lọc theo loSo thay vì phieu.Scope)
            List<string> gangMaMes;
            try
            {
                gangMaMes = await _bbgnSvc.FetchMeThoiHRC1Async(new HRC1_FetchMeThoiRequest
                {
                    NgaySX = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Today),
                    Ca = phieu.Ca ?? 1,
                    IdLoThoi = loSo
                });
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"Không kết nối được hệ thống gang lỏng: {ex.Message}", ex);
            }

            // Lấy mẻ hiện có trong phiếu cho lò này (filter by ScopePhieu = loSo)
            var phanCongs = await _repo.GetMePhanCongsByPhieuAsync(idPhieu, "lo_thoi", loSo);
            var existingIds = phanCongs.Select(pc => pc.MeId).Distinct().ToList();
            var existingMes = existingIds.Count > 0
                ? await _repo.GetMeThepsByIdsAsync(existingIds)
                : new List<HRC1_MeThep>();

            var gangSet = new HashSet<string>(gangMaMes, StringComparer.OrdinalIgnoreCase);
            var existingByMaMe = existingMes
                .Where(m => !string.IsNullOrEmpty(m.MaMe))
                .ToDictionary(m => m.MaMe!, StringComparer.OrdinalIgnoreCase);

            // 1. Mẻ gang trả lại → reset ghost
            foreach (var me in existingMes.Where(m =>
                !string.IsNullOrEmpty(m.MaMe) && gangSet.Contains(m.MaMe!) && m.IsGhost == true))
                me.IsGhost = false;

            // 2. Mẻ không còn bên gang → đánh ghost
            foreach (var me in existingMes.Where(m =>
                !string.IsNullOrEmpty(m.MaMe) && !gangSet.Contains(m.MaMe!) && m.IsGhost != true))
                me.IsGhost = true;

            // 3. Mẻ mới từ gang chưa có → insert, NgayTao = thời điểm bắt đầu ca
            var ngayTaoMe = phieu.NgaySX.HasValue && phieu.Ca.HasValue
                ? (phieu.Ca.Value == 1
                    ? phieu.NgaySX.Value.ToDateTime(new TimeOnly(8, 0))
                    : phieu.NgaySX.Value.ToDateTime(new TimeOnly(20, 0)))
                : DateTime.Now;

            var newMes = gangMaMes
                .Where(ma => !existingByMaMe.ContainsKey(ma))
                .Select(ma => new HRC1_MeThep { MaMe = ma, LoSo = loSo, NgayTao = ngayTaoMe, Ca = phieu.Ca, Kip = phieu.Kip })
                .ToList();

            foreach (var me in newMes)
                _repo.AddMeThep(me);

            await _repo.SaveChangesAsync();

            foreach (var me in newMes)
                _repo.AddMePhanCong(new HRC1_MePhanCong
                {
                    MeId = me.Id,
                    IdPhieu = idPhieu,
                    CongDoan = "lo_thoi",
                    ScopePhieu = loSo
                });

            if (newMes.Count > 0)
                await _repo.SaveChangesAsync();

            return await GetPhieuAsync(idPhieu, loSo);
        }

        // -------------------------------------------------------
        // THÊM/XÓA MẺ TAY (Tinh luyện thêm thủ công)
        // -------------------------------------------------------

        public Task<List<HRC1_MeThepSearchVm>> SearchMeThepAsync(string q) =>
            _repo.SearchMeThepAsync(q.Trim(), 20)
                .ContinueWith(t => t.Result.Select(m => new HRC1_MeThepSearchVm
                {
                    MeId = m.Id,
                    MaMe = m.MaMe ?? string.Empty,
                    ThungSo = m.ThungSo,
                    LoSo = m.LoSo
                }).ToList());

        public async Task<HRC1_ThemMeTayResult> ThemMeTayAsync(HRC1_ThemMeTayRequest req, int userId)
        {
            // Lấy mẻ gốc để copy thông tin cơ bản (MaMe, ThungSo, LoSo)
            var source = await _repo.GetMeByMaMeAsync(req.MaMe)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ '{req.MaMe}'.");

            // Check trùng: phiếu TL đã nhận + mẻ đang lên thẳng máy đúc
            var trungTL = await _repo.GetAllTinhLuyenPhieuByMaMeAsync(req.MaMe);
            var trungLenThang = await _repo.GetLenThangMesByMaMeAsync(req.MaMe);
            var trungInfos = trungTL
                .Concat(trungLenThang.Select(m => new HRC1_TrungMeInfo
                {
                    SoPhieu = m.MaMe ?? $"ME-{m.Id}",
                    TenTinhLuyen = "Lên thẳng máy đúc"
                }))
                .ToList();
            if (trungInfos.Count > 0 && !req.XacNhanTrung)
                return new HRC1_ThemMeTayResult { TrungVoi = trungInfos, DaThemVao = false };

            var phieu = await _repo.GetBmPhieuAsync(req.IdPhieu)
                ?? throw new KeyNotFoundException($"Không tìm thấy phiếu {req.IdPhieu}.");

            var ngayNhanTL = phieu.NgaySX.HasValue
                ? phieu.NgaySX.Value.ToDateTime(TimeOnly.MinValue)
                : DateTime.Now;

            // Tạo HRC1_MeThep mới — độc lập với mẻ gốc lò thổi
            var newMe = new HRC1_MeThep
            {
                MaMe = source.MaMe,
                ThungSo = source.ThungSo,
                LoSo = source.LoSo,
                IsManualTL = true,
                TrangThaiTL = 1,
                NgayNhanTL = ngayNhanTL,
                CaTinhLuyen = phieu.Ca,
                KipTinhLuyen = phieu.Kip,
                NgayTao = ngayNhanTL,
                CapNhatBoi = userId,
                CapNhatLuc = DateTime.Now,
                CapNhatBoiTL = userId
            };
            _repo.AddMeThep(newMe);
            await _repo.SaveChangesAsync(); // EF gán Id cho newMe

            _repo.AddMePhanCong(new HRC1_MePhanCong
            {
                MeId = newMe.Id,
                IdPhieu = req.IdPhieu,
                CongDoan = "tinh_luyen",
                IsManualTL = true,
                ScopePhieu = req.ScopePhieu
            });

            _repo.AddLichSu(new HRC1_LichSu
            {
                MeId = newMe.Id,
                TaiKhoanId = userId,
                HanhDong = "them_me_tay",
                DuLieuMoi = Snapshot(newMe),
                Luc = DateTime.Now
            });
            await _repo.SaveChangesAsync();

            // Liên kết sang Tiêu hao LF giống nhận mẻ bình thường
            if (phieu.NgaySX.HasValue && phieu.Ca.HasValue)
                await _repo.UpsertLfTieuHaoFromMeAsync(newMe, req.ScopePhieu, phieu.NgaySX, phieu.Ca);

            // Recalc IsTrungMeThoi cho tất cả mẻ có cùng MaMe (TL + len_thang)
            await RecalcTrungMeByMaMeAsync(source.MaMe);
            await _repo.SaveChangesAsync();

            return new HRC1_ThemMeTayResult { TrungVoi = trungInfos, DaThemVao = true };
        }

        public async Task XoaMeTayAsync(int mePhanCongId, int userId)
        {
            var pc = await _repo.GetMePhanCongByIdAsync(mePhanCongId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ phân công {mePhanCongId}.");
            if (pc.IsManualTL != true)
                throw new InvalidOperationException("Chỉ có thể xóa dòng được thêm thủ công.");

            var me = await _repo.GetMeByIdAsync(pc.MeId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {pc.MeId}.");
            if (me.IsManualTL != true)
                throw new InvalidOperationException("Mẻ này không phải do tinh luyện tạo tay.");
            if ((me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Mẻ đã được máy đúc xác nhận, không thể xóa.");

            var maMe = me.MaMe;

            // Xóa toàn bộ MePhanCong và chính HRC1_MeThep (mẻ này độc lập, do TL tạo)
            var allPcs = await _repo.GetAllMePhanCongsByMeIdAsync(me.Id);
            _repo.RemoveMePhanCongs(allPcs);
            _repo.RemoveMeThep(me);
            await _repo.SaveChangesAsync();

            // Xóa dòng Tiêu hao LF liên kết vô điều kiện (kể cả đã sửa tay)
            await _repo.SoftDeleteLfTieuHaoByMeThoiAsync(me.Id, maMe);

            // Recalc trùng cho các mẻ cùng MaMe còn lại (bao gồm cả len_thang)
            await RecalcTrungMeByMaMeAsync(maMe);
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // MÁY ĐÚC — CHỐT / BỎ CHỐT MẺ (P.KH)
        // -------------------------------------------------------
        public async Task ChotMeAsync(HRC1_DucChotMeRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;
            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            if(meTheps.Any(m => m.TrangThaiDuc < 1))
                throw new InvalidOperationException("Chỉ có thể chốt mẻ đã được máy đúc xác nhận.");
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.IsChot == true) continue;
                me.IsChot = true;
                me.CapNhatLuc = now;
                me.CapNhatBoiChot = userId;
                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "chot",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
            await UpdateDucPhieuTinhTrangAsync(req.IdPhieu, req.IdMayDuc);
        }

        public async Task BoChotMeAsync(HRC1_DucBoChotMeRequest req, int userId)
        {
            if (req.MeIds.Count == 0) return;
            var meTheps = await _repo.GetMeThepsByIdsAsync(req.MeIds);
            var now = DateTime.Now;

            foreach (var me in meTheps)
            {
                if (me.IsChot != true) continue;
                me.IsChot = false;
                me.CapNhatLuc = now;
                me.CapNhatBoiChot = userId;
                _repo.AddLichSu(new HRC1_LichSu
                {
                    MeId = me.Id,
                    TaiKhoanId = userId,
                    HanhDong = "bo_chot",
                    DuLieuMoi = Snapshot(me),
                    Luc = now
                });
            }
            await _repo.SaveChangesAsync();
            await UpdateDucPhieuTinhTrangAsync(req.IdPhieu, req.IdMayDuc);
        }

        private async Task UpdateDucPhieuTinhTrangAsync(Guid idPhieu, int idMayDuc)
        {
            var phieu = await _repo.GetBmPhieuAsync(idPhieu);
            if (phieu == null || !phieu.NgaySX.HasValue || !phieu.Ca.HasValue) return;

            var allMes = await _repo.GetMeThepsByMayDucAsync(phieu.NgaySX.Value, phieu.Ca.Value, idMayDuc);
            if (allMes.Count == 0) return;

            int newTinhTrang = allMes.All(m => m.IsChot == true) ? 5
                             : allMes.Any(m => (m.TrangThaiDuc ?? 0) >= 1) ? 2
                             : 0;

            if (phieu.TinhTrang != newTinhTrang)
            {
                phieu.TinhTrang = newTinhTrang;
                await _repo.SaveChangesAsync();
            }
        }

        // -------------------------------------------------------
        // CHỐT/HỦY CHỐT PHIẾU BATCH — dành cho P.KH chốt từ danh sách ThongKe
        // Điều kiện: tất cả mẻ trong phiếu phải đã được máy đúc xác nhận (TrangThaiDuc >= 1)
        // -------------------------------------------------------
        public async Task<HRC1_ChotPhieuBatchResult> ChotPhieuBatchAsync(List<Guid> idPhieuList, int userId)
        {
            var result = new HRC1_ChotPhieuBatchResult();
            var now = DateTime.Now;

            foreach (var idPhieu in idPhieuList)
            {
                var phieu = await _repo.GetBmPhieuAsync(idPhieu);
                if (phieu == null || !phieu.NgaySX.HasValue || !phieu.Ca.HasValue || !phieu.Scope.HasValue)
                {
                    result.ThatBai.Add(new HRC1_ChotPhieuBatchThatBai
                    {
                        IdPhieu = idPhieu,
                        SoPhieu = phieu?.SoPhieu ?? idPhieu.ToString(),
                        LyDo = new List<string> { "Phiếu không hợp lệ hoặc thiếu thông tin" }
                    });
                    continue;
                }

                var mes = await _repo.GetMeThepsByMayDucAsync(phieu.NgaySX.Value, phieu.Ca.Value, phieu.Scope.Value);

                // if (mes.Count == 0)
                // {
                //     result.ThatBai.Add(new HRC1_ChotPhieuBatchThatBai
                //     {
                //         IdPhieu = idPhieu,
                //         SoPhieu = phieu.SoPhieu ?? idPhieu.ToString(),
                //         LyDo = new List<string> { "Phiếu chưa có mẻ nào" }
                //     });
                //     continue;
                // }

                // var chuaXacNhan = mes.Where(m => (m.TrangThaiDuc ?? 0) < 1).ToList();
                // if (chuaXacNhan.Count > 0)
                // {
                //     var dsMa = string.Join(", ", chuaXacNhan.Take(5).Select(m => m.MaMe ?? $"ID:{m.Id}"));
                //     result.ThatBai.Add(new HRC1_ChotPhieuBatchThatBai
                //     {
                //         IdPhieu = idPhieu,
                //         SoPhieu = phieu.SoPhieu ?? idPhieu.ToString(),
                //         LyDo = new List<string> { $"{chuaXacNhan.Count} mẻ chưa xác nhận: {dsMa}" }
                //     });
                //     continue;
                // }

                // foreach (var me in mes)
                // {
                //     if (me.IsChot == true) continue;
                //     me.IsChot = true;
                //     me.CapNhatBoi = userId;
                //     me.CapNhatLuc = now;
                //     me.CapNhatBoiDuc = userId;
                //     _repo.AddLichSu(new HRC1_LichSu
                //     {
                //         MeId = me.Id, TaiKhoanId = userId,
                //         HanhDong = "chot", DuLieuMoi = Snapshot(me), Luc = now
                //     });
                // }
                if (mes.Count > 0)
                {
                    var chuaXacNhan = mes.Where(m => (m.TrangThaiDuc ?? 0) < 1).ToList();

                    if (chuaXacNhan.Count > 0)
                    {
                        var dsMa = string.Join(", ",
                            chuaXacNhan.Take(5).Select(m => m.MaMe ?? $"ID:{m.Id}"));

                        result.ThatBai.Add(new HRC1_ChotPhieuBatchThatBai
                        {
                            IdPhieu = idPhieu,
                            SoPhieu = phieu.SoPhieu ?? idPhieu.ToString(),
                            LyDo = new List<string>
                            {
                                $"{chuaXacNhan.Count} mẻ chưa xác nhận: {dsMa}"
                            }
                        });

                        continue;
                    }

                    foreach (var me in mes)
                    {
                        if (me.IsChot == true) continue;

                        me.IsChot = true;
                        me.CapNhatLuc = now;
                        me.CapNhatBoiDuc = userId;

                        _repo.AddLichSu(new HRC1_LichSu
                        {
                            MeId = me.Id,
                            TaiKhoanId = userId,
                            HanhDong = "chot",
                            DuLieuMoi = Snapshot(me),
                            Luc = now
                        });
                    }

                    // await _repo.SaveChangesAsync();
                }
                else
                {
                    // Không có mẻ => vẫn cho chốt phiếu
                    phieu.TinhTrang = 5;
                }
                await _repo.SaveChangesAsync();
                // await UpdateDucPhieuTinhTrangAsync(idPhieu, phieu.Scope.Value);

                if (mes.Count > 0)
                {
                    await UpdateDucPhieuTinhTrangAsync(
                        idPhieu,
                        phieu.Scope.Value);
                }

                result.ThanhCong.Add(phieu.SoPhieu ?? idPhieu.ToString());
            }

            return result;
        }

        public async Task<HRC1_ChotPhieuBatchResult> HuyChotPhieuBatchAsync(List<Guid> idPhieuList, int userId)
        {
            var result = new HRC1_ChotPhieuBatchResult();
            var now = DateTime.Now;

            foreach (var idPhieu in idPhieuList)
            {
                var phieu = await _repo.GetBmPhieuAsync(idPhieu);
                if (phieu == null || !phieu.NgaySX.HasValue || !phieu.Ca.HasValue || !phieu.Scope.HasValue)
                {
                    result.ThatBai.Add(new HRC1_ChotPhieuBatchThatBai
                    {
                        IdPhieu = idPhieu,
                        SoPhieu = phieu?.SoPhieu ?? idPhieu.ToString(),
                        LyDo = new List<string> { "Phiếu không hợp lệ hoặc thiếu thông tin" }
                    });
                    continue;
                }

                var mes = await _repo.GetMeThepsByMayDucAsync(phieu.NgaySX.Value, phieu.Ca.Value, phieu.Scope.Value);

                // foreach (var me in mes.Where(m => m.IsChot == true))
                // {
                //     me.IsChot = false;
                //     me.CapNhatBoi = userId;
                //     me.CapNhatLuc = now;
                //     me.CapNhatBoiDuc = userId;
                //     _repo.AddLichSu(new HRC1_LichSu
                //     {
                //         MeId = me.Id, TaiKhoanId = userId,
                //         HanhDong = "bo_chot", DuLieuMoi = Snapshot(me), Luc = now
                //     });
                // }
                // await _repo.SaveChangesAsync();
                // await UpdateDucPhieuTinhTrangAsync(idPhieu, phieu.Scope.Value);
                if (mes.Count > 0)
                {
                    foreach (var me in mes.Where(m => m.IsChot == true))
                    {
                        me.IsChot = false;
                        me.CapNhatLuc = now;
                        me.CapNhatBoiDuc = userId;

                        _repo.AddLichSu(new HRC1_LichSu
                        {
                            MeId = me.Id,
                            TaiKhoanId = userId,
                            HanhDong = "bo_chot",
                            DuLieuMoi = Snapshot(me),
                            Luc = now
                        });
                    }
                }
                else
                {
                    // Không có mẻ => vẫn cho bỏ chốt phiếu
                    phieu.TinhTrang = 0;
                }

                await _repo.SaveChangesAsync();

                if (mes.Count > 0)
                {
                    await UpdateDucPhieuTinhTrangAsync(
                        idPhieu,
                        phieu.Scope.Value);
                }


                result.ThanhCong.Add(phieu.SoPhieu ?? idPhieu.ToString());
            }

            return result;
        }

        // -------------------------------------------------------
        // GHI CHÚ — cập nhật ghi chú chung, dùng cho cả 3 công đoạn
        // -------------------------------------------------------
        public async Task UpdateGhiChuAsync(int meId, string? ghiChu, string? field, int userId)
        {
            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");
            if (me.IsChot == true)
                throw new InvalidOperationException("Mẻ đã chốt, không thể chỉnh sửa.");

            switch (field)
            {
                case "tl":  me.GhiChuTL  = ghiChu; break;
                case "duc": me.GhiChuDuc = ghiChu; break;
                case "pcn": me.GhiChuPCN = ghiChu; break;
                default:    me.GhiChuLo  = ghiChu; me.CapNhatBoi = userId; break;
            }
            me.CapNhatLuc = DateTime.Now;
            await _repo.SaveChangesAsync();
        }

        // Xóa cứng mẻ ghost (user chủ động xóa sau khi xác nhận bên gang thực sự xóa)
        public async Task XoaMeGhostAsync(int meId)
        {
            var me = await _repo.GetMeByIdAsync(meId)
                ?? throw new KeyNotFoundException($"Không tìm thấy mẻ {meId}");
            if (me.IsGhost != true)
                throw new InvalidOperationException("Chỉ có thể xóa mẻ đã được đánh dấu ghost.");
            if ((me.TrangThaiTL ?? 0) >= 1)
                throw new InvalidOperationException("Không thể xóa mẻ: tinh luyện đã nhận mẻ này.");
            if ((me.TrangThaiDuc ?? 0) >= 1)
                throw new InvalidOperationException("Không thể xóa mẻ: máy đúc đã xác nhận mẻ này.");

            var pcs = await _repo.GetAllMePhanCongsByMeIdAsync(meId);
            _repo.RemoveMePhanCongs(pcs);
            _repo.RemoveMeThep(me);
            await _repo.SaveChangesAsync();
        }

        // -------------------------------------------------------
        // THỐNG KÊ — paginated search từ HRC1_MeThep
        // -------------------------------------------------------
        public Task<HRC1_ThongKeResult> SearchThongKeAsync(HRC1_ThongKeQuery query) =>
            _repo.GetMeThepsPagedAsync(query);

        public Task<HRC1_TongHopResult> TongHopAsync(HRC1_ThongKeQuery query) =>
            _repo.GetTongHopAsync(query);

        // -------------------------------------------------------
        // Helper — danh sách máy đúc cho dropdown
        // Với tinh_luyen: chỉ trả máy đúc có phiếu cùng ngày/ca chưa chốt (TinhTrang != 5)
        // -------------------------------------------------------
        private async Task<List<HRC1_MayDucOptionVm>> BuildDanhSachMayDucAsync(
            string congDoan, DateOnly? ngay, int? ca, List<MayDuc> mayDucs)
        {
            if (congDoan is "tinh_luyen" or "lo_thoi" && ngay.HasValue && ca.HasValue)
            {
                var activeScopes = await _repo.GetActiveDucPhieuScopesAsync(ngay.Value, ca.Value);
                if (activeScopes.Count > 0)
                {
                    var scopeSet = new HashSet<int>(activeScopes);
                    return mayDucs
                        .Where(md => scopeSet.Contains(md.Id))
                        .Select(md => new HRC1_MayDucOptionVm { Id = md.Id, TenMayDuc = md.TenMayDuc ?? string.Empty })
                        .ToList();
                }
            }
            return mayDucs
                .Select(md => new HRC1_MayDucOptionVm { Id = md.Id, TenMayDuc = md.TenMayDuc ?? string.Empty })
                .ToList();
        }

        // -------------------------------------------------------
        // IsTrungMeThoi helper
        // Gọi SAU khi đã SaveChanges để DB phản ánh trạng thái mới.
        // -------------------------------------------------------
        private async Task RecalcTrungMeByMaMeAsync(string? maMe)
        {
            if (string.IsNullOrEmpty(maMe)) return;
            // Mẻ "active conflict": TrangThaiTL >= 1 hoặc DichChuyen = "len_thang"
            var active = await _repo.GetActiveConflictMesByMaMeAsync(maMe);
            if (active.Count >= 2)
                foreach (var m in active) m.IsTrungMeThoi = true;
            else if (active.Count == 1)
                active[0].IsTrungMeThoi = null;
            // Count == 0: không cần làm gì
        }

        // -------------------------------------------------------
        // Helpers
        // -------------------------------------------------------
        private static HRC1_MeThepVm MapToVm(HRC1_MeThep? m, HRC1_MePhanCong pc, List<MayDuc> mayDucs,
            Dictionary<int, HRC1_MeThep>? chuyenVeDict = null)
        {
            if (m is null) return new HRC1_MeThepVm { MePhanCongId = pc.Id };

            string? tenMayDucDich = m.IdMayDucDich.HasValue
                ? mayDucs.FirstOrDefault(d => d.Id == m.IdMayDucDich.Value)?.TenMayDuc
                : null;

            string? chuyenVeMaMe = null;
            string? tenMayDucChuyen = null;
            if (pc.ChuyenVeMeId.HasValue && chuyenVeDict != null
                && chuyenVeDict.TryGetValue(pc.ChuyenVeMeId.Value, out var chuyenVeMe))
            {
                chuyenVeMaMe = chuyenVeMe.MaMe;
                if (chuyenVeMe.IdMayDucDich.HasValue)
                    tenMayDucChuyen = mayDucs.FirstOrDefault(d => d.Id == chuyenVeMe.IdMayDucDich.Value)?.TenMayDuc;
            }

            return new HRC1_MeThepVm
            {
                Id = m.Id,
                MePhanCongId = pc.Id,
                ThuTuTL = pc.ThuTuTL,
                MaMe = m.MaMe,
                ThungSo = m.ThungSo,
                LoSo = m.LoSo,
                ThoiGian = m.ThoiGian,
                KLLFSauThep = m.KLLFSauThep,
                KlLan1 = m.KlLan1,
                KlLan2 = m.KlLan2,
                KlLan3 = m.KlLan3,
                KlThepLong = m.KlThepLong,
                KlThepLongPhanBo = m.KLThepLongPhanBo,
                DichChuyen = m.DichChuyen,
                TLDichSo = m.TLDichSo,
                IdMayDucDich = m.IdMayDucDich,
                TenMayDucDich = tenMayDucDich,
                IsThuNghiem = m.IsThuNghiem,
                IsTrungMeThoi = m.IsTrungMeThoi,
                IsGhost = m.IsGhost,
                IsChot = m.IsChot,
                IsManualTL = m?.IsManualTL,
                GhiChuLo = m.GhiChuLo,
                PhanLoai = m.PhanLoai,
                MacThep = m.MacThep,
                MacThepBKMIS = m.MacThepBKMIS,
                IdMacThep = m.IdMacThep,
                GhiChuTL  = m.GhiChuTL,
                GhiChuDuc = m.GhiChuDuc,
                GhiChuPCN = m.GhiChuPCN,
                TrangThaiLo = m.TrangThaiLo,
                TrangThaiTL = m.TrangThaiTL,
                TrangThaiDuc = m.TrangThaiDuc,
                TrangThaiPCN = m.TrangThaiPCN,
                TrangThaiChotPCN = m.TrangThaiChotPCN,
                CapNhatBoi = m.CapNhatBoi,
                CapNhatLuc = m.CapNhatLuc,
                XacNhanBoi = pc.XacNhanBoi,
                XacNhanLuc = pc.XacNhanLuc,
                ChuyenVeMeId = pc.ChuyenVeMeId,
                ChuyenVeMaMe = chuyenVeMaMe,
                TenMayDucChuyen = tenMayDucChuyen,
                IsChuyenCaDuc = m.CaDucChuyen.HasValue || m.NgaySXDucChuyen.HasValue,
                NgayDuc = m.NgaySXDucChuyen ?? (m.NgayNhanTL.HasValue ? DateOnly.FromDateTime(m.NgayNhanTL.Value) : null),
                CaDuc = m.CaDucChuyen ?? m.CaTinhLuyen,
            };
        }

        private static string Snapshot(HRC1_MeThep m) =>
            JsonSerializer.Serialize(m, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });

        // -------------------------------------------------------
        // EXPORT EXCEL — thống kê HRC1_MeThep (27 cột, header dòng 4, data từ dòng 5)
        // -------------------------------------------------------
        public async Task<ExportFileResult> ExportExcelAsync(HRC1_ExportQuery query)
        {
            var data = await _repo.GetMeThepsForExportAsync(query);

            var p2Parts = new List<string>();
            if (query.TuNgay.HasValue || query.DenNgay.HasValue)
                p2Parts.Add($"Từ ngày: {query.TuNgay:dd/MM/yyyy}  –  Đến ngày: {query.DenNgay:dd/MM/yyyy}");
            if (query.Ca.HasValue) p2Parts.Add($"Ca: {(query.Ca == 1 ? "Ca ngày" : "Ca đêm")}");
            if (!string.IsNullOrEmpty(query.Kip)) p2Parts.Add($"Kíp: {query.Kip}");

            var p3Parts = new List<string>();
            if (query.LoSo.HasValue) p3Parts.Add($"Lò thổi: {query.LoSo}");
            if (query.TlSo.HasValue) p3Parts.Add($"Tinh luyện: {query.TlSo}");
            if (query.IdMayDuc.HasValue) p3Parts.Add($"Máy đúc ID: {query.IdMayDuc}");
            if (!string.IsNullOrEmpty(query.MaMe)) p3Parts.Add($"Mẻ: {query.MaMe}");
            if (!string.IsNullOrEmpty(query.ThungSo)) p3Parts.Add($"Thùng: {query.ThungSo}");
            if (!string.IsNullOrEmpty(query.PhanLoai)) p3Parts.Add($"Phân loại: {query.PhanLoai}");
            if (query.IsChot.HasValue) p3Parts.Add(query.IsChot.Value ? "Đã chốt" : "Chưa chốt");
            if (query.IsManualTL.HasValue) p3Parts.Add($"Nhập tay: {(query.IsManualTL.Value ? "Có" : "Không")}");
            p3Parts.Add($"Tổng: {data.Count} mẻ   |   Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}");

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Thống kê");
            WriteExcelSheet(ws, data, string.Join("   |   ", p2Parts), string.Join("   |   ", p3Parts));

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            return new ExportFileResult
            {
                Content = stream.ToArray(),
                FileName = $"ThongKe_HRC1_BBGN_TL_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            };
        }

        // -------------------------------------------------------
        // EXPORT EXCEL — HRC1_BBGN_ThepLong theo template ISO
        // Template: wwwroot/templates/HRC1_BBGN_ThepLong.xlsx
        // Header ở các dòng 1–4, data bắt đầu từ dòng 5
        // -------------------------------------------------------
        public async Task<ExportFileResult> ExportThepLongISOAsync(HRC1_ExportQuery query)
        {
            const string TEMPLATE_FILE = "HRC1_BBGN_ThepLong.xlsx";
            const int DATA_START_ROW = 5;

            var templatePath = Path.Combine(_env.WebRootPath, "templates", TEMPLATE_FILE);
            if (!File.Exists(templatePath))
                throw new FileNotFoundException(
                    $"Chưa có template Excel. Đặt file tại: wwwroot/templates/{TEMPLATE_FILE}");

            var data = await _repo.GetMeThepsForExportAsync(query);

            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
            try
            {
                using (var wb = new XLWorkbook(templatePath))
                {
                    var ws = wb.Worksheets.First();
                    RenderThepLongISOHeader(ws, query);
                    FillThepLongISOSheet(ws, data, DATA_START_ROW);
                    wb.SaveAs(tempPath);
                }

                var ngay = query.TuNgay.HasValue ? query.TuNgay.Value.ToString("yyyyMMdd") : DateTime.Now.ToString("yyyyMMdd");
                var ca   = query.Ca.HasValue ? (query.Ca == 1 ? "CaNgay" : "CaDem") : "";
                return new ExportFileResult
                {
                    Content     = File.ReadAllBytes(tempPath),
                    FileName    = $"HRC1_BBGN_ThepLong_{ngay}_{ca}.xlsx",
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                };
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        // query != null → render dòng 2 (xuất 1 phiếu); null → bỏ dòng 2 (xuất nhiều phiếu)
        private static void RenderThepLongISOHeader(IXLWorksheet ws, HRC1_ExportQuery? query = null)
        {
            const int TOTAL_COLS = 17;

            // Dòng 1: tiêu đề biên bản
            ws.Range(1, 1, 1, TOTAL_COLS).Merge();
            var c1 = ws.Cell(1, 1);
            c1.Value                      = "BIÊN BẢN GIAO NHẬN THÉP LỎNG";
            c1.Style.Font.Bold            = true;
            c1.Style.Font.FontSize        = 14;
            c1.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c1.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

            if (query == null) return;

            // Dòng 2: thông tin kíp/ca/ngày — chỉ render khi xuất 1 phiếu
            ws.Range(2, 1, 2, TOTAL_COLS).Merge();
            var c2 = ws.Cell(2, 1);

            var caValue   = query.Ca ?? 0;
            var kipSuffix = string.IsNullOrWhiteSpace(query.Kip) ? "" : query.Kip;
            var ngayStr   = query.TuNgay.HasValue ? query.TuNgay.Value.ToString("dd/MM/yyyy") : "";

            string gioBatDau, gioKetThuc, ngayKetThuc;
            if (caValue == 1)
            {
                gioBatDau   = "08 giờ 00";
                gioKetThuc  = "20 giờ 00";
                ngayKetThuc = ngayStr;
            }
            else
            {
                gioBatDau   = "20 giờ 00";
                gioKetThuc  = "08 giờ 00";
                ngayKetThuc = query.TuNgay.HasValue
                    ? query.TuNgay.Value.AddDays(1).ToString("dd/MM/yyyy")
                    : ngayStr;
            }

            c2.Value = $"Kíp {caValue}{kipSuffix}: Từ {gioBatDau} ngày {ngayStr} đến {gioKetThuc} ngày {ngayKetThuc}";
            c2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c2.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        }

        private static readonly int[] _centerCols = { 1, 2, 3, 4, 5, 7, 8, 14, 15, 16 };

        private static void ApplyDataRowStyle(IXLWorksheet ws, int row, int totalCols)
        {
            var range = ws.Range(row, 1, row, totalCols);
            range.Style.Border.TopBorder    = XLBorderStyleValues.Thin;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            range.Style.Border.LeftBorder   = XLBorderStyleValues.Thin;
            range.Style.Border.RightBorder  = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            foreach (var col in _centerCols)
                ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        private static string MeSortKey(HRC1_ExportRow r)
        {
            if (string.IsNullOrEmpty(r.ThoiGian)) return "9999-12-31 99:99";
            var baseDate = r.NgayDuc ?? DateOnly.FromDateTime(r.NgayTao);
            var isNextDay = r.CaDuc == 2 && string.Compare(r.ThoiGian, "19:21", StringComparison.Ordinal) < 0;
            return $"{(isNextDay ? baseDate.AddDays(1) : baseDate):yyyy-MM-dd} {r.ThoiGian}";
        }

        private static void FillThepLongISOSheet(IXLWorksheet ws, List<HRC1_ExportRow> data, int startRow)
        {
            const int TOTAL_COLS = 17;
            int row = startRow, stt = 1;
            decimal sumKlThepLong = 0, sumKlThepLongChot = 0;

            foreach (var item in data.OrderBy(MeSortKey))
            {
                ws.Cell(row, 1).Value = stt++;

                // Ngày: lên thẳng → NgayTao, tinh luyện → NgayNhanTL (fallback NgayTao)
                var ngayCot2 = (item.TinhLuyenLenThang == "Lên thẳng" || !item.NgayNhanTL.HasValue)
                    ? item.NgayTao
                    : item.NgayNhanTL.Value;
                ws.Cell(row, 2).Value = ngayCot2.ToString("dd/MM/yyyy");

                ws.Cell(row, 3).Value = item.CaDuc.HasValue ? (item.CaDuc == 1 ? "Ca ngày" : "Ca đêm") : "";
                ws.Cell(row, 4).Value = item.TenMayDuc ?? "";
                ws.Cell(row, 5).Value = item.MaMe ?? "";
                ws.Cell(row, 6).Value = item.MacThepBKMIS ?? "";
                ws.Cell(row, 7).Value = item.ThungSo ?? "";
                ws.Cell(row, 8).Value = item.ThoiGian ?? "";

                // KL lần 1: nếu không có thì fallback sang KLLFSauThep (lên thẳng)
                var kl1 = item.KlLan1 ?? item.KLLFSauThep;
                if (kl1.HasValue)             ws.Cell(row, 9).Value  = (double)kl1.Value;
                if (item.KlLan2.HasValue)     ws.Cell(row, 10).Value = (double)item.KlLan2.Value;
                if (item.KlLan3.HasValue)     ws.Cell(row, 11).Value = (double)item.KlLan3.Value;
                if (item.KlThepLong.HasValue)
                {
                    ws.Cell(row, 12).Value = (double)item.KlThepLong.Value;
                    sumKlThepLong += item.KlThepLong.Value;
                }

                ws.Cell(row, 13).Value = item.GhiChuDuc ?? "";
                ws.Cell(row, 14).Value = item.TinhLuyenLenThang ?? "";
                ws.Cell(row, 15).Value = item.PhanLoai ?? "";
                if (item.KlThepLongChot.HasValue)
                {
                    ws.Cell(row, 16).Value = (double)item.KlThepLongChot.Value;
                    sumKlThepLongChot += item.KlThepLongChot.Value;
                }
                ws.Cell(row, 17).Value = item.ChuyenVeMaMe ?? "";

                ApplyDataRowStyle(ws, row, TOTAL_COLS);
                if (!string.IsNullOrEmpty(item.ChuyenVeMaMe) && item.ChuyenVeMaMe != item.MaMe)
                    ws.Range(row, 1, row, TOTAL_COLS).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFC000");
                row++;
            }

            // Dòng tổng
            ws.Range(row, 1, row, 11).Merge();
            var sumLabel = ws.Cell(row, 1);
            sumLabel.Value = "Tổng cộng:";
            sumLabel.Style.Font.Bold = true;
            sumLabel.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            var sumCell = ws.Cell(row, 12);
            sumCell.Value = (double)sumKlThepLong;
            sumCell.Style.Font.Bold = true;
            sumCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            var sumChotCell = ws.Cell(row, 16);
            sumChotCell.Value = (double)sumKlThepLongChot;
            sumChotCell.Style.Font.Bold = true;
            sumChotCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ApplyDataRowStyle(ws, row, TOTAL_COLS);
            row++;

            // Dòng ghi chú
            ws.Range(row, 1, row, TOTAL_COLS).Merge();
            var noteCell = ws.Cell(row, 1);
            noteCell.Value = "Ghi chú: Biên bản này được lập thành 2 bản có giá trị như nhau: bên giao và bên nhận mỗi bên giữ 1 bản.";
            noteCell.Style.Font.Italic = true;
            row++;

            // Dòng ký tên: Bên giao (trái) | Bên nhận (phải)
            ws.Range(row, 1, row, 7).Merge();
            var giaoCell = ws.Cell(row, 1);
            giaoCell.Value = "Bên giao";
            giaoCell.Style.Font.Bold = true;
            giaoCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Range(row, 9, row, TOTAL_COLS).Merge();
            var nhanCell = ws.Cell(row, 9);
            nhanCell.Value = "Bên nhận";
            nhanCell.Style.Font.Bold = true;
            nhanCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        public async Task<ExportFileResult> ExportBulkExcelAsync(List<Guid> phieuIds)
        {
            var phieus = await _repo.GetBmPhieusByIdsAsync(phieuIds);
            bool allThepLong = phieus.Count > 0 && phieus.All(p => p.MaBm == "HRC1_BBGN_ThepLong");

            var mayDucs = await _repo.GetMayDucsHRC1Async();
            var mayDucDict = mayDucs.ToDictionary(m => m.Id, m => m.TenMayDuc ?? $"MD{m.Id}");

            if (allThepLong)
            {
                const string TEMPLATE_FILE = "HRC1_BBGN_ThepLong.xlsx";
                const int DATA_START_ROW   = 5;
                var templatePath = Path.Combine(_env.WebRootPath, "templates", TEMPLATE_FILE);
                if (!File.Exists(templatePath))
                    throw new FileNotFoundException(
                        $"Chưa có template Excel. Đặt file tại: wwwroot/templates/{TEMPLATE_FILE}");

                var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xlsx");
                try
                {
                    using var wb = new XLWorkbook(templatePath);
                    var templateWs = wb.Worksheets.First();
                    var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var phieu in phieus)
                    {
                        if (!phieu.NgaySX.HasValue || !phieu.Ca.HasValue) continue;

                        var rows = await _repo.GetMeThepsForExportAsync(new HRC1_ExportQuery
                        {
                            TuNgay   = phieu.NgaySX,
                            DenNgay  = phieu.NgaySX,
                            Ca       = phieu.Ca,
                            IdMayDuc = phieu.Scope,
                        });

                        var sheetName = UniqueSheetName(
                            BuildPhieuSheetLabel(phieu, mayDucDict), usedNames);
                        var ws = templateWs.CopyTo(sheetName);
                        RenderThepLongISOHeader(ws, new HRC1_ExportQuery
                        {
                            TuNgay = phieu.NgaySX,
                            Ca     = phieu.Ca,
                            Kip    = phieu.Kip,
                        });
                        FillThepLongISOSheet(ws, rows, DATA_START_ROW);
                    }

                    templateWs.Delete();
                    if (!wb.Worksheets.Any()) wb.AddWorksheet("Trống");
                    wb.SaveAs(tempPath);
                    return new ExportFileResult
                    {
                        Content     = File.ReadAllBytes(tempPath),
                        FileName    = $"BulkExport_HRC1_ThepLong_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                        ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    };
                }
                finally
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
            }

            // Mixed / non-ThepLong: mỗi phiếu 1 sheet
            using var wbFallback = new XLWorkbook();
            var usedNamesFb = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var phieu in phieus)
            {
                if (!phieu.NgaySX.HasValue || !phieu.Ca.HasValue) continue;

                var query = new HRC1_ExportQuery
                {
                    TuNgay  = phieu.NgaySX,
                    DenNgay = phieu.NgaySX,
                    Ca      = phieu.Ca,
                };
                switch (phieu.MaBm)
                {
                    case "HRC1_TinhLuyen":     query.TlSo    = phieu.Scope; break;
                    case "HRC1_BBGN_ThepLong": query.IdMayDuc = phieu.Scope; break;
                    default:
                        if (phieu.Scope.HasValue) query.LoSo = phieu.Scope;
                        break;
                }
                var rows = await _repo.GetMeThepsForExportAsync(query);
                rows = rows.GroupBy(r => r.MeId).Select(g => g.First())
                           .OrderBy(r => r.NgayTao).ThenBy(r => r.MaMe).ToList();

                var sheetName = UniqueSheetName(
                    BuildPhieuSheetLabel(phieu, mayDucDict), usedNamesFb);
                var ws = wbFallback.AddWorksheet(sheetName);

                var ngayStr = phieu.NgaySX.Value.ToString("dd/MM/yyyy");
                var caStr   = phieu.Ca == 1 ? "Ca ngày" : "Ca đêm";
                var line2   = $"{ngayStr}  {caStr}  {phieu.Kip ?? ""}".TrimEnd();
                var line3   = $"Tổng: {rows.Count} mẻ   |   Xuất lúc: {DateTime.Now:dd/MM/yyyy HH:mm}";
                WriteExcelSheet(ws, rows, line2, line3);
            }

            if (!wbFallback.Worksheets.Any()) wbFallback.AddWorksheet("Trống");

            using var stream = new MemoryStream();
            wbFallback.SaveAs(stream);
            return new ExportFileResult
            {
                Content = stream.ToArray(),
                FileName = $"BulkExport_HRC1_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            };
        }

        // Format: {TenMayDuc/TL{n}/Lo{n}}_C{ca}-{kip}_{ddMMyy}
        private static string BuildPhieuSheetLabel(BmPhieu phieu, Dictionary<int, string> mayDucDict)
        {
            string prefix = phieu.MaBm switch
            {
                "HRC1_BBGN_ThepLong" when phieu.Scope.HasValue =>
                    mayDucDict.TryGetValue(phieu.Scope.Value, out var ten) ? ten : $"MD{phieu.Scope}",
                "HRC1_TinhLuyen" when phieu.Scope.HasValue => $"TL{phieu.Scope}",
                _ when phieu.Scope.HasValue => $"Lo{phieu.Scope}",
                _ => phieu.SoPhieu ?? "Phieu",
            };

            var ca   = phieu.Ca.HasValue ? $"C{phieu.Ca}" : "";
            var kip  = string.IsNullOrWhiteSpace(phieu.Kip) ? "" : $"-{phieu.Kip.Trim()}";
            var ngay = phieu.NgaySX.HasValue ? phieu.NgaySX.Value.ToString("ddMMyy") : "";

            return $"{prefix}_{ca}{kip}_{ngay}";
        }

        private static string UniqueSheetName(string raw, HashSet<string> used)
        {
            var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            var clean = new string(raw.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            if (clean.Length > 31) clean = clean[..31];
            if (used.Add(clean)) return clean;
            for (int i = 2; ; i++)
            {
                var suffix = $"_{i}";
                var candidate = clean.Length + suffix.Length > 31
                    ? clean[..(31 - suffix.Length)] + suffix
                    : clean + suffix;
                if (used.Add(candidate)) return candidate;
            }
        }

        private static void WriteExcelSheet(IXLWorksheet ws, List<HRC1_ExportRow> data, string line2, string line3)
        {
            const int TOTAL_COLS = 39;
            const int HEADER_ROW = 4;
            const int DATA_START = 5;

            static string FmtCa(int? v) => v switch { 1 => "Ca ngày", 2 => "Ca đêm", _ => "" };
            static string FmtTL(int? v)  => v switch { 0 => "Chưa nhận", 1 => "Đã nhận", _ => "" };
            static string FmtDuc(int? v) => v switch { 0 => "Chờ", 1 => "Đã XN", _ => "" };

            ws.Range(1, 1, 1, TOTAL_COLS).Merge();
            var t = ws.Cell(1, 1);
            t.Value = "THỐNG KÊ GIAO NHẬN THÉP LỎNG HRC1";
            t.Style.Font.Bold = true;
            t.Style.Font.FontSize = 14;
            t.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(1).Height = 22;

            ws.Range(2, 1, 2, TOTAL_COLS).Merge();
            ws.Cell(2, 1).Value = line2;

            ws.Range(3, 1, 3, TOTAL_COLS).Merge();
            ws.Cell(3, 1).Value = line3;
            ws.Cell(3, 1).Style.Font.Italic = true;

            string[] headers =
            {
                // 1–4: trạng thái
                "STT", "TT TL", "TT Đúc", "TT chốt",
                // 5–12: thông tin mẻ
                "Ngày đúc", "Ca đúc", "Kíp đúc", "Máy đúc",
                "Mẻ thổi", "Mác thép", "Thùng số", "Thời gian",
                // 13–19: khối lượng
                "KL LF sau thép", "KL lần 1", "KL lần 2", "KL lần 3",
                "KL thép lỏng", "KL phân bổ", "KL chốt",
                // 20–27: chuyển mẻ + phân loại
                "Chuyển mẻ", "Máy đúc chuyển", "TL / Lên thẳng", "TL số",
                "Phân loại", "Thử nghiệm", "PCN XN thử nghiệm", "Nhóm mác",
                // 28–30: ghi chú
                "Ghi chú lò thổi", "Ghi chú TL", "Ghi chú đúc",
                // 31–33: lò thổi
                "Ngày lò thổi", "Ca lò thổi", "Người sửa lò thổi",
                // 34–36: tinh luyện
                "Ngày TL", "Ca TL", "Người sửa TL",
                // 37–39: máy đúc
                "Ngày đúc", "Ca đúc", "Người xác nhận",
            };
            for (int c = 0; c < headers.Length; c++)
            {
                var hCell = ws.Cell(HEADER_ROW, c + 1);
                hCell.Value = headers[c];
                hCell.Style.Font.Bold = true;
                hCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
                hCell.Style.Font.FontColor = XLColor.White;
                hCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                hCell.Style.Alignment.WrapText = true;
                hCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            ws.Row(HEADER_ROW).Height = 36;

            int row = DATA_START, stt = 1;
            foreach (var item in data)
            {
                // Ngày đúc hiệu lực — ưu tiên NgaySXDucChuyen (đã bấm "Chuyển ca"), rồi mới đến
                // mặc định TL: NgayNhanTL; lên thẳng: NgayTao (đã tính sẵn trong item.NgayDuc).
                var ngayDucHieuLuc = item.NgayDuc.HasValue
                    ? item.NgayDuc.Value.ToString("dd/MM/yyyy")
                    : item.NgayTao.ToString("dd/MM/yyyy");
                var caDucHieuLuc = FmtCa(item.CaDuc ?? item.CaTinhLuyen ?? item.Ca);
                var kipDuc = item.KipTinhLuyen ?? item.Kip ?? "";

                // 1–4
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = FmtTL(item.TrangThaiTL);
                ws.Cell(row, 3).Value = FmtDuc(item.TrangThaiDuc);
                var chotCell = ws.Cell(row, 4);
                if (item.IsChot == true)
                {
                    chotCell.Value = "Đã chốt";
                    chotCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#00B050");
                    chotCell.Style.Font.FontColor = XLColor.White;
                    chotCell.Style.Font.Bold = true;
                }
                // 5–12
                ws.Cell(row, 5).Value  = ngayDucHieuLuc;
                ws.Cell(row, 6).Value  = caDucHieuLuc;
                ws.Cell(row, 7).Value  = kipDuc;
                ws.Cell(row, 8).Value  = item.TenMayDuc ?? "";
                ws.Cell(row, 9).Value  = item.MaMe ?? "";
                ws.Cell(row, 10).Value = item.MacThepBKMIS ?? "";
                ws.Cell(row, 11).Value = item.ThungSo ?? "";
                ws.Cell(row, 12).Value = item.ThoiGian ?? "";
                // 13–19: KL
                SetExcelNum(ws.Cell(row, 13), item.KLLFSauThep);
                SetExcelNum(ws.Cell(row, 14), item.KlLan1);
                SetExcelNum(ws.Cell(row, 15), item.KlLan2);
                SetExcelNum(ws.Cell(row, 16), item.KlLan3);
                SetExcelNum(ws.Cell(row, 17), item.KlThepLong);
                SetExcelNum(ws.Cell(row, 18), item.KLThepLongPhanBo);
                SetExcelNum(ws.Cell(row, 19), item.KlThepLongChot);
                // 20–27
                ws.Cell(row, 20).Value = item.ChuyenVeMaMe ?? "";
                ws.Cell(row, 21).Value = item.TenMayDucChuyen ?? "";
                ws.Cell(row, 22).Value = item.TinhLuyenLenThang ?? "";
                ws.Cell(row, 23).Value = item.SoTinhLuyenNhan.HasValue ? $"TL {item.SoTinhLuyenNhan}" : "";
                ws.Cell(row, 24).Value = item.PhanLoai ?? "";
                ws.Cell(row, 25).Value = item.IsThuNghiem == true ? "✓" : "";
                ws.Cell(row, 26).Value = "";  // PCN XN thử nghiệm — chưa có field
                ws.Cell(row, 27).Value = item.TenNhomPhanLoai ?? "";
                // 28–30: ghi chú
                ws.Cell(row, 28).Value = item.GhiChuLo ?? "";
                ws.Cell(row, 29).Value = item.GhiChuTL ?? "";
                ws.Cell(row, 30).Value = item.GhiChuDuc ?? "";
                // 31–33: lò thổi
                ws.Cell(row, 31).Value = item.NgayTao.ToString("dd/MM/yyyy");
                ws.Cell(row, 32).Value = FmtCa(item.Ca);
                ws.Cell(row, 33).Value = item.TenCapNhatBoiLo ?? "";
                // 34–36: tinh luyện
                ws.Cell(row, 34).Value = item.NgayNhanTL.HasValue ? item.NgayNhanTL.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 35).Value = FmtCa(item.CaTinhLuyen);
                ws.Cell(row, 36).Value = item.TenCapNhatBoiTL ?? "";
                // 37–39: máy đúc
                ws.Cell(row, 37).Value = item.NgayDuc.HasValue ? item.NgayDuc.Value.ToString("dd/MM/yyyy") : "";
                ws.Cell(row, 38).Value = FmtCa(item.CaDuc);
                ws.Cell(row, 39).Value = item.TenCapNhatBoiDuc ?? "";

                var rowRange = ws.Range(row, 1, row, TOTAL_COLS);
                rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                if (row % 2 == 0)
                    foreach (var cell in rowRange.Cells().Where(c2 => c2.Style.Fill.BackgroundColor == XLColor.NoColor))
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

                if (item.KlThepLong < 0) ws.Cell(row, 17).Style.Font.FontColor = XLColor.Red;
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.Column(1).Width  = 5;
            ws.Column(5).Width  = 14;
            ws.Column(6).Width  = 10;
            ws.Column(7).Width  = 10;
            ws.Column(28).Width = 28;
            ws.Column(29).Width = 28;
            ws.Column(30).Width = 28;
            ws.Column(31).Width = 14;
            ws.Column(34).Width = 14;
            ws.Column(37).Width = 14;
        }

        private static void SetExcelNum(IXLCell cell, decimal? v)
        {
            if (v.HasValue) cell.Value = (double)v.Value;
        }

        // -------------------------------------------------------
        // EXPORT PDF — dùng template HRC1_BBGN_ThepLong.html
        // -------------------------------------------------------
        public async Task<ExportFileResult> ExportPdfAsync(Guid phieuId)
        {
            var phieu = await _repo.GetBmPhieuAsync(phieuId)
                ?? throw new KeyNotFoundException($"Không tìm thấy phiếu {phieuId}.");
            if (!phieu.NgaySX.HasValue || !phieu.Ca.HasValue)
                throw new InvalidOperationException("Phiếu thiếu NgaySX hoặc Ca.");

            var ca = phieu.Ca.Value;
            var kip = phieu.Kip ?? "";
            var ngay = phieu.NgaySX.Value;

            string tuGio, denGio, tuNgay, denNgay;
            if (ca == 1) { tuGio = "08"; denGio = "20"; tuNgay = ngay.ToString("dd/MM/yyyy"); denNgay = tuNgay; }
            else { tuGio = "20"; denGio = "08"; tuNgay = ngay.ToString("dd/MM/yyyy"); denNgay = ngay.AddDays(1).ToString("dd/MM/yyyy"); }

            var phieuData = await GetPhieuAsync(phieuId);
            var rows = phieuData.DanhSachMe
                .OrderBy(r =>
                {
                    if (string.IsNullOrEmpty(r.ThoiGian)) return "9999-12-31 99:99";
                    var isNextDay = ca == 2 && string.Compare(r.ThoiGian, "19:21", StringComparison.Ordinal) < 0;
                    return $"{(isNextDay ? ngay.AddDays(1) : ngay):yyyy-MM-dd} {r.ThoiGian}";
                })
                .ToList();

            var sb = new StringBuilder();
            decimal sumKl = 0;
            int stt = 1;
            foreach (var r in rows)
            {
                var meStyle = r.IsTrungMeThoi == true ? " style=\"color:red;font-weight:bold\"" : "";
                string dichDisp = r.DichChuyen switch
                {
                    "tinh_luyen" => r.TLDichSo.HasValue ? $"TL {r.TLDichSo}" : "Tinh luyện",
                    "len_thang" => r.TenMayDucDich ?? "Lên thẳng",
                    _ => "",
                };
                sb.Append($@"
                <tr>
                  <td>{stt++}</td>
                  <td>{r.TenMayDucDich ?? ""}</td>
                  <td>{r.MaMe ?? ""}</td>
                  <td{meStyle}>{r.MacThepBKMIS ?? ""}</td>
                  <td>{r.ThungSo ?? ""}</td>
                  <td>{r.ThoiGian ?? ""}</td>
                  <td>{(r.DichChuyen == "len_thang" ? r.KLLFSauThep?.ToString("0.##") : r.KlLan1?.ToString("0.##")) ?? ""}</td>
                  <td>{(r.KlLan2.HasValue ? r.KlLan2.Value.ToString("0.##") : "")}</td>
                  <td>{(r.KlLan3.HasValue ? r.KlLan3.Value.ToString("0.##") : "")}</td>
                  <td>{(r.KlThepLong.HasValue ? r.KlThepLong.Value.ToString("0.##") : "")}</td>
                  <td>{r.GhiChuLo ?? ""}</td>
                  <td>{dichDisp}</td>
                </tr>");
                if (r.KlThepLong.HasValue) sumKl += r.KlThepLong.Value;
            }

            // Lấy danh sách mẻ thuộc phiếu này để tra lịch sử
            var meIds = rows.Select(r => r.Id).ToList();
            var (benGiaoIds, benNhanIds) = await _repo.GetBenGiaoNhanIdsAsync(meIds);

            var allUserIds = benGiaoIds.Union(benNhanIds).Distinct().ToList();
            var userMap = allUserIds.Count > 0
                ? await _masterCtx.Tbl_TaiKhoan
                    .Include(t => t.ViTri)
                    .Where(t => allUserIds.Contains(t.ID_TaiKhoan))
                    .ToDictionaryAsync(t => t.ID_TaiKhoan, t => t)
                : new Dictionary<int, TaiKhoan>();

            string BuildInfoHtml(List<int> ids)
            {
                if (ids.Count == 0) return "";
                var lines = new List<string>();
                foreach (var id in ids)
                {
                    if (!userMap.TryGetValue(id, out var u)) continue;
                    var name = System.Net.WebUtility.HtmlEncode(u.HoVaTen ?? "");
                    var chucVu = System.Net.WebUtility.HtmlEncode(u.ViTri?.TenViTri ?? "");
                    var info = $"Ông/bà: <strong>{name}</strong>";
                    if (!string.IsNullOrEmpty(chucVu))
                        info += $"&nbsp;&nbsp;&nbsp;&nbsp;Chức vụ: <strong>{chucVu}</strong>";
                    lines.Add(info);
                }
                return string.Join("<br/>", lines);
            }

            async Task<string> BuildSigHtmlAsync(List<int> ids)
            {
                if (ids.Count == 0) return "";
                var parts = new List<string>();
                foreach (var id in ids)
                {
                    if (!userMap.TryGetValue(id, out var u)) continue;
                    var imgTag = await FormatChuKyImgAsync(u.ChuKy);
                    var name = System.Net.WebUtility.HtmlEncode(u.HoVaTen ?? "");
                    var chucVu = System.Net.WebUtility.HtmlEncode(u.ViTri?.TenViTri ?? "");
                    var chucVuHtml = !string.IsNullOrEmpty(chucVu)
                        ? $"<div style=\"font-size:9pt;color:#555;margin-top:2px;\">{chucVu}</div>"
                        : "";
                    parts.Add($@"<div style=""display:inline-block;text-align:center;margin:0 16px;"">
{imgTag}
<div style=""font-weight:bold;margin-top:4px;"">{name}</div></div>");
                }
                return string.Join("", parts);
            }

            var benGiaoInfoHtml = BuildInfoHtml(benGiaoIds);
            var benNhanInfoHtml = BuildInfoHtml(benNhanIds);
            var benGiaoKyHtml   = await BuildSigHtmlAsync(benGiaoIds);
            var benNhanKyHtml   = await BuildSigHtmlAsync(benNhanIds);

            var templatePath = Path.Combine(_env.WebRootPath, "template_html", "HRC1_BBGN_ThepLong.html");
            var logoUrl = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(Path.Combine(_env.WebRootPath, "imgs", "LogoPDF.png")))}";
            var html = await File.ReadAllTextAsync(templatePath);
            var bmCode = await _bmConfig.GetBmCodeHtmlAsync("HRC1_BBGN_ThepLong");

            html = html
                .Replace("{{LogoUrl}}", logoUrl)
                .Replace("{{BmCode}}", bmCode)
                .Replace("{{Kip}}", kip)
                .Replace("{{TuGio}}", tuGio)
                .Replace("{{TuNgay}}", tuNgay)
                .Replace("{{DenGio}}", denGio)
                .Replace("{{DenNgay}}", denNgay)
                .Replace("{{Rows}}", sb.ToString())
                .Replace("{{TongKl}}", sumKl.ToString("0.##"))
                .Replace("{{BenGiaoInfoHtml}}", benGiaoInfoHtml)
                .Replace("{{BenNhanInfoHtml}}", benNhanInfoHtml)
                .Replace("{{BenGiaoKyHtml}}", benGiaoKyHtml)
                .Replace("{{BenNhanKyHtml}}", benNhanKyHtml);

            var doc = new HtmlToPdfDocument
            {
                GlobalSettings = { PaperSize = PaperKind.A4, Orientation = DinkToPdf.Orientation.Portrait },
                Objects = { new ObjectSettings { HtmlContent = html, WebSettings = { DefaultEncoding = "utf-8" } } }
            };
            return new ExportFileResult
            {
                Content = _pdfConverter.Convert(doc),
                FileName = $"HRC1_BBGN_ThepLong_{ngay:ddMMyyyy}_Ca{ca}.pdf",
                ContentType = "application/pdf",
            };
        }

        private async Task<string> FormatChuKyImgAsync(string? chuKy)
        {
            if (string.IsNullOrWhiteSpace(chuKy)) return "";

            if (chuKy.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                return $"<img src=\"{chuKy}\" style=\"max-width:130px;max-height:70px;\" />";

            string url = chuKy.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? chuKy
                : (_config.GetValue<string>("AppSettings:Domain") ?? "https://report.hoaphatdungquat.vn").TrimEnd('/') + chuKy;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var bytes = await client.GetByteArrayAsync(url);
                var mime = url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
                return $"<img src=\"data:{mime};base64,{Convert.ToBase64String(bytes)}\" style=\"max-width:130px;max-height:70px;\" />";
            }
            catch { return ""; }
        }
    }
}
