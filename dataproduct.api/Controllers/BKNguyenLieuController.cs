using dataproduct.api.Models;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BKNguyenLieuController : ControllerBase
    {
        private readonly BKNguyenLieuService _service;

        public BKNguyenLieuController(BKNguyenLieuService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(DateOnly? NgaySX, int? Ca, string? Kip) => Ok(await _service.GetAllAsync(NgaySX,Ca,Kip));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BkNguyenLieu model)
        {
            var created = await _service.CreateAsync(model);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BkNguyenLieu model)
        {
            var ok = await _service.UpdateAsync(id, model);
            return ok ? NoContent() : NotFound();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }

        [HttpGet("can-bang-tai")]
        public async Task<IActionResult> GetCanBangTai([FromQuery] DateOnly? NgaySX)
            => Ok(await _service.GetCanBangTaiAsync(NgaySX ?? DateOnly.FromDateTime(DateTime.Today)));
    }
}
