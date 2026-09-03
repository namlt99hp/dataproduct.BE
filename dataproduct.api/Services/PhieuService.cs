using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;
using dataproduct.api.Services;
using dataproduct.api.Services.Exporters;
using dataproduct.api.Services.Initializers;
using dataproduct.api.Services.PhieuEnrichers;
using dataproduct.api.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Any;
using NuGet.Protocol.Core.Types;
using System.Text.Json;

namespace dataproduct.api.Business
{
    public class PhieuService
    {
        private readonly IPhieuRepository _repo;
        private readonly ISTD_NXT_HRC2Repository _std_nxt_hrc2Repo;
        private readonly IBMPheDuyetRepository _pheDuyetRepo;
        private readonly BmPheDuyetService _pheDuyetService;
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterContext;
        private readonly DLNMHRC2Service _dlnmHrc2Service;
        private readonly IEnumerable<IPhieuJsonInitializer> _jsonInitializers;
        private readonly IEnumerable<IPhieuPdfExporter> _pdfExporters;
        private readonly IEnumerable<IPhieuExcelExporter> _excelExporters;
        private readonly PhieuDetailExcelService _detailExcelService;
        private readonly Dictionary<string, IPhieuSearchEnricher> _enricherMap;

        public PhieuService(
            IPhieuRepository repo,
            ISTD_NXT_HRC2Repository std_nxt_hrc2Repo,
            IBMPheDuyetRepository pheDuyetRepo,
            BmPheDuyetService pheDuyetService,
            ProductFormContext context,
            ProductDataMasterDbContext masterContext,
            DLNMHRC2Service dlnmHrc2Service,
            IEnumerable<IPhieuJsonInitializer> jsonInitializers,
            IEnumerable<IPhieuPdfExporter> pdfExporters,
            IEnumerable<IPhieuExcelExporter> excelExporters,
            PhieuDetailExcelService detailExcelService,
            IEnumerable<IPhieuSearchEnricher> enrichers)
        {
            _repo = repo;
            _std_nxt_hrc2Repo = std_nxt_hrc2Repo;
            _pheDuyetRepo = pheDuyetRepo;
            _pheDuyetService = pheDuyetService;
            _context = context;
            _masterContext = masterContext;
            _dlnmHrc2Service = dlnmHrc2Service;
            _jsonInitializers = jsonInitializers;
            _pdfExporters = pdfExporters;
            _excelExporters = excelExporters;
            _detailExcelService = detailExcelService;
            _enricherMap = enrichers.ToDictionary(e => e.MaBm);
        }

        /// <summary>
        /// Export PDF động - nếu có filters sẽ áp dụng, nếu không sẽ export toàn bộ
        /// </summary>
        public async Task<DTOs.Export.ExportFileResult> ExportPdfDynamicAsync(Guid phieuId, List<string>? filters = null)
        {
            var phieu = await _repo.GetByIdAsync(phieuId);
            if (phieu == null)
                throw new Exception("Không tìm thấy phiếu");

            var exporter = _pdfExporters.FirstOrDefault(x => x.CanHandle(phieu.MaBm));
            if (exporter == null)
                throw new NotSupportedException($"Chưa cấu hình export PDF cho biểu mẫu: {phieu.MaBm}");

            // Nếu có filters, dùng ExportPdfAsyncExtra; ngược lại dùng ExportPdfAsync
            if (filters != null && filters.Count > 0)
            {
                return await exporter.ExportPdfAsyncExtra(phieuId, filters);
            }

            return await exporter.ExportPdfAsync(phieuId);
        }

        public async Task<DTOs.Export.ExportFileResult> ExportExcelDynamicPhieuAsync(Guid phieuId)
        {
            var phieu = await _repo.GetByIdAsync(phieuId);
            if (phieu == null)
                throw new Exception("Không tìm thấy phiếu");

            var exporter = _excelExporters.FirstOrDefault(x => x.CanHandle(phieu.MaBm));
            if (exporter == null)
                throw new NotSupportedException($"Chưa cấu hình export excel cho biểu mẫu: {phieu.MaBm}");


            return await exporter.ExportExcelPhieuAsync(phieuId);
        }


        public async Task<DTOs.Export.ExportFileResult> ExportTongHopExcelDynamicAsync(string? maBm, DateOnly? fromDate, DateOnly? toDate)
        {
            if (string.IsNullOrWhiteSpace(maBm))
                throw new ArgumentException("Thiếu maBm");

            var exporter = _excelExporters.FirstOrDefault(x => x.CanHandle(maBm));
            if (exporter == null)
                throw new NotSupportedException($"Chưa cấu hình export Excel tổng hợp cho biểu mẫu: {maBm}");

            return await exporter.ExportTongHopExcelAsync(fromDate, toDate);
        }


