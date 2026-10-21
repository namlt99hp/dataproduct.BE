using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaiKhoanController : ControllerBase
    {
        private readonly ProductDataMasterDbContext _context;
        private readonly ProductFormContext _formContext;
        private readonly IConfiguration _config;

        public TaiKhoanController(ProductDataMasterDbContext context, ProductFormContext formContext, IConfiguration config)
        {
            _context = context;
            _formContext = formContext;
            _config = config;
        }

        [HttpGet("nguoiky")]
        public async Task<IActionResult> GetNguoiKy([FromQuery] string? maphongBan)
        {
            // viTri có thể là: "NM.CTD", "P.QLCL", "NM.HRC1"

            var query = _context.Tbl_TaiKhoan
                               .Include(x => x.PhongBan) // <-- để join sang bảng phòng ban
                               .AsQueryable();

            //if (!string.IsNullOrEmpty(viTri))
            //{
            //    if (viTri == "NM.CTD")
            //        query = query.Where(x => x.Xuong_API == "NM.CTD");
            //    else if (viTri == "P.QLCL")
            //        query = query.Where(x => x.PhongBan_API == "P.QLCL");
            //    else if (viTri == "NM.HRC1")
            //        query = query.Where(x => x.Xuong_API == "NM.HRC1");
            //}
            if (!string.IsNullOrEmpty(maphongBan))
            {
                query = query.Where(x => x.PhongBan.TenNgan.Contains(maphongBan));
            }

            var list = await query
                            .Select(x => new
                            {
                                x.ID_TaiKhoan,
                                x.HoVaTen,
                                x.TenTaiKhoan,
                                x.PhongBan_API,
                                x.Xuong_API,
                                x.PhongBan.TenNgan,
                                x.ChuKy,
                                TenPhongBan = x.PhongBan != null ? x.PhongBan.TenPhongBan : null
                            })
                            .ToListAsync();

            return Ok(list);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.username) || string.IsNullOrEmpty(request.password))
                return BadRequest(new { message = "Thiếu tên tài khoản hoặc mật khẩu" });

            string hashedPassword = SecurityHelper.ToMD5(request.password);

            var user = await _context.Tbl_TaiKhoan
                .Include(x => x.PhongBan)
                .FirstOrDefaultAsync(x =>
                    x.TenTaiKhoan == request.username &&
                    x.MatKhau == hashedPassword);

            if (user == null)
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });

            // Tạo JWT
            var expiryHours = double.Parse(_config["Jwt:ExpiryHours"]!);
            var expiresAt   = DateTime.UtcNow.AddHours(expiryHours);
            var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
            var creds       = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.TenTaiKhoan),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("userId",   user.ID_TaiKhoan.ToString()),
                new Claim("username", user.TenTaiKhoan),
            };

            var jwtToken = new JwtSecurityToken(
                issuer:            _config["Jwt:Issuer"],
                audience:          _config["Jwt:Audience"],
                claims:            claims,
                expires:           expiresAt,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(jwtToken);

            return Ok(new
            {
                token      = tokenString,
                expiresAt  = expiresAt,
                user.ID_TaiKhoan,
                user.TenTaiKhoan,
                user.HoVaTen,
                user.ChuKy,
                user.PhongBan_API,
                user.PhongBan.TenNgan,
                user.ID_PhanXuong,
                user.ID_PhongBan,
                user.Xuong_API,
                TenPhongBan = user.PhongBan?.TenPhongBan,
                user.ID_Quyen,
            });
        }
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                var user = await _context.Tbl_TaiKhoan
                .FirstOrDefaultAsync(x =>
                    x.TenTaiKhoan == User.Identity.Name);
                var result = new
                {
                    user.ID_TaiKhoan,
                    user.TenTaiKhoan,
                    user.HoVaTen,
                    user.ChuKy,
                    user.PhongBan_API,
                    user.PhongBan.TenNgan,
                    user.ID_PhanXuong,
                    user.ID_PhongBan,
                    user.Xuong_API,
                    TenPhongBan = user.PhongBan?.TenPhongBan
                };
                return Ok(result);
            }

            return Unauthorized(new
            {
                isAuthenticated = false
            });
        }

        /// <summary>
        /// Lấy thông tin tài khoản theo tên đăng nhập
        /// </summary>
        /// <param name="tenTaiKhoan">Tên tài khoản cần lấy thông tin</param>
        /// <returns>Thông tin tài khoản giống như khi login</returns>
        [HttpGet("info/{tenTaiKhoan}")]
        public async Task<IActionResult> GetUserInfo(string tenTaiKhoan)
        {
            if (string.IsNullOrEmpty(tenTaiKhoan))
                return BadRequest(new { message = "Tên tài khoản không được để trống" });

            var user = await _context.Tbl_TaiKhoan
                .Include(x => x.PhongBan)
                .FirstOrDefaultAsync(x => x.TenTaiKhoan == tenTaiKhoan);

            if (user == null)
                return NotFound(new { message = $"Không tìm thấy tài khoản: {tenTaiKhoan}" });

            var bmQuyenXlList = await _formContext.BmQuyenXls
                .AsNoTracking()
                .Where(x => x.IdTaiKhoan == user.ID_TaiKhoan)
                .OrderBy(x => x.MaBm)
                .Select(x => new
                {
                    maBm = x.MaBm,
                    quyenChucNang = x.QuyenChucNang,
                    khuVucPhu = x.KhuVucPhu,
                })
                .ToListAsync();

            var result = new
            {
                user.ID_TaiKhoan,
                user.TenTaiKhoan,
                user.HoVaTen,
                user.ChuKy,
                user.PhongBan_API,
                user.PhongBan?.TenNgan,
                user.ID_PhanXuong,
                user.ID_PhongBan,
                user.Xuong_API,
                TenPhongBan = user.PhongBan?.TenPhongBan,
                user.ID_Quyen,
                bmQuyenXlList,
            };

            return Ok(result);
        }

        [HttpGet("quyen/{tenTaiKhoan}")]
        public async Task<IActionResult> GetQuyen(string tenTaiKhoan)
        {
            if (string.IsNullOrEmpty(tenTaiKhoan))
                return BadRequest(new { message = "Tên tài khoản không được để trống" });

            var user = await _context.Tbl_TaiKhoan
                .FirstOrDefaultAsync(x => x.TenTaiKhoan == tenTaiKhoan);

            if (user == null)
                return NotFound(new { message = $"Không tìm thấy tài khoản: {tenTaiKhoan}" });

            var bmQuyenXlList = await _formContext.BmQuyenXls
                .AsNoTracking()
                .Where(x => x.IdTaiKhoan == user.ID_TaiKhoan)
                .OrderBy(x => x.MaBm)
                .Select(x => new
                {
                    maBm = x.MaBm,
                    quyenChucNang = x.QuyenChucNang,
                    khuVucPhu = x.KhuVucPhu,
                })
                .ToListAsync();

            var quyenTheoLo = (await _formContext.BmQuyenXls
                    .AsNoTracking()
                    .Where(x => x.IdTaiKhoan == user.ID_TaiKhoan && x.KhuVucPhu != null)
                    .Select(x => new { x.MaBm, x.KhuVucPhu })
                    .ToListAsync())
                .GroupBy(x => x.MaBm)
                .Select(g => new
                {
                    maBm = g.Key,
                    khuVucPhus = g.Select(x => x.KhuVucPhu!).Distinct().ToList()
                })
                .ToList();

            // Tương tự quyenTheoLo nhưng group theo MaKhuVuc (dùng cho các BM "ảo" chỉ có
            // scope, không có KhuVucPhu — vd THONGKE_HRC1/THONGKE_HRC2 phân quyền theo tab).
            var quyenTheoScope = (await _formContext.BmQuyenXls
                    .AsNoTracking()
                    .Where(x => x.IdTaiKhoan == user.ID_TaiKhoan && x.MaKhuVuc != null)
                    .Select(x => new { x.MaBm, x.MaKhuVuc })
                    .ToListAsync())
                .GroupBy(x => x.MaBm)
                .Select(g => new
                {
                    maBm = g.Key,
                    maKhuVucs = g.Select(x => x.MaKhuVuc!).Distinct().ToList()
                })
                .ToList();

            return Ok(new { bmQuyenXlList, quyenTheoLo, quyenTheoScope });
        }

        [HttpGet("nguoiky-by-scope")]
        public async Task<IActionResult> GetNguoiKyByScope([FromQuery] int scope)
        {
            var mapping = await _formContext.TKVV_Scope_Xuong_Mapping
                .FirstOrDefaultAsync(m => m.Scope == scope);

            if (mapping?.ID_Xuong_BBGN == null)
                return Ok(new List<object>());

            var idXuong = mapping.ID_Xuong_BBGN.Value;

            var list = await _context.Tbl_TaiKhoan
                .Include(x => x.PhongBan)
                .Where(x => x.ID_PhanXuong == idXuong)
                .Select(x => new
                {
                    x.ID_TaiKhoan,
                    x.HoVaTen,
                    x.TenTaiKhoan,
                    x.PhongBan_API,
                    x.Xuong_API,
                    x.PhongBan.TenNgan,
                    x.ChuKy,
                    TenPhongBan = x.PhongBan != null ? x.PhongBan.TenPhongBan : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // scope optional — không truyền thì giữ nguyên hành vi cũ (không lọc theo khu vực), để không ảnh
        // hưởng các trang khác đang gọi API này mà chưa truyền scope (vd Giao nhận thép lỏng, các BM
        // không có khái niệm Lò/Scope). Khi có scope: 1 người chỉ được liệt kê nếu MaKhuVuc của họ ở
        // đúng maBm này là "ALL" (không giới hạn khu vực) hoặc khớp đúng scope của phiếu đang mở — quy
        // ước "ALL" đã dùng sẵn ở PhanQuyenBieuMau.tsx/BulkSave, MaKhuVuc null coi như chưa gán khu vực
        // cụ thể (cũng không giới hạn), tránh loại nhầm các dòng quyền cũ tạo trước khi có khái niệm scope.
        [HttpGet("list-ky-duyet")]
        public async Task<IActionResult> ListkyDuyet([FromQuery] string maBm, int loaiQuyen, [FromQuery] string? scope = null)
        {
            var quyenChucNangList = loaiQuyen == (int)LoaiQuyenChucNangEnum.NguoiTao
                ? new List<byte> { 1, 4 }
                : new List<byte> { 2, 4 };

            var query = _formContext.BmQuyenXls
                .Where(x => x.MaBm == maBm
                    && x.QuyenChucNang != null
                    && quyenChucNangList.Contains(x.QuyenChucNang.Value));

            if (!string.IsNullOrEmpty(scope))
                query = query.Where(x => x.MaKhuVuc == null || x.MaKhuVuc == "ALL" || x.MaKhuVuc == scope);

            var idTaiKhoans = await query
                .Select(x => x.IdTaiKhoan)
                .Where(id => id != null)
                .Select(id => id!.Value)
                .Distinct()
                .ToListAsync();

            var taiKhoanList = await _context.Tbl_TaiKhoan
                .Include(x => x.PhongBan)
                .Where(x => idTaiKhoans.Contains(x.ID_TaiKhoan))
                .Select(x => new
                {
                    x.ID_TaiKhoan,
                    x.HoVaTen,
                    x.TenTaiKhoan,
                    x.PhongBan_API,
                    x.Xuong_API,
                    x.PhongBan.TenNgan,
                    x.ChuKy,
                    TenPhongBan = x.PhongBan != null ? x.PhongBan.TenPhongBan : null
                })
                .ToListAsync();

            return Ok(taiKhoanList);
        }

    }

}

public class LoginRequest
{
    public string username { get; set; } = "";
    public string password { get; set; } = "";
}

public enum LoaiQuyenChucNangEnum{
    NguoiTao = 1,
    NguoiDuyet = 2
}

