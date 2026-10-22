using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Models.NMLC;
using dataproduct.api.Repositories;

namespace dataproduct.api.Services.NMLC
{
    public class LC_ThanhPhamService
    {
        private readonly ILC_ThanhPhamRepository _repo;

        public LC_ThanhPhamService(ILC_ThanhPhamRepository repo)
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
                ID = id,
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

        public async Task<List<PheuDto>> GetAllPheuAsync(int? thapThanId = null, string? searchKey = null, int? scope = null)
        {
            var list = await _repo.GetAllPheuAsync(thapThanId, searchKey, scope);
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
                TenPheu = dto.TenPheu,
                ThuTu = dto.ThuTu
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
                TenPheu = dto.TenPheu,
                ThuTu = dto.ThuTu
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
            => (await _repo.GetAllNhomAsync()).ToDictionary(x => x.ID, x => x.Ten ?? string.Empty);

        private async Task<Dictionary<int, LC_TP_ThapThan_Pheu>> GetPheuMapAsync()
            => (await _repo.GetAllPheuAsync()).ToDictionary(x => x.Id);

        private static NhomThapThanDto ToDto(LC_CumThap e) => new()
        {
            Id = e.ID,
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
            TenPheu = e.TenPheu,
            ThuTu = e.ThuTu
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

        // ─── Kết quả phân tích than ───

        public async Task<List<KetQuaPhanTichThanDto>> GetKetQuaPhanTichAsync(Guid phieuCaId, int scope)
        {
            var list = await _repo.GetKetQuaPhanTichAsync(phieuCaId, scope);
            return list.Select(ToDto).ToList();
        }

        // Lưu toàn bộ kết quả phân tích của 1 phiếu ca + scope (xóa cũ, thêm mới)
        public async Task<List<KetQuaPhanTichThanDto>> SaveKetQuaPhanTichAsync(
            Guid phieuCaId, int scope, List<KetQuaPhanTichThanSaveDto> items)
        {
            var saved = await _repo.ReplaceKetQuaPhanTichAsync(
                phieuCaId, scope, items.Select(ToEntity).ToList());
            return saved.Select(ToDto).ToList();
        }

        public Task<int> DeleteKetQuaPhanTichAsync(Guid phieuCaId, int scope)
            => _repo.DeleteKetQuaPhanTichAsync(phieuCaId, scope);

        private static KetQuaPhanTichThanDto ToDto(LC_TP_KetQuaPhanTichThan e) => new()
        {
            Id = e.Id,
            PhieuCaId = e.PhieuCaId,
            Scope = e.Scope,
            ChiTieu = e.ChiTieu,
            Lan1 = e.Lan1,
            Lan2 = e.Lan2,
            Lan3 = e.Lan3,
            Lan4 = e.Lan4,
            Lan5 = e.Lan5,
            Lan6 = e.Lan6,
            GhiChu = e.GhiChu
        };

        private static LC_TP_KetQuaPhanTichThan ToEntity(KetQuaPhanTichThanSaveDto d) => new()
        {
            ChiTieu = d.ChiTieu.Trim(),
            Lan1 = d.Lan1,
            Lan2 = d.Lan2,
            Lan3 = d.Lan3,
            Lan4 = d.Lan4,
            Lan5 = d.Lan5,
            Lan6 = d.Lan6,
            GhiChu = d.GhiChu
        };

        // ─── Sự cố trong ca ───

        public async Task<List<SuCoTrongCaDto>> GetSuCoAsync(Guid phieuId, int scope)
        {
            var list = await _repo.GetSuCoAsync(phieuId, scope);
            return list.Select(ToDto).ToList();
        }

        // Lưu toàn bộ sự cố của phiếu (xóa cũ, thêm mới); Stt theo thứ tự danh sách
        public async Task<List<SuCoTrongCaDto>> SaveSuCoAsync(
            Guid phieuId, int scope, List<SuCoTrongCaSaveDto> items)
        {
            var saved = await _repo.ReplaceSuCoAsync(phieuId, scope, items.Select(ToEntity).ToList());
            return saved.Select(ToDto).ToList();
        }

        public Task<int> DeleteSuCoAsync(Guid phieuId, int scope)
            => _repo.DeleteSuCoAsync(phieuId, scope);

        private static SuCoTrongCaDto ToDto(LC_TP_SuCoTrongCa e) => new()
        {
            Id = e.Id,
            PhieuId = e.PhieuId,
            Scope = e.Scope,
            Stt = e.Stt,
            TinhTrangSuCo = e.TinhTrangSuCo,
            TgPhatHienSuCo = e.TgPhatHienSuCo,
            CachKhacPhuc = e.CachKhacPhuc,
            TgBatDau = e.TgBatDau,
            TgKetThuc = e.TgKetThuc,
            NhanLuc = e.NhanLuc
        };

        private static LC_TP_SuCoTrongCa ToEntity(SuCoTrongCaSaveDto d) => new()
        {
            TinhTrangSuCo = d.TinhTrangSuCo,
            TgPhatHienSuCo = d.TgPhatHienSuCo,
            CachKhacPhuc = d.CachKhacPhuc,
            TgBatDau = d.TgBatDau,
            TgKetThuc = d.TgKetThuc,
            NhanLuc = d.NhanLuc
        };

        // ─── Chất lượng cốc ───

        private const int SoLanChatLuongCoc = 6;

        public async Task<ChatLuongCocDanhMucDto> GetDanhMucChatLuongCocAsync()
        {
            var chiTieu = await _repo.GetChiTieuCocAsync();
            var ids = chiTieu.Select(x => x.Id).ToHashSet();
            var tcnb = (await _repo.GetTcnbCocAsync()).Where(x => ids.Contains(x.ChiTieuId));

            return new ChatLuongCocDanhMucDto
            {
                ChiTieu = chiTieu.Select(x => new ChiTieuCocDto
                {
                    Id = x.Id,
                    Ten = x.Ten,
                    DonVi = x.DonVi,
                    CoPhanLoai = x.CoPhanLoai,
                    ThuTu = x.ThuTu
                }).ToList(),
                Tcnb = tcnb.Select(x => new TcnbCocDto
                {
                    ChiTieuId = x.ChiTieuId,
                    Loai = x.Loai,
                    GiaTriTCNB = x.GiaTriTCNB
                }).ToList()
            };
        }

        public async Task<List<ChatLuongCocDto>> GetChatLuongCocAsync(Guid phieuId, int scope)
        {
            var list = await _repo.GetChatLuongCocAsync(phieuId, scope);
            return list.Select(ToDto).ToList();
        }

        // Lưu toàn bộ kết quả chất lượng cốc của phiếu + scope. Không tự đánh giá: DatYeuCau là
        // "Đánh giá" người dùng chọn cho từng chỉ tiêu (FE gửi kèm trên các dòng của chỉ tiêu đó).
        // Lỗi nghiệp vụ → InvalidOperationException.
        public async Task<List<ChatLuongCocDto>> SaveChatLuongCocAsync(
            Guid phieuId, int scope, List<ChatLuongCocSaveDto> items)
        {
            var chiTieuById = (await _repo.GetChiTieuCocAsync(includeDeleted: true)).ToDictionary(x => x.Id);

            for (var i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!chiTieuById.ContainsKey(it.ChiTieuId))
                    throw new InvalidOperationException($"Dòng {i + 1}: chỉ tiêu {it.ChiTieuId} không tồn tại.");
                if (it.Lan < 1 || it.Lan > SoLanChatLuongCoc)
                    throw new InvalidOperationException(
                        $"Dòng {i + 1}: lần đo phải từ 1 đến {SoLanChatLuongCoc}.");
            }

            // Mỗi chỉ tiêu chỉ có 1 giá trị cho mỗi lần đo
            var trung = items.GroupBy(x => (x.ChiTieuId, x.Lan)).FirstOrDefault(g => g.Count() > 1);
            if (trung != null)
                throw new InvalidOperationException(
                    $"Trùng kết quả đo: chỉ tiêu {chiTieuById[trung.Key.ChiTieuId].Ten}, lần {trung.Key.Lan}.");

            // Loai (NOT NULL trong DB) không còn ý nghĩa vì không phân loại tự động → 0 = chưa phân loại
            var entities = items.Select(d => new LC_TP_ChatLuongCocCa
            {
                ChiTieuId = d.ChiTieuId,
                Loai = 0,
                Lan = d.Lan,
                GiaTri = d.GiaTri,
                DatYeuCau = d.DatYeuCau
            }).ToList();

            var saved = await _repo.ReplaceChatLuongCocAsync(phieuId, scope, entities);
            return saved.Select(ToDto).ToList();
        }

        public Task<int> DeleteChatLuongCocAsync(Guid phieuId, int scope) => _repo.DeleteChatLuongCocAsync(phieuId, scope);

        private static ChatLuongCocDto ToDto(LC_TP_ChatLuongCocCa e) => new()
        {
            Id = e.Id,
            PhieuId = e.PhieuId,
            Scope = e.Scope,
            ChiTieuId = e.ChiTieuId,
            Loai = e.Loai,
            Lan = e.Lan,
            GiaTri = e.GiaTri,
            DatYeuCau = e.DatYeuCau
        };

        // ─── Tiêu hao ───

        public async Task<List<DanhMucTieuHaoDto>> GetDanhMucTieuHaoAsync()
        {
            var list = await _repo.GetDanhMucTieuHaoAsync();
            var tenNhom = (await _repo.GetNhomTieuHaoAsync()).ToDictionary(n => n.Id, n => n.Ten);
            return list.Select(x => new DanhMucTieuHaoDto
            {
                Id = x.Id,
                Ten = x.Ten,
                NhomId = x.NhomId,
                TenNhom = x.NhomId.HasValue && tenNhom.TryGetValue(x.NhomId.Value, out var t) ? t : null,
                DonVi = x.DonVi,
                DonViTieuHao = x.DonViTieuHao,
                ThuTu = x.ThuTu
            }).ToList();
        }

        public async Task<List<TieuHaoDto>> GetTieuHaoAsync(Guid phieuId, int scope)
        {
            var list = await _repo.GetTieuHaoAsync(phieuId, scope);
            return list.Select(ToDto).ToList();
        }

        // Lưu toàn bộ tiêu hao của phiếu (xóa cũ, thêm mới). Lỗi nghiệp vụ → InvalidOperationException.
        public async Task<List<TieuHaoDto>> SaveTieuHaoAsync(Guid phieuId, int scope, List<TieuHaoSaveDto> items)
        {
            var trung = items.GroupBy(x => x.HangMucId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (trung.Count > 0)
                throw new InvalidOperationException(
                    $"Hạng mục bị trùng trong phiếu: {string.Join(", ", trung)}.");

            var khongTonTai = await _repo.FindHangMucKhongTonTaiAsync(items.Select(x => x.HangMucId));
            if (khongTonTai.Count > 0)
                throw new InvalidOperationException(
                    $"Hạng mục không tồn tại trong danh mục: {string.Join(", ", khongTonTai)}.");

            var saved = await _repo.ReplaceTieuHaoAsync(phieuId, scope, items.Select(d => new LC_TP_TieuHao
            {
                HangMucId = d.HangMucId,
                ChiSoDauCa = d.ChiSoDauCa,
                ChiSoCuoiCa = d.ChiSoCuoiCa,
                TrongCa = d.TrongCa,
                TongNhaMay = d.TongNhaMay,
                LuyKe = d.LuyKe,
                TieuHaoTrenTan = d.TieuHaoTrenTan
            }).ToList());
            return saved.Select(ToDto).ToList();
        }

        public Task<int> DeleteTieuHaoAsync(Guid phieuId, int scope) => _repo.DeleteTieuHaoAsync(phieuId, scope);

        private static TieuHaoDto ToDto(LC_TP_TieuHao e) => new()
        {
            Id = e.Id,
            PhieuId = e.PhieuId,
            Scope = e.Scope,
            HangMucId = e.HangMucId,
            ChiSoDauCa = e.ChiSoDauCa,
            ChiSoCuoiCa = e.ChiSoCuoiCa,
            TrongCa = e.TrongCa,
            TongNhaMay = e.TongNhaMay,
            LuyKe = e.LuyKe,
            TieuHaoTrenTan = e.TieuHaoTrenTan
        };
    }
}
