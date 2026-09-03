using dataproduct.api.DTOs;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class STD_NXT_HRC1Controller : ControllerBase
    {
        private readonly STD_NXT_HRC1Service _service;
        public STD_NXT_HRC1Controller(STD_NXT_HRC1Service service)
        {
            _service = service;
        }

        [HttpPost("upsert")]
        public async Task<IActionResult> Upsert([FromBody] STD_NXT_HRC1_UpsertDto entity)
        {
            try
            {
                var result = await _service.UpsertAsync(entity);
                return Ok(result);

            }
            catch (ApplicationException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }

        }

        [HttpGet("get-by-phieu-id/{phieuId}")]
        public async Task<IActionResult> GetByBm(Guid phieuId)
        {
            try
            {
                var result = await _service.GetByPhieuIdAsync(phieuId);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("phan-bo")]
        public async Task<IActionResult> PhanBo([FromBody] STD_NXT_HRC1_PhanBoDto entity)
        {
            try
            {
                var result = await _service.PhanBoAsync(entity);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("thu-hoi-phan-bo")]
        public async Task<IActionResult> ThuHoiPhanBo([FromBody] STD_NXT_HRC1_PhanBoDto entity)
        {
            try
            {
                var result = await _service.ThuHoiPhanBoAsync(entity);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("khong-phan-bo")]
        public async Task<IActionResult> KhongPhanBo([FromBody] STD_NXT_HRC1_KhongPhanBoDto entity)
        {
            try
            {
                var result = await _service.KhongPhanBoAsync(entity);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("search-nhapxuatton")]
        public async Task<IActionResult> SearchNhapXuatTon([FromBody] STD_NXT_HRC1_NhapXuatTonSearchRequest request)
        {
            try
            {
                var result = await _service.GetNhapXuatTonAsync(request);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("related-phieu-statuses")]
        public async Task<IActionResult> GetRelatedPhieuStatuses([FromBody] STD_NXT_RelatedPhieuStatusRequest request)
        {
            try
            {
                var result = await _service.GetRelatedPhieuStatusesAsync(request);
                return Ok(new { data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
