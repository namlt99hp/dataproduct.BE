using dataproduct.api.DTOs.NMLG_Dto;
using dataproduct.api.Models;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    public class LGNLRepository : ILGNLRepository
    {
        private readonly ProductFormContext _context;

        public LGNLRepository(ProductFormContext context)
        {
            _context = context;
        }

        // ─── TS Mapping lookup ───────────────────────────────────────────────

        public async Task<List<LGNLTsMappingDto>> GetTsMappingListAsync()
        {
            return await _context.LG1_NL_TS_Mapping
                .Where(x => x.IsActive == true)
                .AsNoTracking()
                .Select(x => new LGNLTsMappingDto
                {
                    Id = x.ID,
                    TagKey = x.TagKey,
                    IsActive = x.IsActive
                })
                .ToListAsync();
        }

        // ─── Dữ liệu thô LG1_DuLieuNL ───────────────────────────────────────

        public async Task<List<LG1_DuLieuNL>> GetDuLieuRawAsync(
            IEnumerable<string> tagKeys, int idLoCao, DateTime timeFrom, DateTime timeTo)
        {
            var keys = tagKeys.ToList();
            return await _context.LG1_DuLieuNL
                .Where(d => d.ID_LoCao == idLoCao
                         && d.Time >= timeFrom
                         && d.Time < timeTo
                         && keys.Contains(d.TagKey!))
                .OrderBy(d => d.Time)
                .AsNoTracking()
                .ToListAsync();
        }

        // ─── SiLo Master ─────────────────────────────────────────────────────

        public async Task<List<LG_NL_SiLo>> GetSiLoMasterListAsync(int? idLoCao)
        {
            return await _context.LG_NL_SiLo
                .Where(x => x.IsDelete != true && (idLoCao == null || x.IDLoCao == idLoCao))
                .OrderBy(x => x.IDLoCao)
                .ThenBy(x => x.ThuTu)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LG_NL_SiLo?> GetSiLoMasterByIdAsync(int id)
            => await _context.LG_NL_SiLo.FindAsync(id);

        public async Task<LG_NL_SiLo> AddSiLoMasterAsync(LG_NL_SiLo entity)
        {
            entity.NgayTao = DateTime.Now;
            await _context.LG_NL_SiLo.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LG_NL_SiLo?> UpdateSiLoMasterAsync(int id, LG_NL_SiLo entity)
        {
            var existing = await _context.LG_NL_SiLo.FindAsync(id);
            if (existing == null) return null;

            existing.IDLoCao = entity.IDLoCao;
            existing.TenSiLo = entity.TenSiLo;
            existing.ThuTu = entity.ThuTu;
            existing.TagKey = entity.TagKey;
            existing.IsDelete = entity.IsDelete;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteSiLoMasterAsync(int id)
        {
            var existing = await _context.LG_NL_SiLo.FindAsync(id);
            if (existing == null) return false;
            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Mapping (join SiLo + NVL) ───────────────────────────────────────

        public async Task<List<LGNLMappingDto>> GetMappingListAsync(DateTime? ngay, int? idCa, int? idLoCao)
        {
            return await (
                from m in _context.LG_NL_Mapping
                join s in _context.LG_NL_SiLo.Where(x => x.IsDelete != true)
                    on m.IDSiLo equals s.ID into siloGroup
                from s in siloGroup.DefaultIfEmpty()
                join n in _context.LG_NL_NVL.Where(x => x.IsDelete != true)
                    on m.IDNVL equals n.ID into nvlGroup
                from n in nvlGroup.DefaultIfEmpty()
                join nh in _context.LG_NL_NhomNVL
                    on n.IDNhomNVL equals nh.ID into nhomGroup
                from nh in nhomGroup.DefaultIfEmpty()
                where
                    (ngay == null || m.Ngay == ngay) &&
                    (idCa == null || m.IDCa == idCa) &&
                    (idLoCao == null || m.IDLoCao == idLoCao)
                orderby m.Ngay, m.IDCa, s.ThuTu
                select new LGNLMappingDto
                {
                    Id = m.ID,
                    Ngay = m.Ngay,
                    IdCa = m.IDCa,
                    IdLoCao = m.IDLoCao,
                    IdSiLo = m.IDSiLo,
                    TenSiLo = s != null ? s.TenSiLo : null,
                    TagKey = s != null ? s.TagKey : null,
                    IdNVL = m.IDNVL,
                    TenNVL = n != null ? (n.XacNhan == true && n.TenNVL_TK != null ? n.TenNVL_TK : n.TenNVL_NM) : null,
                    NhomHienThi = nh != null ? nh.TenNhom : null,
                    ThuTuNhom = nh != null ? nh.ThuTu : null,
                    ThoiDiemBD = m.ThoiDiemBD,
                    NgayHetHL = m.NgayHetHL,
                    IdCaHetHL = m.IDCaHetHL,
                    GhiChu = m.GhiChu,
                    NgayTao = m.NgayTao
                }
            ).AsNoTracking().ToListAsync();
        }

        //public async Task<List<LGNLSiloSnapshotDto>> GetSiloSnapshotAsync(
        //    int idLoCao, DateTime ngay, int idCa)
        //{
        //    // 1. Tìm config đang hiệu lực (cùng logic với pivot)
        //    var configRef = await _context.LG_NL_Mapping
        //        .Where(m => m.IDLoCao == idLoCao
        //                 && m.ThoiDiemBD == null
        //                 && (m.Ngay < ngay || (m.Ngay == ngay && m.IDCa <= idCa))
        //                 && (m.NgayHetHL == null
        //                     || m.NgayHetHL > ngay
        //                     || (m.NgayHetHL == ngay && m.IDCaHetHL > idCa)))
        //        .OrderByDescending(m => m.Ngay)
        //        .ThenByDescending(m => m.IDCa)
        //        .Select(m => new { m.Ngay, m.IDCa })
        //        .FirstOrDefaultAsync();

        //    if (configRef == null) return [];

        //    var now = DateTime.Now;

        //    // 2. Lấy tất cả mapping của config (gồm cả đổi NVL giữa ca)
        //    var mappings = await (
        //        from m in _context.LG_NL_Mapping
        //        join s in _context.LG_NL_SiLo on m.IDSiLo equals s.ID into sg
        //        from s in sg.DefaultIfEmpty()
        //        join n in _context.LG_NL_NVL on m.IDNVL equals n.ID into ng
        //        from n in ng.DefaultIfEmpty()
        //        where m.IDLoCao == idLoCao
        //           && m.Ngay == configRef.Ngay
        //           && m.IDCa == configRef.IDCa
        //        select new
        //        {
        //            IDSiLo = m.IDSiLo,
        //            TenSiLo = s != null ? s.TenSiLo : null,
        //            IDNVL = n != null ? (int?)n.ID : null,
        //            TenNVL = n != null ? (n.XacNhan == true && n.TenNVL_TK != null ? n.TenNVL_TK : n.TenNVL_NM) : null,
        //            ThoiDiemBD = m.ThoiDiemBD,
        //            ThuTu = s != null ? s.ThuTu : (int?)null,
        //        }
        //    ).AsNoTracking().ToListAsync();

        //    // 3. Với mỗi silo, tìm NVL đang hiệu lực tại thời điểm hiện tại
        //    return mappings
        //        .GroupBy(m => m.IDSiLo)
        //        .Select(g =>
        //        {
        //            var rows = g.OrderBy(r => r.ThoiDiemBD ?? DateTime.MinValue).ToList();
        //            //var activeRow = rows.LastOrDefault(r => r.ThoiDiemBD == null || r.ThoiDiemBD <= now)
        //            //                ?? rows.First();
        //            var activeRow = rows
        //            .OrderBy(r => r.ThoiDiemBD ?? DateTime.MinValue)
        //            .Last();
        //            return new LGNLSiloSnapshotDto
        //            {
        //                IDSiLo = g.Key ?? 0,
        //                TenSiLo = activeRow.TenSiLo,
        //                IDNVL = activeRow.IDNVL,
        //                TenNVL = activeRow.TenNVL,
        //                ThoiDiemBD = activeRow.ThoiDiemBD,
        //                DaDoiGiuaCa = rows.Any(r => r.ThoiDiemBD != null),
        //                ThuTu = activeRow.ThuTu,
        //            };
        //        })
        //        .OrderBy(s => s.ThuTu)
        //        .ToList();
        //}
        public async Task<List<LGNLSiloSnapshotDto>> GetSiloSnapshotAsync( int idLoCao, DateTime ngay, int idCa)
        {
            var mappings = await (
                from m in _context.LG_NL_Mapping

                join s in _context.LG_NL_SiLo.Where(x => x.IsDelete != true)
                    on m.IDSiLo equals s.ID into sg
                from s in sg.DefaultIfEmpty()

                join n in _context.LG_NL_NVL.Where(x => x.IsDelete != true)
                    on m.IDNVL equals n.ID into ng
                from n in ng.DefaultIfEmpty()

                where m.IDLoCao == idLoCao
                   && m.Ngay == ngay.Date
                   && m.IDCa == idCa

                select new
                {
                    IDSiLo = m.IDSiLo,
                    TenSiLo = s != null ? s.TenSiLo : null,

                    IDNVL = n != null
                        ? (int?)n.ID
                        : null,

                    TenNVL = n != null
                        ? (
                            n.XacNhan == true &&
                            !string.IsNullOrWhiteSpace(n.TenNVL_TK)
                                ? n.TenNVL_TK
                                : n.TenNVL_NM
                          )
                        : null,

                    ThoiDiemBD = m.ThoiDiemBD,

                    ThuTu = s != null
                        ? s.ThuTu
                        : (int?)null
                }
            )
            .AsNoTracking()
            .ToListAsync();

            if (!mappings.Any())
                return [];

            return mappings
                .GroupBy(x => x.IDSiLo)
                .Select(g =>
                {
                    // Mapping cuối cùng của silo trong ca
                    var activeRow = g
                        .OrderBy(x => x.ThoiDiemBD ?? DateTime.MinValue)
                        .Last();

                    return new LGNLSiloSnapshotDto
                    {
                        IdSiLo = g.Key ?? 0,

                        TenSiLo = activeRow.TenSiLo,

                        IdNVL = activeRow.IDNVL,

                        TenNVL = activeRow.TenNVL,

                        ThoiDiemBD = activeRow.ThoiDiemBD,

                        DaDoiGiuaCa = g.Count() > 1,

                        ThuTu = activeRow.ThuTu
                    };
                })
                .OrderBy(x => x.ThuTu)
                .ToList();
        }
        // Số ca tối đa lùi lại để tìm mapping (30 ngày x 2 ca) — tránh vòng lặp vô hạn
        // nếu dữ liệu chưa từng được cấu hình.
        private const int MaxShiftLookback = 60;

        // Ca liền trước: Ca1 -> Ca2 hôm trước, Ca2 -> Ca1 cùng ngày.
        private static (DateTime Ngay, int Ca) GetPrevShift(DateTime ngay, int caNum)
        {
            var prevCa = caNum == 1 ? 2 : 1;
            var prevNgay = caNum == 1 ? ngay.Date.AddDays(-1) : ngay.Date;
            return (prevNgay, prevCa);
        }

        // Sao chép mapping Silo→NVL từ ca gần nhất có dữ liệu, thay vì chỉ đúng ca liền kề:
        // nếu ca liền kề cũng chưa có mapping, tiếp tục lùi dần cho đến khi tìm được ca có
        // mapping hoặc hết MaxShiftLookback.
        public async Task<CopyMappingFromPreviousShiftResultDto> CopyMappingFromPreviousShiftAsync(
            int idLoCao, DateTime ngay, int idCa)
        {
            var sourceNgay = ngay.Date;
            var sourceCa = idCa;
            List<LGNLSiloSnapshotDto> mappedSource = [];
            var shiftsSearched = 0;

            for (var i = 0; i < MaxShiftLookback; i++)
            {
                (sourceNgay, sourceCa) = GetPrevShift(sourceNgay, sourceCa);
                shiftsSearched++;

                var snapshot = await GetSiloSnapshotAsync(idLoCao, sourceNgay, sourceCa);
                mappedSource = snapshot.Where(x => x.IdNVL != null).ToList();
                if (mappedSource.Count > 0) break;
            }

            if (mappedSource.Count == 0)
            {
                return new CopyMappingFromPreviousShiftResultDto
                {
                    Found = false,
                    ShiftsSearched = shiftsSearched,
                    Message = $"Không tìm thấy ca nào có mapping trong {MaxShiftLookback} ca gần nhất để sao chép"
                };
            }

            var currentSnapshot = await GetSiloSnapshotAsync(idLoCao, ngay.Date, idCa);
            var alreadyMappedIds = currentSnapshot
                .Where(x => x.IdNVL != null)
                .Select(x => x.IdSiLo)
                .ToHashSet();

            var toCreate = mappedSource.Where(x => !alreadyMappedIds.Contains(x.IdSiLo)).ToList();

            if (toCreate.Count == 0)
            {
                return new CopyMappingFromPreviousShiftResultDto
                {
                    Found = true,
                    SourceNgay = sourceNgay,
                    SourceCa = sourceCa,
                    ShiftsSearched = shiftsSearched,
                    CreatedCount = 0,
                    TotalToCreate = 0,
                    Message = "Tất cả Silo đã được map ở ca hiện tại, không cần sao chép"
                };
            }

            var createdCount = 0;
            foreach (var item in toCreate)
            {
                try
                {
                    await AddMappingAsync(new LG_NL_Mapping
                    {
                        Ngay = ngay.Date,
                        IDCa = idCa,
                        IDLoCao = idLoCao,
                        IDSiLo = item.IdSiLo,
                        IDNVL = item.IdNVL!.Value,
                        GhiChu = null,
                    });
                    createdCount++;
                }
                catch
                {
                    // bỏ qua lỗi từng item, tiếp tục
                }
            }

            return new CopyMappingFromPreviousShiftResultDto
            {
                Found = true,
                SourceNgay = sourceNgay,
                SourceCa = sourceCa,
                ShiftsSearched = shiftsSearched,
                CreatedCount = createdCount,
                TotalToCreate = toCreate.Count,
                Message = $"Đã sao chép {createdCount}/{toCreate.Count} mapping từ Ca {sourceCa} ngày {sourceNgay:dd/MM/yyyy}"
            };
        }

        public async Task<LG_NL_Mapping?> GetMappingByIdAsync(int id)
            => await _context.LG_NL_Mapping.FindAsync(id);

        public async Task<LG_NL_Mapping> AddMappingAsync(LG_NL_Mapping entity)
        {
            entity.NgayTao = DateTime.Now;
            entity.ThoiDiemBD = null; // config đầu ca

            // Mỗi (Ngay, IDCa) là batch độc lập — không expire batch khác.
            // Xóa config cũ của đúng silo này trong cùng (Ngay, IDCa) nếu đang ghi đè
            // Không xóa silo khác → nhiều silo có thể cùng 1 batch (Ngay, IDCa)
            var existing = await _context.LG_NL_Mapping
                .Where(m => m.IDLoCao == entity.IDLoCao
                         && m.Ngay == entity.Ngay
                         && m.IDCa == entity.IDCa
                         && m.IDSiLo == entity.IDSiLo
                         && m.ThoiDiemBD == null)
                .ToListAsync();
            _context.LG_NL_Mapping.RemoveRange(existing);

            await _context.LG_NL_Mapping.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LG_NL_Mapping?> UpdateMappingAsync(int id, LG_NL_Mapping entity)
        {
            var existing = await _context.LG_NL_Mapping.FindAsync(id);
            if (existing == null) return null;

            existing.Ngay = entity.Ngay;
            existing.IDCa = entity.IDCa;
            existing.IDLoCao = entity.IDLoCao;
            existing.IDSiLo = entity.IDSiLo;
            existing.IDNVL = entity.IDNVL;
            existing.GhiChu = entity.GhiChu;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<LG_NL_Mapping> ChangeSiLoNVLAsync(
            int idLoCao, DateTime ngay, int idCa, int idSiLo, int idNVLMoi,
            DateTime thoiDiem, string? ghiChu)
        {
            // Nếu đã có row cùng ThoiDiemBD → đây là sửa lại (correction), update thay vì insert mới
            var existing = await _context.LG_NL_Mapping
                .Where(m => m.IDLoCao == idLoCao
                         && m.Ngay == ngay.Date
                         && m.IDCa == idCa
                         && m.IDSiLo == idSiLo
                         && m.ThoiDiemBD == thoiDiem
                         && m.NgayHetHL == null)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.IDNVL = idNVLMoi;
                existing.GhiChu = ghiChu;
                existing.NgayTao = DateTime.Now;
                await _context.SaveChangesAsync();
                return existing;
            }

            // ThoiDiemBD khác → đổi NVL lần mới, giữ nguyên row cũ (data trước thời điểm này vẫn map đúng NVL cũ)
            var newRow = new LG_NL_Mapping
            {
                IDLoCao = idLoCao,
                Ngay = ngay,
                IDCa = idCa,
                IDSiLo = idSiLo,
                IDNVL = idNVLMoi,
                ThoiDiemBD = thoiDiem,
                NgayHetHL = null,
                IDCaHetHL = null,
                GhiChu = ghiChu,
                NgayTao = DateTime.Now,
            };

            await _context.LG_NL_Mapping.AddAsync(newRow);
            await _context.SaveChangesAsync();
            return newRow;
        }

        public async Task<int> UndoChangeSiLoNVLAsync(int idLoCao, DateTime ngay, int idCa, int idSiLo)
        {
            var midShiftRows = await _context.LG_NL_Mapping
                .Where(m => m.IDLoCao == idLoCao
                         && m.Ngay == ngay.Date
                         && m.IDCa == idCa
                         && m.IDSiLo == idSiLo
                         && m.ThoiDiemBD != null
                         && m.NgayHetHL == null)
                .ToListAsync();

            if (midShiftRows.Count == 0) return 0;

            _context.LG_NL_Mapping.RemoveRange(midShiftRows);
            await _context.SaveChangesAsync();
            return midShiftRows.Count;
        }

        public async Task<bool> DeleteMappingAsync(int id)
        {
            var existing = await _context.LG_NL_Mapping.FindAsync(id);
            if (existing == null) return false;
            _context.LG_NL_Mapping.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── Nhóm NVL ────────────────────────────────────────────────────────

        public async Task<List<LG_NL_NhomNVL>> GetNhomNvlListAsync(int? idLoCao)
        {
            return await _context.LG_NL_NhomNVL
                .OrderBy(x => x.ThuTu)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<LG_NL_NhomNVL?> GetNhomNvlByIdAsync(int id)
            => await _context.LG_NL_NhomNVL.FindAsync(id);

        public async Task<LG_NL_NhomNVL> AddNhomNvlAsync(LG_NL_NhomNVL entity)
        {
            entity.NgayTao = DateTime.Now;
            await _context.LG_NL_NhomNVL.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LG_NL_NhomNVL?> UpdateNhomNvlAsync(int id, LG_NL_NhomNVL entity)
        {
            var existing = await _context.LG_NL_NhomNVL.FindAsync(id);
            if (existing == null) return null;

            existing.IDLoCao = entity.IDLoCao;
            existing.TenNhom = entity.TenNhom;
            existing.ThuTu = entity.ThuTu;
            existing.GhiChu = entity.GhiChu;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteNhomNvlAsync(int id)
        {
            var existing = await _context.LG_NL_NhomNVL.FindAsync(id);
            if (existing == null) return false;
            _context.LG_NL_NhomNVL.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }

        // ─── NVL ─────────────────────────────────────────────────────────────

        public async Task<List<LGNLNvlDto>> GetNvlListAsync(int? idLoCao)
        {
            return await (
                from n in _context.LG_NL_NVL
                join nh in _context.LG_NL_NhomNVL
                    on n.IDNhomNVL equals nh.ID into nhomGroup
                from nh in nhomGroup.DefaultIfEmpty()
                where (idLoCao == null || n.IDLoCao == idLoCao) && n.IsDelete != true
                select new LGNLNvlDto
                {
                    Id = n.ID,
                    IdLoCao = n.IDLoCao,
                    IdNhomNVL = n.IDNhomNVL,
                    TenNVL_NM = n.TenNVL_NM,
                    ThuTu = n.ThuTu,
                    GhiChu = n.GhiChu,
                    NgayTao = n.NgayTao,
                    NhomHienThi = nh != null ? nh.TenNhom : null,
                    ThuTuNhom = nh != null ? nh.ThuTu : null,
                    TenNVL_TK = n.TenNVL_TK,
                    XacNhan = n.XacNhan
                }
            ).AsNoTracking().ToListAsync();
        }

        public async Task<LG_NL_NVL?> GetNvlByIdAsync(int id)
            => await _context.LG_NL_NVL.FindAsync(id);

        public async Task<LG_NL_NVL> AddNvlAsync(LG_NL_NVL entity)
        {
            entity.NgayTao = DateTime.Now;
            await _context.LG_NL_NVL.AddAsync(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public async Task<LG_NL_NVL?> UpdateNvlAsync(int id, LG_NL_NVL entity)
        {
            var existing = await _context.LG_NL_NVL.FindAsync(id);
            if (existing == null) return null;

            existing.IDLoCao = entity.IDLoCao;
            existing.IDNhomNVL = entity.IDNhomNVL;
            existing.TenNVL_NM = entity.TenNVL_NM;
            existing.TenNVL_TK = entity.TenNVL_TK;
            existing.XacNhan = entity.XacNhan;
            existing.ThuTu = entity.ThuTu;
            existing.GhiChu = entity.GhiChu;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteNvlAsync(int id)
        {
            var existing = await _context.LG_NL_NVL.FindAsync(id);
            if (existing == null) return false;
            existing.IsDelete = true;
            await _context.SaveChangesAsync();
            return true;
        }
       
        (DateTime timeFrom, DateTime timeTo) GetTimeRangeByCa(DateTime ngay, int idCa) => idCa switch
        {
            // Ca ngày: 07:31 -> 19:31 (loại trừ)
            //1 => (
            //    ngay.Date.AddHours(7).AddMinutes(31),
            //    ngay.Date.AddHours(19).AddMinutes(31)
            //),
            1 => (
                ngay.Date.AddHours(7).AddMinutes(30).AddSeconds(1),
                ngay.Date.AddHours(19).AddMinutes(30)
            ),

            // Ca đêm: 19:31 -> 07:31 hôm sau (loại trừ)
            //2 => (
            //    ngay.Date.AddHours(19).AddMinutes(31),
            //    ngay.Date.AddDays(1).AddHours(7).AddMinutes(31)
            //),
            2 => (
                ngay.Date.AddHours(19).AddMinutes(30).AddSeconds(1),
                ngay.Date.AddDays(1).AddHours(7).AddMinutes(30)
            ),

            _ => throw new ArgumentOutOfRangeException(nameof(idCa), $"Ca không hợp lệ: {idCa}")
        };
        // Khoảng cách tối đa (giây) giữa nhóm không có TS0 và mẻ có TS0 để coi là cùng 1 mẻ
        private const int SoGiayToiDaGopMe = 120;


        private static void GopMeBiTach(List<LG1_DuLieuNL> duLieuTho)
        {
            var theoGio = duLieuTho.Where(bg => bg.Time != null)
                .GroupBy(bg => bg.Time!.Value)
                .ToDictionary(nhom => nhom.Key, nhom => nhom.ToList());

            var gioCoTS0 = theoGio
                .Where(cap => cap.Value.Any(bg => bg.TagKey == "TS0"))
                .Select(cap => cap.Key)
                .OrderBy(gio => gio)
                .ToList();
            if (gioCoTS0.Count == 0) return;

            var tagTheoMeDich = gioCoTS0.ToDictionary(
                gio => gio, gio => theoGio[gio].Select(bg => bg.TagKey).ToHashSet());

            foreach (var nhomMoCoi in theoGio.Where(cap => !gioCoTS0.Contains(cap.Key)).OrderBy(cap => cap.Key))
            {
                var gioSau = gioCoTS0.FirstOrDefault(gio => gio > nhomMoCoi.Key);
                var gioTruoc = gioCoTS0.LastOrDefault(gio => gio < nhomMoCoi.Key);

                var gioUngVien = new List<DateTime>();
                if (gioSau != default && (gioSau - nhomMoCoi.Key).TotalSeconds <= SoGiayToiDaGopMe) gioUngVien.Add(gioSau);
                if (gioTruoc != default && (nhomMoCoi.Key - gioTruoc).TotalSeconds <= SoGiayToiDaGopMe) gioUngVien.Add(gioTruoc);

                var tagMoCoi = nhomMoCoi.Value.Select(bg => bg.TagKey).ToHashSet();
                foreach (var gioDich in gioUngVien)
                {
                    if (tagTheoMeDich[gioDich].Overlaps(tagMoCoi)) continue;

                    foreach (var bg in nhomMoCoi.Value) bg.Time = gioDich;
                    tagTheoMeDich[gioDich].UnionWith(tagMoCoi);
                    break;
                }
            }
        }

        // Số phút đọc lùi trước mốc đầu ca để lấy TS0 ngay trước mốc (dùng phân xử mốc giao ca)
        private const int SoPhutDocLuiTruocMoc = 30;

        /// <summary>
        private static List<LG1_DuLieuNL> LocBanGhiMocGiaoCa(
            List<LG1_DuLieuNL> duLieuTho, DateTime tuLuc, DateTime denLuc)
        {
            var mocDauCa = tuLuc.AddSeconds(-1); // 07:30:00 / 19:30:00 (đầu ca)
            var mocCuoiCa = denLuc;                    // 19:30:00 / 07:30:00 (cuối ca)

            var banGhiTS0 = duLieuTho.Where(bg => bg.TagKey == "TS0" && bg.Time != null && bg.Value != null).ToList();

            // true = mốc "gio" thuộc ca cũ (TS0 nối tiếp), false = thuộc ca mới (TS0 đã reset)
            // null = mốc không có TS0 để phân xử
            bool? ThuocCaCu(DateTime gio)
            {
                var hienTai = banGhiTS0.FirstOrDefault(bg => bg.Time == gio);
                if (hienTai == null) return null;
                var truoc = banGhiTS0.Where(bg => bg.Time < gio).OrderBy(bg => bg.Time).LastOrDefault();
                return truoc == null || hienTai.Value >= truoc.Value;
            }

            // Đầu ca: chỉ nhận mốc thuộc ca mới (không có TS0 hoặc không có mẻ trước thì không nhận)
            var giuMocDau = ThuocCaCu(mocDauCa) == false;
            // Cuối ca: nhận mốc thuộc ca cũ (không có TS0 thì giữ như cách tính cũ)
            var giuMocCuoi = ThuocCaCu(mocCuoiCa) != false;

            var ketQua = duLieuTho.Where(bg => bg.Time >= tuLuc && bg.Time < mocCuoiCa).ToList();
            if (giuMocDau) ketQua.AddRange(duLieuTho.Where(bg => bg.Time == mocDauCa));
            if (giuMocCuoi) ketQua.AddRange(duLieuTho.Where(bg => bg.Time == mocCuoiCa));
            return ketQua.OrderBy(bg => bg.Time).ToList();
        }

        public async Task<LGNLDuLieuSiLoResult> GetDuLieuSiloPivotAsync(DateTime ngay, int idCa, int idLoCao)
        {
            // 1. Lấy danh sách nhóm
            var allNhom = await _context.LG_NL_NhomNVL
                .OrderBy(nh => nh.ThuTu)
                .AsNoTracking()
                .ToListAsync();

            // 2. Kiểm tra có mapping đúng ngày/ca này không (không fallback sang ngày/ca khác)
            var hasMapping = await _context.LG_NL_Mapping
                .AnyAsync(m => m.IDLoCao == idLoCao
                            && m.ThoiDiemBD == null
                            && m.Ngay == ngay.Date
                            && m.IDCa == idCa);

            if (!hasMapping)
                return new LGNLDuLieuSiLoResult { Columns = new(), Rows = new() };

            // 3. Lấy mapping đúng ngày/ca
            var mappings = await (
                from m in _context.LG_NL_Mapping
                join s in _context.LG_NL_SiLo.Where(x => x.IsDelete != true) on m.IDSiLo equals s.ID
                join n in _context.LG_NL_NVL on m.IDNVL equals n.ID
                join nh in _context.LG_NL_NhomNVL on n.IDNhomNVL equals nh.ID into nhg
                from nh in nhg.DefaultIfEmpty()
                where m.IDLoCao == idLoCao
                   && m.Ngay == ngay.Date
                   && m.IDCa == idCa
                   && n.IsDelete != true
                // Bỏ điều kiện s.TagKey != null — silo không có tagKey vẫn được đưa vào cột
                // để người dùng có thể nhập tay (không có data SCADA tự động)
                orderby nh != null ? nh.ThuTu : 999,
                        s.ThuTu,
                        m.ThoiDiemBD == null ? 0 : 1,
                        m.ThoiDiemBD
                select new
                {
                    TagKey = s.TagKey,       // có thể null khi silo chưa cấu hình tagKey
                    IDNVL = n.ID,
                    IDNhomNVL = n.IDNhomNVL,
                    ThoiDiemBD = m.ThoiDiemBD,
                    TenNVL = n.XacNhan == true && n.TenNVL_TK != null
                        ? n.TenNVL_TK
                        : n.TenNVL_NM,
                    TenNhom = nh != null ? nh.TenNhom : null,
                    ThuTuNhom = nh != null ? (nh.ThuTu ?? 999) : 999,
                }
            ).AsNoTracking().ToListAsync();

            if (mappings.Count == 0)
                return new LGNLDuLieuSiLoResult
                {
                    Columns = new(),
                    Rows = new(),
                    NgayHieuLuc = ngay.Date
                };

            // 4. Build NVL từ mapping
            var mappingNvl = mappings
                .GroupBy(x => x.IDNVL)
                .Select(g => g.First())
                .ToList();

            // ═══════════════════════════════════════════════════════
            // 5. Build columns với slot cố định theo ThuTu của nhóm
            // ═══════════════════════════════════════════════════════

            // Key = ThuTu của nhóm, Value = số slot cố định
            var slotConfig = new Dictionary<int, int>
            {
                { 1, 2 }, // Quặng thiêu kết → 2 slots
                { 2, 2 }, // Quặng vê viên   → 2 slots
                { 3, 2 }, // Quặng sống      → 2 slots
                { 4, 2 }, // Than cốc        → 2 slots
                { 5, 1 }, // Cốc vụn         → 1 slot
                { 6, 2 }, // Loại khác       → 2 slots
            };

            var columns = new List<LGNLColumnDto>();

            foreach (var nhom in allNhom)
            {
                var children = mappingNvl
                    .Where(x => x.IDNhomNVL == nhom.ID)
                    .OrderBy(x => x.IDNVL)
                    .Select(n => new LGNLColumnDto
                    {
                        Title = n.TenNVL ?? n.IDNVL.ToString(),
                        DataIndex = n.IDNVL.ToString(),
                        IsEmpty = false
                    })
                    .ToList();

                // Lấy slot count theo ThuTu, mặc định 1 nếu không có config
                var slotCount = slotConfig.GetValueOrDefault(nhom.ThuTu ?? 99, 1);

                // Pad thêm slot rỗng nếu thiếu
                while (children.Count < slotCount)
                {
                    children.Add(new LGNLColumnDto
                    {
                        Title = "",
                        DataIndex = $"__empty_{nhom.ID}_{children.Count + 1}",
                        IsEmpty = true
                    });
                }

                columns.Add(new LGNLColumnDto
                {
                    Title = nhom.TenNhom ?? $"nhom_{nhom.ID}",
                    Children = children
                });
            }

            // 6. Thời gian SCADA
            var (timeFrom, timeTo) = GetTimeRangeByCa(ngay, idCa);

            // 7. Timeline mapping
            // Chỉ đưa vào tagTimeline những mapping có TagKey — dùng để query SCADA
            // Silo không có tagKey vẫn xuất hiện trong cột nhưng không có data tự động
            var tagTimeline = mappings
                .Where(m => m.TagKey != null)
                .GroupBy(m => m.TagKey!)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(m => m.ThoiDiemBD ?? DateTime.MinValue)
                          .Select(m => (
                              From: m.ThoiDiemBD ?? timeFrom,
                              DataIndex: m.IDNVL.ToString(),
                              m.IDNVL
                          ))
                          .ToList()
                );

            string? ResolveDataIndex(string tagKey, DateTime time)
            {
                if (!tagTimeline.TryGetValue(tagKey, out var tl)) return null;
                var seg = tl.LastOrDefault(t => t.From <= time);
                return seg.DataIndex;
            }

            // 8. Query SCADA
            var tagKeys = tagTimeline.Keys.Append("TS0").Distinct().ToList();
            var rawData = await _context.LG1_DuLieuNL
                .Where(d => d.ID_LoCao == idLoCao
                         && d.Time >= timeFrom.AddSeconds(-1).AddMinutes(-SoPhutDocLuiTruocMoc)
                         && d.Time <= timeTo
                         && tagKeys.Contains(d.TagKey!))
                .OrderBy(d => d.Time)
                .AsNoTracking()
                .ToListAsync();

            // 8a. Một mẻ có thể bị SCADA ghi tách làm 2 thời điểm (vd lò 5: TS9..TS16 lúc 19:10:00 không
            // có TS0, phần còn lại + TS0 lúc 19:11:00). Gộp nhóm "mồ côi" vào mẻ có TS0 gần nhất.
            GopMeBiTach(rawData);

            // 8b. Bản ghi nằm đúng mốc giao ca (07:30:00 / 19:30:00) có thể là mẻ CUỐI của ca
            // trước (TS0 = 67) hoặc mẻ ĐẦU của ca sau (TS0 reset về 1). Phân xử theo số mẻ (TS0).
            rawData = LocBanGhiMocGiaoCa(rawData, timeFrom, timeTo);

            // 9. Pivot
            var rows = rawData
                .GroupBy(d => d.Time)
                .OrderBy(g => g.Key)
                .Select((g, idx) =>
                {
                    var row = new Dictionary<string, object?>
                    {
                        ["id"] = idx + 1,
                        ["time"] = g.Key?.ToString("yyyy-MM-ddTHH:mm:ss")
                    };

                    foreach (var d in g)
                    {
                        if (d.TagKey == "TS0") { row["soMe"] = d.Value; continue; }
                        if (d.TagKey == null || d.Time == null) continue;

                        var di = ResolveDataIndex(d.TagKey, d.Time.Value);
                        if (di == null) continue;

                        row[di] = row.TryGetValue(di, out var cur)
                            ? Convert.ToDecimal(cur) + Convert.ToDecimal(d.Value ?? 0f)
                            : Convert.ToDecimal(d.Value ?? 0f);
                    }

                    return row;
                })
                .ToList();

            return new LGNLDuLieuSiLoResult
            {
                Columns = columns,
                Rows = rows,
                NgayHieuLuc = ngay.Date
            };
        }
        private static void FillEmptyPlaceholders(List<LGNLColumnDto> columns)
        {
            foreach (var col in columns.Where(c => c.Children != null && c.Children.Count == 0))
                col.Children!.Add(new LGNLColumnDto
                {
                    Title = "—",
                    DataIndex = $"_empty_{col.Title}",
                });
        }

        // ─── Chi tiết nạp liệu theo phiếu ────────────────────────────────────────

        public async Task DeleteChiTietByPhieuIdAsync(Guid idPhieu)
        {
            await _context.LG_NL_ChiTiet
                .Where(x => x.IDPhieu == idPhieu)
                .ExecuteDeleteAsync();
        }

        public async Task AddChiTietRangeAsync(List<LG_NL_ChiTiet> entities)
        {
            await _context.LG_NL_ChiTiet.AddRangeAsync(entities);
            await _context.SaveChangesAsync();
        }

        // Xóa + ghi mới trong cùng 1 transaction — nếu insert lỗi giữa chừng thì rollback
        // luôn phần xóa, tránh mất trắng dữ liệu chi tiết của phiếu (trước đây xóa và ghi
        // là 2 lệnh tách rời, lỗi ở bước ghi sẽ để lại phiếu không còn chi tiết nào).
        public async Task ReplaceChiTietAsync(Guid idPhieu, List<LG_NL_ChiTiet> entities)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.LG_NL_ChiTiet
                    .Where(x => x.IDPhieu == idPhieu)
                    .ExecuteDeleteAsync();

                if (entities.Count > 0)
                {
                    await _context.LG_NL_ChiTiet.AddRangeAsync(entities);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<LGNLChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu)
        {
            return await _context.LG_NL_ChiTiet
                .Where(x => x.IDPhieu == idPhieu)
                .OrderBy(x => x.SoMe)
                .ThenBy(x => x.ThuTu)
                .ThenBy(x => x.IDNVL)
                .Select(x => new LGNLChiTietDto
                {
                    Id = x.ID,
                    IdPhieu = x.IDPhieu,
                    IdLoCao = x.IDLoCao,
                    Ngay = x.Ngay,
                    IdCa = x.IDCa,
                    ThoiGianNapLieu = x.ThoiGianNapLieu,
                    SoMe = x.SoMe,
                    MeGio = x.MeGio,
                    CheDo = x.CheDo,
                    ThuocThamLieu1 = x.ThuocThamLieu1,
                    ThuocThamLieu2 = x.ThuocThamLieu2,
                    GhiChu = x.GhiChu,
                    IdNVL = x.IDNVL,
                    GiaTri = x.GiaTri,
                    ThuTu = x.ThuTu,
                    DoAm = x.DoAm,
                    QuyKho = x.QuyKho,
                    ManualGiaTri = x.ManualGiaTri,
                    GiaTri_Goc = x.GiaTri_Goc,
                })
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<BmPhieu?> GetPhieuByIdAsync(Guid idPhieu)
            => await _context.BmPhieus.FindAsync(idPhieu);

        public async Task<List<LGNLDuLieuScadaDto>> GetDataByFilterAsync(
             int? idLoCao, DateTime? ngayBatDau, DateTime? ngayKetThuc)
        {
            // Query dữ liệu thô từ LG1_DuLieuNL
            var query = from d in _context.LG1_DuLieuNL
                        where
                              (idLoCao == null || d.ID_LoCao == idLoCao) &&
                              (ngayBatDau == null || d.Time >= ngayBatDau) &&
                              (ngayKetThuc == null || d.Time < ngayKetThuc)
                        orderby d.Time
                        select new LGNLDuLieuScadaDto
                        {
                            Id = d.ID,
                            TagName = d.TagName,
                            TagKey = d.TagKey,
                            Time = d.Time,
                            Value = d.Value,
                            IdLoCao = d.ID_LoCao
                        };

            return await query.AsNoTracking().ToListAsync();
        }

        public async Task<List<int>> GetNvlIdsByNhomAsync(IEnumerable<int> nhomNVLIds, int idLoCao)
        {
            return await _context.LG_NL_NVL
                .Where(n => n.IDNhomNVL != null
                         && nhomNVLIds.Contains(n.IDNhomNVL.Value)
                         && n.IDLoCao == idLoCao
                         && n.IsDelete != true)
                .OrderBy(n => n.ThuTu)
                .AsNoTracking()
                .Select(n => n.ID)
                .ToListAsync();
        }
    }
}
