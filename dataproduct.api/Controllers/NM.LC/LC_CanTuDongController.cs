using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // API "Cân tự động" cho TabCapLieuSilo — đọc dữ liệu cân thô (LC_CanTuDongRawLog) theo đúng
    // slot của phiếu và "Gán" SUM vào KhoiLuongAmThucTe của LC_CapLieu, ghi NGAY (không qua nút
    // Lưu phiếu). Xem LC_CanTuDongRepository.
    [ApiController]
    [Route("api/LC_CanTuDong")]
    public class LC_CanTuDongController : ControllerBase
    {
        private readonly LC_CanTuDongService _service;

        public LC_CanTuDongController(LC_CanTuDongService service)
        {
            _service = service;
        }

        [HttpGet("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.GetRawLogByPhieuAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPost("by-phieu/{idPhieu:guid}/sync")]
        public async Task<IActionResult> SyncByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.SyncRawLogByPhieuAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Đồng bộ dữ liệu cân tự động thất bại: " + ex.Message });
            }
        }

        [HttpPost("by-phieu/{idPhieu:guid}/gan")]
        public async Task<IActionResult> GanByPhieu(Guid idPhieu, [FromBody] LcCanTuDongGanRequestDto dto)
        {
            try
            {
                return Ok(await _service.GanAsync(idPhieu, dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("by-phieu/{idPhieu:guid}/bo-gan")]
        public async Task<IActionResult> BoGanByPhieu(Guid idPhieu, [FromBody] LcCanTuDongBoGanRequestDto dto)
        {
            try
            {
                return Ok(await _service.BoGanAsync(idPhieu, dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
