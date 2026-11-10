using dataproduct.api.DTOs.NMLC;
using dataproduct.api.Models;
using dataproduct.api.Models.NMLC;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // "Gán" dữ liệu cân tự động (LC_CanTuDongRawLog) vào cột KhoiLuongAmThucTe của TabCapLieuSilo —
    // xem SECTION 27-28 .claude/migrations/LuyenCoc_Migration/lc_bcxs_xnl_tp.sql và
    // [[lc-bcsx-xnl-cantudong-design]]. Ghi NGAY vào DB khi bấm "Gán", KHÔNG qua nút Lưu phiếu.
    public class LC_CanTuDongRepository : ILC_CanTuDongRepository
    {
        private readonly ProductFormContext _context;

        public LC_CanTuDongRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<List<LcCanTuDongRawLogDto>> GetRawLogByPhieuAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Now);
            var ca = phieu.Ca ?? 0;

            var rows = await _context.LC_CanTuDongRawLog.AsNoTracking()
                .Where(x => x.Scope == scope && x.NgaySX == ngaySx && x.Ca == ca)
                .OrderBy(x => x.NgayGio)
                .ToListAsync();

            return rows.Select(ToDto).ToList();
        }

        public async Task<LcCanTuDongGanResultDto> GanAsync(Guid idPhieu, LcCanTuDongGanRequestDto dto)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể gán.");

            if (dto.RawLogIds == null || dto.RawLogIds.Count == 0)
                throw new InvalidOperationException("Vui lòng chọn ít nhất 1 dòng dữ liệu cân.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Now);
            var ca = phieu.Ca ?? 0;

            var rawLogRows = await _context.LC_CanTuDongRawLog
                .Where(x => dto.RawLogIds.Contains(x.ID))
                .ToListAsync();
            if (rawLogRows.Count != dto.RawLogIds.Count
                || rawLogRows.Any(x => x.Scope != scope || x.NgaySX != ngaySx || x.Ca != ca))
                throw new InvalidOperationException("Một số dòng dữ liệu cân không hợp lệ hoặc không thuộc đúng kíp này.");
            if (rawLogRows.Any(x => x.IsDaGan))
                throw new InvalidOperationException("Một số dòng dữ liệu cân đã được gán trước đó.");

            var silo = await _context.LC_Silo.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Scope == scope && s.SiloSo == dto.SiloSo && !s.IsDelete)
                ?? throw new InvalidOperationException("Không tìm thấy silo.");
            var stt = (byte)Math.Max(1, dto.Stt);

            var capLieu = await _context.LC_CapLieu
                .FirstOrDefaultAsync(x => x.IDPhieu == idPhieu && x.ID_Silo == silo.ID && x.Stt == stt);
            if (capLieu != null && capLieu.KhoiLuongAmThucTeCanTuDong != null)
                throw new InvalidOperationException("Dòng Cấp liệu này đã được gán cân tự động trước đó.");

            var sum = Math.Round(rawLogRows.Sum(x => x.KhoiLuong), 3);

            if (capLieu == null)
            {
                // Lớp liệu (Stt>1 "Tách liệu") chỉ tồn tại ở state FE cho tới khi Lưu phiếu —
                // tạo skeleton ngay để Gán ghi được, mirror nhánh "dòng mới" của SaveTabAsync.
                capLieu = new LC_CapLieu
                {
                    IDPhieu = idPhieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    ID_Silo = silo.ID,
                    SiloSo = silo.SiloSo,
                    Stt = stt,
                    TonDauKyQuyKho = 0,
                    NgayTao = DateTime.Now,
                };
                _context.LC_CapLieu.Add(capLieu);
            }
            capLieu.KhoiLuongAmThucTe = sum;
            capLieu.KhoiLuongAmThucTeCanTuDong = sum;

            foreach (var row in rawLogRows)
                row.IsDaGan = true;

            // Cần capLieu.ID (DB-generated) để ghi junction — lưu trước 1 lần cho dòng mới có ID.
            await _context.SaveChangesAsync();

            foreach (var row in rawLogRows)
                _context.LC_CanTuDongGanChiTiet.Add(new LC_CanTuDongGanChiTiet
                {
                    ID_CapLieu = capLieu.ID,
                    ID_RawLog = row.ID,
                    NgayTao = DateTime.Now,
                });
            await _context.SaveChangesAsync();

            return new LcCanTuDongGanResultDto
            {
                SiloSo = silo.SiloSo,
                Stt = stt,
                KhoiLuongAmThucTe = sum,
                KhoiLuongAmThucTeCanTuDong = sum,
                RawLogRows = await GetRawLogByPhieuAsync(idPhieu),
            };
        }

        public async Task<List<LcCanTuDongRawLogDto>> BoGanAsync(Guid idPhieu, LcCanTuDongBoGanRequestDto dto)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể bỏ gán.");

            var scope = phieu.Scope ?? 0;
            var stt = (byte)Math.Max(1, dto.Stt);

            var silo = await _context.LC_Silo.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Scope == scope && s.SiloSo == dto.SiloSo && !s.IsDelete)
                ?? throw new InvalidOperationException("Không tìm thấy silo.");

            var capLieu = await _context.LC_CapLieu
                .FirstOrDefaultAsync(x => x.IDPhieu == idPhieu && x.ID_Silo == silo.ID && x.Stt == stt)
                ?? throw new InvalidOperationException("Không tìm thấy dòng Cấp liệu.");
            if (capLieu.KhoiLuongAmThucTeCanTuDong == null)
                throw new InvalidOperationException("Dòng Cấp liệu này chưa từng được gán cân tự động.");

            var chiTietRows = await _context.LC_CanTuDongGanChiTiet
                .Where(x => x.ID_CapLieu == capLieu.ID)
                .ToListAsync();
            var rawLogIds = chiTietRows.Select(x => x.ID_RawLog).ToList();

            var rawLogRows = await _context.LC_CanTuDongRawLog
                .Where(x => rawLogIds.Contains(x.ID))
                .ToListAsync();
            foreach (var row in rawLogRows)
                row.IsDaGan = false;

            _context.LC_CanTuDongGanChiTiet.RemoveRange(chiTietRows);

            capLieu.KhoiLuongAmThucTe = null;
            capLieu.KhoiLuongAmThucTeCanTuDong = null;

            await _context.SaveChangesAsync();

            return await GetRawLogByPhieuAsync(idPhieu);
        }

        private static LcCanTuDongRawLogDto ToDto(LC_CanTuDongRawLog x) => new()
        {
            Id = x.ID,
            NgayGio = x.NgayGio,
            TenCan = x.TenCan,
            LoaiLieuTen = x.LoaiLieuTen,
            KhoiLuong = x.KhoiLuong,
            IsDaGan = x.IsDaGan,
        };
    }
}
