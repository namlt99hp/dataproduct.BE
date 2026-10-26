using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // API cho tab "Cấp cốc" (LcTabXuatCap, LC_BCSX_XNL_TP) — đọc/lưu LC_XuatCap theo phiếu +
    // liệt kê/gán-bỏ gán nguồn BBGN thô. Xem LC_XuatCapService, .claude/LuyenCoc/PhanTich_TabCapCoc.md §9.
    [ApiController]
    [Route("api/LC_XuatCap")]
    public class LC_XuatCapController : ControllerBase
    {
        private readonly LC_XuatCapService _service;

        public LC_XuatCapController(LC_XuatCapService service)
        {
            _service = service;
        }

        [HttpGet("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetByPhieu(Guid idPhieu)
        {
            return Ok(await _service.GetTabAsync(idPhieu));
        }

        [HttpPut("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> SaveByPhieu(Guid idPhieu, [FromBody] List<LcXuatCapSaveSlotDto> slots)
        {
            try
            {
                return Ok(await _service.SaveTabAsync(idPhieu, slots));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // "Làm mới" nguồn BBGN — danh sách chấm chuyến thô trong ngày/ca cho FE hiển thị bên
        // cạnh layout để người dùng tự gán tay (không auto-map). KHÔNG còn tham số Kip.
        [HttpGet("nguon-bbgn")]
        public async Task<IActionResult> GetNguonBbgn([FromQuery] DateTime ngay, [FromQuery] string ca, [FromQuery] int scope)
        {
            return Ok(await _service.GetNguonBbgnAsync(ngay, ca, scope));
        }

        [HttpPost("gan")]
        public async Task<IActionResult> Gan([FromBody] LcXuatCapGanRequest req)
        {
            try
            {
                return Ok(await _service.GanNguonAsync(req));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("gan/{idXuatCapNguon:int}")]
        public async Task<IActionResult> BoGan(int idXuatCapNguon)
        {
            try
            {
                return Ok(await _service.BoGanNguonAsync(idXuatCapNguon));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
