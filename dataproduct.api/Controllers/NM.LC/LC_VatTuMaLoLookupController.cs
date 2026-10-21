using dataproduct.api.Services.NM.LC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NM.LC
{
    [ApiController]
    [Route("api/LC_VatTuMaLoLookup")]
    public class LC_VatTuMaLoLookupController : ControllerBase
    {
        private readonly LC_VatTuMaLoLookupService _service;

        public LC_VatTuMaLoLookupController(LC_VatTuMaLoLookupService service)
        {
            _service = service;
        }

        [HttpGet("vattu-search")]
        public async Task<IActionResult> SearchVatTu([FromQuery] string? searchKey)
        {
            return Ok(await _service.SearchVatTuAsync(searchKey));
        }

        [HttpGet("malo-by-vattu")]
        public async Task<IActionResult> GetMaLoByVatTu([FromQuery] int idVatTu)
        {
            if (idVatTu <= 0) return BadRequest("idVatTu không hợp lệ");
            return Ok(await _service.GetMaLoByVatTuAsync(idVatTu));
        }

        // POST vì body là list id — tránh N+1 khi load phiếu đã có sẵn nhiều dòng đã chọn than.
        [HttpPost("malo-by-vattu-batch")]
        public async Task<IActionResult> GetMaLoByVatTuBatch([FromBody] List<int> idVatTuList)
        {
            if (idVatTuList == null || idVatTuList.Count == 0) return Ok(new List<object>());
            return Ok(await _service.GetMaLoByVatTuListAsync(idVatTuList));
        }
    }
}
