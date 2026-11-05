
using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Services.NMLC;
using Microsoft.AspNetCore.Mvc;

namespace dataproduct.api.Controllers.NMLC
{
    [Route("api/[controller]")]
    [ApiController]
    public class LC_ThanhPhamController : ControllerBase
    {
        private readonly LC_ThanhPhamService _service;

        public LC_ThanhPhamController(LC_ThanhPhamService service)
        {
            _service = service;
        }

        // =====================================================
        // 1. NHÓM THÁP THAN
        // =====================================================

        [HttpGet("nhom-thap-than")]
        public async Task<IActionResult> GetNhomThapThan(
     [FromQuery] string? searchKey,
            [FromQuery] int? scope)
        {
            var result = await _service.GetAllNhomAsync(searchKey, scope);
            return Ok(result);
        }

        [HttpGet("nhom-thap-than/{id}")]
        public async Task<IActionResult> GetNhomThapThanById(int id)
        {
            var result = await _service.GetNhomByIdAsync(id);

            return result == null
                ? NotFound(new
                {
                    message = $"Không tìm thấy nhóm tháp than ID = {id}."
                })
                : Ok(result);
        }

        [HttpPost("nhom-thap-than")]
        public async Task<IActionResult> CreateNhomThapThan(
            [FromBody] NhomThapThanSaveDto dto)
        {
            var result = await _service.AddNhomAsync(dto);

            return CreatedAtAction(
                nameof(GetNhomThapThanById),
                new { id = result.Id },
                result);
        }

        [HttpPut("nhom-thap-than/{id}")]
        public async Task<IActionResult> UpdateNhomThapThan(
            int id,
            [FromBody] NhomThapThanSaveDto dto)
        {
            var result = await _service.UpdateNhomAsync(id, dto);

            return result == null
                ? NotFound(new
                {
                    message = $"Không tìm thấy nhóm tháp than ID = {id}."
                })
                : Ok(result);
        }

        [HttpDelete("nhom-thap-than/{id}")]
        public async Task<IActionResult> DeleteNhomThapThan(int id)
        {
            var deleted = await _service.DeleteNhomAsync(id);

            return deleted
                ? NoContent()
                : NotFound(new
                {
                    message = $"Không tìm thấy nhóm tháp than ID = {id}."
                });
        }

        // =====================================================
        // 2. PHỄU
        // =====================================================

        [HttpGet("pheu")]
        public async Task<IActionResult> GetPheu(
            [FromQuery] int? thapThanId,
            [FromQuery] string? searchKey,
            [FromQuery] int? scope)
        {
            var result = await _service.GetAllPheuAsync(
                thapThanId, searchKey, scope);

            return Ok(result);
        }

        [HttpGet("pheu/{id}")]
        public async Task<IActionResult> GetPheuById(int id)
        {
            var result = await _service.GetPheuByIdAsync(id);

            return result == null
                ? NotFound(new
                {
                    message = $"Không tìm thấy phễu ID = {id}."
                })
                : Ok(result);
        }

        [HttpPost("pheu")]
        public async Task<IActionResult> CreatePheu(
            [FromBody] PheuSaveDto dto)
        {
            var result = await _service.AddPheuAsync(dto);

            if (result == null)
            {
                return BadRequest(new
                {
                    message = "Nhóm tháp than không tồn tại."
                });
            }

            return CreatedAtAction(
                nameof(GetPheuById),
                new { id = result.Id },
                result);
        }

        [HttpPut("pheu/{id}")]
        public async Task<IActionResult> UpdatePheu(
            int id,
            [FromBody] PheuSaveDto dto)
        {
            var result = await _service.UpdatePheuAsync(id, dto);

            return result == null
                ? NotFound(new
                {
                    message = "Không tìm thấy phễu hoặc nhóm tháp than được tham chiếu."
                })
                : Ok(result);
        }

        [HttpDelete("pheu/{id}")]
        public async Task<IActionResult> DeletePheu(int id)
        {
            var deleted = await _service.DeletePheuAsync(id);

            return deleted
                ? NoContent()
                : NotFound(new
                {
                    message = $"Không tìm thấy phễu ID = {id}."
                });
        }

        // =====================================================
        // 3. TAB THÁP/PHỄU — API by-phieu riêng, KHÔNG qua DataJson (xem LC_TP_TonPheu/
        // LC_TP_CapThap, LC_ThanhPhamRepository). Mirror pattern LC_PhoiTronController.
        // =====================================================

