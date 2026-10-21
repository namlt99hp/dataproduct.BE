using dataproduct.api.Business;
using dataproduct.api.DTOs;
using dataproduct.api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace dataproduct.api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhieusController : ControllerBase
    {
        private readonly PhieuService _service;

        public PhieusController(PhieuService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PhieuDto>>> GetAll(string? MaBM, int? NguoiTaoID, int? NguoiDuyetID, int? isCheckDuyet)
            => Ok(await _service.GetAllAsync(MaBM, NguoiTaoID, NguoiDuyetID, isCheckDuyet));

        [HttpGet("{id}")]
        public async Task<ActionResult<PhieuDto>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<BmPhieu>> Create([FromBody] JsonElement formData)
        {
            try
            {
                var created = await _service.CreateAsync(formData);
                if (created == null)
                {
                    return BadRequest(new { message = "Tạo phiếu thất bại (CreateAsync trả về null)" });
                }
                return CreatedAtAction(nameof(GetById), new { id = created.Idphieu }, created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("auto-create-phieu")]
        public async Task<ActionResult<BmPhieu>> AutoCreatePhieu([FromBody] JsonElement formData)
        {
            try
            {
                var created = await _service.CreateAsync(formData);
                if (created == null)
                {
                    return BadRequest(new { message = "Tạo phiếu thất bại (CreateAsync trả về null)" });
                }
                return Ok(new
                {
                    success = true,
                    id = created.Idphieu
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] JsonElement formData)
        {
            try
            {
                var (phieu, warnings) = await _service.UpdateAsync(id, formData);
                if (phieu == null) return NotFound();
                return Ok(new { success = true, warnings });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Cập nhật chỉ dữ liệu bảng mà không kiểm tra ràng buộc tình trạng phiếu
        /// Sử dụng cho phép cập nhật khi phiếu ở trạng thái HoanThanh
        /// </summary>
        [HttpPut("{id}/update-table-data")]
        public async Task<IActionResult> UpdateTableDataOnly(Guid id, [FromBody] JsonElement formData)
        {
            try
            {
                var (phieu, warnings) = await _service.UpdateTableDataOnlyAsync(id, formData);
                if (phieu == null) return NotFound();
                return Ok(new { success = true, warnings });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }

        [HttpPost("{id}/clone")]
        public async Task<ActionResult<BmPhieu>> Clone(Guid id, [FromBody] JsonElement formData)
        {
            try
            {
                var cloned = await _service.CloneAsync(id, formData);
                return cloned != null ? Ok(cloned) : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/status")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request)
        {
            try
            {
                var ok = await _service.ChangeStatusAsync(id, request.Status, request.IdUser);
                return ok ? NoContent() : NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("exist")]
        public async Task<IActionResult> CheckExistSoPhieu([FromQuery] string maBm,
        [FromQuery] DateOnly ngaySX,
        [FromQuery] int ca,
        [FromQuery] int? scope,
        [FromQuery] int? mayduc)
        {
            var exists = await _service.CheckExistsAsync(maBm, ngaySX, ca, scope, mayduc);
            return Ok(new { exists });
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] SearchPhieuRequest request)
        {
            try
            {
                var result = await _service.SearchWithPagingAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("search-by-user")]
        public async Task<IActionResult> SearchByUser([FromBody] SearchPhieuByUserRequest request)
        {
            try
            {
                var result = await _service.SearchWithPagingByUserAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("so-phieu")]
        public async Task<IActionResult> GetSoPhieu([FromQuery] string maBm, [FromQuery] DateOnly? ngaySX, [FromQuery] int? ca)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(maBm))
                    return BadRequest(new { message = "Thiếu tham số maBm" });

                var soPhieus = await _service.GetSoPhieuAsync(maBm, ngaySX, ca);
                return Ok(new
                {
                    success = true,
                    data = soPhieus,
                    count = soPhieus != null ? soPhieus.Max() : 100
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("{id:guid}/export-pdf")]
        public async Task<IActionResult> ExportPdf(Guid id, [FromQuery] List<string>? filters = null)
        {
            try
            {
                var file = await _service.ExportPdfDynamicAsync(id, filters);
                return File(file.Content, file.ContentType, file.FileName);
            }
            catch (NotSupportedException ex) { return StatusCode(501, ex.Message); }
            catch (Exception ex)             { return StatusCode(500, ex.Message); }
        }

        [HttpGet("{id:guid}/export-excel-detail")]
        public async Task<IActionResult> ExportExcelDetail(Guid id)
        {
            try
            {
                var file = await _service.ExportDetailExcelDynamicAsync(id);
                return File(file.Content, file.ContentType, file.FileName);
            }
            catch (NotSupportedException ex) { return StatusCode(501, ex.Message); }
            catch (Exception ex)             { return StatusCode(500, ex.Message); }
        }

        [HttpGet("export-excel-tonghop")]
        public async Task<IActionResult> ExportExcelTongHop([FromQuery] string maBm, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate)
        {
            var file = await _service.ExportTongHopExcelDynamicAsync(maBm, fromDate, toDate);
            return File(file.Content, file.ContentType, file.FileName);
        }

        [HttpGet("{id:guid}/export-excel")]
        public async Task<IActionResult> ExportExcelPhieu(Guid id)
        {
            var file = await _service.ExportExcelDynamicPhieuAsync(id);
            return File(file.Content, file.ContentType, file.FileName);
        }


        [HttpPut("{id}/status-extended")]
        public async Task<IActionResult> UpdateStatusExtended(Guid id, [FromBody] UpdatePhieuStatusRequest request)
        {
            try
            {
                var ok = await _service.UpdateStatusExtendedAsync(id, request.Status, request.IsLock, request.IsDelete);
                return ok ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("chot-nhieu-phieu")]
        public async Task<IActionResult> ChotNhieuPhieu([FromBody] ChotNhieuPhieuRequest request)
        {
            try
            {
                await _service.ChotNhieuPhieuAsync(request.IdPhieus, request.IdUser, request.Status);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpPost("check-nhieu-phieu")]
        public async Task<IActionResult> CheckNhieuPhieu([FromBody] CheckNhieuPhieuRequest request)
        {
            try
            {
                await _service.CheckNhieuPhieuAsync(request.IdPhieus, request.IsCheck);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("hrc2-std-nxt/status")]
        public async Task<IActionResult> GetStatusHRC2StdNxt([FromQuery] DateOnly ngaySX, [FromQuery] int ca)
        {
            var status = await _service.GetStatusHRC2_STD_NXT(ngaySX, ca);
            return Ok(new { tinhTrang = status });
        }

        [HttpPut("{id}/sync-nguoi-tao")]
        public async Task<IActionResult> SyncNguoiTao(Guid id, [FromBody] int? NguoiTaoID)
        {
            try
            {
                var result = await _service.UpdateNguoiTaoAsync(id, NguoiTaoID);
                if (result == null)
                {
                    return BadRequest(new { success = false, message = "Đồng bộ người tạo thất bại. Kiểm tra dữ liệu đầu vào." });
                }
                return Ok(new { success = true, message = "Đồng bộ người tạo thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }
        [HttpGet("DsLoCao")]
        public async Task<IActionResult> GetDsLoCao()
        {
            var data = await _service.GetAllLoCaoAsync();
            return Ok(data);
        }

        /// <summary>
        /// Reset phiếu về trạng thái "Đang lưu" (TinhTrang = 0)
        /// Sử dụng khi cần đưa phiếu trở lại trạng thái chỉnh sửa
        /// </summary>
        [HttpPut("{id}/reset")]
        public async Task<IActionResult> ResetPhieu(Guid id)
        {
            try
            {
                var result = await _service.ResetPhieuAsync(id);
                if (result == null)
                    return NotFound(new { success = false, message = "Không tìm thấy phiếu" });

                return Ok(new
                {
                    success = true,
                    message = "Reset phiếu thành công",
                    data = result,
                    tinhTrang = result.TinhTrang
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = ex.Message });
            }
        }
    }
}


public class ChangeStatusRequest
{
    public int Status { get; set; }
    public int? IdUser { get; set; }
}

public class UpdatePhieuStatusRequest
{
    public int? Status { get; set; }
    public int? IsLock { get; set; }
    public int? IsDelete { get; set; }
}

public class ChotNhieuPhieuRequest
{
    public List<Guid> IdPhieus { get; set; } = new();
    public int? IdUser { get; set; }
    public int Status { get; set; }
}

public class CheckNhieuPhieuRequest
{
    public List<Guid> IdPhieus { get; set; } = new();
    public int IsCheck { get; set; }
}

