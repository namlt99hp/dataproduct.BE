using dataproduct.api.DTOs;
using dataproduct.api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace dataproduct.api.Controllers
{
    /// <summary>
    /// HRC1 - Biên bản giao nhận thép lỏng
    /// Base route: /api/hrc1
    /// userId lấy từ header X-User-Id (int)
    /// </summary>
    [Route("api/hrc1")]
    [ApiController]
    public class HRC1_BBGNController : ControllerBase
    {
        private readonly HRC1_BBGNService _svc;
        private readonly BBGN_ThepLongService _bbgnSvc;
        private readonly SyncPhanLoaiService _syncSvc;

        public HRC1_BBGNController(
            HRC1_BBGNService svc,
            BBGN_ThepLongService bbgnSvc,
            SyncPhanLoaiService syncSvc)
        {
            _svc     = svc;
            _bbgnSvc = bbgnSvc;
            _syncSvc = syncSvc;
        }

        // -------------------------------------------------------
        // Phiếu — dùng chung cho cả 3 công đoạn
        // -------------------------------------------------------

        /// <summary>GET /api/hrc1/phieu/{idPhieu}?loSo=&amp;scopePhieu=&amp;idMayDuc=</summary>
        [HttpGet("phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetPhieu(Guid idPhieu,
            [FromQuery] int? loSo = null,
            [FromQuery] int? scopePhieu = null,
            [FromQuery] int? idMayDuc = null)
        {
            try
            {
                var result = await _svc.GetPhieuAsync(idPhieu, loSo, scopePhieu, idMayDuc);
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
            catch (Exception ex)            { return StatusCode(500, ex.Message); }
        }

        // -------------------------------------------------------
        // Lò thổi
        // -------------------------------------------------------

        /// <summary>POST /api/hrc1/phieu/{idPhieu}/sync-lo-thoi — đồng bộ mẻ thổi từ gang lỏng theo lò cụ thể</summary>
        [HttpPost("phieu/{idPhieu:guid}/sync-lo-thoi")]
        public async Task<IActionResult> SyncLoThoi(Guid idPhieu, [FromBody] HRC1_SyncLoThoiRequest req)
        {
            try
            {
                var result = await _svc.SyncMeThoiLoThoiAsync(idPhieu, req.LoSo);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>DELETE /api/hrc1/lo-thoi/me/{meId} — xóa cứng mẻ ghost (user xác nhận thủ công)</summary>
        [HttpDelete("lo-thoi/me/{meId:int}")]
        public async Task<IActionResult> XoaMeGhost(int meId)
        {
            try
            {
                await _svc.XoaMeGhostAsync(meId);
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/lo-thoi/fetch-me — load mẻ thổi từ gang lỏng theo ngày/ca/lò</summary>
        [HttpPost("lo-thoi/fetch-me")]
        public async Task<IActionResult> FetchMeThoi([FromBody] HRC1_FetchMeThoiRequest req)
        {
            try
            {
                var result = await _bbgnSvc.FetchMeThoiHRC1Async(req);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return StatusCode(502, ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>PUT /api/hrc1/lo-thoi/{meId}</summary>
        [HttpPut("lo-thoi/{meId:int}")]
        public async Task<IActionResult> UpdateLoThoi(int meId, [FromBody] JsonElement body)
        {
            try
            {
                await _svc.UpdateMeAsync(meId, body, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/lo-thoi/{meId}/xac-nhan</summary>
        [HttpPost("lo-thoi/{meId:int}/xac-nhan")]
        public async Task<IActionResult> XacNhanLoThoi(int meId)
        {
            try
            {
                await _svc.XacNhanLoThoiAsync(meId, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/lo-thoi/{meId}/bo-xac-nhan</summary>
        [HttpPost("lo-thoi/{meId:int}/bo-xac-nhan")]
        public async Task<IActionResult> BoXacNhanLoThoi(int meId)
        {
            try
            {
                await _svc.BoXacNhanLoThoiAsync(meId, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/lo-thoi/{meId}/lam-moi</summary>
        [HttpPost("lo-thoi/{meId:int}/lam-moi")]
        public async Task<IActionResult> LamMoiLoThoi(int meId)
        {
            try
            {
                await _svc.LamMoiAsync(meId, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/lo-thoi/chuyen-ca-duc — mẻ lên thẳng: chuyển routing mẻ sang phiếu Đúc ca trước/sau</summary>
        [HttpPost("lo-thoi/chuyen-ca-duc")]
        public async Task<IActionResult> ChuyenCaDucLoThoi([FromBody] HRC1_ChuyenCaDucRequest req)
        {
            try
            {
                await _svc.ChuyenCaDucLoThoiAsync(req.MeId, req.Huong, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/lo-thoi/huy-chuyen-ca-duc — mẻ lên thẳng: hủy chuyển ca đúc (dùng chung logic hủy với tinh luyện)</summary>
        [HttpPost("lo-thoi/huy-chuyen-ca-duc")]
        public async Task<IActionResult> HuyChuyenCaDucLoThoi([FromBody] HRC1_ChuyenCaDucRequest req)
        {
            try
            {
                await _svc.HuyChuyenCaDucAsync(req.MeId, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        // -------------------------------------------------------
        // Tinh luyện
        // -------------------------------------------------------

        /// <summary>GET /api/hrc1/tinh-luyen/cho-nhan</summary>
        [HttpGet("tinh-luyen/cho-nhan")]
        public async Task<IActionResult> GetChoNhan()
        {
            try
            {
                var result = await _svc.GetChoNhanAsync();
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>GET /api/hrc1/tinh-luyen/me-cho-nhan — paged + filter theo ngày/ca/mẻ</summary>
        [HttpGet("tinh-luyen/me-cho-nhan")]
        public async Task<IActionResult> GetMeChoNhan([FromQuery] HRC1_GetMeChoNhanQuery q)
        {
            try
            {
                var result = await _svc.GetMeChoNhanPagedAsync(q);
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/tinh-luyen/nhan-me</summary>
        [HttpPost("tinh-luyen/nhan-me")]
        public async Task<IActionResult> NhanMe([FromBody] HRC1_NhanMeRequest req)
        {
            try
            {
                await _svc.NhanMeAsync(req, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>PUT /api/hrc1/tinh-luyen/{mePhanCongId}</summary>
        [HttpPut("tinh-luyen/{mePhanCongId:int}")]
        public async Task<IActionResult> UpdateTinhLuyen(int mePhanCongId, [FromBody] JsonElement body)
        {
            try
            {
                await _svc.UpdateMePhanCongAsync(mePhanCongId, body, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/tinh-luyen/huy-nhan-me</summary>
        [HttpPost("tinh-luyen/huy-nhan-me")]
        public async Task<IActionResult> HuyNhanMe([FromBody] HRC1_HuyNhanMeRequest req)
        {
            try
            {
                await _svc.HuyNhanMeAsync(req, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>GET /api/hrc1/tinh-luyen/search-me?q= — autocomplete tìm mẻ thổi</summary>
        [HttpGet("tinh-luyen/search-me")]
        public async Task<IActionResult> SearchMeThep([FromQuery] string q = "")
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(Array.Empty<object>());
            try
            {
                var result = await _svc.SearchMeThepAsync(q);
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/tinh-luyen/them-me-tay — thêm mẻ tay vào phiếu TL</summary>
        [HttpPost("tinh-luyen/them-me-tay")]
        public async Task<IActionResult> ThemMeTay([FromBody] HRC1_ThemMeTayRequest req)
        {
            try
            {
                var result = await _svc.ThemMeTayAsync(req, LayUserId());
                return Ok(result);
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>DELETE /api/hrc1/tinh-luyen/me-tay/{mePhanCongId} — xóa dòng mẻ thêm tay</summary>
        [HttpDelete("tinh-luyen/me-tay/{mePhanCongId:int}")]
        public async Task<IActionResult> XoaMeTay(int mePhanCongId)
        {
            try
            {
                await _svc.XoaMeTayAsync(mePhanCongId, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/tinh-luyen/chuyen-ca-duc — chuyển routing mẻ sang phiếu Đúc ca trước/sau</summary>
        [HttpPost("tinh-luyen/chuyen-ca-duc")]
        public async Task<IActionResult> ChuyenCaDuc([FromBody] HRC1_ChuyenCaDucRequest req)
        {
            try
            {
                await _svc.ChuyenCaDucAsync(req.MeId, req.Huong, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/tinh-luyen/huy-chuyen-ca-duc</summary>
        [HttpPost("tinh-luyen/huy-chuyen-ca-duc")]
        public async Task<IActionResult> HuyChuyenCaDuc([FromBody] HRC1_ChuyenCaDucRequest req)
        {
            try
            {
                await _svc.HuyChuyenCaDucAsync(req.MeId, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/tinh-luyen/them-dong</summary>
        [HttpPost("tinh-luyen/them-dong")]
        public async Task<IActionResult> ThemDong([FromBody] HRC1_ThemDongTLRequest req)
        {
            try
            {
                await _svc.ThemDongAsync(req, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        // -------------------------------------------------------
        // Máy đúc
        // -------------------------------------------------------

        /// <summary>POST /api/hrc1/duc/xac-nhan — trả về ThanhCong/ThatBai (mẻ thiếu dữ liệu bị re-check
        /// sẽ nằm trong ThatBai kèm lý do, không NoContent như trước để FE biết mà cảnh báo + reload)</summary>
        [HttpPost("duc/xac-nhan")]
        public async Task<IActionResult> XacNhanDuc([FromBody] HRC1_DucXacNhanRequest req)
        {
            try
            {
                var result = await _svc.XacNhanDucAsync(req, LayUserId());
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/bo-xac-nhan</summary>
        [HttpPost("duc/bo-xac-nhan")]
        public async Task<IActionResult> BoXacNhanDuc([FromBody] HRC1_DucBoXacNhanRequest req)
        {
            try
            {
                await _svc.BoXacNhanDucAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/xac-nhan-pcn — chỉ áp dụng mẻ IsThuNghiem=true</summary>
        [HttpPost("duc/xac-nhan-pcn")]
        public async Task<IActionResult> XacNhanPCN([FromBody] HRC1_DucXacNhanPCNRequest req)
        {
            try
            {
                await _svc.XacNhanPCNAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/khong-xac-nhan-pcn — chỉ áp dụng mẻ IsThuNghiem=true</summary>
        [HttpPost("duc/khong-xac-nhan-pcn")]
        public async Task<IActionResult> KhongXacNhanPCN([FromBody] HRC1_DucKhongXacNhanPCNRequest req)
        {
            try
            {
                await _svc.KhongXacNhanPCNAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/reset-xac-nhan-pcn — reset TrangThaiPCN về null</summary>
        [HttpPost("duc/reset-xac-nhan-pcn")]
        public async Task<IActionResult> ResetXacNhanPCN([FromBody] HRC1_DucResetXacNhanPCNRequest req)
        {
            try
            {
                await _svc.ResetXacNhanPCNAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/chot-pcn — P.KH chốt PCN (khóa vĩnh viễn), từ trang Thống kê</summary>
        [HttpPost("duc/chot-pcn")]
        public async Task<IActionResult> ChotPCN([FromBody] HRC1_ChotPCNRequest req)
        {
            try
            {
                await _svc.ChotPCNAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/bo-chot-pcn — P.KH bỏ chốt PCN, từ trang Thống kê</summary>
        [HttpPost("duc/bo-chot-pcn")]
        public async Task<IActionResult> BoChotPCN([FromBody] HRC1_BoChotPCNRequest req)
        {
            try
            {
                await _svc.BoChotPCNAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/chot-me — P.KH chốt mẻ đúc</summary>
        [HttpPost("duc/chot-me")]
        public async Task<IActionResult> ChotMe([FromBody] HRC1_DucChotMeRequest req)
        {
            try
            {
                await _svc.ChotMeAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/bo-chot-me — P.KH bỏ chốt mẻ đúc</summary>
        [HttpPost("duc/bo-chot-me")]
        public async Task<IActionResult> BoChotMe([FromBody] HRC1_DucBoChotMeRequest req)
        {
            try
            {
                await _svc.BoChotMeAsync(req, LayUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/chot-phieu-batch — P.KH chốt nhiều phiếu máy đúc, check điều kiện từng phiếu</summary>
        [HttpPost("duc/chot-phieu-batch")]
        public async Task<IActionResult> ChotPhieuBatch([FromBody] HRC1_ChotPhieuBatchRequest req)
        {
            try
            {
                var result = await _svc.ChotPhieuBatchAsync(req.IdPhieuList, LayUserId());
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/duc/huy-chot-phieu-batch — P.KH hủy chốt nhiều phiếu máy đúc</summary>
        [HttpPost("duc/huy-chot-phieu-batch")]
        public async Task<IActionResult> HuyChotPhieuBatch([FromBody] HRC1_ChotPhieuBatchRequest req)
        {
            try
            {
                var result = await _svc.HuyChotPhieuBatchAsync(req.IdPhieuList, LayUserId());
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/sync-phan-loai-me-thep — đồng bộ phân loại &amp; mác BKMIS từ Linked Server vào HRC1_MeThep</summary>
        [HttpPost("sync-phan-loai-me-thep")]
        public async Task<IActionResult> SyncPhanLoaiMeThep([FromBody] HRC1_SyncPhanLoaiRequest req)
        {
            try
            {
                var result = await _syncSvc.SyncHRC1MeThepAsync(req.MaMes);
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>PUT /api/hrc1/me/{meId}/ghi-chu — cập nhật ghi chú dùng chung cả 3 công đoạn</summary>
        [HttpPut("me/{meId:int}/ghi-chu")]
        public async Task<IActionResult> UpdateGhiChu(int meId, [FromBody] HRC1_UpdateGhiChuRequest req)
        {
            try
            {
                await _svc.UpdateGhiChuAsync(meId, req.GhiChu, req.Field, LayUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex)      { return NotFound(ex.Message); }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        // -------------------------------------------------------
        // Export
        // -------------------------------------------------------

        /// <summary>GET /api/hrc1/duc/get-me-by-daterange?fromDate=&amp;toDate= — mẻ BBGN thép lỏng lọc theo khoảng ThoiGian thực tế (giao ca)</summary>
        [HttpGet("duc/get-me-by-daterange")]
        public async Task<IActionResult> GetMeTheoThoiGian([FromQuery] HRC1_MeTheoThoiGianQuery query)
        {
            try
            {
                var result = await _svc.GetMeTheoThoiGianAsync(query.FromDate, query.ToDate);
                return Ok(result);
            }
            catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
            catch (Exception ex)                 { return StatusCode(500, ex.Message); }
        }

        /// <summary>GET /api/hrc1/thong-ke — tìm kiếm phân trang từ HRC1_MeThep</summary>
        [HttpGet("thong-ke")]
        public async Task<IActionResult> ThongKe([FromQuery] HRC1_ThongKeQuery query)
        {
            try
            {
                var result = await _svc.SearchThongKeAsync(query);
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>GET /api/hrc1/thong-ke/tong-hop — tổng hợp theo nhóm từ HRC1_MeThep</summary>
        [HttpGet("thong-ke/tong-hop")]
        public async Task<IActionResult> TongHop([FromQuery] HRC1_ThongKeQuery query)
        {
            try
            {
                var result = await _svc.TongHopAsync(query);
                return Ok(result);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>GET /api/hrc1/export/excel — xuất Excel danh sách mẻ theo điều kiện lọc</summary>
        [HttpGet("export/excel")]
        public async Task<IActionResult> ExportExcel([FromQuery] HRC1_ExportQuery query)
        {
            try
            {
                var result = await _svc.ExportExcelAsync(query);
                return File(result.Content, result.ContentType, result.FileName);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>POST /api/hrc1/export/excel/bulk — xuất Excel nhiều phiếu gộp vào 1 file</summary>
        [HttpPost("export/excel/bulk")]
        public async Task<IActionResult> ExportExcelBulk([FromBody] List<string> idPhieuList)
        {
            try
            {
                if (idPhieuList is null || idPhieuList.Count == 0)
                    return BadRequest("Danh sách phiếu không được rỗng.");
                var guids = idPhieuList
                    .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();
                if (guids.Count == 0) return BadRequest("Không có ID phiếu hợp lệ.");
                var result = await _svc.ExportBulkExcelAsync(guids);
                return File(result.Content, result.ContentType, result.FileName);
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        /// <summary>GET /api/hrc1/export/pdf — xuất PDF (chưa triển khai)</summary>
        [HttpGet("export/pdf")]
        public IActionResult ExportPdf([FromQuery] HRC1_ExportQuery _)
        {
            return StatusCode(501, "Chưa hỗ trợ export PDF cho HRC1 BBGN Thép lỏng.");
        }

        // -------------------------------------------------------
        private int LayUserId()
        {
            if (Request.Headers.TryGetValue("X-User-Id", out var val)
                && int.TryParse(val, out var id))
                return id;
            return 0;
        }
    }
}
