
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
        // 3. DỮ LIỆU THÁP THAN
        // =====================================================


        // =====================================================
        // 3. CHI TIẾT THÁP THAN
        // =====================================================

        [HttpGet("chi-tiet-thap-than")]
        public async Task<IActionResult> GetChiTietThapThan(
            [FromQuery] DateTime? ngaySx,
            [FromQuery] int? ca,
            [FromQuery] int? pheuId)
        {
            var result = await _service.GetChiTietAsync(
                ngaySx, ca, pheuId);

            return Ok(result);
        }

        [HttpGet("chi-tiet-thap-than/{id}")]
        public async Task<IActionResult> GetChiTietThapThanById(int id)
        {
            var result = await _service.GetChiTietByIdAsync(id);

            return result == null
                ? NotFound(new
                {
                    message = $"Không tìm thấy chi tiết tháp than ID = {id}."
                })
                : Ok(result);
        }

        [HttpPost("chi-tiet-thap-than")]
        public async Task<IActionResult> CreateChiTietThapThan(
            [FromBody] ChiTietThapThanSaveDto dto)
        {
            var result = await _service.AddChiTietAsync(dto);

            if (result == null)
            {
                return BadRequest(new
                {
                    message = "Phễu được tham chiếu không tồn tại."
                });
            }

            return CreatedAtAction(
                nameof(GetChiTietThapThanById),
                new { id = result.Id },
                result);
        }

        [HttpPut("chi-tiet-thap-than/{id}")]
        public async Task<IActionResult> UpdateChiTietThapThan(
            int id,
            [FromBody] ChiTietThapThanSaveDto dto)
        {
            var result = await _service.UpdateChiTietAsync(id, dto);

            return result == null
                ? NotFound(new
                {
                    message = "Không tìm thấy chi tiết tháp than hoặc phễu được tham chiếu."
                })
                : Ok(result);
        }

        [HttpDelete("chi-tiet-thap-than/{id}")]
        public async Task<IActionResult> DeleteChiTietThapThan(int id)
        {
            var deleted = await _service.DeleteChiTietAsync(id);

            return deleted
                ? NoContent()
                : NotFound(new
                {
                    message = $"Không tìm thấy chi tiết tháp than ID = {id}."
                });
        }

        [HttpPut("chi-tiet-thap-than/theo-ngay-ca")]
        public async Task<IActionResult> ReplaceChiTietThapThanTheoNgayCa(
            [FromQuery] DateTime ngaySx,
            [FromQuery] int ca,
            [FromBody] List<ChiTietThapThanSaveDto> items)
        {
            var result = await _service.SaveChiTietNgayCaAsync(
                ngaySx, ca, items);

            return result == null
                ? BadRequest(new
                {
                    message = "Không thể lưu dữ liệu. Vui lòng kiểm tra các phễu được tham chiếu."
                })
                : Ok(result);
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
    }
}