        public async Task<DTOs.Export.ExportFileResult> ExportDetailExcelDynamicAsync(Guid phieuId)
        {
            var phieu = await _repo.GetByIdAsync(phieuId);
            if (phieu == null)
                throw new Exception("Không tìm thấy phiếu");

            // Thử exporter riêng theo maBm trước
            var exporter = _excelExporters.FirstOrDefault(x => x.CanHandle(phieu.MaBm));
            if (exporter != null)
            {
                try
                {
                    return await exporter.ExportDetailExcelAsync(phieuId);
                }
                catch (NotSupportedException)
                {
                    // Chưa implement → fallback về generic
                }
            }

            // Fallback: render generic từ DataJson
            var content = await _detailExcelService.ExportAsync(phieuId);
            return new DTOs.Export.ExportFileResult
            {
                Content = content,
                FileName = $"{phieu.MaBm}_{phieu.SoPhieu}_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        public async Task<IEnumerable<PhieuDto>> GetAllAsync(string? MaBM, int? NguoiTaoID, int? NguoiDuyetID, int? isCheckDuyet)
        {
            var data = (await _repo.GetAllAsync(MaBM, NguoiTaoID)).ToList();
            var listduyet = (await _pheDuyetService.GetAllAsync(NguoiDuyetID, isCheckDuyet)).ToList();

            if (NguoiDuyetID != null) // lọc theo user được duyệt
            {
                // Join 2 danh sách theo Id phiếu
                var result = (from p in data
                              join d in listduyet on p.Idphieu equals d.PhieuId
                              //into joined
                              //from d in joined.DefaultIfEmpty()  // left join
                              select new PhieuDto
                              {
                                  Idphieu = p.Idphieu,
                                  MaBm = p.MaBm,
                                  SoPhieu = p.SoPhieu,
                                  XuongId = p.XuongId,
                                  IdphongBan = p.IdphongBan,
                                  Idkip = p.Idkip,
                                  Ca = p.Ca,
                                  Kip = p.Kip,
                                  Scope = p.Scope,
                                  NgayTao = p.NgayTao,
                                  NgaySX = p.NgaySX,
                                  MayDuc = p.MayDuc,
                                  NguoiTaoId = p.NguoiTaoId,
                                  TinhTrang = p.TinhTrang,
                                  //   DataJson = p.DataJson,
                                  IsDelete = p.IsDelete,
                                  IsLock = p.IsLock,
                                  LoaiPhieu = p.LoaiPhieu,
                                  IsClone = p.IsClone,
                                  VersionClone = p.VersionClone,
                                  ID_PhieuGoc = p.ID_PhieuGoc,
                                  PheDuyet = new List<BM_PheDuyetDto> { d },
                              }).ToList();

                return result;
            }
            else
            {
                return data.Select(p => new PhieuDto
                {
                    Idphieu = p.Idphieu,
                    MaBm = p.MaBm,
                    SoPhieu = p.SoPhieu,
                    XuongId = p.XuongId,
                    IdphongBan = p.IdphongBan,
                    Idkip = p.Idkip,
                    Ca = p.Ca,
                    Kip = p.Kip,
                    Scope = p.Scope,
                    NgayTao = p.NgayTao,
                    NgaySX = p.NgaySX,
                    MayDuc = p.MayDuc,
                    NguoiTaoId = p.NguoiTaoId,
                    TinhTrang = p.TinhTrang,
                    // DataJson = p.DataJson,
                    IsDelete = p.IsDelete,
                    IsLock = p.IsLock,
                    LoaiPhieu = p.LoaiPhieu,
                    IsClone = p.IsClone,
                    VersionClone = p.VersionClone,
                    ID_PhieuGoc = p.ID_PhieuGoc,
                    // PheDuyet = new List<BM_PheDuyetDto>(),
                });
            }


        }

        public async Task<PhieuDto?> GetByIdAsync(Guid id)
        {
            var item = await _repo.GetByIdAsync(id);
            if (item == null) return null;

            // Parse JSON trong DataJson thành object
            var jsonObject = !string.IsNullOrEmpty(item.DataJson)
                ? JsonSerializer.Deserialize<JsonElement>(item.DataJson)
                : new JsonElement();
            // Thông tin phê duyệt

            var duyet = await _pheDuyetService.GetByIdPhieuAsync(id);

            return new PhieuDto
            {
                Idphieu = item.Idphieu,
                MaBm = item.MaBm,
                SoPhieu = item.SoPhieu,
                XuongId = item.XuongId,
                IdphongBan = item.IdphongBan,
                Idkip = item.Idkip,
                Ca = item.Ca,
                Kip = item.Kip,
                Scope = item.Scope,
                NgayTao = item.NgayTao,
                NgaySX = item.NgaySX,
                MayDuc = item.MayDuc,
                NguoiTaoId = item.NguoiTaoId,
                TinhTrang = item.TinhTrang,
                //DataJson = item.DataJson,
                JsonData = item.DataJson != null ? jsonObject : null,
                IsDelete = item.IsDelete,
                IsLock = item.IsLock,
                LoaiPhieu = item.LoaiPhieu,
                IsClone = item.IsClone,
                VersionClone = item.VersionClone,
                ID_PhieuGoc = item.ID_PhieuGoc,
                PheDuyet = duyet?.ToList() ?? new List<BM_PheDuyetDto>(),
            };
        }

        public async Task<BmPhieu?> CreateAsync(JsonElement formData, bool skipDuplicateCheck = false)
        {
            if (!skipDuplicateCheck)
            {
                await CheckDuplicateAsync(formData);
            }

            try
            {
                using var tran = await _context.Database.BeginTransactionAsync();

                var phieu = await _repo.AddAsync(formData);
                if (phieu == null)
                {
                    await tran.RollbackAsync();
                    return null;
                }

                await ResolveAndSaveKipAsync(phieu);

                await RunJsonInitializersAsync(phieu);

                await _context.SaveChangesAsync();
                await tran.CommitAsync();

                // Đồng bộ kíp nếu Ca hoặc NgaySX thay đổi
                await ResolveAndSaveKipAsync(phieu);

                return phieu;
            }
            catch (Exception)
            {
                try
                {
                    await _context.Database.RollbackTransactionAsync();
                }
                catch { }
                return null;
            }
        }

        public async Task<(BmPhieu? Phieu, List<string> Warnings)> UpdateAsync(Guid id, JsonElement formData)
        {
            // 1. Lấy phiếu hiện tại
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return (null, new List<string>());
            EnsurePhieuOperable(existing);
            // Cho phép update khi ĐangLuu (0), Đã thu hồi (3) hoặc Hiệu chỉnh (7 = phiếu clone đang chỉnh sửa). Lưu ở trạng thái 7 không đổi TinhTrang.
            if (existing == null || (existing.TinhTrang != 0 && existing.TinhTrang != 3 && existing.TinhTrang != 7)) return (null, new List<string>());

            // 2. Cập nhật các field chính (nếu có trong JSON)
            if (formData.TryGetProperty("NgaySX", out var ngaySXProp) && ngaySXProp.ValueKind != JsonValueKind.Null)
                existing.NgaySX = DateOnly.FromDateTime(ngaySXProp.GetDateTime());

            if (formData.TryGetProperty("ca", out var caProp) && caProp.ValueKind != JsonValueKind.Null)
                existing.Ca = caProp.GetInt32();

            if (formData.TryGetProperty("mayduc", out var mayDucProp) && mayDucProp.ValueKind != JsonValueKind.Null)
                existing.MayDuc = mayDucProp.GetInt32();

            if (formData.TryGetProperty("nguoiTaoId", out var nguoiTaoProp) && nguoiTaoProp.ValueKind != JsonValueKind.Null)
                existing.NguoiTaoId = nguoiTaoProp.GetInt32();

            if (formData.TryGetProperty("xuongId", out var xuongIdProp) && xuongIdProp.ValueKind != JsonValueKind.Null)
                existing.XuongId = xuongIdProp.GetInt32();

            if (formData.TryGetProperty("idphongBan", out var idphongBan) && idphongBan.ValueKind != JsonValueKind.Null)
                existing.IdphongBan = idphongBan.GetInt32();
            if (formData.TryGetProperty("scope", out var scopeProp) && scopeProp.ValueKind != JsonValueKind.Null)
                existing.Scope = scopeProp.GetInt32();

            // 3. Lưu lại JSON gốc (form động)
            existing.DataJson = formData.GetRawText();
            existing.NgayTao = existing.NgayTao; // giữ nguyên ngày tạo
            existing.IsLock = 0; // nếu muốn mở khóa khi sửa
            // Giữ nguyên TinhTrang hiện tại, không reset về 0
            // existing.TinhTrang giữ nguyên giá trị hiện tại

            // 4. Gọi repository để lưu
            await _repo.UpdateAsync(existing);

            // 4.1 Đồng bộ kíp nếu Ca hoặc NgaySX thay đổi
            await ResolveAndSaveKipAsync(existing);

            // 4.2 Đồng bộ lại dữ liệu bảng chi tiết từ DataJson.
            var warnings = await RunJsonInitializersAsync(existing);

            // Cập nhật thông tin phê duyệt
            // Lưu thông tin phê duyệt



            List<BmPheDuyet> pheDuyetList = new();

            if (formData.TryGetProperty("pheDuyet", out var pheDuyetProp) && pheDuyetProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in pheDuyetProp.EnumerateArray())
                {
                    var phe = new BmPheDuyet
                    {
                        PhieuId = existing.Idphieu,
                        CapDuyet = item.TryGetProperty("capDuyet", out var capProp) ? capProp.GetInt32() : 0,
                        NguoiDuyetId = item.TryGetProperty("nguoiDuyetId", out var ndProp) ? ndProp.GetInt32() : 0,
                        TinhTrang = item.TryGetProperty("tinhTrang", out var ttProp) ? ttProp.GetInt32() : 0,
                        GhiChu = item.TryGetProperty("ghiChu", out var gcProp) ? gcProp.GetString() : null,
                    };

                    pheDuyetList.Add(phe);
                }
            }
            if (pheDuyetList.Count > 0)
            {
                // gọi repo bmpheduyet addlist
                await _pheDuyetRepo.AddListAsync(pheDuyetList, existing.Idphieu);
            }


            return (existing, warnings);
        }

        /// <summary>
        /// Cập nhật chỉ dữ liệu bảng (DataJson) mà không kiểm tra ràng buộc tình trạng phiếu
        /// Sử dụng cho phép cập nhật dữ liệu bảng khi phiếu ở trạng thái HoanThanh và người dùng có quyền Chốt
        /// </summary>
        public async Task<(BmPhieu? Phieu, List<string> Warnings)> UpdateTableDataOnlyAsync(Guid id, JsonElement formData)
        {
            // 1. Lấy phiếu hiện tại
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return (null, new List<string>());

            // 2. Cho phép cập nhật cho các trạng thái: Chốt (5)
            // Không kiểm tra ràng buộc như UpdateAsync - mục đích chỉ để update dữ liệu bảng
            if (existing.TinhTrang == 5)
                return (null, new List<string> { "Trạng thái phiếu không cho phép cập nhật dữ liệu bảng" });

            // 3. Cập nhật DataJson (chỉ dữ liệu bảng, không cập nhật các field chính)
            existing.DataJson = formData.GetRawText();
            existing.NgayTao = existing.NgayTao; // giữ nguyên ngày tạo

            // 4. Gọi repository để lưu
            await _repo.UpdateAsync(existing);

            // 5. Đồng bộ lại dữ liệu bảng chi tiết từ DataJson
            var warnings = await RunJsonInitializersAsync(existing);

            return (existing, warnings);
        }

        public async Task<BmPhieu?> UpdateNguoiTaoAsync(Guid id, int? NguoiTaoID)
        {
            // 1. Lấy phiếu hiện tại
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return null;



            int nguoiTaoId = NguoiTaoID ?? 0;

            // 3. Lấy NgaySX, Ca, MaBm từ phiếu hiện tại
            var ngaySX = existing.NgaySX;
            var ca = existing.Ca;
            var maBm = existing.MaBm;

            if (!ngaySX.HasValue || !ca.HasValue || string.IsNullOrEmpty(maBm))
            {
                return null; // Thiếu thông tin cần thiết
            }

            // 4. Tìm tất cả các phiếu cùng NgaySX, Ca và MaBm
            var phieusToUpdate = await _context.BmPhieus
                .Where(x => x.NgaySX == ngaySX.Value
                         && x.Ca == ca.Value
                         && x.MaBm == maBm
                         && x.IsDelete != 1) // Không cập nhật phiếu đã xóa
                .ToListAsync();

            if (!phieusToUpdate.Any())
            {
                return null;
            }

            // 5. Cập nhật NguoiTaoId cho tất cả phiếu
            foreach (var phieu in phieusToUpdate)
            {
                phieu.NguoiTaoId = nguoiTaoId;
            }

            // 6. Lưu thay đổi
            _context.BmPhieus.UpdateRange(phieusToUpdate);
            await _context.SaveChangesAsync();

            // 7. Cập nhật hoặc tạo mới BM_PheDuyet cho người tạo (CapDuyet = 0)
            foreach (var phieu in phieusToUpdate)
            {
                // Tìm bản ghi phê duyệt với CapDuyet = 0
                var pheDuyetCapDuyet0 = await _context.BmPheDuyets
                    .FirstOrDefaultAsync(x => x.PhieuId == phieu.Idphieu && x.CapDuyet == 0);

                if (pheDuyetCapDuyet0 != null)
                {
                    // Nếu đã tồn tại, cập nhật NguoiDuyetId
                    pheDuyetCapDuyet0.NguoiDuyetId = nguoiTaoId;
                    _context.BmPheDuyets.Update(pheDuyetCapDuyet0);
                }
                else
                {
                    // Nếu chưa tồn tại, tạo mới
                    var newPheDuyet = new BmPheDuyet
                    {
                        PhieuId = phieu.Idphieu,
                        CapDuyet = 0,
                        NguoiDuyetId = nguoiTaoId,
                        TinhTrang = 0,
                        GhiChu = null
                    };
                    await _context.BmPheDuyets.AddAsync(newPheDuyet);
                }
            }

            // 8. Lưu thay đổi BM_PheDuyet
            await _context.SaveChangesAsync();

            // 9. Trả về phiếu hiện tại đã được cập nhật
            return existing;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var exists = await _repo.ExistsAsync(id);
            if (!exists) return false;
            await _repo.DeleteAsync(id);
            return true;
        }

        public async Task<BmPhieu?> CloneAsync(Guid id, JsonElement formData)
        {
            try
            {
                // 1. Lấy phiếu được bấm "Đề nghị hiệu chỉnh" (có thể là phiếu gốc hoặc phiếu clone),
                // chỉ cho clone khi đang Hoàn thành (2) hoặc Đang phê duyệt (6)
                var phieuGoc = await _repo.GetByIdAsync(id);
                if (phieuGoc == null) return null;
                EnsurePhieuOperable(phieuGoc);
                PhieuStatusHelper.CheckAllowStatusChange(phieuGoc.TinhTrang ?? 0, 7);
                await EnsureNoPendingPhanBoAsync(phieuGoc);

                // 2. Phiếu cha chỉ IsLock = 1 để ẩn khỏi trang, không đổi TinhTrang
                phieuGoc.IsLock = 1;
                await _repo.UpdateAsync(phieuGoc);

                await _context.SaveChangesAsync();

                // 3. Tạo phiếu clone từ formData (copy dữ liệu y như phiếu cũ)
                var phieu = await CreateAsync(formData, skipDuplicateCheck: true);
                if (phieu == null) return null;

                // 4. Số phiếu clone = SoPhieu gốc + đuôi _HieuChinh_{VersionClone} (max 50 ký tự)
                var nextVersion = (phieuGoc.VersionClone ?? 0) + 1;
                var suffix = $"_HC_{nextVersion}";
                // Strip đuôi _HC_{n} cũ (nếu có) để không bị cộng dồn _HC_1_HC_2... de
                var soPhieuBase = System.Text.RegularExpressions.Regex.Replace(
                    (phieuGoc.SoPhieu ?? "").Trim(),
                    @"_HC_\d+$", "");
                if (soPhieuBase.Length + suffix.Length > 50)
                    soPhieuBase = soPhieuBase.Substring(0, 50 - suffix.Length);
                phieu.SoPhieu = soPhieuBase + suffix;

                // 5. Clone mang trạng thái Hiệu chỉnh (7), hiện nút Lưu / Lưu và Gửi như Đang lưu
                phieu.IsClone = true;
                phieu.VersionClone = nextVersion;
                phieu.Kip = phieuGoc.Kip;
                // ID_PhieuGoc luôn trỏ về phiếu cha (phiếu bị bấm clone), hỗ trợ clone nhiều tầng: A -> A1 -> A2...
                phieu.ID_PhieuGoc = phieuGoc.Idphieu;
                phieu.TinhTrang = 7;
                await _repo.UpdateAsync(phieu);

                return phieu;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> ChangeStatusAsync(Guid id, int status, int? idUser)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;
            EnsurePhieuOperable(existing);
            await CheckAllowPheDuyetAsync(existing.TinhTrang ?? 0, status);
            if (status == 1)
            {
                // Khi "Đã gửi" thì cập nhật NguoiTaoId = người thực hiện gửi.
                // idUser được FE gửi lên; nếu null/0 thì không ghi đè để tránh phá dữ liệu hiện hữu.
                if (idUser.HasValue && idUser.Value > 0)
                {
                    existing.NguoiTaoId = idUser.Value;
                }

                var allPheDuyet = await _pheDuyetRepo.GetByIdPhieuAsync(id);
                if (allPheDuyet == null)
                    return false;
                var isCreatorZero = allPheDuyet.Where(x => x.CapDuyet == 0).FirstOrDefault();
                if (isCreatorZero == null) return false;
                isCreatorZero.TinhTrang = 1;
                await _pheDuyetRepo.UpdateAsync(isCreatorZero);
            }
            //if(status == 2) {
            //var allPheDuyet = await _pheDuyetRepo.GetByIdPhieuAsync(id);
            //if(allPheDuyet == null || !allPheDuyet.Any()) return false;
            //var isCreatorZero = allPheDuyet.Any(x => x.CapDuyet == 0);
            //if(isCreatorZero) return false;
            //foreach(var item in allPheDuyet) {
            // item.TinhTrang = 1;
            // await _pheDuyetRepo.UpdateAsync(item);
            //}
            //}

            if (status == 5)
                await CaptureExcelHeaderSnapshotIfApplicableAsync(existing);

            existing.TinhTrang = status;
            await _repo.UpdateAsync(existing);

            return true;
        }

        /// <summary>MaBm → bieuMau ngắn (BOF/LF/RH) — 3 biểu mẫu Tiêu Hao Nấu Luyện HRC2 áp dụng
        /// cơ chế snapshot cấu hình Excel lúc Chốt (xem CaptureExcelHeaderSnapshotIfApplicableAsync).</summary>
        private static readonly Dictionary<string, string> HRC2_TieuHao_MaBm_To_BieuMau =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "HRC2_BB_NauLuyen_BOF", "BOF" },
                { "HRC2_BB_NauLuyen_LF", "LF" },
                { "HRC2_BB_NauLuyen_RH", "RH" },
            };

