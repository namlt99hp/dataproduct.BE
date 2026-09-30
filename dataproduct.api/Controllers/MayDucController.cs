using dataproduct.api.Models;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MayDucController : ControllerBase
    {
        private readonly MayDucService _service;

        public MayDucController(MayDucService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(byte? nhaMay, bool? isLock, string? tenMayDuc)
            => Ok(await _service.GetAllAsync(nhaMay, isLock, tenMayDuc));

        [HttpGet("search")]
        public async Task<IActionResult> Search(string? searchKey, byte? nhaMay, bool? isLock, int page = 1, int pageSize = 30)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 200) pageSize = 200;

            var rows = await _service.GetAllAsync(nhaMay, isLock, searchKey);
            var ordered = rows.OrderBy(x => x.TenMayDuc).ToList();

            var totalCount = ordered.Count;
            var data = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new { x.Id, x.TenMayDuc, x.NhaMay, x.IsLock, x.LoaiMayDuc });

            return Ok(new
            {
                data,
                totalRecords = totalCount,
                page,
                pageSize,
                totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MayDuc model)
        {
            try
            {
                var created = await _service.CreateAsync(model);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] MayDuc model)
        {
            try
            {
                var ok = await _service.UpdateAsync(id, model);
                return ok ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // Không có xóa cứng — chỉ khóa (xem MayDucService.LockAsync)
        [HttpPut("{id}/lock")]
        public async Task<IActionResult> Lock(int id)
        {
            var ok = await _service.LockAsync(id);
            return ok ? NoContent() : NotFound();
        }
    }
}

