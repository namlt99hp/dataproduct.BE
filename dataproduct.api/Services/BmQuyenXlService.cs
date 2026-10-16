using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using dataproduct.api.Utils.Enums;

namespace dataproduct.api.Services
{
    public class BmQuyenXlService
    {
        private readonly IBmQuyenXlRepository _repo;

        public BmQuyenXlService(IBmQuyenXlRepository repo)
        {
            _repo = repo;
        }

        private static BmQuyenXlDto ToDto(BmQuyenXl x) => new()
        {
            Id = x.Id,
            IdTaiKhoan = x.IdTaiKhoan,
            MaBm = x.MaBm,
            MaKhuVuc = x.MaKhuVuc,
            QuyenChucNang = x.QuyenChucNang,
            KhuVucPhu = x.KhuVucPhu
        };

        public async Task<IEnumerable<BmQuyenXlDto>> GetAllAsync(int? idTaiKhoan, string? maBm, string? maKhuVuc)
        {
            var data = await _repo.GetAllAsync(idTaiKhoan, maBm, maKhuVuc);
            return data.Select(ToDto);
        }

        public async Task<BmQuyenXlDto?> GetByIdAsync(int id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity == null ? null : ToDto(entity);
        }

        public async Task<IEnumerable<BmQuyenXlDto>> GetByTaiKhoanIdAsync(int idTaiKhoan)
        {
            var data = await _repo.GetByTaiKhoanIdAsync(idTaiKhoan);
            return data.Select(ToDto);
        }

        /// <summary>Quyền mở rộng riêng theo BM (value >= 6, khai báo ở FE trong bmQuyenConfig.ts, vd "Xác nhận PCN").</summary>
        private const byte EXTRA_QUYEN_THRESHOLD = 6;

        /// <summary>
        /// Lấy danh sách MaBM cho menu:
        /// - processing (Việc tôi bắt đầu): MaBM có QuyenChucNang = 1 (XULY) hoặc 4 (XULY_VA_PHEDUYET).
        /// - approving (Việc đến tôi): MaBM có QuyenChucNang = 2 (PHEDUYET) hoặc 4 (XULY_VA_PHEDUYET).
        /// - viewing (Chỉ xem): MaBM có QuyenChucNang = 5 (XEM).
        /// - extraQuyens: raw (MaBm, QuyenChucNang) cho các quyền mở rộng (>= 6) — BE không biết BM nào
        ///   dùng vùng nào cho quyền này, chỉ pass-through; FE (bmQuyenConfig.ts, field `extraQuyens[].vung`)
        ///   tự tra và cộng vào đúng vùng menu tương ứng.
        /// MaBM có trong nhiều list thì FE có thể hiển thị theo nhu cầu.
        /// </summary>
        public async Task<MenuPermissionsDto> GetMenuPermissionsAsync(int idTaiKhoan)
        {
            var data = (await _repo.GetByTaiKhoanIdAsync(idTaiKhoan)).ToList();

            // Việc tôi bắt đầu: chỉ QuyenChucNang = 1 (XULY) hoặc 4 (XULY_VA_PHEDUYET)
            var processing = data
                .Where(x =>
                {
                    var q = x.QuyenChucNang;
                    return q == (byte)QuyenChucNangEnum.XULY
                        || q == (byte)QuyenChucNangEnum.XULY_VA_PHEDUYET;
                })
                .Select(x => x.MaBm != null ? x.MaBm.Trim() : null)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            // Việc đến tôi: QuyenChucNang = 2 (PHEDUYET) hoặc 4 (XULY_VA_PHEDUYET)
            var approving = data
                .Where(x =>
                {
                    var q = x.QuyenChucNang;
                    return q == (byte)QuyenChucNangEnum.PHEDUYET
                        || q == (byte)QuyenChucNangEnum.XULY_VA_PHEDUYET;
                })
                .Select(x => x.MaBm != null ? x.MaBm.Trim() : null)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            // Chỉ xem: QuyenChucNang = 5 (XEM)
            var viewing = data
                .Where(x => x.QuyenChucNang == (byte)QuyenChucNangEnum.XEM)
                .Select(x => x.MaBm != null ? x.MaBm.Trim() : null)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();
            // quyền chốt phiếu QuyenChucNang = 3 (CHỐT)
            var chotPhieu = data
                .Where(x => x.QuyenChucNang == (byte)QuyenChucNangEnum.CHOT)
                .Select(x => x.MaBm != null ? x.MaBm.Trim() : null)
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .ToList();

            // Quyền mở rộng riêng theo BM (>= 6) — trả raw để FE tự map sang vùng qua bmQuyenConfig.ts
            var extraQuyens = data
                .Where(x => x.MaBm != null && x.QuyenChucNang >= EXTRA_QUYEN_THRESHOLD)
                .Select(x => new ExtraQuyenMenuItemDto(x.MaBm!.Trim(), x.QuyenChucNang!.Value))
                .Distinct()
                .ToList();

            // Kho dữ liệu (vung 5): mã KDL_* có QuyenChucNang = 5 (XEM)
            var kdlForms = data
                .Where(x => x.MaBm != null && x.MaBm.StartsWith("KDL_") && x.QuyenChucNang == (byte)QuyenChucNangEnum.XEM)
                .Select(x => x.MaBm!.Trim())
                .Distinct()
                .ToList();

            // Quản trị (vung 6): mã QTRI_* có QuyenChucNang = 1 (XULY)
            var qtriForms = data
                .Where(x => x.MaBm != null && x.MaBm.StartsWith("QTRI_") && x.QuyenChucNang == (byte)QuyenChucNangEnum.XULY)
                .Select(x => x.MaBm!.Trim())
                .Distinct()
                .ToList();

            return new MenuPermissionsDto
            {
                ProcessingForms = processing,
                ApprovingForms = approving,
                ViewingForms = viewing,
                ChotPhieuForms = chotPhieu,
                ExtraQuyens = extraQuyens,
                KdlForms = kdlForms,
                QtriForms = qtriForms,
            };
        }