        /// <summary>
        /// Đúng lúc phiếu chuyển sang trạng thái Chốt (TinhTrang=5): chụp lại TOÀN BỘ danh sách cột
        /// theo config Excel hiện tại của Header_Key (IsUsed_Excel/LoaiExcel/ThuTu_Excel_*) và lưu
        /// vào DataJson[PhieuDetailExcelService.ExcelHeaderSnapshotJsonKey]. Mục đích: export Excel/
        /// PDF sau này (PhieuDetailExcelService.GetExportDataAsync) sẽ đọc lại đúng snapshot này thay
        /// vì tính lại theo config hiện tại — tránh việc phiếu Chốt "mất" cột phụ liệu nếu sau này ai
        /// đó đổi config Excel. Chỉ áp dụng cho 3 biểu mẫu Tiêu Hao Nấu Luyện HRC2 (BOF/LF/RH) — các
        /// biểu mẫu khác không dùng cơ chế Header_Key Excel config này nên bỏ qua.
        /// Không throw nếu lỗi — không được chặn việc chốt phiếu chỉ vì chụp snapshot thất bại;
        /// export sẽ tự fallback về config live nếu không tìm thấy/đọc được snapshot.
        /// </summary>
        private async Task CaptureExcelHeaderSnapshotIfApplicableAsync(BmPhieu phieu)
        {
            if (phieu?.MaBm == null || !HRC2_TieuHao_MaBm_To_BieuMau.TryGetValue(phieu.MaBm, out var bieuMau))
                return;

            try
            {
                var (headersBOF, headersLF, headersRH) = await _detailExcelService.GetLiveExcelHeadersAsync();
                var headers = bieuMau == "BOF" ? headersBOF : (bieuMau == "RH" ? headersRH : headersLF);

                var snapshot = headers
                    .Select(h => new { headerKeyId = h.IDHeaderKey, label = h.TenPhuLieu, loaiPhieu = h.LoaiPhieu })
                    .ToList();

                var root = string.IsNullOrWhiteSpace(phieu.DataJson)
                    ? new System.Text.Json.Nodes.JsonObject()
                    : (System.Text.Json.Nodes.JsonNode.Parse(phieu.DataJson) as System.Text.Json.Nodes.JsonObject)
                        ?? new System.Text.Json.Nodes.JsonObject();

                root[PhieuDetailExcelService.ExcelHeaderSnapshotJsonKey] =
                    JsonSerializer.SerializeToNode(snapshot);

                phieu.DataJson = root.ToJsonString();
            }
            catch
            {
                // Không chặn việc chốt phiếu nếu chụp snapshot lỗi — export sẽ tự fallback về config live.
            }
        }

