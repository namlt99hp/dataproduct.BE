using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/CanBangNL")]
    [ApiController]
    public class CanBangNLController : ControllerBase
    {
        private readonly CanBangNLService _service;

        public CanBangNLController(CanBangNLService service)
        {
            _service = service;
        }

        // GET api/CanBangNL?ngay=2026-09-20&ca=1&coAm=true&idLoCao=1
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] DateTime ngay,
            [FromQuery] int? ca,
            [FromQuery] bool coAm = true,
            [FromQuery] int? idLoCao = null)
        {
            try { return Ok(await _service.GetCanBangAsync(ngay, ca, coAm, idLoCao)); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.GetBaseException().Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.GetBaseException().Message }); }
        }

        // GET api/CanBangNL/trend?ngay=2026-09-20&material=COKE&soNgay=7&coAm=true
        [HttpGet("trend")]
        public async Task<IActionResult> GetTrend(
            [FromQuery] DateTime ngay,
            [FromQuery] string material,
            [FromQuery] int soNgay = 7,
            [FromQuery] int? ca = null,
            [FromQuery] bool coAm = true,
            [FromQuery] int? idLoCao = null)
        {
            try { return Ok(await _service.GetTrendAsync(ngay, ca, coAm, idLoCao, material, soNgay)); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.GetBaseException().Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.GetBaseException().Message }); }
        }

        // GET api/CanBangNL/config — xem cấu hình đang áp dụng (đã validate)
        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            try { return Ok(await _service.GetConfigAsync()); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.GetBaseException().Message }); }
            catch (Exception ex) { return StatusCode(500, new { message = ex.GetBaseException().Message }); }
        }
    }
}
