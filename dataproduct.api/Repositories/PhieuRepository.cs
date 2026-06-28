using dataproduct.api.DTOs;
using dataproduct.api.DTOs.CTD_Dto;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.ResponseModels;
using dataproduct.api.Services;
using dataproduct.api.Utils;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace dataproduct.api.Repositories
{
    public class PhieuRepository : IPhieuRepository
    {
        private readonly ProductFormContext _context;
        private readonly PheDuyetService _pdservice;
        private readonly IPheDuyetRepository _pheDuyetRepo;
        private readonly ProductDataMasterDbContext _mastercontext;

        public PhieuRepository(ProductFormContext context, PheDuyetService pdservice, IPheDuyetRepository pheDuyetRepo, ProductDataMasterDbContext mastercontext)
        {
            _context = context;
            _pdservice = pdservice; 
            _pheDuyetRepo = pheDuyetRepo;
            _mastercontext = mastercontext;
        }

        public async Task<IEnumerable<BmPhieu>> GetAllAsync(string? MaBM, int? NguoiTaoID)
        {
            var query = _context.BmPhieus.Where(x => x.IsDelete != 1 && x.IsLock != 1).OrderByDescending(x => x.NgayTao).AsQueryable();


            if (!string.IsNullOrEmpty(MaBM))
                query = query.Where(x => x.MaBm == MaBM);
            if (NguoiTaoID != null)
                query = query.Where(x => x.NguoiTaoId == NguoiTaoID);

            return await query.ToListAsync();
        }

        public async Task<BmPhieu?> GetByIdAsync(Guid id)
        {
            return await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == id && x.IsDelete != 1 && x.IsLock != 1);
        }

        public async Task<BmPhieu?> GetByIdPhieuChaAsync(Guid id)
        {
            return await _context.BmPhieus.FirstOrDefaultAsync(x => x.Idphieu == id && x.IsDelete != 1);
        }

        public async Task<BmPhieu> AddAsync([FromBody] JsonElement formData)
        {
            try
            {

                string maBM = formData.GetProperty("maBm").GetString() ?? "UNKNOWN";
                string prefix = formData.TryGetProperty("prefix", out var p) ? p.GetString() ?? "UNKNOWN" : "UNKNOWN";
                int Ca = formData.TryGetProperty("ca", out var ca) ? ca.GetInt32() : 0;
                // Scope nullable: HRC1_LoThoi và HRC1_TinhLuyen không dùng scope ở phiếu (scope theo mẻ)
                int? Scope = formData.TryGetProperty("scope", out var scopeProp) && scopeProp.ValueKind == JsonValueKind.Number
                    ? scopeProp.GetInt32()
                    : (int?)null;
                DateOnly? NgaySX = formData.TryGetProperty("NgaySX", out var ngaySXProp)
                                ? DateOnly.FromDateTime(ngaySXProp.GetDateTime())
                                : null;
                string soPhieu = await SoPhieuHelper.GenerateAutoSoPhieu(_context, prefix, Scope ?? 0, Ca, NgaySX);
                // lấy tên scope từ formData (an toàn cho cả string/number)
                string? tenScope = null;
                if (formData.TryGetProperty("tenScope", out var tenScopeProp))
                {
                    tenScope = tenScopeProp.ValueKind switch
                    {
                        JsonValueKind.String => tenScopeProp.GetString(),
                        JsonValueKind.Number => tenScopeProp.GetRawText(),
                        _ => null
                    };
                }

                var phieu = new BmPhieu
                {
                    Idphieu = Guid.NewGuid(),
                    MaBm = maBM,
                    SoPhieu = soPhieu,
                    NgaySX = NgaySX,
                    Ca = Ca,
                    MayDuc = formData.TryGetProperty("mayduc", out var mdProp) ? mdProp.GetInt32() : null,
                    NguoiTaoId = formData.TryGetProperty("nguoiTaoId", out var nguoitao) ? nguoitao.GetInt32() : null,
                    Scope = Scope,
                    //XuongId = formData.TryGetProperty("xuongId", out var xuongId) ? xuongId.GetInt32() : null,
                    //IdphongBan = formData.TryGetProperty("idphongBan", out var idphongBan) ? idphongBan.GetInt32() : null,
                    TenScope = tenScope,
                    DataJson = formData.GetRawText(),
                    NgayTao = DateTime.Now,
                    TinhTrang = 0,
                    IsDelete = 0,
                    IsLock = 0
                };
                _context.BmPhieus.Add(phieu);
                await _context.SaveChangesAsync();
                // Lưu thông tin phê duyệt

                List<BmPheDuyet> pheDuyetList = new();

                if (formData.TryGetProperty("pheDuyet", out var pheDuyetProp) && pheDuyetProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in pheDuyetProp.EnumerateArray())
                    {
                        var phe = new BmPheDuyet
                        {
                            PhieuId = phieu.Idphieu,
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
                    // xóa dữ liệu phê duyệt cũ
                    var listDuyet = _context.BmPheDuyets.Where(p => p.PhieuId == phieu.Idphieu).ToList();
                    _context.BmPheDuyets.RemoveRange(listDuyet);
                    // Lưu danh sách phê duyệt mới
                    foreach (var item in pheDuyetList)
                    {
                        _context.BmPheDuyets.Add(item);
                    }
                    _context.SaveChanges();
                }

                return phieu;

            }
            catch (Exception ex)
            {
                return null;
                //BadRequest(new { success = false, message = ex.Message });
            }
        }

        public async Task UpdateAsync(BmPhieu entity)
        {
            // EF tự nhận diện Guid làm khóa chính → Update không lỗi
            _context.BmPhieus.Update(entity);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var item = await _context.BmPhieus.FindAsync(id);
            if (item != null)
            {
                _context.BmPhieus.Remove(item);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _context.BmPhieus.AnyAsync(e => e.Idphieu == id);
        }

        public async Task<bool> CheckExistsAsync(string maBm, DateOnly ngaySX, int ca, int? scope, int? mayduc)
        {
            var query = _context.BmPhieus.Where(x =>
                x.MaBm == maBm &&
                x.NgaySX == ngaySX &&
                x.Ca == ca &&
                x.IsDelete != 1 &&
                x.IsLock != 1
            );

            if (scope.HasValue)
                query = query.Where(x => x.Scope == scope.Value);

            if (mayduc.HasValue)
                query = query.Where(x => x.MayDuc == mayduc.Value);

            return await query.AnyAsync();
        }

        public async Task<(IEnumerable<SearchPhieuResponseModel> Data, int TotalCount)> SearchWithPagingAsync(SearchPhieuRequest request)
        {
            var query = _context.BmPhieus
                .Where(x => x.IsDelete != 1 && x.IsLock != 1)
                // Nếu truyền NguoiDuyetId => chỉ lấy phiếu có user này trong ds phê duyệt
                .Where(x => !request.NguoiDuyetId.HasValue || request.NguoiDuyetId.Value <= 0
                    ? true
                    : _context.BmPheDuyets.Any(pd =>
                        pd.PhieuId == x.Idphieu &&
                        pd.NguoiDuyetId == request.NguoiDuyetId.Value &&
                        pd.CapDuyet != 0
                    )
                )
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .AsQueryable();
            if (request.TuNgay.HasValue)
            {
                query = query.Where(x => x.NgaySX >= DateOnly.FromDateTime(request.TuNgay.Value));
            }
            if (request.DenNgay.HasValue)
            {
                query = query.Where(x => x.NgaySX <= DateOnly.FromDateTime(request.DenNgay.Value));
            }
            if (request.Ca.HasValue)
            {
                query = query.Where(x => x.Ca == request.Ca.Value);
            }
            if (request.Scope.HasValue)
            {
                query = query.Where(x => x.Scope == request.Scope.Value);
            }
            if (request.MayDuc.HasValue)
            {
                query = query.Where(x => x.MayDuc == request.MayDuc.Value);
            }
            if (request.MaBmList != null && request.MaBmList.Count > 0)
            {
                query = query.Where(x => request.MaBmList.Contains(x.MaBm));
            }
            else if (!string.IsNullOrEmpty(request.MaBm))
            {
                query = query.Where(x => x.MaBm == request.MaBm);
            }
            if (!string.IsNullOrEmpty(request.searchText))
            {
                query = query.Where(x => x.SoPhieu.Contains(request.searchText));
            }
            if (request.TinhTrang.HasValue)
            {
                query = query.Where(x => x.TinhTrang == request.TinhTrang.Value);
            }
            if (request.NguoiTaoId.HasValue && request.NguoiTaoId.Value > 0)
            {
                var nguoiTao = request.NguoiTaoId.Value;
                query = query.Where(x =>
                    x.NguoiTaoId == null
                    || x.NguoiTaoId == nguoiTao
                    || _context.BmPheDuyets.Any(pd =>
                        pd.PhieuId == x.Idphieu &&
                        pd.CapDuyet == 0 &&
                        pd.NguoiDuyetId == nguoiTao
                    )
                );
            }
            if (request.IsCheck.HasValue)
            {
                query = query.Where(x => x.IsCheck == request.IsCheck.Value);
            }
            var totalCount = await query.CountAsync();
            var data = await query.Skip((request.page - 1) * request.pageSize).Take(request.pageSize).ToListAsync();
            var ids = data.Select(x => x.Idphieu).ToList();
            var result = data.Select(x => new SearchPhieuResponseModel
            {
                Idphieu = x.Idphieu,
                SoPhieu = x.SoPhieu,
                MaBm = x.MaBm,
                NgaySX = x.NgaySX.HasValue ? x.NgaySX.Value : DateOnly.MinValue,
                Ca = x.Ca,
                Kip = x.Kip,
                Scope = x.Scope,
                MayDuc = x.MayDuc,
                TinhTrang = x.TinhTrang,
                NguoiTao = x.NguoiTaoId,
                IsCheck = x.IsCheck,
            }).ToList();
            foreach (var item in result)
            {
                var pheDuyet = await _pdservice.GetPheDuyetPhieuAsync(item.Idphieu);

                // Nếu là biểu mẫu CTD_Phoinong thì kiểm tra trạng thái CTD/QLCL của CtdPhoiNong
                // và override TinhTrang của từng người xử lý trước khi trả về
                if (item.MaBm == "CTD_BB_Phoinong")
                {
                    var hasPendingCTD = await _context.CtdPhoiNongs
                        .AnyAsync(x => x.NgaySx == item.NgaySX && x.Ca == item.Ca && x.NmCan == item.MayDuc && x.TinhTrangCTD != 1);

                    var hasPendingQLCL = await _context.CtdPhoiNongs
                        .AnyAsync(x => x.NgaySx == item.NgaySX && x.Ca == item.Ca && x.NmCan == item.MayDuc && x.TinhTrangQLCL != 1);

                    // CapDuyet = 2: người xử lý CTD
                    if (hasPendingCTD)
                    {
                        var ctd = pheDuyet.FirstOrDefault(x => x.CapDuyet == 2);
                        if (ctd != null) ctd.TinhTrang = 0;
                    }

                    // CapDuyet = 1: người xử lý QLCL
                    if (hasPendingQLCL)
                    {
                        var qlcl = pheDuyet.FirstOrDefault(x => x.CapDuyet == 1);
                        if (qlcl != null) qlcl.TinhTrang = 0;
                    }
                }

                else if (item.MaBm == "HRC1_BB_GiaoNhanPhoiNhapKho")
                {
                    var start = item.NgaySX.ToDateTime(TimeOnly.MinValue);
                    var nextDay = start.AddDays(1);

                    var queryPNK = _context.BM_PhoiNhapKho
                        .Where(x =>
                            x.NgaySX >= start && x.NgaySX < nextDay &&
                            x.Ca == item.Ca &&
                            x.MayDuc == item.MayDuc);
                    // Check từ BM_PhoiNhapKho table
                    var hasPendingCap0 = await queryPNK.AnyAsync() &&
                     await queryPNK.AllAsync(x => x.TinhTrangCap0 == 1);

                    var hasPendingCap1 = await queryPNK.AnyAsync() &&
                      await queryPNK.AllAsync(x => x.TinhTrangCap1 == 1);

                    var hasPendingCap2 = await queryPNK.AnyAsync() &&
                     await queryPNK.AllAsync(x => x.TinhTrangCap2 == 1);

                    // CapDuyet = 0: Xuống/Factory
                    if (hasPendingCap0)
                    {
                        var cap0 = pheDuyet.FirstOrDefault(x => x.CapDuyet == 0);
                        if (cap0 != null) cap0.TinhTrang = 1;

                    }

                    // CapDuyet = 1: QLCL/QC
                    if (hasPendingCap1)
                    {
                        var cap1 = pheDuyet.FirstOrDefault(x => x.CapDuyet == 1);
                        if (cap1 != null) cap1.TinhTrang = 1;
                    }

                    // CapDuyet = 2: Đúc/Casting
                    if (hasPendingCap2)
                    {
                        var cap2 = pheDuyet.FirstOrDefault(x => x.CapDuyet == 2);
                        if (cap2 != null) cap2.TinhTrang = 1;
                    }
                }

                item.PheDuyet = pheDuyet.ToList();
            }

            return (result, totalCount);
        }

        public async Task<(IEnumerable<SearchPhieuResponseModel> Data, int TotalCount)> SearchWithPagingByUserAsync(SearchPhieuByUserRequest request)
        {
            // ===== BƯỚC 1: LoaiVung — mặc định 1 (Việc tôi bắt đầu) nếu client chưa gửi =====
            var loaiVung = request.LoaiVung ?? 1;

            // ===== BƯỚC 2: Base query =====
            var query = _context.BmPhieus
                .Where(x => x.IsDelete != 1 && x.IsLock != 1)
                .AsQueryable();

            // ===== BƯỚC 3: Filter theo LoaiVung =====
            switch (loaiVung)
            {
                case 1: // Việc tôi bắt đầu — quyền 1|4, lọc MaKhuVuc → Scope
                {
                    if (!request.UserId.HasValue || request.UserId.Value <= 0)
                        return (Enumerable.Empty<SearchPhieuResponseModel>(), 0);

                    var userId = request.UserId.Value;

                    // Hiển thị TẤT CẢ phiếu user có quyền 1|4 (như vùng 3 nhưng quyền khác).
                    // FE tự kiểm tra TinhTrang + NguoiTaoId để ẩn/hiện nút thao tác:
                    //   - TinhTrang ∈ {0, 3, 7} → mọi user có quyền được thao tác
                    //   - TinhTrang khác → chỉ NguoiTaoId hoặc người phê duyệt CapDuyet=0 mới được thao tác

                    // --- TRƯỚC: chỉ hiển thị phiếu tinhTrang 0|3|7 hoặc là người tạo (comment để revert) ---
                    // var hrc1LoTLQuery1 = _context.BmPhieus
                    //     .Where(x => x.IsDelete != 1 && x.IsLock != 1
                    //              && (x.MaBm == "HRC1_LoThoi" || x.MaBm == "HRC1_TinhLuyen"))
                    //     .Where(x => _context.BmQuyenXls.Any(q =>
                    //         q.IdTaiKhoan == userId &&
                    //         q.MaBm == x.MaBm &&
                    //         (q.QuyenChucNang == 1 || q.QuyenChucNang == 4)
                    //     ) && (x.NguoiTaoId == userId || x.TinhTrang == 0 || x.TinhTrang == 7 || x.TinhTrang == 3));
                    // var regularQuery1 = _context.BmPhieus
                    //     .Where(x => x.IsDelete != 1 && x.IsLock != 1
                    //              && x.MaBm != "HRC1_LoThoi" && x.MaBm != "HRC1_TinhLuyen")
                    //     .Where(x =>
                    //         x.MaBm != null &&
                    //         _context.BmQuyenXls.Any(q =>
                    //             q.IdTaiKhoan == userId &&
                    //             q.MaBm == x.MaBm &&
                    //             (q.MaKhuVuc == "ALL" || q.MaKhuVuc == x.Scope.ToString()) &&
                    //             (q.QuyenChucNang == 1 || q.QuyenChucNang == 4)
                    //         ) &&
                    //         (x.NguoiTaoId == userId || x.TinhTrang == 0 || x.TinhTrang == 7 || x.TinhTrang == 3));

                    // HRC1_LoThoi/TinhLuyen: không có scope
                    var hrc1LoTLQuery1 = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && (x.MaBm == "HRC1_LoThoi" || x.MaBm == "HRC1_TinhLuyen"))
                        .Where(x => _context.BmQuyenXls.Any(q =>
                            q.IdTaiKhoan == userId &&
                            q.MaBm == x.MaBm &&
                            (q.QuyenChucNang == 1 || q.QuyenChucNang == 4)
                        ));

                    var regularQuery1 = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && x.MaBm != "HRC1_LoThoi" && x.MaBm != "HRC1_TinhLuyen")
                        .Where(x =>
                            x.MaBm != null &&
                            _context.BmQuyenXls.Any(q =>
                                q.IdTaiKhoan == userId &&
                                q.MaBm == x.MaBm &&
                                (q.MaKhuVuc == "ALL" || q.MaKhuVuc == x.Scope.ToString()) &&
                                (q.QuyenChucNang == 1 || q.QuyenChucNang == 4)
                            ));

                    query = regularQuery1.Union(hrc1LoTLQuery1);
                    break;
                }

                case 2: // Việc đến tôi — quyền 2|4
                {
                    if (!request.UserId.HasValue || request.UserId.Value <= 0)
                        return (Enumerable.Empty<SearchPhieuResponseModel>(), 0);

                    var userId = request.UserId.Value;

                    // HRC1_BBGN_ThepLong: trả tất cả, user xử lý từng mẻ trong phiếu, không phê duyệt phiếu
                    var hrc1BbgnQuery = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && x.MaBm == "HRC1_BBGN_ThepLong");

                    // HRC2_BBGN_PhoiTam: workflow slab (Đúc/Kho/PKH), không dùng BmPheDuyet
                    var hrc2PhoiTamQuery = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && x.MaBm == "HRC2_BBGN_PhoiTam")
                        .Where(x => _context.BmQuyenXls.Any(q =>
                            q.IdTaiKhoan == userId &&
                            q.MaBm == x.MaBm &&
                            (q.QuyenChucNang == 2 || q.QuyenChucNang == 4)
                        ));

                    // HRC1_LoThoi/TinhLuyen: phiếu không có scope, trả nếu user có quyền 2|4 cho maBm
                    var hrc1LoTLQuery2 = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && (x.MaBm == "HRC1_LoThoi" || x.MaBm == "HRC1_TinhLuyen"))
                        .Where(x => _context.BmQuyenXls.Any(q =>
                            q.IdTaiKhoan == userId &&
                            q.MaBm == x.MaBm &&
                            (q.QuyenChucNang == 2 || q.QuyenChucNang == 4)
                        ));

                    // Các loại khác: quyền 2|4 + phê duyệt (scope-based)
                    var regularQuery2 = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && x.MaBm != "HRC1_BBGN_ThepLong"
                                 && x.MaBm != "HRC2_BBGN_PhoiTam"
                                 && x.MaBm != "HRC1_LoThoi"
                                 && x.MaBm != "HRC1_TinhLuyen")
                        .Where(x =>
                            x.MaBm != null &&
                            _context.BmQuyenXls.Any(q =>
                                q.IdTaiKhoan == userId &&
                                q.MaBm == x.MaBm &&
                                (q.MaKhuVuc == "ALL" || q.MaKhuVuc == x.Scope.ToString()) &&
                                (q.QuyenChucNang == 2 || q.QuyenChucNang == 4)
                            ) &&
                            _context.BmPheDuyets.Any(pd =>
                                pd.PhieuId == x.Idphieu &&
                                pd.CapDuyet != null && pd.CapDuyet != 0 &&
                                pd.NguoiDuyetId == userId
                            ) &&
                            x.TinhTrang != 0
                        );

                    query = regularQuery2.Union(hrc1BbgnQuery).Union(hrc2PhoiTamQuery).Union(hrc1LoTLQuery2);
                    break;
                }

                case 3: // Chỉ xem — quyền 5
                {
                    if (!request.UserId.HasValue || request.UserId.Value <= 0)
                        return (Enumerable.Empty<SearchPhieuResponseModel>(), 0);

                    var userId = request.UserId.Value;

                    // HRC1_LoThoi/TinhLuyen: không có scope, chỉ cần quyền 5 cho maBm
                    var hrc1LoTLQuery3 = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && (x.MaBm == "HRC1_LoThoi" || x.MaBm == "HRC1_TinhLuyen"))
                        .Where(x => _context.BmQuyenXls.Any(q =>
                            q.IdTaiKhoan == userId &&
                            q.MaBm == x.MaBm &&
                            q.QuyenChucNang == 5
                        ));

                    var regularQuery3 = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && x.MaBm != "HRC1_LoThoi" && x.MaBm != "HRC1_TinhLuyen")
                        .Where(x =>
                            x.MaBm != null &&
                            _context.BmQuyenXls.Any(q =>
                                q.IdTaiKhoan == userId &&
                                q.MaBm == x.MaBm &&
                                (q.MaKhuVuc == "ALL" || q.MaKhuVuc == x.Scope.ToString()) &&
                                q.QuyenChucNang == 5
                            )
                        );

                    query = regularQuery3.Union(hrc1LoTLQuery3);
                    break;
                }

                case 4: // Thống kê — chỉ PKH / Admin, không filter theo user
                {
                    if (request.IsThongKeUser != true)
                        return (Enumerable.Empty<SearchPhieuResponseModel>(), 0);
                    break;
                }

                default:
                    return (Enumerable.Empty<SearchPhieuResponseModel>(), 0);
            }

            // ===== BƯỚC 4: Các filter thông thường (giữ nguyên như cũ) =====
            if (request.TuNgay.HasValue)
                query = query.Where(x => x.NgaySX >= DateOnly.FromDateTime(request.TuNgay.Value));

            if (request.DenNgay.HasValue)
                query = query.Where(x => x.NgaySX <= DateOnly.FromDateTime(request.DenNgay.Value));

            if (request.Ca.HasValue)
                query = query.Where(x => x.Ca == request.Ca.Value);

            if (request.ScopeFilters != null && request.ScopeFilters.Count > 0)
            {
                var pairs = request.ScopeFilters
                    .Select(sf => sf.Split("::"))
                    .Where(p => p.Length == 2 && int.TryParse(p[1], out _))
                    .Select(p => (MaBm: p[0], Scope: int.Parse(p[1])))
                    .ToList();

                if (pairs.Count > 0)
                {
                    IQueryable<BmPhieu>? filtered = null;
                    foreach (var pair in pairs)
                    {
                        var lMaBm = pair.MaBm; var lScope = pair.Scope;
                        // HRC1_LoThoi/TinhLuyen: phiếu không có scope, lọc chỉ theo maBm
                        var sub = (lMaBm == "HRC1_LoThoi" || lMaBm == "HRC1_TinhLuyen")
                            ? query.Where(x => x.MaBm == lMaBm)
                            : query.Where(x => x.MaBm == lMaBm && x.Scope == lScope);
                        filtered = filtered == null ? sub : filtered.Union(sub);
                    }
                    query = filtered!;
                }
            }
            else if (request.Scope.HasValue)
                query = query.Where(x => x.Scope == request.Scope.Value);

            if (request.MayDuc.HasValue)
                query = query.Where(x => x.MayDuc == request.MayDuc.Value);

            if (request.MaBmList != null && request.MaBmList.Count > 0)
                query = query.Where(x => request.MaBmList.Contains(x.MaBm));
            else if (!string.IsNullOrEmpty(request.MaBm))
                query = query.Where(x => x.MaBm == request.MaBm);

            if (!string.IsNullOrEmpty(request.searchText))
                query = query.Where(x => x.SoPhieu.Contains(request.searchText));

            if (request.TinhTrang.HasValue)
                query = query.Where(x => x.TinhTrang == request.TinhTrang.Value);

            query = query.OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca);
            // ===== BƯỚC 5: Paging + Assemble (giống hàm cũ) =====
            var totalCount = await query.CountAsync();
            var data = await query.Skip((request.page - 1) * request.pageSize).Take(request.pageSize).ToListAsync();

            var result = data.Select(x => new SearchPhieuResponseModel
            {
                Idphieu    = x.Idphieu,
                SoPhieu    = x.SoPhieu,
                MaBm       = x.MaBm,
                NgaySX     = x.NgaySX.HasValue ? x.NgaySX.Value : DateOnly.MinValue,
                Ca         = x.Ca,
                Kip        = x.Kip,
                Scope      = x.Scope,
                MayDuc     = x.MayDuc,
                TinhTrang  = x.TinhTrang,
                NguoiTao   = x.NguoiTaoId,
                TenScope   = x.TenScope
            }).ToList();
            foreach (var item in result)
            {
                var pheDuyet = await _pdservice.GetPheDuyetPhieuAsync(item.Idphieu);
                item.PheDuyet = pheDuyet.ToList();
            }

            return (result, totalCount);
        }

        public async Task<IEnumerable<Tbl_LoCao>> GetAllLoCaoAsync()
        {
            return await _mastercontext.Tbl_LoCao.AsNoTracking().ToListAsync();
        }
        public async Task<IEnumerable<int?>> GetSoPhieuAsync(string maBm, DateOnly? ngaySX, int? ca)
        {
            var query = _context.BmPhieus
                .Where(x => x.MaBm == maBm)
                .AsQueryable();

            if (ngaySX.HasValue)
                query = query.Where(x => x.NgaySX == ngaySX.Value);

            // if (ca.HasValue)
            //     query = query.Where(x => x.Ca == ca.Value);

            var result = await query
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .Select(x => x.Scope)
                .ToListAsync();

            return result;
        }

    }
}
