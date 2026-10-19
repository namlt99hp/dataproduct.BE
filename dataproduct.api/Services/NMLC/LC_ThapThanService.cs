using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories;

namespace dataproduct.api.Services.NMLC
{
    public class LC_ThapThanService
    {
        private readonly ILC_ThapThanRepository _repo;

        public LC_ThapThanService(ILC_ThapThanRepository repo)
        {
            _repo = repo;
        }

        // ─── Nhóm tháp than ───

        public async Task<List<NhomThapThanDto>> GetAllNhomAsync(string? searchKey = null, int? scope = null)
        {
            var list = await _repo.GetAllNhomAsync(searchKey, scope);
            return list.Select(ToDto).ToList();
        }

        public async Task<NhomThapThanDto?> GetNhomByIdAsync(int id)
        {
            var entity = await _repo.GetNhomByIdAsync(id);
            return entity == null ? null : ToDto(entity);
        }

        public async Task<NhomThapThanDto> AddNhomAsync(NhomThapThanSaveDto dto)
        {
            var entity = await _repo.AddNhomAsync(new LC_CumThap
            {
                Scope = dto.Scope,
                Ma = dto.Ma,
                Ten = dto.Ten,
                ThuTu = dto.ThuTu
            });
            return ToDto(entity);
        }

        public async Task<NhomThapThanDto?> UpdateNhomAsync(int id, NhomThapThanSaveDto dto)
        {
            var entity = await _repo.UpdateNhomAsync(new LC_CumThap
            {
                Id = id,
                Scope = dto.Scope,
                Ma = dto.Ma,
                Ten = dto.Ten,
                ThuTu = dto.ThuTu
            });
            return entity == null ? null : ToDto(entity);
        }

        public Task<bool> DeleteNhomAsync(int id)
            => _repo.DeleteNhomAsync(id);

        // ─── Phễu ───

        public async Task<List<PheuDto>> GetAllPheuAsync(int? thapThanId = null, string? searchKey = null)
        {
            var list = await _repo.GetAllPheuAsync(thapThanId, searchKey);
            var tenNhom = await GetTenNhomMapAsync();
            return list.Select(x => ToDto(x, tenNhom)).ToList();
        }

        public async Task<PheuDto?> GetPheuByIdAsync(int id)
        {
            var entity = await _repo.GetPheuByIdAsync(id);
            if (entity == null) return null;
            return ToDto(entity, await GetTenNhomMapAsync());
        }

        // Trả null nếu nhóm tháp than không tồn tại
        public async Task<PheuDto?> AddPheuAsync(PheuSaveDto dto)
        {
            if (!await _repo.NhomExistsAsync(dto.ThapThanId)) return null;

            var entity = await _repo.AddPheuAsync(new LC_TP_ThapThan_Pheu
            {
                ThapThanId = dto.ThapThanId,
                TenPheu = dto.TenPheu
            });
            return ToDto(entity, await GetTenNhomMapAsync());
        }

        // Trả null nếu phễu hoặc nhóm tháp than không tồn tại
        public async Task<PheuDto?> UpdatePheuAsync(int id, PheuSaveDto dto)
        {
            if (!await _repo.NhomExistsAsync(dto.ThapThanId)) return null;

            var entity = await _repo.UpdatePheuAsync(new LC_TP_ThapThan_Pheu
            {
                Id = id,
                ThapThanId = dto.ThapThanId,
                TenPheu = dto.TenPheu
            });
            return entity == null ? null : ToDto(entity, await GetTenNhomMapAsync());
        }

        public Task<bool> DeletePheuAsync(int id)
            => _repo.DeletePheuAsync(id);

        // ─── Chi tiết tháp than ───

        public async Task<List<ChiTietThapThanDto>> GetChiTietAsync(
            DateTime? ngaySx = null, int? ca = null, int? pheuId = null)
        {
            var list = await _repo.GetChiTietAsync(ngaySx, ca, pheuId);
            var pheuMap = await GetPheuMapAsync();
            return list.Select(x => ToDto(x, pheuMap)).ToList();
        }

        public async Task<ChiTietThapThanDto?> GetChiTietByIdAsync(int id)
        {
            var entity = await _repo.GetChiTietByIdAsync(id);
            if (entity == null) return null;
            return ToDto(entity, await GetPheuMapAsync());
        }

        // Trả null nếu phễu không tồn tại
        public async Task<ChiTietThapThanDto?> AddChiTietAsync(ChiTietThapThanSaveDto dto)
        {
            if (!await _repo.PheuExistsAsync(dto.PheuId)) return null;

            var entity = await _repo.AddChiTietAsync(ToEntity(dto));
            return ToDto(entity, await GetPheuMapAsync());
        }

