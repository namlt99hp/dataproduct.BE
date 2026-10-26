using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories.NM.LC;
using dataproduct.api.Utils;

namespace dataproduct.api.Services.NM.LC
{
    public class LC_SiloTonService
    {
        private static readonly string[] LoaiLieuHopLe = { "MM_10_80", "MM_DUOI_10" };

        private readonly ILC_SiloTonRepository _repo;

        public LC_SiloTonService(ILC_SiloTonRepository repo)
        {
            _repo = repo;
        }

        public Task<List<LcSiloTonDto>> GetListAsync(int? scope)
            => _repo.GetListAsync(scope);

        public Task<LC_SiloTon?> GetByIdAsync(int id)
            => _repo.GetByIdAsync(id);

        public async Task<(LC_SiloTon? Result, string? Error)> AddAsync(CreateLcSiloTonDto dto)
        {
            var error = Validate(dto);
            if (error != null) return (null, error);

            var entity = new LC_SiloTon
            {
                Scope = dto.Scope,
                TenSilo = dto.TenSilo.Trim(),
                LoaiLieu = dto.LoaiLieu,
                TenNhapLieu = string.IsNullOrWhiteSpace(dto.TenNhapLieu) ? "Chiều cao" : dto.TenNhapLieu.Trim(),
                CongThuc = dto.CongThuc.Trim(),
            };
            return (await _repo.AddAsync(entity), null);
        }

        public async Task<(LC_SiloTon? Result, string? Error)> UpdateAsync(int id, UpdateLcSiloTonDto dto)
        {
            var error = Validate(dto);
            if (error != null) return (null, error);

            var entity = new LC_SiloTon
            {
                Scope = dto.Scope,
                TenSilo = dto.TenSilo.Trim(),
                LoaiLieu = dto.LoaiLieu,
                TenNhapLieu = string.IsNullOrWhiteSpace(dto.TenNhapLieu) ? "Chiều cao" : dto.TenNhapLieu.Trim(),
                CongThuc = dto.CongThuc.Trim(),
            };
            var result = await _repo.UpdateAsync(id, entity);
            return (result, result == null ? "Không tìm thấy Silo tồn." : null);
        }

        public Task<bool> DeleteAsync(int id)
            => _repo.DeleteAsync(id);

        public TestCongThucResultDto TestCongThuc(TestCongThucDto dto)
        {
            var (ketQua, loi) = CongThucEvaluator.Evaluate(dto.CongThuc, dto.GiaTriNhap);
            return new TestCongThucResultDto { KetQua = ketQua, Loi = loi };
        }

        private static string? Validate(CreateLcSiloTonDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.TenSilo))
                return "Tên silo/phụ liệu không được để trống.";
            if (!LoaiLieuHopLe.Contains(dto.LoaiLieu))
                return $"Loại liệu không hợp lệ (chỉ nhận {string.Join("/", LoaiLieuHopLe)}).";
            if (string.IsNullOrWhiteSpace(dto.CongThuc))
                return "Công thức không được để trống.";
            return CongThucEvaluator.Validate(dto.CongThuc);
        }
    }
}
