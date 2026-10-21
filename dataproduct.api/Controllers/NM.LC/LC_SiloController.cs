using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    [ApiController]
    [Route("api/[controller]")]
    public class LC_SiloController : ControllerBase
    {
        private readonly LC_SiloService _service;

        public LC_SiloController(LC_SiloService service)
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
        public async Task<IActionResult> Add([FromBody] CreateLcSiloDto dto)
        {
            var (result, error) = await _service.AddAsync(dto);
            if (error != null) return BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateLcSiloDto dto)
        {
            var (result, error) = await _service.UpdateAsync(id, dto);
            if (result == null) return error == "Không tìm thấy Silo." ? NotFound() : BadRequest(new { message = error });
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }
    }
}
