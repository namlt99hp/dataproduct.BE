using dataproduct.api.DTOs;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers
{
    [Route("api/hrc1-slab")]
    [ApiController]
    public class Hrc1SlabController : ControllerBase
    {
        private readonly Hrc1SlabService _svc;

        public Hrc1SlabController(Hrc1SlabService svc)
        {
            _svc = svc;
        }

        // ── Đọc dữ liệu ─────────────────────────────────────────────────────

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] Hrc1SlabSearchRequest request)
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

        [HttpGet("phieu-bbsl")]
        public async Task<IActionResult> GetPhieuBBSL(
            [FromQuery] string? kip,
            [FromQuery] int? ca)
        {
            var data = await _svc.GetPhieuBBSLAsync(kip, ca);
            return Ok(data);
        }

        [HttpGet("ruot-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetRuotPhieu(Guid idPhieu)
        {
            var data = await _svc.GetRuotPhieuAsync(idPhieu);
            return Ok(data);
        }

        [HttpGet("slabs-by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetSlabsByPhieu(Guid idPhieu)
        {
            var data = await _svc.GetSlabsByPhieuAsync(idPhieu);
            return Ok(data);
        }

        // ── Chuyển phôi sang ca kề ──────────────────────────────────────────

        [HttpPost("chuyen-phoi")]
        public async Task<IActionResult> ChuyenPhoi([FromBody] Hrc1ChuyenPhoiRequest request)
        {
            if (request.Huong != "truoc" && request.Huong != "sau")
                return BadRequest("Huong phải là 'truoc' hoặc 'sau'.");
            if (request.IdSlabs == null || request.IdSlabs.Count == 0)
                return BadRequest("Phải có ít nhất 1 slab.");

            try
            {
                var affected = await _svc.ChuyenPhoiAsync(request);
                return Ok(new WorkflowResult
                {
                    Success = true,
                    Message = $"Đã chuyển {affected} slab thành công.",
                    AffectedRows = affected
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ── Cán Tấm / PKH Workflow ───────────────────────────────────────────

        [HttpPost("xac-nhan")]
        public async Task<IActionResult> XacNhan([FromBody] XacNhanRequest request)
        {
            if (request.IdSlabs.Count == 0)
                return BadRequest("Danh sách slab không được rỗng.");
            if (request.LoaiXacNhan != "Duc" && request.LoaiXacNhan != "Kho")
                return BadRequest("LoaiXacNhan phải là 'Duc' hoặc 'Kho'.");

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
            if (request.LoaiXacNhan != "Duc" && request.LoaiXacNhan != "Kho")
                return BadRequest("LoaiXacNhan phải là 'Duc' hoặc 'Kho'.");

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

        // ── Sync từ TSC API ──────────────────────────────────────────────────

        [HttpPost("sync")]
        public async Task<IActionResult> Sync([FromBody] Hrc1SlabSyncRequest request)
        {
            var result = await _svc.SyncAsync(request.NgaySX, request.CaSX);
            return Ok(result);
        }

        // ── Cập nhật MacThep từ HRC1_MeThep ─────────────────────────────────

        [HttpPost("fill-mac-thep")]
        public async Task<IActionResult> FillMacThep()
        {
            var updated = await _svc.FillMacThepAsync();
            return Ok(new WorkflowResult
            {
                Success = true,
                Message = updated > 0
                    ? $"Đã cập nhật Mác thép cho {updated} slab."
                    : "Không có slab nào cần cập nhật (tất cả đã có Mác thép, hoặc HRC1_MeThep chưa có dữ liệu).",
                AffectedRows = updated
            });
        }

        // ── Cập nhật GhiChu / MaVatTu per slab ──────────────────────────────

        [HttpPatch("{id:int}")]
        public async Task<IActionResult> UpdateSlab(int id, [FromBody] Hrc1SlabUpdateRequest req)
        {
            try
            {
                await _svc.UpdateSlabAsync(id, req);
                return Ok(new WorkflowResult { Success = true, Message = "Đã cập nhật.", AffectedRows = 1 });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPatch("bulk-ma-vat-tu")]
        public async Task<IActionResult> BulkUpdateMaVatTu([FromBody] Hrc1BulkUpdateMaVatTuRequest req)
        {
            var updated = await _svc.BulkUpdateMaVatTuAsync(req);
            return Ok(new WorkflowResult { Success = true, Message = $"Đã cập nhật {updated} slab.", AffectedRows = updated });
        }

        // ── Tổng hợp ghi chú ─────────────────────────────────────────────────

        [HttpGet("tonghop-ghi-chu/{idPhieu:guid}")]
        public async Task<IActionResult> GetTongHopGhiChu(Guid idPhieu)
        {
            var data = await _svc.GetTongHopGhiChuAsync(idPhieu);
            return Ok(data);
        }

        [HttpPost("tonghop-ghi-chu")]
        public async Task<IActionResult> SaveTongHopGhiChu([FromBody] Hrc1SaveTongHopGhiChuRequest req)
        {
            await _svc.SaveTongHopGhiChuAsync(req);
            return Ok(new WorkflowResult { Success = true, Message = "Đã lưu ghi chú.", AffectedRows = 1 });
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
