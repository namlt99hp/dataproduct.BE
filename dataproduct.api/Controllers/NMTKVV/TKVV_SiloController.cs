using dataproduct.api.DTOs.NMTKVV_Dto;
using dataproduct.api.Services.NMTKVV;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NMTKVV
{
    [ApiController]
    [Route("api/[controller]")]
    public class TKVV_SiloController : ControllerBase
    {
        private readonly TKVV_SiloService _service;
        private readonly TKVV_NvlBbgnMappingService _nvlBbgnMappingService;

        public TKVV_SiloController(TKVV_SiloService service, TKVV_NvlBbgnMappingService nvlBbgnMappingService)
        {
            _service = service;
            _nvlBbgnMappingService = nvlBbgnMappingService;
        }

        // ─── Silo ──────────────────────────────────────────────────────────────

        [HttpGet("silo")]
        public async Task<IActionResult> GetSiloList([FromQuery] string? scope)
        {
            var result = await _service.GetSiloListAsync(scope);
            return Ok(result);
        }

        [HttpGet("silo/{id}")]
        public async Task<IActionResult> GetSiloById(int id)
        {
            var entity = await _service.GetSiloByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        [HttpPost("silo")]
        public async Task<IActionResult> AddSilo([FromBody] CreateTKVVSiloDto dto)
        {
            var result = await _service.AddSiloAsync(dto);
            return Ok(result);
        }

        [HttpPut("silo/{id}")]
        public async Task<IActionResult> UpdateSilo(int id, [FromBody] UpdateTKVVSiloDto dto)
        {
            var result = await _service.UpdateSiloAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("silo/{id}")]
        public async Task<IActionResult> DeleteSilo(int id)
        {
            var ok = await _service.DeleteSiloAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }

        // ─── NVL ↔ Silo Mapping ────────────────────────────────────────────────

        [HttpGet("nvl-silo-mapping")]
        public async Task<IActionResult> GetNvlSiloMappingList([FromQuery] string? maBM, [FromQuery] string? scope, [FromQuery] int? nvlId, [FromQuery] int? siloId, [FromQuery] DateOnly? ngaySX, [FromQuery] int? ca)
        {
            var result = await _service.GetNvlSiloMappingListAsync(maBM, scope, nvlId, siloId, ngaySX, ca);
            return Ok(result);
        }

        [HttpGet("nvl-silo-mapping/ensure")]
        public async Task<IActionResult> EnsureMapping(
            [FromQuery] DateOnly ngaySX,
            [FromQuery] int ca,
            [FromQuery] string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
                return BadRequest(new { message = "Thiếu scope." });
            var result = await _service.EnsureMappingAsync(ngaySX, ca, scope);
            return Ok(result.Select(m => new TKVVNvlSiloMappingDto
            {
                Id              = m.ID,
                Ca              = m.Ca,
                NgaySX          = m.NgaySX,
                NguyenVatLieuID = m.NguyenVatLieuID,
                SiloID          = m.SiloID,
                ThuTu           = m.ThuTu,
                Scope           = m.Scope,
                MaBM            = m.MaBM,
                GhiChu          = m.GhiChu,
                TrangThai       = m.TrangThai,
                NgayCapNhat     = m.NgayCapNhat,
            }));
        }

        [HttpGet("nvl-silo-mapping/nearest")]
        public async Task<IActionResult> GetNearestMapping(
            [FromQuery] string? maBM,
            [FromQuery] string scope,
            [FromQuery] DateOnly beforeDate)
        {
            if (string.IsNullOrWhiteSpace(scope))
                return BadRequest(new { message = "Thiếu scope." });
            var result = await _service.GetNearestMappingAsync(maBM, scope, beforeDate);
            return Ok(result);
        }

        [HttpPost("nvl-silo-mapping/batch")]
        public async Task<IActionResult> BatchCreateNvlSiloMapping([FromBody] BatchCreateNvlSiloMappingDto dto)
        {
            if (dto.Rows == null || dto.Rows.Count == 0)
                return BadRequest(new { message = "Danh sách mapping trống." });
            var count = await _service.BatchCreateNvlSiloMappingAsync(dto);
            return Ok(new { count });
        }

        [HttpGet("nvl-silo-mapping/{id}")]
        public async Task<IActionResult> GetNvlSiloMappingById(int id)
        {
            var entity = await _service.GetNvlSiloMappingByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        [HttpPost("nvl-silo-mapping")]
        public async Task<IActionResult> AddNvlSiloMapping([FromBody] CreateTKVVNvlSiloMappingDto dto)
        {
            var result = await _service.AddNvlSiloMappingAsync(dto);
            if (result == null) return BadRequest(new { message = "NVL hoặc Silo không hợp lệ, hoặc scope giữa NVL và Silo không khớp." });
            return Ok(result);
        }

        [HttpPut("nvl-silo-mapping/{id}")]
        public async Task<IActionResult> UpdateNvlSiloMapping(int id, [FromBody] UpdateTKVVNvlSiloMappingDto dto)
        {
            var result = await _service.UpdateNvlSiloMappingAsync(id, dto);
            if (result == null) return BadRequest(new { message = "NVL hoặc Silo không hợp lệ, hoặc scope giữa NVL và Silo không khớp." });
            return Ok(result);
        }

        [HttpDelete("nvl-silo-mapping/{id}")]
        public async Task<IActionResult> DeleteNvlSiloMapping(int id)
        {
            var ok = await _service.DeleteNvlSiloMappingAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }

        // ─── Silo ↔ Tag EMS Mapping ────────────────────────────────────────────

        [HttpGet("silo-tag-mapping")]
        public async Task<IActionResult> GetSiloTagMappingList([FromQuery] int? siloId, [FromQuery] string? maBM)
        {
            var result = await _service.GetSiloTagMappingListAsync(siloId, maBM);
            return Ok(result);
        }

        [HttpGet("silo-tag-mapping/{id}")]
        public async Task<IActionResult> GetSiloTagMappingById(int id)
        {
            var entity = await _service.GetSiloTagMappingByIdAsync(id);
            if (entity == null) return NotFound();
            return Ok(entity);
        }

        [HttpPost("silo-tag-mapping")]
        public async Task<IActionResult> AddSiloTagMapping([FromBody] CreateTKVVSiloTagMappingDto dto)
        {
            var result = await _service.AddSiloTagMappingAsync(dto);
            return Ok(result);
        }

        [HttpPut("silo-tag-mapping/{id}")]
        public async Task<IActionResult> UpdateSiloTagMapping(int id, [FromBody] UpdateTKVVSiloTagMappingDto dto)
        {
            var result = await _service.UpdateSiloTagMappingAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("silo-tag-mapping/{id}")]
        public async Task<IActionResult> DeleteSiloTagMapping(int id)
        {
            var ok = await _service.DeleteSiloTagMappingAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }

        // ─── Tra cứu Vật tư SAP (PRODUCTDATA.Tbl_VatTu) ──────────────────────────

        [HttpGet("vattu-search")]
        public async Task<IActionResult> SearchVatTu(
            [FromQuery] string? searchKey,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            return Ok(await _nvlBbgnMappingService.SearchVatTuAsync(searchKey, page, pageSize));
        }

        // ─── NVL ↔ Vật tư BBGN Mapping ────────────────────────────────────────

        [HttpGet("nvl-vattu-mapping")]
        public async Task<IActionResult> GetNvlVatTuMappingList([FromQuery] int? tkvvNvlId)
        {
            return Ok(await _nvlBbgnMappingService.GetListAsync(tkvvNvlId));
        }

        [HttpPost("nvl-vattu-mapping")]
        public async Task<IActionResult> AddNvlVatTuMapping([FromBody] CreateTKVVNvlBbgnMappingDto dto)
        {
            var result = await _nvlBbgnMappingService.AddAsync(dto);
            return Ok(result);
        }

        [HttpPut("nvl-vattu-mapping/{id}")]
        public async Task<IActionResult> UpdateNvlVatTuMapping(int id, [FromBody] UpdateTKVVNvlBbgnMappingDto dto)
        {
            var result = await _nvlBbgnMappingService.UpdateAsync(id, dto);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("nvl-vattu-mapping/{id}")]
        public async Task<IActionResult> DeleteNvlVatTuMapping(int id)
        {
            var ok = await _nvlBbgnMappingService.DeleteAsync(id);
            if (!ok) return NotFound();
            return Ok();
        }
    }
}