        public async Task ChotNhieuPhieuAsync(List<Guid> idPhieus, int? idUser, int status)
        {
            if (idPhieus == null || idPhieus.Count == 0)
                throw new InvalidOperationException("Danh sách phiếu không được để trống.");

            if (status != 5 && status != 2)
                throw new InvalidOperationException("Status không hợp lệ. Chỉ chấp nhận 5 (chốt) hoặc 2 (hủy chốt).");

            var phieus = new List<BmPhieu>();
            foreach (var id in idPhieus)
            {
                var phieu = await _repo.GetByIdAsync(id);
                if (phieu == null)
                    throw new InvalidOperationException($"Không tìm thấy phiếu {id}.");
                EnsurePhieuOperable(phieu);
                phieus.Add(phieu);
            }

            if (status == 5)
            {
                var notReady = phieus.Where(p => p.TinhTrang != 2).ToList();
                if (notReady.Any())
                {
                    var soPhieus = string.Join(", ", notReady.Select(p => p.SoPhieu));
                    throw new InvalidOperationException($"Các phiếu sau chưa ở trạng thái Hoàn thành, không thể chốt: {soPhieus}");
                }
            }
            else // status == 2
            {
                var notChot = phieus.Where(p => p.TinhTrang != 5).ToList();
                if (notChot.Any())
                {
                    var soPhieus = string.Join(", ", notChot.Select(p => p.SoPhieu));
                    throw new InvalidOperationException($"Các phiếu sau chưa ở trạng thái chốt, không thể hủy chốt: {soPhieus}");
                }
            }

            foreach (var phieu in phieus)
            {
                if (status == 5)
                    await CaptureExcelHeaderSnapshotIfApplicableAsync(phieu);

                phieu.TinhTrang = status;
                await _repo.UpdateAsync(phieu);
            }
        }