        // Trả null nếu chi tiết hoặc phễu không tồn tại
        public async Task<ChiTietThapThanDto?> UpdateChiTietAsync(int id, ChiTietThapThanSaveDto dto)
        {
            if (!await _repo.PheuExistsAsync(dto.PheuId)) return null;

            var source = ToEntity(dto);
            source.Id = id;
            var entity = await _repo.UpdateChiTietAsync(source);
            return entity == null ? null : ToDto(entity, await GetPheuMapAsync());
        }

        public Task<bool> DeleteChiTietAsync(int id)
            => _repo.DeleteChiTietAsync(id);

        // Lưu toàn bộ chi tiết của một ngày + ca (xóa cũ, thêm mới). Trả null nếu có phễu không tồn tại
        public async Task<List<ChiTietThapThanDto>?> SaveChiTietNgayCaAsync(
            DateTime ngaySx, int ca, List<ChiTietThapThanSaveDto> items)
        {
            foreach (var pheuId in items.Select(x => x.PheuId).Distinct())
            {
                if (!await _repo.PheuExistsAsync(pheuId)) return null;
            }

            var saved = await _repo.ReplaceChiTietByNgayCaAsync(ngaySx, ca, items.Select(ToEntity).ToList());
            var pheuMap = await GetPheuMapAsync();
            return saved.Select(x => ToDto(x, pheuMap)).ToList();
        }

        // ─── Lookup & mapping ───

        private async Task<Dictionary<int, string>> GetTenNhomMapAsync()
            => (await _repo.GetAllNhomAsync()).ToDictionary(x => x.Id, x => x.Ten ?? string.Empty);

        private async Task<Dictionary<int, LC_TP_ThapThan_Pheu>> GetPheuMapAsync()
            => (await _repo.GetAllPheuAsync()).ToDictionary(x => x.Id);

        private static NhomThapThanDto ToDto(LC_CumThap e) => new()
        {
            Id = e.Id,
            Scope = e.Scope,
            Ma = e.Ma,
            Ten = e.Ten,
            ThuTu = e.ThuTu
        };

        private static PheuDto ToDto(LC_TP_ThapThan_Pheu e, Dictionary<int, string> tenNhom) => new()
        {
            Id = e.Id,
            ThapThanId = e.ThapThanId,
            TenThapThan = tenNhom.GetValueOrDefault(e.ThapThanId),
            TenPheu = e.TenPheu
        };

        private static ChiTietThapThanDto ToDto(LC_TP_ThapThan_ChiTiet e, Dictionary<int, LC_TP_ThapThan_Pheu> pheuMap)
        {
            pheuMap.TryGetValue(e.PheuId, out var pheu);
            return new ChiTietThapThanDto
            {
                Id = e.Id,
                NgaySx = e.NgaySx,
                Ca = e.Ca,
                PheuId = e.PheuId,
                TenPheu = pheu?.TenPheu,
                SoYCCN = e.SoYCCN,
                GhiChuPhoiTron = e.GhiChuPhoiTron,
                DoAm = e.DoAm,
                KhoiLuongQuyKhoCan2A = e.KhoiLuongQuyKhoCan2A,
                ThuTu = e.ThuTu,
                SiLoId = e.SiLoId,
                LoVatLieuId = e.LoVatLieuId,
                TyLeKeHoach = e.TyLeKeHoach,
                TyLeQuyKhoThucTe = e.TyLeQuyKhoThucTe,
                KhoiLuongCan2A = e.KhoiLuongCan2A,
                KhoiLuongQuyKho = e.KhoiLuongQuyKho,
                LuyKeQuyKho = e.LuyKeQuyKho
            };
        }

        private static LC_TP_ThapThan_ChiTiet ToEntity(ChiTietThapThanSaveDto d) => new()
        {
            NgaySx = d.NgaySx,
            Ca = d.Ca,
            PheuId = d.PheuId,
            SoYCCN = d.SoYCCN,
            GhiChuPhoiTron = d.GhiChuPhoiTron,
            DoAm = d.DoAm,
            KhoiLuongQuyKhoCan2A = d.KhoiLuongQuyKhoCan2A,
            ThuTu = d.ThuTu,
            SiLoId = d.SiLoId,
            LoVatLieuId = d.LoVatLieuId,
            TyLeKeHoach = d.TyLeKeHoach,
            TyLeQuyKhoThucTe = d.TyLeQuyKhoThucTe,
            KhoiLuongCan2A = d.KhoiLuongCan2A,
            KhoiLuongQuyKho = d.KhoiLuongQuyKho,
            LuyKeQuyKho = d.LuyKeQuyKho
        };
    }
}
