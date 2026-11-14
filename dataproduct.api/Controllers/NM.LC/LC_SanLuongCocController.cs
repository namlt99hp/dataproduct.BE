using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // API riêng cho tab Sản lượng cốc + Dập cốc — đọc/lưu trực tiếp bảng quan hệ
    // (LC_TP_DapCoc/LC_TP_CocChuyenNhaSang), KHÔNG qua BmPhieu.DataJson.
    [ApiController]
    [Route("api/LC_SanLuongCoc")]
    public class LC_SanLuongCocController : ControllerBase
    {
        private readonly LC_SanLuongCocService _service;

        public LC_SanLuongCocController(LC_SanLuongCocService service)
        {
            _service = service;
        }

        [HttpGet("cum-san-xuat")]
        public async Task<IActionResult> GetCumSanXuat([FromQuery] int? scope)
            => Ok(await _service.GetCumSanXuatListAsync(scope));

        [HttpGet("vi-tri-dap-coc")]
        public async Task<IActionResult> GetViTriDapCoc([FromQuery] int? scope)
            => Ok(await _service.GetViTriDapCocListAsync(scope));

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
        public async Task<IActionResult> SaveByPhieu(Guid idPhieu, [FromBody] SanLuongCocTabSaveDto dto)
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
