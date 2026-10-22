using dataproduct.api.DTOs.NMTKVV_Dto;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Services.NMTKVV;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Controllers.NMTKVV
{
    [Route("api/[controller]")]
    [ApiController]
    public class TKVV_BCSL_ChiPhiController : ControllerBase
    {
        private readonly TKVV_BCSL_ChiPhiService _service;
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterContext;

        public TKVV_BCSL_ChiPhiController(TKVV_BCSL_ChiPhiService service, ProductFormContext context, ProductDataMasterDbContext masterContext)
        {
            _service = service;
            _context = context;
            _masterContext = masterContext;
        }

        [HttpGet("scope-xuong-mapping")]
        public async Task<IActionResult> GetScopeXuongMapping([FromQuery] int scope)
        {
            var mapping = await _context.TKVV_Scope_Xuong_Mapping
                .FirstOrDefaultAsync(m => m.Scope == scope);
            if (mapping == null)
                return NotFound(new { message = $"Không tìm thấy mapping cho scope {scope}" });

            string? tenVatTu = null;
            if (mapping.ID_NVL_BBGN_ThanhPham.HasValue)
            {
                var vatTu = await _masterContext.Tbl_VatTu
                    .FirstOrDefaultAsync(v => v.ID_VatTu == mapping.ID_NVL_BBGN_ThanhPham.Value);
                tenVatTu = vatTu?.TenVatTu;
            }

            return Ok(new
            {
                id = mapping.ID,
                scope = mapping.Scope,
                maXuong = mapping.MaXuong,
                tenXuong = mapping.TenXuong,
                idXuongBBGN = mapping.ID_Xuong_BBGN,
                idNvlBbgnThanhPham = mapping.ID_NVL_BBGN_ThanhPham,
                tenVatTu,
            });
        }

        // Đổ giá trị NVL tự động từ EMS (SP_TKVV_GetGiaTriNVL_Auto) vào bảng khi
        // nhấn "Làm mới dữ liệu" trên phiếu Báo cáo Sản lượng & Chi phí.
        // Trả danh sách NVL kèm GiaTri (tổng cân băng tải EMS) theo maBM + scope + ngày + ca.
        // FE map: TenNVL → nguyenLieu, GiaTri → klAm.
        [HttpGet("get-dulieu-can")]
        public async Task<IActionResult> GetDuLieuCan(
            [FromQuery] DateTime ngay,
            [FromQuery] int ca,
            [FromQuery] string maBM,
            [FromQuery] string loaiDuLieu = "SANLUONG",
            [FromQuery] int scope = 1)
        {
            try
            {
                if (ca != 1 && ca != 2)
                    return BadRequest(new { message = "ca chỉ nhận 1 hoặc 2." });
                if (scope < 1 || scope > 6)
                    return BadRequest(new { message = "scope phải từ 1 đến 6." });
                if (string.IsNullOrWhiteSpace(maBM))
                    return BadRequest(new { message = "Thiếu tham số maBM." });
                return Ok(await _service.GetDuLieuCanAsync(ngay, ca, maBM, loaiDuLieu, scope));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [HttpGet("get-giatri-nvl-auto")]
        public async Task<IActionResult> GetGiaTriNVLAuto(
            [FromQuery] DateTime ngay,
            [FromQuery] int ca,
            [FromQuery] int scope,
            [FromQuery] string maBM)
        {
            try
            {
                if (ca != 1 && ca != 2)
                    return BadRequest(new { message = "ca chỉ nhận giá trị 1 hoặc 2." });
                if (string.IsNullOrWhiteSpace(maBM))
                    return BadRequest(new { message = "Thiếu tham số maBM." });
                if (scope < 1 || scope > 6)
                    return BadRequest(new { message = "scope phải từ 1 đến 6." });
                return Ok(await _service.GetGiaTriNVLAutoAsync(ngay, ca, scope, maBM));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        // Tải dữ liệu cân từ EMS cho 1 Ca, upsert vào TKVV_BaoCaoSanLuongChiPhi,
        // trả về dữ liệu đã lưu kèm KLAmAuto và IsAdjusted.
        [HttpPost("load-dulieu")]
        public async Task<IActionResult> LoadDuLieu([FromBody] LoadDuLieuCanRequestDto request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.MaBM))
                    return BadRequest(new { message = "Thiếu tham số maBM." });
                if (request.Scope < 1 || request.Scope > 6)
                    return BadRequest(new { message = "scope phải từ 1 đến 6." });
                if (request.CaSX != 1 && request.CaSX != 2)
                    return BadRequest(new { message = "caSX chỉ nhận giá trị 1 hoặc 2." });
                var result = await _service.LoadAndSaveAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        // Lấy dữ liệu đã lưu theo ngày, scope int 1-6 và ca sx (dùng khi load lại phiếu).
        [HttpGet("get-baocao-data")]
        public async Task<IActionResult> GetBaoCaoData(
            [FromQuery] DateOnly ngaySX,
            [FromQuery] string maBM,
            [FromQuery] int scope,
            [FromQuery] int? caSX = null)
        {
            try
            {
                if (scope < 1 || scope > 6)
                    return BadRequest(new { message = "scope phải từ 1 đến 6." });
                if (caSX.HasValue && caSX != 1 && caSX != 2)
                    return BadRequest(new { message = "caSX chỉ nhận giá trị 1 hoặc 2." });
                var result = await _service.GetBaoCaoDataAsync(ngaySX, maBM, scope, caSX);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        // Lưu giá trị người dùng nhập (KLAm, DoAm, ThanhPham...) — backend tự xác định IsAdjusted.
        [HttpPost("save-phieu-rows")]
        public async Task<IActionResult> SavePhieuRows([FromBody] SaveBcSlPhieuRequestDto request)
        {
            try
            {
                if (request.Rows == null || request.Rows.Count == 0)
                    return BadRequest(new { message = "Danh sách dòng trống." });
                await _service.SavePhieuRowsAsync(request);
                return Ok(new { message = "Đã lưu." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [HttpPost("{id}/clear-bbgn")]
        public async Task<IActionResult> ClearBbgn(long id)
        {
            var row = await _context.TKVV_BaoCaoSanLuongChiPhi.FindAsync(id);
            if (row == null) return NotFound();
            row.ID_CT_BBGN = null;
            row.ThanhPhamL1 = null;
            row.ThanhPhamL2 = null;
            row.ThanhPhamL3 = null;
            row.ThanhPham_Note = null;
            row.IsAdjusted = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("refresh-bbgn-batch")]
        public async Task<IActionResult> RefreshBbgnBatch([FromBody] RefreshBbgnBatchRequestDto request)
        {
            try
            {
                if (request.PhieuIds == null || request.PhieuIds.Count == 0)
                    return BadRequest(new { message = "Danh sách phiếu trống." });

                int totalUpdated = 0;
                foreach (var phieuId in request.PhieuIds)
                    totalUpdated += await _service.RefreshBbgnAsync(phieuId, request.CurrentUserId);

                return Ok(new { message = $"Đã làm mới dữ liệu BBGN cho {request.PhieuIds.Count} phiếu." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
            }
        }
    }
}
