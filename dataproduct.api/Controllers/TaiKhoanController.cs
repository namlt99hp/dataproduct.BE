using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaiKhoanController : ControllerBase
    {
        private readonly ProductDataMasterDbContext _context;
        private readonly ProductFormContext _formContext;

        public TaiKhoanController(ProductDataMasterDbContext context, ProductFormContext formContext)
        {
            _context = context;
            _formContext = formContext;
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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.username) || string.IsNullOrEmpty(request.password))
                return BadRequest(new { message = "Thiếu tên tài khoản hoặc mật khẩu" });

            // 🔒 Mã hóa mật khẩu bằng MD5
            string hashedPassword = SecurityHelper.ToMD5(request.password);

            var user = await _context.Tbl_TaiKhoan
                .Include(x => x.PhongBan)
                .FirstOrDefaultAsync(x =>
                    x.TenTaiKhoan == request.username &&
                    x.MatKhau == hashedPassword);

            if (user == null)
                return Unauthorized(new { message = "Sai tài khoản hoặc mật khẩu" });

            // Lưu session (nếu dùng session trên server)
            //HttpContext.Session.SetString("UserID", user.ID_TaiKhoan.ToString());
            //HttpContext.Session.SetString("UserName", user.HoVaTen ?? "");
            //HttpContext.Session.SetString("PhongBan", user.PhongBan?.TenPhongBan ?? "");
            //HttpContext.Session.SetString("Xuong", user.Xuong_API ?? "");
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
                TenPhongBan = user.PhongBan?.TenPhongBan,
                user.ID_Quyen,
            };

            return Ok(result);
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

            return Ok(new { bmQuyenXlList, quyenTheoLo });
        }

        [HttpGet("list-ky-duyet")]
        public async Task<IActionResult> ListkyDuyet([FromQuery] string maBm, int loaiQuyen)
        {
            var quyenChucNangList = loaiQuyen == (int)LoaiQuyenChucNangEnum.NguoiTao
                ? new List<byte> { 1, 4 }
                : new List<byte> { 2, 4 };

            var idTaiKhoans = await _formContext.BmQuyenXls
                .Where(x => x.MaBm == maBm
                    && x.QuyenChucNang != null
                    && quyenChucNangList.Contains(x.QuyenChucNang.Value))
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

