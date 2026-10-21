using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // API riêng cho tab Cấp liệu & Silo — đọc/lưu trực tiếp bảng quan hệ
    // (LC_CapLieu/LC_TonSilo/LC_KetQuaPhanTich), KHÔNG qua BmPhieu.DataJson.
    [ApiController]
    [Route("api/LC_CapLieuSilo")]
    public class LC_CapLieuSiloController : ControllerBase
    {
        private readonly LC_CapLieuSiloService _service;

        public LC_CapLieuSiloController(LC_CapLieuSiloService service)
        {
            _service = service;
        }

        [HttpGet("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.GetTabAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPut("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> SaveByPhieu(Guid idPhieu, [FromBody] LcCapLieuSiloTabDto dto)
        {
            try
            {
                return Ok(await _service.SaveTabAsync(idPhieu, dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // "Làm mới" — tính lại Tồn đầu/Lũy kế theo dữ liệu MỚI NHẤT của kíp liền trước,
        // không cần người dùng gửi lại toàn bộ dữ liệu đang nhập (xem LC_CapLieuSiloRepository).
        [HttpPost("by-phieu/{idPhieu:guid}/lam-moi")]
        public async Task<IActionResult> RefreshByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.RefreshTonDauTuCaTruocAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
