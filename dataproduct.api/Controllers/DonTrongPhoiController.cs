using dataproduct.api.Models;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DonTrongPhoiController : ControllerBase
    {
        private readonly DonTrongPhoiService _service;

        public DonTrongPhoiController(DonTrongPhoiService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(string? macPhoi, string? mac, string? kichThuoc, bool excludeLocked = false, string? maVatTu = null)
            => Ok(await _service.GetAllAsync(macPhoi, mac, kichThuoc, excludeLocked: excludeLocked, maVatTu: maVatTu));

        [HttpGet("search")]
        public async Task<IActionResult> Search(string? searchKey, string? mac, string? kichThuoc, int? isXacNhan, string? maVatTu, int page = 1, int pageSize = 30)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 30;
            if (pageSize > 200) pageSize = 200;

            var rows = await _service.GetAllAsync(searchKey, mac, kichThuoc, isXacNhan, maVatTu: maVatTu);
            var ordered = rows.OrderBy(x => x.MacPhoi).ThenBy(x => x.KichThuoc).ToList();

            var totalCount = ordered.Count;
            var data = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new { x.Id, x.MacPhoi, x.DonTrong, x.Mac, x.KichThuoc, x.IsXacNhan, x.MaVatTu, x.TenVatTu, x.IsSync, x.IsLock });

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
        public async Task<IActionResult> Create([FromBody] DonTrongPhoi model)
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
        public async Task<IActionResult> Update(int id, [FromBody] DonTrongPhoi model)
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }

        [HttpGet("export-excel")]
        public async Task<IActionResult> ExportExcel()
        {
            var bytes = await _service.ExportExcelAsync();
            var fileName = $"DonTrongPhoi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPut("{id}/toggle-lock")]
        public async Task<IActionResult> ToggleLock(int id)
        {
            var ok = await _service.ToggleLockAsync(id);
            return ok ? NoContent() : NotFound();
        }

        [HttpPost("sync-ma-vat-tu")]
        public async Task<IActionResult> SyncMaVatTu()
        {
            try
            {
                await _service.SyncMaVatTuAsync();
                return Ok(new { message = "Đồng bộ mã vật tư thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Lỗi đồng bộ: {ex.Message}" });
            }
        }

        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn file Excel." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx" && ext != ".xls")
                return BadRequest(new { message = "Chỉ hỗ trợ file Excel (.xlsx, .xls)." });

            try
            {
                var result = await _service.ImportExcelAsync(file);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Lỗi đọc file: {ex.Message}" });
            }
        }
    }
}