        public async Task CheckNhieuPhieuAsync(List<Guid> idPhieus, int isCheck)
        {
            if (idPhieus == null || idPhieus.Count == 0)
                throw new InvalidOperationException("Danh sách phiếu không được để trống.");

            var phieus = await _context.BmPhieus
                .Where(x => idPhieus.Contains(x.Idphieu))
                .ToListAsync();

            var notFound = idPhieus.Except(phieus.Select(p => p.Idphieu)).ToList();
            if (notFound.Any())
                throw new InvalidOperationException($"Không tìm thấy {notFound.Count} phiếu trong danh sách.");

            foreach (var phieu in phieus)
                phieu.IsCheck = isCheck;

            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateStatusExtendedAsync(Guid id, int? status, int? isLock, int? isDelete)
        {
            var existing = await _repo.GetByIdAsync(id);
            if (existing == null) return false;
            if (status.HasValue) existing.TinhTrang = status.Value;
            if (isLock.HasValue) existing.IsLock = isLock.Value;
            if (isDelete.HasValue) existing.IsDelete = isDelete.Value;
            await _repo.UpdateAsync(existing);
            return true;
        }

        public Task<bool> CheckAllowPheDuyetAsync(int currentStatus, int status)
        {
            PhieuStatusHelper.CheckAllowStatusChange(currentStatus, status);
            return Task.FromResult(true);
        }

        public async Task<bool> CheckExistsAsync(string maBm, DateOnly ngaySX, int ca, int? scope, int? mayduc)
        {
            return await _repo.CheckExistsAsync(maBm, ngaySX, ca, scope, mayduc);
        }

        public async Task<PagedResult<SearchPhieuResponseModel>> SearchWithPagingAsync(SearchPhieuRequest request)
        {
            var (data, totalCount) = await _repo.SearchWithPagingAsync(request);
            var dataList = data.ToList();

            foreach (var item in dataList.Where(x => x.MaBm == "HRC2_STD_NXT"))
            {
                item.TinhTrang = await GetStatusHRC2_STD_NXT(item.NgaySX, item.Ca ?? 0);
            }

            return new PagedResult<SearchPhieuResponseModel>
            {
                Data = dataList,
                TotalRecords = totalCount,
                Page = request.page,
                PageSize = request.pageSize
            };
        }

        public async Task<PagedResult<SearchPhieuResponseModel>> SearchWithPagingByUserAsync(SearchPhieuByUserRequest request)
        {
            var (data, totalCount) = await _repo.SearchWithPagingByUserAsync(request);
            var dataList = data.OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca).ThenBy(x => x.Scope).ToList();

            foreach (var item in dataList)
            {
                if (_enricherMap.TryGetValue(item.MaBm ?? "", out var enricher))
                    await enricher.EnrichAsync(item);
            }

            return new PagedResult<SearchPhieuResponseModel>
            {
                Data = dataList,
                TotalRecords = totalCount,
                Page = request.page,
                PageSize = request.pageSize
            };
        }


