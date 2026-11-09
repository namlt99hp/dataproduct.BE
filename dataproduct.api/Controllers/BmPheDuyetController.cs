using dataproduct.api.Models;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BmPheDuyetController : ControllerBase
    {
        private readonly BmPheDuyetService _service;

        public BmPheDuyetController(BmPheDuyetService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(int? NguoiDuyetID, int? isCheckDuyet) => Ok(await _service.GetAllAsync(NguoiDuyetID, isCheckDuyet));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BmPheDuyet model)
        {
            var created = await _service.CreateAsync(model);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BmPheDuyet model)
        {
            var ok = await _service.UpdateAsync(id, model);
            return ok ? NoContent() : NotFound();
        }

        [HttpPut("update-tinhtrang")]
        public async Task<IActionResult> UpdateTinhTrang([FromBody] UpdateTinhTrangRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var ok = await _service.UpdateTinhTrangAsync(request.PhieuId, request.NguoiDuyetId, request.TinhTrang);
                return ok ? NoContent() : NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        //[HttpDelete("{id}")]
        //public async Task<IActionResult> Delete(int id)
        //{
        //    var ok = await _service.DeleteAsync(id);
        //    return ok ? NoContent() : NotFound();
        //}
    }

    public class UpdateTinhTrangRequest
    {
        public Guid PhieuId { get; set; }
        public int NguoiDuyetId { get; set; }
        public int TinhTrang { get; set; } // 1 = xác nhận, 0 = không xác nhận
    }
}
