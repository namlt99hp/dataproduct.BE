using dataproduct.api.DTOs;
using dataproduct.api.DTOs.CTD_Dto;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories.PhieuSearchFilters;
using dataproduct.api.ResponseModels;
using dataproduct.api.Services;
using dataproduct.api.Services.PhieuEnrichers;
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
        private readonly IEnumerable<IPhieuSearchFilterProvider> _searchFilterProviders;
        private readonly Dictionary<string, IPhieuTinhTrangFilterEnricher> _tinhTrangFilterMap;

        public PhieuRepository(
            ProductFormContext context,
            PheDuyetService pdservice,
            IPheDuyetRepository pheDuyetRepo,
            ProductDataMasterDbContext mastercontext,
            IEnumerable<IPhieuSearchFilterProvider> searchFilterProviders,
            IEnumerable<IPhieuTinhTrangFilterEnricher> tinhTrangFilterEnrichers)
        {
            _context = context;
            _pdservice = pdservice;
            _pheDuyetRepo = pheDuyetRepo;
            _mastercontext = mastercontext;
            _searchFilterProviders = searchFilterProviders;
            _tinhTrangFilterMap = tinhTrangFilterEnrichers.ToDictionary(e => e.MaBm);
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
                DateOnly? NgaySX = formData.TryGetProperty("NgaySX", out var ngaySXProp) || formData.TryGetProperty("ngaySX", out ngaySXProp)
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
            // Hàm này KHÔNG chứa logic riêng cho bất kỳ MaBm nào. Các BM có luồng phân quyền/tìm kiếm
            // khác chuẩn (không dùng Scope, không dùng BmPheDuyet...) được đăng ký qua
            // IPhieuSearchFilterProvider (thư mục Repositories/PhieuSearchFilters) — muốn thêm ngoại lệ
            // cho 1 BM mới thì viết provider mới, không sửa switch bên dưới.
            switch (loaiVung)
            {
                // LoaiVung 1 (Việc tôi bắt đầu): hiển thị TẤT CẢ phiếu user có quyền 1|4 (không lọc TinhTrang).
                // FE tự kiểm tra TinhTrang + NguoiTaoId để ẩn/hiện nút thao tác:
                //   - TinhTrang ∈ {0, 3, 7} → mọi user có quyền được thao tác
                //   - TinhTrang khác → chỉ NguoiTaoId hoặc người phê duyệt CapDuyet=0 mới được thao tác
                case 1: // Việc tôi bắt đầu — quyền 1|4, lọc MaKhuVuc → Scope
                case 2: // Việc đến tôi — quyền 2|4 + phê duyệt (scope-based)
                case 3: // Chỉ xem — quyền 5, lọc MaKhuVuc → Scope
                {
                    if (!request.UserId.HasValue || request.UserId.Value <= 0)
                        return (Enumerable.Empty<SearchPhieuResponseModel>(), 0);

                    var userId = request.UserId.Value;

                    // MaBm đã có provider riêng → loại khỏi query mặc định, để tránh trùng/mâu thuẫn
                    var specialMaBms = _searchFilterProviders
                        .SelectMany(p => p.MaBms)
                        .ToHashSet();

                    var scopedBase = _context.BmPhieus
                        .Where(x => x.IsDelete != 1 && x.IsLock != 1
                                 && x.MaBm != null && !specialMaBms.Contains(x.MaBm));

                    IQueryable<BmPhieu> regularQuery = loaiVung switch
                    {
                        1 => PhieuSearchFilterHelpers.WithScopeBasedXuLy(scopedBase, _context, userId),

                        2 => scopedBase.Where(x =>
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
                                x.TinhTrang != 0),

                        3 => PhieuSearchFilterHelpers.WithScopeBasedXem(scopedBase, _context, userId),

                        _ => _context.BmPhieus.Where(x => false),
                    };

                    // Hợp nhất với kết quả từ từng provider đăng ký cho loaiVung hiện tại
                    query = _searchFilterProviders.Aggregate(
                        regularQuery,
                        (acc, provider) => acc.Union(provider.BuildQuery(_context, loaiVung, userId)));

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

            if (!string.IsNullOrEmpty(request.Kip))
                query = query.Where(x => x.Kip == request.Kip);

            if (request.ScopeFilters != null && request.ScopeFilters.Count > 0)
            {
                var pairs = request.ScopeFilters
                    .Select(sf => sf.Split("::"))
                    .Where(p => p.Length == 2 && int.TryParse(p[1], out _))
                    .Select(p => (MaBm: p[0], Scope: int.Parse(p[1])))
                    .ToList();

                if (pairs.Count > 0)
                {
                    // BM nào có provider khai HasScope=false thì phiếu không có Scope → lọc chỉ theo MaBm
                    var noScopeMaBms = _searchFilterProviders
                        .Where(p => !p.HasScope)
                        .SelectMany(p => p.MaBms)
                        .ToHashSet();

                    IQueryable<BmPhieu>? filtered = null;
                    foreach (var pair in pairs)
                    {
                        var lMaBm = pair.MaBm; var lScope = pair.Scope;
                        var sub = noScopeMaBms.Contains(lMaBm)
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
                query = await ApplyTinhTrangFilterAsync(query, request.TinhTrang.Value);

            query = query.OrderByDescending(x => x.NgaySX).ThenByDescending(x => x.Ca).ThenBy(x => x.MaBm).ThenByDescending(x => x.Scope);
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

            // NguoiTao lưu ID người sửa cuối (được cập nhật lại mỗi lần lưu phiếu) — resolve tên hiển thị
            var nguoiTaoIds = result.Where(x => x.NguoiTao.HasValue).Select(x => x.NguoiTao!.Value).Distinct().ToList();
            if (nguoiTaoIds.Count > 0)
            {
                var tenNguoiTaoMap = await _mastercontext.Tbl_TaiKhoan
                    .Where(tk => nguoiTaoIds.Contains(tk.ID_TaiKhoan))
                    .Select(tk => new { tk.ID_TaiKhoan, tk.HoVaTen })
                    .ToDictionaryAsync(tk => tk.ID_TaiKhoan, tk => tk.HoVaTen);

                foreach (var item in result)
                {
                    if (item.NguoiTao.HasValue && tenNguoiTaoMap.TryGetValue(item.NguoiTao.Value, out var hoVaTen))
                        item.TenNguoiTao = hoVaTen;
                }
            }

            foreach (var item in result)
            {
                var pheDuyet = await _pdservice.GetPheDuyetPhieuAsync(item.Idphieu);
                item.PheDuyet = pheDuyet.ToList();
            }

            return (result, totalCount);
        }

        /// <summary>
        /// Lọc TinhTrang cho SearchWithPagingByUserAsync. Với MaBm có trạng thái tổng hợp tính
        /// runtime (VD HRC1_BBSL_PhoiTam/HRC2_BBSL_PhoiTam — xem IPhieuTinhTrangFilterEnricher),
        /// cột BM_Phieu.TinhTrang thật không bao giờ chứa giá trị tổng hợp (11/12) nên không thể
        /// so sánh trực tiếp; phải lấy các phiếu ứng viên của MaBm đó ra rồi nhờ enricher tương
        /// ứng tính lại (batch) xem phiếu nào khớp, trước khi áp Skip/Take.
        /// </summary>
        private async Task<IQueryable<BmPhieu>> ApplyTinhTrangFilterAsync(IQueryable<BmPhieu> query, int tinhTrang)
        {
            if (_tinhTrangFilterMap.Count == 0)
                return query.Where(x => x.TinhTrang == tinhTrang);

            var specialMaBms = _tinhTrangFilterMap.Keys.ToList();
            var specialCandidates = await query
                .Where(x => x.MaBm != null && specialMaBms.Contains(x.MaBm))
                .ToListAsync();

            var matchedIds = new List<Guid>();
            foreach (var group in specialCandidates.GroupBy(x => x.MaBm!))
            {
                if (_tinhTrangFilterMap.TryGetValue(group.Key, out var enricher))
                    matchedIds.AddRange(await enricher.FilterByTinhTrangAsync(group.ToList(), tinhTrang));
            }

            return query.Where(x =>
                (x.MaBm != null && specialMaBms.Contains(x.MaBm) && matchedIds.Contains(x.Idphieu))
                || (!(x.MaBm != null && specialMaBms.Contains(x.MaBm)) && x.TinhTrang == tinhTrang));
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