        public async Task<int> GetStatusHRC2_STD_NXT(DateOnly workDate, int shift)
        {
            var idPhieus = await _context.BmPhieus
                .Where(p => p.MaBm == "HRC2_STD_NXT"
                         && p.NgaySX == workDate
                         && p.Ca == shift
                         && p.IsDelete != 1)
                .Select(p => p.Idphieu)
                .ToListAsync();

            bool phanBoComplete = idPhieus.Any()
                && !await _context.STD_NXT_TOTAL_HRC2s
                    .Where(r => idPhieus.Contains(r.Id_Phieu) && r.HasPhanBo == null)
                    .AnyAsync();

            var nauLuyenMaBms = new[] { "HRC2_BB_NauLuyen_BOF", "HRC2_BB_NauLuyen_LF", "HRC2_BB_NauLuyen_RH" };
            var relatedStatuses = await _context.BmPhieus
                .Where(p => nauLuyenMaBms.Contains(p.MaBm)
                         && p.NgaySX == workDate
                         && p.Ca == shift
                         && p.IsDelete != 1
                         && p.IsLock != 1)
                .Select(p => p.TinhTrang)
                .ToListAsync();

            bool relatedComplete = relatedStatuses.Any()
                && relatedStatuses.All(t => t == 2 || t == 5);

            return (phanBoComplete && relatedComplete) ? 2 : 1;
        }

