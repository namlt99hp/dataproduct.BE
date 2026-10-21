using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // API riêng cho tab Phối trộn — đọc/lưu trực tiếp bảng quan hệ
    // (LC_PhoiTron/LC_PhoiTronChiTiet), KHÔNG qua BmPhieu.DataJson.
    [ApiController]
    [Route("api/LC_PhoiTron")]
    public class LC_PhoiTronController : ControllerBase
    {
        private readonly LC_PhoiTronService _service;

        public LC_PhoiTronController(LC_PhoiTronService service)
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
        public async Task<IActionResult> SaveByPhieu(Guid idPhieu, [FromBody] List<LcPhoiTronCardDto> cards)
        {
            try
            {
                return Ok(await _service.SaveTabAsync(idPhieu, cards));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // "Làm mới" — tính lại Lũy kế quy khô theo dữ liệu MỚI NHẤT của kíp liền trước,
        // không cần người dùng gửi lại toàn bộ dữ liệu đang nhập (xem LC_PhoiTronRepository).
        [HttpPost("by-phieu/{idPhieu:guid}/lam-moi")]
        public async Task<IActionResult> RefreshByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.RefreshLuyKeTuCaTruocAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
