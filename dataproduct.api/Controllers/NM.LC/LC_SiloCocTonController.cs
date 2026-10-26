using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // API riêng cho tab "Tồn cốc silo" (`tonCoc`, TabCocTonSilo.tsx) — đọc/lưu trực tiếp bảng
    // quan hệ LC_SiloCocTon theo phiếu, KHÔNG qua BmPhieu.DataJson. Xem LC_SiloCocTonRepository.
    [ApiController]
    [Route("api/LC_SiloCocTon")]
    public class LC_SiloCocTonController : ControllerBase
    {
        private readonly LC_SiloCocTonService _service;

        public LC_SiloCocTonController(LC_SiloCocTonService service)
        {
            _service = service;
        }

        [HttpGet("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetByPhieu(Guid idPhieu)
        {
            return Ok(await _service.GetTabAsync(idPhieu));
        }

        [HttpPut("by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> SaveByPhieu(Guid idPhieu, [FromBody] List<LcCocTonCumDto> cums)
        {
            try
            {
                return Ok(await _service.SaveTabAsync(idPhieu, cums));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