        // Helper
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
                double.TryParse(val.GetString(), out var d))
                return d;

            return null;
        }
        private async Task CheckDuplicateAsync(JsonElement formData)
        {
            string? maBM = formData.TryGetProperty("maBm", out var mBm)
                ? mBm.GetString()
                : null;

            if (string.IsNullOrEmpty(maBM))
                return;

            int Ca = formData.TryGetProperty("ca", out var ca)
                ? ca.GetInt32()
                : 0;

            int? Scope = formData.TryGetProperty("scope", out var scope) && scope.ValueKind != JsonValueKind.Null
                ? scope.GetInt32()
                : null;

            int? MayDuc = formData.TryGetProperty("mayduc", out var md) && md.ValueKind != JsonValueKind.Null
                ? md.GetInt32()
                : null;

            DateOnly? NgaySX = formData.TryGetProperty("NgaySX", out var nsx) && nsx.ValueKind != JsonValueKind.Null
                ? DateOnly.FromDateTime(nsx.GetDateTime())
                : null;

            if (!NgaySX.HasValue)
                return;

            bool exists = await _repo.CheckExistsAsync(maBM, NgaySX.Value, Ca, Scope, MayDuc);

            if (exists)
            {
                throw new InvalidOperationException(
                    $"Đã tồn tại phiếu {maBM} cho ngày {NgaySX:dd/MM/yyyy} ca {Ca}"
                );
            }
        }

        /// <summary>
        /// Tra cứu Tbl_Kip theo NgaySX + Ca, nếu tìm thấy thì cập nhật Kip và Idkip trên phiếu.
        /// </summary>
        private async Task ResolveAndSaveKipAsync(BmPhieu? phieu)
        {
            if (phieu is null || !phieu.NgaySX.HasValue || !phieu.Ca.HasValue)
                return;

            var kip = await _masterContext.Tbl_Kip
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NgayLamViec == phieu.NgaySX.Value
                                       && x.TenCa == phieu.Ca.Value.ToString());

            if (kip is null)
                return;

            if (phieu.Kip == kip.TenKip && phieu.Idkip == kip.ID_Kip)
                return;

            phieu.Kip = kip.TenKip;
            phieu.Idkip = kip.ID_Kip;
            await _repo.UpdateAsync(phieu);
        }

        private async Task<List<string>> RunJsonInitializersAsync(BmPhieu? phieu)
        {
            var warnings = new List<string>();
            if (phieu == null)
                return warnings;

            foreach (var initializer in _jsonInitializers)
            {
                if (initializer.CanHandle(phieu.MaBm))
                {
                    var w = await initializer.InitializeAsync(phieu);
                    if (w?.Count > 0)
                        warnings.AddRange(w);
                }
            }

            return warnings;
        }


        public async Task<IEnumerable<Tbl_LoCao>> GetAllLoCaoAsync()
        {
            return await _repo.GetAllLoCaoAsync();
        }
        private static void EnsurePhieuOperable(BmPhieu phieu)
        {
            if (phieu.IsDelete == 1)
            {
                throw new InvalidOperationException("Phiếu đã bị xóa hoặc từ chối. Vui lòng quay về danh sách để tải lại dữ liệu mới nhất.");
            }

            if (phieu.IsLock == 1)
            {
                throw new InvalidOperationException("Phiếu đã bị khóa do đang có bản hiệu chỉnh. Vui lòng quay về danh sách để mở phiếu hợp lệ.");
            }
        }

        /// <summary>MaBm của phiếu Tiêu Hao/Nấu Luyện → nhóm STD_NXT tương ứng ("HRC1"/"HRC2") cần kiểm
        /// tra trước khi cho "Đề nghị hiệu chỉnh" — sửa lại mẻ/tiêu hao sau khi đã phân bổ chênh lệch sẽ
        /// làm sai lệch số liệu đã tính (KLPB_*/KLTK_*) trên Sổ Xuất-Nhập-Tồn.</summary>
        private static readonly Dictionary<string, string> TieuHaoMaBmToStdNxtGroup =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["HRC1_BB_TieuHao_BOF"] = "HRC1",
                ["HRC1_BB_TieuHao_LF"] = "HRC1",
                ["HRC2_BB_NauLuyen_BOF"] = "HRC2",
                ["HRC2_BB_NauLuyen_LF"] = "HRC2",
                ["HRC2_BB_NauLuyen_RH"] = "HRC2",
            };

        /// <summary>
        /// Chặn "Đề nghị hiệu chỉnh" trên phiếu Tiêu Hao (HRC1 Lò Thổi/Tinh Luyện LF) hoặc Nấu Luyện
        /// (HRC2 BOF/LF/RH) nếu Sổ Xuất-Nhập-Tồn tương ứng (cùng Ngày SX + Ca) đã có phụ liệu được quyết
        /// định Phân bổ hoặc Không phân bổ (HasPhanBo != null). Người dùng phải Reset (Thu hồi phân bổ /
        /// đưa về Chưa xử lý) trên Sổ XNT trước, mới được hiệu chỉnh lại tiêu hao.
        /// </summary>
        private async Task EnsureNoPendingPhanBoAsync(BmPhieu phieu)
        {
            if (string.IsNullOrEmpty(phieu.MaBm) ||
                !TieuHaoMaBmToStdNxtGroup.TryGetValue(phieu.MaBm, out var group))
                return;

            if (!phieu.NgaySX.HasValue || !phieu.Ca.HasValue)
                return;

            var ngaySX = phieu.NgaySX.Value.ToDateTime(TimeOnly.MinValue);
            var ca = phieu.Ca.Value;

            var hasPending = group == "HRC1"
                ? await _context.STD_NXT_TOTAL_HRC1s.AnyAsync(x =>
                    x.NgaySX == ngaySX && x.Ca == ca && x.HasPhanBo != null)
                : await _context.STD_NXT_TOTAL_HRC2s.AnyAsync(x =>
                    x.NgaySX == ngaySX && x.Ca == ca && x.HasPhanBo != null);

            if (hasPending)
            {
                throw new InvalidOperationException(
                    "Đã có phụ liệu được Phân bổ hoặc Không phân bổ trên Sổ theo dõi Xuất-Nhập-Tồn cho " +
                    "Ngày/Ca này. Vui lòng Reset (Thu hồi phân bổ) trên Sổ Xuất-Nhập-Tồn trước khi đề nghị " +
                    "hiệu chỉnh phiếu tiêu hao.");
            }
        }

        public async Task<IEnumerable<int?>> GetSoPhieuAsync(string maBm, DateOnly? ngaySX, int? ca)
        {
            return await _repo.GetSoPhieuAsync(maBm, ngaySX, ca);
        }

        /// <summary>
        /// Lấy dữ liệu ca kíp từ Tbl_Kip theo ngày và ca
        /// </summary>
        public async Task<dynamic?> GetKipByDateAndCaAsync(DateOnly ngayLamViec, int ca)
        {
            var kip = await _masterContext.Tbl_Kip
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NgayLamViec == ngayLamViec
                                       && x.TenCa == ca.ToString());

            if (kip == null)
                return null;

            return new
            {
                id_kip = kip.ID_Kip,
                ngayLamViec = kip.NgayLamViec,
                tenCa = kip.TenCa,
                tenKip = kip.TenKip
            };
        }

        /// <summary>
        /// Reset phiếu về trạng thái "Đang lưu" (TinhTrang = 0)
        /// </summary>
        public async Task<BmPhieu?> ResetPhieuAsync(Guid id)
        {
            var phieu = await _repo.GetByIdAsync(id);
            if (phieu == null)
                return null;

            EnsurePhieuOperable(phieu);

            // Reset về trạng thái Đang lưu (0)
            phieu.TinhTrang = 0;
            phieu.IsLock = 0;
            phieu.IsDelete = 0;
            phieu.IsClone = false;
            // phieu.NgayTao = DateTime.Now;
            // nếu MaBm = CTD_BienBan_SanLuong thì check ở BK_KCS_BBXNSanLuong reset IDPhieu = null và sau đó call đến store sp_Sync_BK_KCS_BBXN_SANLUONG truyền ngày sx vào
            if (phieu.MaBm == "CTD_BienBan_SanLuong")
            {
                var relatedRecords = await _context.BkKcsBbxnSanLuongs
                                            .Where(x => x.IDPhieu == phieu.Idphieu)
                                            .ToListAsync();

                if (relatedRecords.Any())
                {
                    foreach (var item in relatedRecords)
                    {
                        item.IDPhieu = null;
                    }

                    await _context.SaveChangesAsync();
                    // Gọi store procedure để đồng bộ dữ liệu
                    await _context.Database.ExecuteSqlRawAsync("EXEC sp_Sync_BK_KCS_BBXN_SANLUONG @FromDate = {0} @ToDate = {1}", phieu.NgaySX, phieu.NgaySX);
                }

            }

            await _repo.UpdateAsync(phieu);

            return phieu;
        }

    }
}

