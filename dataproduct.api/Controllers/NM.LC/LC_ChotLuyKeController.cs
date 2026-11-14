using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // "Chốt lũy kế" (xem LC_ChotLuyKeService / SECTION 34 lc_bcxs_xnl_tp.sql) — nút riêng ở
    // trang phiếu LC_BCSX_XNL_TP, cạnh "Làm mới".
    [ApiController]
    [Route("api/LC_ChotLuyKe")]
    public class LC_ChotLuyKeController : ControllerBase
    {
        private readonly LC_ChotLuyKeService _service;

        public LC_ChotLuyKeController(LC_ChotLuyKeService service)
        {
            _service = service;
        }

        [HttpPost("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> ChotLuyKe(Guid idPhieu, [FromQuery] string? nguoiChot)
        {
            try
            {
                return Ok(await _service.ChotLuyKeAsync(idPhieu, nguoiChot));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
