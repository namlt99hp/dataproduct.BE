using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    // Trang quản lý danh mục "Cụm nhận" (cha) + "Chi tiết" (con) vùng Cấp cốc
    // (/lc-xuatcapdanhmuc) — xem LC_XuatCapDanhMucService, PhanTich_TabCapCoc.md §9.
    [ApiController]
    [Route("api/LC_XuatCapDanhMuc")]
    public class LC_XuatCapDanhMucController : ControllerBase
    {
        private readonly LC_XuatCapDanhMucService _service;

        public LC_XuatCapDanhMucController(LC_XuatCapDanhMucService service)
        {
            _service = service;
        }

        [HttpGet("cum")]
        public async Task<IActionResult> GetCumList([FromQuery] int? scope)
        {
            return Ok(await _service.GetCumListAsync(scope));
        }

        [HttpGet("phongban-options")]
        public async Task<IActionResult> GetPhongBanOptions()
        {
            return Ok(await _service.GetPhongBanOptionsAsync());
        }

        [HttpGet("cum-options")]
        public async Task<IActionResult> GetCumOptions([FromQuery] int? scope)
        {
            return Ok(await _service.GetCumOptionsAsync(scope));
        }

        [HttpGet("loaicoc-options")]
        public async Task<IActionResult> GetLoaiCocOptions()
        {
            return Ok(await _service.GetLoaiCocOptionsAsync());
        }

        [HttpGet("loainhan-options")]
        public async Task<IActionResult> GetLoaiNhanOptions()
        {
            return Ok(await _service.GetLoaiNhanOptionsAsync());
        }

        [HttpPost("cum")]
        public async Task<IActionResult> AddCum([FromBody] CreateLcXuatCapDanhMucCumDto dto)
        {
            var (result, error) = await _service.AddCumAsync(dto);
            if (error != null) return BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpPut("cum/{id}")]
        public async Task<IActionResult> UpdateCum(int id, [FromBody] UpdateLcXuatCapDanhMucCumDto dto)
        {
            var (result, error) = await _service.UpdateCumAsync(id, dto);
            if (result == null) return error == "Không tìm thấy Cụm nhận." ? NotFound() : BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpDelete("cum/{id}")]
        public async Task<IActionResult> DeleteCum(int id)
        {
            var ok = await _service.DeleteCumAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }

        [HttpPost("chitiet")]
        public async Task<IActionResult> AddChiTiet([FromBody] CreateLcXuatCapDanhMucChiTietDto dto)
        {
            var (result, error) = await _service.AddChiTietAsync(dto);
            if (error != null) return BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpPut("chitiet/{id}")]
        public async Task<IActionResult> UpdateChiTiet(int id, [FromBody] UpdateLcXuatCapDanhMucChiTietDto dto)
        {
            var (result, error) = await _service.UpdateChiTietAsync(id, dto);
            if (result == null) return error == "Không tìm thấy Chi tiết." ? NotFound() : BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpDelete("chitiet/{id}")]
        public async Task<IActionResult> DeleteChiTiet(int id)
        {
            var ok = await _service.DeleteChiTietAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }
    }
}
