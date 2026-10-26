using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    [ApiController]
    [Route("api/[controller]")]
    public class LC_SiloTonController : ControllerBase
    {
        private readonly LC_SiloTonService _service;

        public LC_SiloTonController(LC_SiloTonService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] int? scope)
        {
            var result = await _service.GetListAsync(scope);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var entity = await _service.GetByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] CreateLcSiloTonDto dto)
        {
            var (result, error) = await _service.AddAsync(dto);
            if (error != null) return BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateLcSiloTonDto dto)
        {
            var (result, error) = await _service.UpdateAsync(id, dto);
            if (result == null) return error == "Không tìm thấy Silo tồn." ? NotFound() : BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }

        // Tính thử công thức (UI "máy tính") — không cần đã lưu danh mục, dùng cả lúc tạo mới.
        [HttpPost("test-cong-thuc")]
        public IActionResult TestCongThuc([FromBody] TestCongThucDto dto)
        {
            return Ok(_service.TestCongThuc(dto));
        }
    }
}