        [HttpGet("thap-pheu/by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetThapPheuByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.GetThapPheuTabAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPut("thap-pheu/by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> SaveThapPheuByPhieu(Guid idPhieu, [FromBody] ThapPheuTabSaveDto dto)
        {
            try
            {
                return Ok(await _service.SaveThapPheuTabAsync(idPhieu, dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // "Làm mới" — tính lại Tồn đầu (Phễu) + Lũy kế cấp lên tháp/nạp lò (Tháp) theo dữ
        // liệu MỚI NHẤT của kíp liền trước, không nhận input FE — xem LC_PhoiTronController.
        [HttpPost("thap-pheu/by-phieu/{idPhieu:guid}/lam-moi")]
        public async Task<IActionResult> RefreshThapPheuByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.RefreshThapPheuTuCaTruocAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // =====================================================
        // 3b. BỤI CỐC (Sản lượng/Tồn/Xuất) — gộp vào tab Tháp/Phễu theo yêu cầu, TP1 ONLY.
        // Xem LC_TP_BuiCoc/LC_TP_BuiCocXuat, LC_ThanhPhamRepository.
        // =====================================================

        [HttpGet("bui-coc-noi-nhan")]
        public async Task<IActionResult> GetBuiCocNoiNhan()
        {
            return Ok(await _service.GetAllBuiCocNoiNhanAsync());
        }

        [HttpGet("bui-coc-noi-nhan/{id}")]
        public async Task<IActionResult> GetBuiCocNoiNhanById(int id)
        {
            var result = await _service.GetBuiCocNoiNhanByIdAsync(id);
            return result == null
                ? NotFound(new { message = $"Không tìm thấy nơi nhận ID = {id}." })
                : Ok(result);
        }

        [HttpPost("bui-coc-noi-nhan")]
        public async Task<IActionResult> CreateBuiCocNoiNhan([FromBody] BuiCocNoiNhanSaveDto dto)
        {
            var result = await _service.AddBuiCocNoiNhanAsync(dto);
            return CreatedAtAction(nameof(GetBuiCocNoiNhanById), new { id = result.Id }, result);
        }

        [HttpPut("bui-coc-noi-nhan/{id}")]
        public async Task<IActionResult> UpdateBuiCocNoiNhan(int id, [FromBody] BuiCocNoiNhanSaveDto dto)
        {
            var result = await _service.UpdateBuiCocNoiNhanAsync(id, dto);
            return result == null
                ? NotFound(new { message = $"Không tìm thấy nơi nhận ID = {id}." })
                : Ok(result);
        }

        [HttpDelete("bui-coc-noi-nhan/{id}")]
        public async Task<IActionResult> DeleteBuiCocNoiNhan(int id)
        {
            var deleted = await _service.DeleteBuiCocNoiNhanAsync(id);
            return deleted
                ? NoContent()
                : NotFound(new { message = $"Không tìm thấy nơi nhận ID = {id}." });
        }

        [HttpGet("bui-coc/by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> GetBuiCocByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.GetBuiCocTabAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        [HttpPut("bui-coc/by-phieu/{idPhieu:guid}")]
        public async Task<IActionResult> SaveBuiCocByPhieu(Guid idPhieu, [FromBody] BuiCocTabSaveDto dto)
        {
            try
            {
                return Ok(await _service.SaveBuiCocTabAsync(idPhieu, dto));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("bui-coc/by-phieu/{idPhieu:guid}/lam-moi")]
        public async Task<IActionResult> RefreshBuiCocByPhieu(Guid idPhieu)
        {
            try
            {
                return Ok(await _service.RefreshBuiCocTuCaTruocAsync(idPhieu));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // =====================================================
        // 4. KẾT QUẢ PHÂN TÍCH THAN
        // =====================================================

        [HttpGet("ket-qua-phan-tich")]
        public async Task<IActionResult> GetKetQuaPhanTich(
            [FromQuery] Guid phieuCaId,
            [FromQuery] int scope)
        {
            return Ok(await _service.GetKetQuaPhanTichAsync(phieuCaId, scope));
        }

        // Thay thế toàn bộ kết quả phân tích của phiếu ca + scope
        [HttpPut("ket-qua-phan-tich")]
        public async Task<IActionResult> ReplaceKetQuaPhanTich(
            [FromQuery] Guid phieuCaId,
            [FromQuery] int scope,
            [FromBody] List<KetQuaPhanTichThanSaveDto> items)
        {
            var duplicated = items
                .GroupBy(x => x.ChiTieu.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicated != null)
            {
                return BadRequest(new
                {
                    message = $"Chỉ tiêu '{duplicated.Key}' bị trùng."
                });
            }

            return Ok(await _service.SaveKetQuaPhanTichAsync(phieuCaId, scope, items));
        }

        [HttpDelete("ket-qua-phan-tich")]
        public async Task<IActionResult> DeleteKetQuaPhanTich(
            [FromQuery] Guid phieuCaId,
            [FromQuery] int scope)
        {
            var deleted = await _service.DeleteKetQuaPhanTichAsync(phieuCaId, scope);

            return deleted > 0
                ? NoContent()
                : NotFound(new { message = "Không có kết quả phân tích để xóa." });
        }

        // =====================================================
        // 5. SỰ CỐ TRONG CA
        // =====================================================

        [HttpGet("su-co")]
        public async Task<IActionResult> GetSuCo(
            [FromQuery] Guid phieuId,
            [FromQuery] int scope)
        {
            return Ok(await _service.GetSuCoAsync(phieuId, scope));
        }

        // Thay thế toàn bộ sự cố của phiếu + scope
        [HttpPut("su-co")]
        public async Task<IActionResult> ReplaceSuCo(
            [FromQuery] Guid phieuId,
            [FromQuery] int scope,
            [FromBody] List<SuCoTrongCaSaveDto> items)
        {
            var invalid = items.FindIndex(x =>
                x.TgBatDau.HasValue && x.TgKetThuc.HasValue && x.TgKetThuc < x.TgBatDau);

            if (invalid >= 0)
            {
                return BadRequest(new
                {
                    message = $"Sự cố dòng {invalid + 1}: thời gian kết thúc phải sau thời gian bắt đầu."
                });
            }

            return Ok(await _service.SaveSuCoAsync(phieuId, scope, items));
        }

        [HttpDelete("su-co")]
        public async Task<IActionResult> DeleteSuCo(
            [FromQuery] Guid phieuId,
            [FromQuery] int scope)
        {
            var deleted = await _service.DeleteSuCoAsync(phieuId, scope);

            return deleted > 0
                ? NoContent()
                : NotFound(new { message = "Phiếu không có sự cố để xóa." });
        }

        // =====================================================
        // 7. CHẤT LƯỢNG CỐC
        // =====================================================

        // Danh mục chỉ tiêu (kèm hướng đạt) + bảng TCNB theo (chỉ tiêu, loại)
        [HttpGet("danh-muc-chat-luong-coc")]
        public async Task<IActionResult> GetDanhMucChatLuongCoc()
        {
            return Ok(await _service.GetDanhMucChatLuongCocAsync());
        }

        [HttpGet("chat-luong-coc")]
        public async Task<IActionResult> GetChatLuongCoc([FromQuery] Guid phieuId, [FromQuery] int scope)
        {
            return Ok(await _service.GetChatLuongCocAsync(phieuId, scope));
        }

        // Thay thế toàn bộ kết quả chất lượng cốc của phiếu + scope (DatYeuCau = đánh giá người dùng chọn)
        [HttpPut("chat-luong-coc")]
        public async Task<IActionResult> ReplaceChatLuongCoc(
            [FromQuery] Guid phieuId,
            [FromQuery] int scope,
            [FromBody] List<ChatLuongCocSaveDto> items)
        {
            try
            {
                return Ok(await _service.SaveChatLuongCocAsync(phieuId, scope, items));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("chat-luong-coc")]
        public async Task<IActionResult> DeleteChatLuongCoc([FromQuery] Guid phieuId, [FromQuery] int scope)
        {
            var deleted = await _service.DeleteChatLuongCocAsync(phieuId, scope);

            return deleted > 0
                ? NoContent()
                : NotFound(new { message = "Phiếu không có kết quả chất lượng cốc để xóa." });
        }

        // =====================================================
        // 6. TIÊU HAO
        // =====================================================

        [HttpGet("danh-muc-tieu-hao")]
        public async Task<IActionResult> GetDanhMucTieuHao()
        {
            return Ok(await _service.GetDanhMucTieuHaoAsync());
        }

        [HttpGet("tieu-hao")]
        public async Task<IActionResult> GetTieuHao([FromQuery] Guid phieuId, [FromQuery] int scope)
        {
            return Ok(await _service.GetTieuHaoAsync(phieuId, scope));
        }

        // Thay thế toàn bộ tiêu hao của phiếu
        [HttpPut("tieu-hao")]
        public async Task<IActionResult> ReplaceTieuHao(
            [FromQuery] Guid phieuId,
            [FromQuery] int scope,
            [FromBody] List<TieuHaoSaveDto> items)
        {
            try
            {
                return Ok(await _service.SaveTieuHaoAsync(phieuId, scope, items));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("tieu-hao")]
        public async Task<IActionResult> DeleteTieuHao([FromQuery] Guid phieuId, [FromQuery] int scope)
        {
            var deleted = await _service.DeleteTieuHaoAsync(phieuId, scope);

            return deleted > 0
                ? NoContent()
                : NotFound(new { message = "Phiếu không có tiêu hao để xóa." });
        }
    }
}
