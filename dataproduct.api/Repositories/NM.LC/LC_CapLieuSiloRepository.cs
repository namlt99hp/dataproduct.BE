using System.Data;
using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tab "Cấp liệu & Silo" của LC_BCSX_XNL_TP — gộp LC_CapLieu + LC_TonSilo (1 dòng/lớp
    // liệu, xem TabCapLieuSilo.tsx) + LC_KetQuaPhanTich (Cỡ hạt sau nghiền 1).
    //
    // KHÔNG dùng BmPhieu.DataJson cho tab này (quyết định 2026-10-19 — lưu JSON rủi ro mất
    // đồng bộ, hai nguồn dữ liệu khác nhau). Đọc/lưu đi thẳng vào bảng quan hệ qua
    // LC_CapLieuSiloController (GET/PUT riêng), độc lập với luồng Phiếu generic. Initializer
    // chỉ còn nhiệm vụ DUY NHẤT: copy forward từ kíp liền trước lúc phiếu được tạo.
    //
    // TÁCH LIỆU (2026-10-21): 1 SiloSo có thể có NHIỀU dòng LC_CapLieu (Stt=1 lớp gốc,
    // Stt>1 lớp tách thêm qua nút "Tách liệu" ở FE) — khoá thật là (ID_Silo, Stt), không
    // còn giả định 1 dòng/silo như bản trước. LC_TonSilo vẫn đúng 1 dòng/silo (đo vật lý,
    // không phân biệt lớp) — chỉ áp giá trị tồn-silo (Dto 4 cột AmSiloCuoiKy/KLAmTonCuoi/
    // DoAmSilo/KLTonCuoiQuyKho) cho dòng Stt=1 của mỗi silo.
    public class LC_CapLieuSiloRepository : ILC_CapLieuSiloRepository
    {
        private readonly ProductFormContext _context;

        public LC_CapLieuSiloRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<LcCapLieuSiloTabDto> GetTabAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            var scope = phieu.Scope ?? 0;

            var silos = await _context.LC_Silo.AsNoTracking()
                .Where(s => s.Scope == scope && !s.IsDelete)
                .OrderBy(s => s.SiloSo)
                .ToListAsync();

            var capLieuRows = await _context.LC_CapLieu.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .ToListAsync();
            var capLieuBySilo = capLieuRows
                .GroupBy(x => x.ID_Silo)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Stt).ToList());

            var tonSiloBySilo = await _context.LC_TonSilo.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.ID_Silo);

            var result = new LcCapLieuSiloTabDto();
            foreach (var silo in silos)
            {
                tonSiloBySilo.TryGetValue(silo.ID, out var ts);
                if (!capLieuBySilo.TryGetValue(silo.ID, out var layers) || layers.Count == 0)
                {
                    // Chưa từng lưu gì cho silo này — 1 dòng skeleton Stt=1 (giữ hành vi cũ).
                    result.CapLieu.Add(new LcCapLieuSiloRowDto
                    {
                        SiloSo = silo.SiloSo,
                        Stt = 1,
                        AmSiloCuoiKy = ts?.AmSiloCuoiKy,
                        KLAmTonCuoi = ts?.KLAmTonCuoi,
                        DoAmSilo = ts?.DoAmSilo,
                        KLTonCuoiQuyKho = ts?.KLTonCuoiQuyKho,
                    });
                    continue;
                }

                foreach (var cl in layers)
                {
                    result.CapLieu.Add(new LcCapLieuSiloRowDto
                    {
                        SiloSo = silo.SiloSo,
                        Stt = cl.Stt,
                        IdVatTu = cl.ID_VatTu,
                        VatLieuTen = cl.VatLieuTen,
                        IdMaLo = cl.ID_MaLo,
                        SoHieuLo = cl.SoHieuLo,
                        KhoiLuongKeHoach = cl.KhoiLuongKeHoach,
                        KhoiLuongAmThucTe = cl.KhoiLuongAmThucTe,
                        DoAm = cl.DoAm,
                        KhoiLuongKhoThucTe = cl.KhoiLuongKhoThucTe,
                        LuyKeQuyKho = cl.LuyKeQuyKho,
                        TonDauKyQuyKho = cl.TonDauKyQuyKho,
                        // Tồn-silo (vật lý, 1 lần/silo) chỉ gắn vào lớp Stt=1 — các lớp khác để NULL.
                        AmSiloCuoiKy = cl.Stt == 1 ? ts?.AmSiloCuoiKy : null,
                        KLAmTonCuoi = cl.Stt == 1 ? ts?.KLAmTonCuoi : null,
                        DoAmSilo = cl.Stt == 1 ? ts?.DoAmSilo : null,
                        KLTonCuoiQuyKho = cl.Stt == 1 ? ts?.KLTonCuoiQuyKho : null,
                        GhiChu = cl.GhiChu,
                    });
                }
            }

            var coHatGroups = new[] { "CO_HAT_1MM", "CO_HAT_3MM" };
            var ketQuaRows = await _context.LC_KetQuaPhanTich.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu && coHatGroups.Contains(x.NhomPhanTich))
                .ToListAsync();

            foreach (var nhom in coHatGroups)
            {
                var rows = ketQuaRows.Where(x => x.NhomPhanTich == nhom).ToDictionary(x => x.LanPhanTich);
                rows.TryGetValue((byte)1, out var l1);
                rows.TryGetValue((byte)2, out var l2);
                rows.TryGetValue((byte)3, out var l3);
                result.CoHatNghien1.Add(new LcCoHatNghien1RowDto
                {
                    NhomPhanTich = nhom,
                    Lan1 = l1?.GiaTri,
                    Lan2 = l2?.GiaTri,
                    Lan3 = l3?.GiaTri,
                    GhiChu = l1?.GhiChu ?? l2?.GhiChu ?? l3?.GhiChu,
                });
            }

            return result;
        }

        public async Task<LcCapLieuSiloTabDto> SaveTabAsync(Guid idPhieu, LcCapLieuSiloTabDto dto)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Now);
            var ca = phieu.Ca ?? 0;

            var siloBySo = await _context.LC_Silo.AsNoTracking()
                .Where(s => s.Scope == scope && !s.IsDelete)
                .ToDictionaryAsync(s => s.SiloSo);

            var existingCapLieu = await _context.LC_CapLieu
                .Where(x => x.IDPhieu == idPhieu)
                .ToListAsync();
            var existingCapLieuByKey = existingCapLieu.ToDictionary(x => (x.ID_Silo, x.Stt));

            var existingTonSiloBySilo = await _context.LC_TonSilo
                .Where(x => x.IDPhieu == idPhieu)
                .ToDictionaryAsync(x => x.ID_Silo);

            var incomingKeys = new HashSet<(int ID_Silo, byte Stt)>();

            foreach (var row in dto.CapLieu)
            {
                if (!siloBySo.TryGetValue(row.SiloSo, out var silo)) continue;
                var stt = (byte)Math.Max(1, row.Stt);
                incomingKeys.Add((silo.ID, stt));

                // F01 — backend tự tính, KHÔNG dùng KhoiLuongKhoThucTe client gửi lên.
                var khoThucTe = ComputeKhoiLuongKhoThucTe(row.KhoiLuongAmThucTe, row.DoAm);

                if (existingCapLieuByKey.TryGetValue((silo.ID, stt), out var cl))
                {
                    // null = "không đổi" — giữ nguyên ID_VatTu/ID_MaLo đã có (vd dòng kế thừa
                    // từ kíp trước mà người dùng chưa đụng tới), tránh ghi đè mất dữ liệu.
                    if (row.IdVatTu != null) { cl.ID_VatTu = row.IdVatTu; cl.VatLieuTen = row.VatLieuTen; }
                    if (row.IdMaLo != null) { cl.ID_MaLo = row.IdMaLo; cl.SoHieuLo = row.SoHieuLo; }
                    cl.KhoiLuongKeHoach = row.KhoiLuongKeHoach;
                    cl.KhoiLuongAmThucTe = row.KhoiLuongAmThucTe;
                    cl.DoAm = row.DoAm;
                    cl.KhoiLuongKhoThucTe = khoThucTe;
                    cl.GhiChu = row.GhiChu;
                }
                else
                {
                    // Dòng mới — lớp vừa "Tách liệu" ở FE, hoặc phòng trường hợp gọi Save
                    // trước khi initializer từng chạy (dữ liệu cũ). TonDauKyQuyKho = 0 vì SP
                    // ngay dưới sẽ set lại nếu khớp đúng lô ở kíp trước (lớp mới thì không).
                    var newCl = new LC_CapLieu
                    {
                        IDPhieu = idPhieu,
                        NgaySX = phieu.NgaySX,
                        Ca = phieu.Ca,
                        Scope = scope,
                        ID_Silo = silo.ID,
                        SiloSo = silo.SiloSo,
                        Stt = stt,
                        ID_VatTu = row.IdVatTu,
                        VatLieuTen = row.VatLieuTen,
                        ID_MaLo = row.IdMaLo,
                        SoHieuLo = row.SoHieuLo,
                        KhoiLuongKeHoach = row.KhoiLuongKeHoach,
                        KhoiLuongAmThucTe = row.KhoiLuongAmThucTe,
                        DoAm = row.DoAm,
                        KhoiLuongKhoThucTe = khoThucTe,
                        TonDauKyQuyKho = 0,
                        GhiChu = row.GhiChu,
                        NgayTao = DateTime.Now,
                    };
                    _context.LC_CapLieu.Add(newCl);
                    existingCapLieuByKey[(silo.ID, stt)] = newCl;
                }

                // Tồn-silo (vật lý, 1 dòng/silo) chỉ nhận giá trị từ lớp Stt=1 — các lớp khác
                // (Stt>1) không chạm vào LC_TonSilo (FE đã khoá nhập ở các cột này rồi).
                if (stt != 1) continue;

                // F05a (TP1 only — TP2 dùng F05b, CHƯA có công thức, OQ-03 còn mở cho TP2) +
                // F01 áp lại cho (KLAmTonCuoi, DoAmSilo) — backend tự tính, bỏ qua giá trị FE
                // gửi (các cột này đã readonly ở FE từ trước, chỉ là echo lại giá trị cũ).
                var klAmTonCuoi = scope == 1 ? ComputeKLAmTonCuoi_TP1(row.AmSiloCuoiKy) : null;
                var klTonCuoiQuyKho = ComputeKhoiLuongKhoThucTe(klAmTonCuoi, row.DoAmSilo);

                if (existingTonSiloBySilo.TryGetValue(silo.ID, out var ts))
                {
                    ts.AmSiloCuoiKy = row.AmSiloCuoiKy;
                    ts.KLAmTonCuoi = klAmTonCuoi;
                    ts.DoAmSilo = row.DoAmSilo;
                    ts.KLTonCuoiQuyKho = klTonCuoiQuyKho;
                }
                else
                {
                    _context.LC_TonSilo.Add(new LC_TonSilo
                    {
                        IDPhieu = idPhieu,
                        NgaySX = phieu.NgaySX,
                        Ca = phieu.Ca,
                        Scope = scope,
                        ID_Silo = silo.ID,
                        SiloSo = silo.SiloSo,
                        AmSiloCuoiKy = row.AmSiloCuoiKy,
                        KLAmTonCuoi = klAmTonCuoi,
                        DoAmSilo = row.DoAmSilo,
                        KLTonCuoiQuyKho = klTonCuoiQuyKho,
                        NgayTao = DateTime.Now,
                    });
                }
            }

            // Xoá các lớp liệu không còn trong danh sách gửi lên (người dùng bấm "Xoá lớp").
            var layersToDelete = existingCapLieu.Where(x => !incomingKeys.Contains((x.ID_Silo, x.Stt))).ToList();
            if (layersToDelete.Count > 0)
                _context.LC_CapLieu.RemoveRange(layersToDelete);

            var existingKq = await _context.LC_KetQuaPhanTich
                .Where(x => x.IDPhieu == idPhieu && (x.NhomPhanTich == "CO_HAT_1MM" || x.NhomPhanTich == "CO_HAT_3MM"))
                .ToListAsync();

            foreach (var row in dto.CoHatNghien1)
            {
                UpsertKetQuaPhanTich(existingKq, phieu, scope, row.NhomPhanTich, 1, row.Lan1, row.GhiChu);
                UpsertKetQuaPhanTich(existingKq, phieu, scope, row.NhomPhanTich, 2, row.Lan2, row.GhiChu);
                UpsertKetQuaPhanTich(existingKq, phieu, scope, row.NhomPhanTich, 3, row.Lan3, row.GhiChu);
            }

            await _context.SaveChangesAsync();

            await RecomputeTonDauVaLuyKeAsync(idPhieu, ngaySx, ca, scope);

            return await GetTabAsync(idPhieu);
        }

        // "Làm mới" — xem ILC_CapLieuSiloRepository. Không đổi cấu trúc dòng, chỉ tính lại
        // TonDauKyQuyKho/LuyKeQuyKho của các dòng LC_CapLieu đã có, theo dữ liệu MỚI NHẤT của
        // kíp liền trước (khác SaveTabAsync: không nhận/ghi đè dữ liệu nhập tay từ FE).
        public async Task<LcCapLieuSiloTabDto> RefreshTonDauTuCaTruocAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể làm mới.");

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Now);
            var ca = phieu.Ca ?? 0;

            await RecomputeTonDauVaLuyKeAsync(idPhieu, ngaySx, ca, scope);

            return await GetTabAsync(idPhieu);
        }

        // Dùng chung bởi SaveTabAsync và RefreshTonDauTuCaTruocAsync: re-run SP cho TẤT CẢ
        // dòng LC_CapLieu hiện có của phiếu (idempotent, SP tự guard Chốt/Lock) rồi tính lại
        // F03 (LuyKeQuyKho = TonDauKyQuyKho vừa set + KhoiLuongKhoThucTe kíp này).
        private async Task RecomputeTonDauVaLuyKeAsync(Guid idPhieu, DateOnly ngaySx, int ca, int scope)
        {
            var allRowKeys = await _context.LC_CapLieu.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .Select(x => new { x.ID_Silo, x.Stt })
                .ToListAsync();
            await RunInitTonDauAsync(idPhieu, ngaySx, ca, scope, allRowKeys.Select(r => (r.ID_Silo, r.Stt)));

            var rowsToRecalc = await _context.LC_CapLieu
                .Where(x => x.IDPhieu == idPhieu)
                .ToListAsync();
            foreach (var r in rowsToRecalc)
                r.LuyKeQuyKho = (r.TonDauKyQuyKho ?? 0) + (r.KhoiLuongKhoThucTe ?? 0);

            await _context.SaveChangesAsync();
        }

        // ─── Initializer: CHỈ copy forward lúc phiếu được tạo (lần đầu, chưa có dòng
        // LC_CapLieu nào) — mirror STD_XNT_HRC2Repository.InitializeHRC2_STD_NXTAsync. Các
        // lần lưu sau đi qua SaveTabAsync (API riêng), hàm này tự no-op nếu đã init rồi. ────
        public async Task InitializeLC_BCSX_XNL_TPAsync(BmPhieu phieu)
        {
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;

            var scope = phieu.Scope ?? 0;
            var ngaySx = phieu.NgaySX ?? DateOnly.FromDateTime(DateTime.Now);
            var ca = phieu.Ca ?? 0;

            if (await _context.LC_CapLieu.AnyAsync(x => x.IDPhieu == phieu.Idphieu))
                return; // đã init rồi — lưu/sửa tiếp theo đi qua SaveTabAsync, không phải ở đây

            var silos = await _context.LC_Silo.AsNoTracking()
                .Where(s => s.Scope == scope && !s.IsDelete)
                .OrderBy(s => s.SiloSo)
                .ToListAsync();
            if (silos.Count == 0) return;

            var prevPhieuId = await _context.BmPhieus
                .Where(x => x.MaBm == "LC_BCSX_XNL_TP"
                         && x.Scope == scope
                         && x.Idphieu != phieu.Idphieu
                         && (x.IsDelete == null || x.IsDelete == 0)
                         && (x.NgaySX < ngaySx || (x.NgaySX == ngaySx && x.Ca < ca)))
                .OrderByDescending(x => x.NgaySX)
                .ThenByDescending(x => x.Ca)
                .Select(x => x.Idphieu)
                .FirstOrDefaultAsync();

            // Copy forward TOÀN BỘ lớp (Stt) của mỗi silo ở kíp trước — không chỉ Stt=1, vì 1
            // silo có thể đã bị "Tách liệu" thành nhiều lớp ở kíp trước đó.
            var prevBySilo = new Dictionary<int, List<LC_CapLieu>>();
            if (prevPhieuId != default)
            {
                var prevRows = await _context.LC_CapLieu.AsNoTracking()
                    .Where(x => x.IDPhieu == prevPhieuId)
                    .OrderBy(x => x.Stt)
                    .ToListAsync();
                prevBySilo = prevRows.GroupBy(x => x.ID_Silo).ToDictionary(g => g.Key, g => g.ToList());
            }

            var newRows = new List<LC_CapLieu>();
            foreach (var silo in silos)
            {
                if (prevBySilo.TryGetValue(silo.ID, out var prevLayers) && prevLayers.Count > 0)
                {
                    foreach (var prev in prevLayers)
                    {
                        newRows.Add(new LC_CapLieu
                        {
                            IDPhieu = phieu.Idphieu,
                            NgaySX = phieu.NgaySX,
                            Ca = phieu.Ca,
                            Scope = scope,
                            ID_Silo = silo.ID,
                            SiloSo = silo.SiloSo,
                            Stt = prev.Stt,
                            ID_VatTu = prev.ID_VatTu,
                            VatLieuTen = prev.VatLieuTen,
                            ID_MaLo = prev.ID_MaLo,
                            SoHieuLo = prev.SoHieuLo,
                            TonDauKyQuyKho = 0, // SP ngay dưới sẽ set lại nếu khớp đúng lô kíp trước
                            NgayTao = DateTime.Now,
                        });
                    }
                }
                else
                {
                    // Silo chưa từng có dữ liệu (vd kíp đầu tiên của Scope) — 1 lớp gốc Stt=1.
                    newRows.Add(new LC_CapLieu
                    {
                        IDPhieu = phieu.Idphieu,
                        NgaySX = phieu.NgaySX,
                        Ca = phieu.Ca,
                        Scope = scope,
                        ID_Silo = silo.ID,
                        SiloSo = silo.SiloSo,
                        Stt = 1,
                        TonDauKyQuyKho = 0,
                        NgayTao = DateTime.Now,
                    });
                }
            }
            _context.LC_CapLieu.AddRange(newRows);

            foreach (var silo in silos)
            {
                _context.LC_TonSilo.Add(new LC_TonSilo
                {
                    IDPhieu = phieu.Idphieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    ID_Silo = silo.ID,
                    SiloSo = silo.SiloSo,
                    NgayTao = DateTime.Now,
                });
            }

            await _context.SaveChangesAsync();

            await RunInitTonDauAsync(phieu.Idphieu, ngaySx, ca, scope, newRows.Select(r => (r.ID_Silo, r.Stt)));

            var rowsToRecalc = await _context.LC_CapLieu
                .Where(x => x.IDPhieu == phieu.Idphieu)
                .ToListAsync();
            foreach (var r in rowsToRecalc)
                r.LuyKeQuyKho = (r.TonDauKyQuyKho ?? 0) + (r.KhoiLuongKhoThucTe ?? 0);

            await _context.SaveChangesAsync();
        }

        private void UpsertKetQuaPhanTich(
            List<LC_KetQuaPhanTich> existing, BmPhieu phieu, int scope,
            string nhom, byte lanPhanTich, decimal? giaTri, string? ghiChu)
        {
            var row = existing.FirstOrDefault(x => x.NhomPhanTich == nhom && x.LanPhanTich == lanPhanTich);
            if (row != null)
            {
                row.GiaTri = giaTri;
                row.GhiChu = ghiChu;
            }
            else if (giaTri != null)
            {
                _context.LC_KetQuaPhanTich.Add(new LC_KetQuaPhanTich
                {
                    IDPhieu = phieu.Idphieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    NhomPhanTich = nhom,
                    LanPhanTich = lanPhanTich,
                    GiaTri = giaTri,
                    GhiChu = ghiChu,
                    NgayTao = DateTime.Now,
                });
            }
        }

        private static decimal? ComputeKhoiLuongKhoThucTe(decimal? klAm, decimal? doAm)
        {
            if (klAm == null || doAm == null) return null;
            return Math.Round(klAm.Value * (100 - doAm.Value) / 100m, 3);
        }

        // F05a — KL ẩm tồn silo THAN, CHỈ áp dụng cho TP1 (Scope=1). Công thức thật từ workbook
        // (N = AmSiloCuoiKy, cột "Âm silo cuối kíp"):
        //   =ROUND(IF(N>13.75, ((24.5-N)/10.75)^2*165, IF(N<=13.75, 165+52.564*(13.75-N))), 3)
        // TP2 dùng công thức khác (F05b, LC_01 mục 5) — CHƯA có, OQ-03 còn mở riêng cho TP2,
        // không suy diễn từ F05a.
        private static decimal? ComputeKLAmTonCuoi_TP1(decimal? amSiloCuoiKy)
        {
            if (amSiloCuoiKy == null) return null;
            var am = amSiloCuoiKy.Value;
            decimal result;
            if (am > 13.75m)
            {
                var x = (24.5m - am) / 10.75m;
                result = x * x * 165m;
            }
            else
            {
                result = 165m + 52.564m * (13.75m - am);
            }
            return Math.Round(result, 3);
        }

        private async Task RunInitTonDauAsync(
            Guid idPhieu, DateOnly ngaySx, int ca, int scope, IEnumerable<(int ID_Silo, byte Stt)> rows)
        {
            var table = new DataTable();
            table.Columns.Add("ID_Silo", typeof(int));
            table.Columns.Add("Stt", typeof(byte));
            foreach (var (idSilo, stt) in rows)
                table.Rows.Add(idSilo, stt);
            if (table.Rows.Count == 0) return;

            var parameters = new[]
            {
                new SqlParameter("@NgaySX", SqlDbType.Date) { Value = ngaySx.ToDateTime(TimeOnly.MinValue) },
                new SqlParameter("@Ca", SqlDbType.Int) { Value = ca },
                new SqlParameter("@Scope", SqlDbType.Int) { Value = scope },
                new SqlParameter("@Id_Phieu", SqlDbType.UniqueIdentifier) { Value = idPhieu },
                new SqlParameter("@ListRow", SqlDbType.Structured)
                {
                    TypeName = "dbo.TT_LC_CapLieu_Row",
                    Value = table,
                },
            };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_Init_LC_CapLieu_TonDau @NgaySX, @Ca, @Scope, @Id_Phieu, @ListRow",
                parameters);
        }
    }
}