        public async Task<BmQuyenXl> CreateAsync(BmQuyenXlCreateDto dto)
        {
            // Kiểm tra trùng lặp
            var isDuplicate = await _repo.CheckDuplicateAsync(dto.IdTaiKhoan, dto.MaBm, dto.MaKhuVuc, dto.QuyenChucNang, dto.KhuVucPhu);
            if (isDuplicate)
            {
                throw new InvalidOperationException(
                    $"Quyền xử lý đã tồn tại cho Tài khoản ID: {dto.IdTaiKhoan ?? 0}, Mã BM: '{dto.MaBm}', Khu vực: '{dto.MaKhuVuc}'"
                );
            }

            var entity = new BmQuyenXl
            {
                IdTaiKhoan = dto.IdTaiKhoan,
                MaBm = dto.MaBm,
                MaKhuVuc = dto.MaKhuVuc,
                QuyenChucNang = dto.QuyenChucNang,
                KhuVucPhu = dto.KhuVucPhu
            };

            await _repo.AddAsync(entity);
            return entity;
        }

        public async Task<bool> UpdateAsync(int id, BmQuyenXlUpdateDto dto)
        {
            if (!await _repo.ExistsAsync(id)) return false;

            // Kiểm tra trùng lặp (loại trừ bản ghi hiện tại)
            var isDuplicate = await _repo.CheckDuplicateAsync(dto.IdTaiKhoan, dto.MaBm, dto.MaKhuVuc, dto.QuyenChucNang, dto.KhuVucPhu, id);
            if (isDuplicate)
            {
                throw new InvalidOperationException(
                    $"Quyền xử lý đã tồn tại cho Tài khoản ID: {dto.IdTaiKhoan ?? 0}, Mã BM: '{dto.MaBm}', Khu vực: '{dto.MaKhuVuc}'"
                );
            }

            var entity = new BmQuyenXl
            {
                Id = id,
                IdTaiKhoan = dto.IdTaiKhoan,
                MaBm = dto.MaBm,
                MaKhuVuc = dto.MaKhuVuc,
                QuyenChucNang = dto.QuyenChucNang,
                KhuVucPhu = dto.KhuVucPhu
            };

            await _repo.UpdateAsync(entity);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            if (!await _repo.ExistsAsync(id)) return false;
            await _repo.DeleteAsync(id);
            return true;
        }

        /// <summary>
        /// Lưu hàng loạt: tích Descartes (MaBm × MaKhuVuc × QuyenChucNang) → mỗi tổ hợp = 1 dòng.
        /// IdsToDelete: xóa các bản ghi cũ trước khi tạo mới (dùng khi cập nhật toàn bộ quyền user).
        /// </summary>
        public async Task<List<BmQuyenXl>> BulkSaveAsync(BmQuyenXlBulkSaveDto dto)
        {
            if (dto.IdsToDelete.Count > 0)
                await _repo.DeleteRangeAsync(dto.IdsToDelete);

            var entities = new List<BmQuyenXl>();
            foreach (var item in dto.Items)
            {
                // KhuVucPhu optional: nếu không truyền thì lưu null, nếu có thì tổ hợp theo từng giá trị
                var khuVucPhuList = item.KhuVucPhus.Count > 0
                    ? item.KhuVucPhus.Select(k => (string?)k).ToList()
                    : new List<string?> { null };

                foreach (var maKhuVuc in item.MaKhuVucs)
                {
                    foreach (var quyen in item.QuyenChucNangs)
                    {
                        foreach (var khuVucPhu in khuVucPhuList)
                        {
                            var isDuplicate = await _repo.CheckDuplicateAsync(dto.IdTaiKhoan, item.MaBm, maKhuVuc, quyen, khuVucPhu);
                            if (isDuplicate)
                                throw new InvalidOperationException(
                                    $"Quyền xử lý đã tồn tại cho Tài khoản ID: {dto.IdTaiKhoan}, Mã BM: '{item.MaBm}', Khu vực: '{maKhuVuc}', Quyền: {quyen}"
                                );

                            entities.Add(new BmQuyenXl
                            {
                                IdTaiKhoan = dto.IdTaiKhoan,
                                MaBm = item.MaBm,
                                MaKhuVuc = maKhuVuc,
                                QuyenChucNang = quyen,
                                KhuVucPhu = khuVucPhu
                            });
                        }
                    }
                }
            }

            await _repo.AddRangeAsync(entities);
            return entities;
        }

        public async Task<bool> DeleteByTaiKhoanAsync(int idTaiKhoan)
        {
            await _repo.DeleteByTaiKhoanAsync(idTaiKhoan);
            return true;
        }
    }
}
