using dataproduct.api.DTOs;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/hrc2-slab")]
    [ApiController]
    public class Hrc2SlabController : ControllerBase
    {
        private readonly Hrc2SlabWorkflowService _svc;

        public Hrc2SlabController(Hrc2SlabWorkflowService svc)
        {
            _svc = svc;
        }

        // ── Đọc dữ liệu ─────────────────────────────────────────────────────

        /// <summary>Danh sách slab kèm trạng thái workflow</summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] HrcSlabSearchRequest request)
        {
            var (data, total) = await _svc.SearchAsync(request);
            return Ok(new
            {
                data,
                totalCount = total,
                page = request.Page,
                pageSize = request.PageSize
            });
        }

        /// <summary>Tổng hợp GROUP BY 5 điều kiện</summary>
        [HttpGet("tong-hop")]
        public async Task<IActionResult> GetTongHop(
            [FromQuery] DateOnly? tuNgay,
            [FromQuery] DateOnly? denNgay,
            [FromQuery] string? ca,
            [FromQuery] string? kip)
        {
            var data = await _svc.GetTongHopAsync(tuNgay, denNgay, ca, kip);
            return Ok(data);
        }

        /// <summary>Danh sách phiếu BBSL chưa chốt (để chọn khi KCS chuyển)</summary>
        [HttpGet("phieu-bbsl")]
        public async Task<IActionResult> GetPhieuBBSL(
            [FromQuery] string? kip,
            [FromQuery] int? ca)
        {
            var data = await _svc.GetPhieuBBSLAsync(kip, ca);
            return Ok(data);
        }

        /// <summary>Ruột phiếu — danh sách slab trong phiếu, GROUP BY 5 điều kiện</summary>
        [HttpGet("ruot-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetRuotPhieu(Guid idPhieu)
        {
            var data = await _svc.GetRuotPhieuAsync(idPhieu);
            return Ok(data);
        }

        /// <summary>Danh sách slab cá nhân thuộc phiếu (đã chuyển KCS)</summary>
        [HttpGet("slabs-by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetSlabsByPhieu(Guid idPhieu)
        {
            var data = await _svc.GetSlabsByPhieuAsync(idPhieu);
            return Ok(data);
        }

        // ── KCS Workflow ─────────────────────────────────────────────────────

        [HttpPost("chuyen-bbsl")]
        public async Task<IActionResult> ChuyenBBSL([FromBody] ChuyenBBSLRequest request)
        {
            if (request.IdSlabs.Count == 0)
                return BadRequest("Danh sách slab không được rỗng.");

            await _svc.ChuyenBBSLAsync(request);
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = $"Đã chuyển {request.IdSlabs.Count} slab lên phiếu.",
                AffectedRows = request.IdSlabs.Count
            });
        }

        [HttpPost("thu-hoi")]
        public async Task<IActionResult> ThuHoi([FromBody] ThuHoiRequest request)
        {
            if (request.IdSlabs.Count == 0)
                return BadRequest("Danh sách slab không được rỗng.");

            await _svc.ThuHoiAsync(request);
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = $"Đã thu hồi {request.IdSlabs.Count} slab.",
                AffectedRows = request.IdSlabs.Count
            });
        }

        // ── Đúc/Kho Workflow ─────────────────────────────────────────────────

        [HttpPost("xac-nhan")]
        public async Task<IActionResult> XacNhan([FromBody] XacNhanRequest request)
        {
            if (request.IdSlabs.Count == 0)
                return BadRequest("Danh sách slab không được rỗng.");
            if (request.LoaiXacNhan != "Duc" && request.LoaiXacNhan != "Kho" && request.LoaiXacNhan != "PKH")
                return BadRequest("LoaiXacNhan phải là 'Duc', 'Kho' hoặc 'PKH'.");

            await _svc.XacNhanAsync(request);
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = $"Đã xác nhận ({request.LoaiXacNhan}) {request.IdSlabs.Count} slab.",
                AffectedRows = request.IdSlabs.Count
            });
        }

        [HttpPost("huy-xac-nhan")]
        public async Task<IActionResult> HuyXacNhan([FromBody] XacNhanRequest request)
        {
            if (request.IdSlabs.Count == 0)
                return BadRequest("Danh sách slab không được rỗng.");
            if (request.LoaiXacNhan != "Duc" && request.LoaiXacNhan != "Kho" && request.LoaiXacNhan != "PKH")
                return BadRequest("LoaiXacNhan phải là 'Duc', 'Kho' hoặc 'PKH'.");

            await _svc.HuyXacNhanAsync(request);
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = $"Đã hủy xác nhận ({request.LoaiXacNhan}) {request.IdSlabs.Count} slab.",
                AffectedRows = request.IdSlabs.Count
            });
        }

        // ── PKH Workflow ─────────────────────────────────────────────────────

        [HttpPost("chot-phieu")]
        public async Task<IActionResult> ChotPhieu([FromBody] ChotPhieuRequest request)
        {
            await _svc.ChotPhieuAsync(request);
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = "Đã chốt phiếu thành công.",
                AffectedRows = 1
            });
        }

        [HttpPost("huy-chot-phieu")]
        public async Task<IActionResult> HuyChotPhieu([FromBody] ChotPhieuRequest request)
        {
            await _svc.HuyChotPhieuAsync(request);
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = "Đã hủy chốt phiếu thành công.",
                AffectedRows = 1
            });
        }

        // ── Sync BKMIS ──────────────────────────────────────────────────────

        [HttpPost("sync")]
        public async Task<IActionResult> Sync([FromBody] Hrc2SlabSyncRequest request)
        {
            var result = await _svc.SyncAsync(request.NgayBatDau, request.NgayKetThuc);
            return Ok(result);
        }

        [HttpGet("sync/status")]
        public async Task<IActionResult> GetSyncStatus()
        {
            var result = await _svc.GetSyncStatusAsync();
            return result == null ? NoContent() : Ok(result);
        }

        // ── Export ────────────────────────────────────────────────────────────

        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel([FromQuery] Guid idPhieu, [FromQuery] string tab = "chitiet")
        {
            var result = tab == "tonghop"
                ? await _svc.ExportTongHopExcelAsync(idPhieu)
                : await _svc.ExportChiTietExcelAsync(idPhieu);
            return File(result.Content, result.ContentType, result.FileName);
        }

        [HttpGet("export/pdf")]
        public async Task<IActionResult> ExportPdf([FromQuery] Guid idPhieu)
        {
            var result = await _svc.ExportTongHopPdfAsync(idPhieu);
            return File(result.Content, result.ContentType, result.FileName);
        }
    }
}
