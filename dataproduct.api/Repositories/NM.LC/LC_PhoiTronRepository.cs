using dataproduct.api.DTOs.NM.LC;
using dataproduct.api.Models;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories.NM.LC
{
    // Tab "Phối trộn" của LC_BCSX_XNL_TP — mỗi cụm tháp than (LC_CumThap) có N "thẻ" công
    // thức phối (LC_PhoiTron, phân biệt bởi LoaiPhoiTron+ThuTu), mỗi thẻ có đúng 1 dòng chi
    // tiết (LC_PhoiTronChiTiet) cho MỖI LỚP LIỆU hiện có ở LC_CapLieu của phiếu đó (không
    // phải 1 dòng/silo tĩnh — từ khi có "Tách liệu" ở tab Cấp liệu, 1 silo có thể có nhiều
    // lớp, mỗi lớp là 1 dòng phối trộn riêng, xem TabPhoiTron.tsx/TabCapLieuSilo.tsx).
    //
    // KHÔNG dùng BmPhieu.DataJson (cùng nguyên tắc đã áp cho LC_CapLieuSiloRepository) — đọc/
    // lưu đi thẳng vào bảng quan hệ qua LC_PhoiTronController (GET/PUT riêng).
    public class LC_PhoiTronRepository : ILC_PhoiTronRepository
    {
        private readonly ProductFormContext _context;

        public LC_PhoiTronRepository(ProductFormContext context)
        {
            _context = context;
        }

        public async Task<List<LcPhoiTronCardDto>> GetTabAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            var scope = phieu.Scope ?? 0;

            var cumThaps = await _context.LC_CumThap.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .OrderBy(c => c.ThuTu).ThenBy(c => c.Ma)
                .ToListAsync();

            // Danh sách lớp liệu THẬT của phiếu này (Cấp liệu) — nguồn duy nhất cho số dòng
            // chi tiết của MỌI thẻ phối trộn. Nếu Cấp liệu chưa có gì (initializer lỗi/chưa
            // chạy), fallback skeleton 1 lớp/silo theo danh mục LC_Silo.
            var capLieuRows = await _context.LC_CapLieu.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .OrderBy(x => x.SiloSo).ThenBy(x => x.Stt)
                .ToListAsync();
            if (capLieuRows.Count == 0)
            {
                var silos = await _context.LC_Silo.AsNoTracking()
                    .Where(s => s.Scope == scope && !s.IsDelete)
                    .OrderBy(s => s.SiloSo)
                    .ToListAsync();
                capLieuRows = silos
                    .Select(s => new LC_CapLieu { ID_Silo = s.ID, SiloSo = s.SiloSo, Stt = 1 })
                    .ToList();
            }

            var existingHeaders = await _context.LC_PhoiTron.AsNoTracking()
                .Where(x => x.IDPhieu == idPhieu)
                .OrderBy(x => x.ThuTu)
                .ToListAsync();

            var headerIds = existingHeaders.Select(h => h.ID).ToList();
            var detailByHeaderId = (await _context.LC_PhoiTronChiTiet.AsNoTracking()
                    .Where(x => headerIds.Contains(x.ID_PhoiTron))
                    .ToListAsync())
                .GroupBy(x => x.ID_PhoiTron)
                .ToDictionary(g => g.Key, g => g.ToDictionary(x => (x.SiloSo, x.Stt)));

            var result = new List<LcPhoiTronCardDto>();
            foreach (var cum in cumThaps)
            {
                var headersOfCum = existingHeaders.Where(h => h.ID_CumThap == cum.ID).ToList();
                if (headersOfCum.Count == 0)
                {
                    result.Add(BuildEmptyCard(cum.Ma, capLieuRows, "SAN_XUAT", 1));
                    continue;
                }

                foreach (var h in headersOfCum)
                {
                    var detailByKey = detailByHeaderId.TryGetValue(h.ID, out var d)
                        ? d
                        : new Dictionary<(int SiloSo, byte Stt), LC_PhoiTronChiTiet>();
                    var card = new LcPhoiTronCardDto
                    {
                        Id = h.ID,
                        CumThapMa = h.CumThapMa,
                        LoaiPhoiTron = h.LoaiPhoiTron,
                        ThuTu = h.ThuTu,
                        SoPhieuYeuCau = h.SoPhieuYeuCau,
                        MaMauThuNghiem = h.MaMauThuNghiem,
                        GhiChuPhoi = h.GhiChuPhoi,
                        KLCanAm = h.KLCanAm,
                        DoAm = h.DoAm,
                        KLQuyKhoCan = h.KLQuyKhoCan,
                    };
                    foreach (var cl in capLieuRows)
                    {
                        detailByKey.TryGetValue((cl.SiloSo, cl.Stt), out var row);
                        card.ChiTiet.Add(new LcPhoiTronChiTietRowDto
                        {
                            SiloSo = cl.SiloSo,
                            Stt = cl.Stt,
                            VatLieuTen = row?.VatLieuTen ?? cl.VatLieuTen,
                            TyLeKeHoach = row?.TyLeKeHoach,
                            TyLeThucTe = row?.TyLeThucTe,
                            KLQuyKho = row?.KLQuyKho,
                            LuyKeQuyKho = row?.LuyKeQuyKho,
                            GhiChu = row?.GhiChu,
                        });
                    }
                    result.Add(card);
                }
            }
            return result;
        }

        public async Task<List<LcPhoiTronCardDto>> SaveTabAsync(Guid idPhieu, List<LcPhoiTronCardDto> cards)
        {
            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể sửa.");

            var scope = phieu.Scope ?? 0;

            var cumThapByMa = await _context.LC_CumThap.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .ToDictionaryAsync(c => c.Ma);

            var siloBySo = await _context.LC_Silo.AsNoTracking()
                .Where(s => s.Scope == scope && !s.IsDelete)
                .ToDictionaryAsync(s => s.SiloSo);

            // Snapshot "than đang khai" của phiếu này (Cấp liệu, TẤT CẢ lớp) — nguồn DUY NHẤT
            // cho VatLieuTen/ID_VatTu/ID_CapLieu ở chi tiết, KHÔNG nhận giá trị FE gửi cho các
            // cột này. Khoá (SiloSo, Stt) vì 1 silo giờ có thể nhiều lớp khác than.
            var capLieuByKey = (await _context.LC_CapLieu.AsNoTracking()
                    .Where(x => x.IDPhieu == idPhieu)
                    .ToListAsync())
                .ToDictionary(x => (x.SiloSo, x.Stt));

            var existingHeaders = await _context.LC_PhoiTron
                .Where(x => x.IDPhieu == idPhieu)
                .ToListAsync();
            var existingHeaderIds = existingHeaders.Select(h => h.ID).ToList();
            var existingDetails = await _context.LC_PhoiTronChiTiet
                .Where(x => existingHeaderIds.Contains(x.ID_PhoiTron))
                .ToListAsync();

            var incomingIds = new HashSet<int>();

            foreach (var card in cards)
            {
                if (!cumThapByMa.TryGetValue(card.CumThapMa, out var cum)) continue;

                // F01: KL quy khô cân = KL cân ẩm × (100 − Độ ẩm) / 100.
                var klQuyKhoCan = ComputeKlQuyKhoCan(card.KLCanAm, card.DoAm);

                LC_PhoiTron? header = card.Id > 0
                    ? existingHeaders.FirstOrDefault(h => h.ID == card.Id)
                    : existingHeaders.FirstOrDefault(h =>
                        h.ID_CumThap == cum.ID && h.LoaiPhoiTron == card.LoaiPhoiTron && h.ThuTu == card.ThuTu);

                if (header == null)
                {
                    header = new LC_PhoiTron
                    {
                        IDPhieu = idPhieu,
                        NgaySX = phieu.NgaySX,
                        Ca = phieu.Ca,
                        Scope = scope,
                        ID_CumThap = cum.ID,
                        CumThapMa = cum.Ma,
                        NgayTao = DateTime.Now,
                    };
                    _context.LC_PhoiTron.Add(header);
                    existingHeaders.Add(header);
                }

                header.LoaiPhoiTron = card.LoaiPhoiTron;
                header.ThuTu = card.ThuTu;
                header.SoPhieuYeuCau = card.SoPhieuYeuCau;
                header.MaMauThuNghiem = card.MaMauThuNghiem;
                header.GhiChuPhoi = card.GhiChuPhoi;
                header.KLCanAm = card.KLCanAm;
                header.DoAm = card.DoAm;
                header.KLQuyKhoCan = klQuyKhoCan;

                var incomingDetailKeys = new HashSet<(int SiloSo, byte Stt)>();

                foreach (var rowDto in card.ChiTiet)
                {
                    if (!siloBySo.TryGetValue(rowDto.SiloSo, out var silo)) continue;
                    var stt = (byte)Math.Max(1, rowDto.Stt);
                    incomingDetailKeys.Add((rowDto.SiloSo, stt));
                    capLieuByKey.TryGetValue((rowDto.SiloSo, stt), out var capLieu);

                    // F02: KL quy khô theo tỷ lệ = Tỷ lệ thực tế × KL quy khô cân / 100.
                    var klQuyKho = ComputeKlQuyKhoTheoTyLe(rowDto.TyLeThucTe, klQuyKhoCan);

                    // header.ID chỉ có giá trị thật SAU SaveChangesAsync đầu tiên (entity mới) —
                    // nên match theo tham chiếu header (identity) qua _headerToNewDetails,
                    // KHÔNG match theo ID_PhoiTron khi header vừa mới Add (còn 0).
                    var detailRow = header.ID > 0
                        ? existingDetails.FirstOrDefault(d => d.ID_PhoiTron == header.ID && d.SiloSo == rowDto.SiloSo && d.Stt == stt)
                        : null;
                    detailRow ??= _headerToNewDetails.TryGetValue(header, out var list)
                        ? list.FirstOrDefault(d => d.SiloSo == rowDto.SiloSo && d.Stt == stt)
                        : null;

                    if (detailRow == null)
                    {
                        detailRow = new LC_PhoiTronChiTiet
                        {
                            ID_Silo = silo.ID,
                            SiloSo = silo.SiloSo,
                            Stt = stt,
                            NgayTao = DateTime.Now,
                        };
                        _context.LC_PhoiTronChiTiet.Add(detailRow);
                        // ID_PhoiTron sẽ được gán sau khi header có ID thật (xem vòng lặp sau
                        // SaveChangesAsync lần 1) — không dùng navigation property ở đây để
                        // giữ nhất quán với convention "KHÔNG FOREIGN KEY" của module.
                        if (!_headerToNewDetails.TryGetValue(header, out var newList))
                        {
                            newList = new List<LC_PhoiTronChiTiet>();
                            _headerToNewDetails[header] = newList;
                        }
                        newList.Add(detailRow);
                    }

                    detailRow.ID_CapLieu = capLieu?.ID;
                    detailRow.ID_VatTu = capLieu?.ID_VatTu;
                    detailRow.VatLieuTen = capLieu?.VatLieuTen;
                    detailRow.TyLeKeHoach = rowDto.TyLeKeHoach;
                    detailRow.TyLeThucTe = rowDto.TyLeThucTe;
                    detailRow.KLQuyKho = klQuyKho;
                    detailRow.LuyKeQuyKho = await ComputeLuyKeQuyKhoAsync(
                        idPhieu, scope, phieu.NgaySX, phieu.Ca, cum.Ma, card.LoaiPhoiTron, card.ThuTu,
                        silo.SiloSo, stt, capLieu?.ID_VatTu, klQuyKho);
                    detailRow.GhiChu = rowDto.GhiChu;
                }

                // Xoá chi tiết không còn trong danh sách gửi lên của CHÍNH thẻ này (vd 1 lớp
                // liệu vừa bị "Xoá lớp" ở tab Cấp liệu, không còn match silo/lớp nào nữa).
                if (header.ID > 0)
                {
                    var staleDetails = existingDetails
                        .Where(d => d.ID_PhoiTron == header.ID && !incomingDetailKeys.Contains((d.SiloSo, d.Stt)))
                        .ToList();
                    if (staleDetails.Count > 0) _context.LC_PhoiTronChiTiet.RemoveRange(staleDetails);
                }

                if (header.ID > 0) incomingIds.Add(header.ID);
            }

            // Lưu lần 1: tạo ID cho header mới (cần ID để gán FK cho chi tiết mới của header đó).
            await _context.SaveChangesAsync();
            foreach (var kv in _headerToNewDetails)
            {
                foreach (var detail in kv.Value)
                    detail.ID_PhoiTron = kv.Key.ID;
                incomingIds.Add(kv.Key.ID);
            }
            _headerToNewDetails.Clear();

            // Xoá thẻ không còn trong danh sách gửi lên (người dùng bỏ thẻ đã lưu trước đó).
            var headersToDelete = existingHeaders.Where(h => h.ID > 0 && !incomingIds.Contains(h.ID)).ToList();
            if (headersToDelete.Count > 0)
            {
                var idsToDelete = headersToDelete.Select(h => h.ID).ToList();
                var detailsToDelete = existingDetails.Where(d => idsToDelete.Contains(d.ID_PhoiTron)).ToList();
                _context.LC_PhoiTronChiTiet.RemoveRange(detailsToDelete);
                _context.LC_PhoiTron.RemoveRange(headersToDelete);
            }

            await _context.SaveChangesAsync();

            return await GetTabAsync(idPhieu);
        }

        // "Làm mới" — xem ILC_PhoiTronRepository. Chỉ tính lại LuyKeQuyKho của các dòng chi
        // tiết đã có (dùng KLQuyKho hiện có của chính dòng đó làm "cộng thêm"), KHÔNG nhận dữ
        // liệu nhập tay từ FE như SaveTabAsync — tránh ghi đè KL cân/tỷ lệ đang gõ chưa lưu.
        public async Task<List<LcPhoiTronCardDto>> RefreshLuyKeTuCaTruocAsync(Guid idPhieu)
        {
            var phieu = await _context.BmPhieus.AsNoTracking().FirstOrDefaultAsync(p => p.Idphieu == idPhieu)
                ?? throw new InvalidOperationException("Không tìm thấy phiếu.");
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1)
                throw new InvalidOperationException("Phiếu đã chốt/khóa, không thể làm mới.");

            var scope = phieu.Scope ?? 0;

            var headers = await _context.LC_PhoiTron
                .Where(x => x.IDPhieu == idPhieu)
                .ToListAsync();
            var headerById = headers.ToDictionary(h => h.ID);

            var headerIds = headers.Select(h => h.ID).ToList();
            var details = await _context.LC_PhoiTronChiTiet
                .Where(x => headerIds.Contains(x.ID_PhoiTron))
                .ToListAsync();

            foreach (var d in details)
            {
                if (!headerById.TryGetValue(d.ID_PhoiTron, out var h)) continue;
                d.LuyKeQuyKho = await ComputeLuyKeQuyKhoAsync(
                    idPhieu, scope, phieu.NgaySX, phieu.Ca, h.CumThapMa, h.LoaiPhoiTron, h.ThuTu,
                    d.SiloSo, d.Stt, d.ID_VatTu, d.KLQuyKho);
            }

            await _context.SaveChangesAsync();

            return await GetTabAsync(idPhieu);
        }

        // Theo dõi chi tiết MỚI theo từng header trong phạm vi 1 lần gọi SaveTabAsync — cần vì
        // header mới chưa có ID thật cho tới sau SaveChangesAsync lần 1 (không dùng navigation
        // property ở đây để giữ nhất quán với convention "KHÔNG FOREIGN KEY" của module).
        private readonly Dictionary<LC_PhoiTron, List<LC_PhoiTronChiTiet>> _headerToNewDetails = new();

        public async Task InitializeLC_BCSX_XNL_TP_PhoiTronAsync(BmPhieu phieu)
        {
            if (phieu.TinhTrang == 5 || phieu.IsLock == 1) return;
            if (await _context.LC_PhoiTron.AnyAsync(x => x.IDPhieu == phieu.Idphieu)) return; // đã init rồi

            var scope = phieu.Scope ?? 0;
            var cumThaps = await _context.LC_CumThap.AsNoTracking()
                .Where(c => c.Scope == scope && !c.IsDelete)
                .ToListAsync();
            if (cumThaps.Count == 0) return;

            // Lớp liệu THẬT của phiếu này (Cấp liệu đã init trước đó trong cùng luồng tạo
            // phiếu) — nếu rỗng, fallback 1 lớp/silo theo danh mục LC_Silo.
            var capLieuRows = await _context.LC_CapLieu.AsNoTracking()
                .Where(x => x.IDPhieu == phieu.Idphieu)
                .ToListAsync();
            if (capLieuRows.Count == 0)
            {
                var silos = await _context.LC_Silo.AsNoTracking()
                    .Where(s => s.Scope == scope && !s.IsDelete)
                    .OrderBy(s => s.SiloSo)
                    .ToListAsync();
                capLieuRows = silos
                    .Select(s => new LC_CapLieu { ID_Silo = s.ID, SiloSo = s.SiloSo, Stt = 1 })
                    .ToList();
            }
            if (capLieuRows.Count == 0) return;

            foreach (var cum in cumThaps)
            {
                var header = new LC_PhoiTron
                {
                    IDPhieu = phieu.Idphieu,
                    NgaySX = phieu.NgaySX,
                    Ca = phieu.Ca,
                    Scope = scope,
                    ID_CumThap = cum.ID,
                    CumThapMa = cum.Ma,
                    LoaiPhoiTron = "SAN_XUAT",
                    ThuTu = 1,
                    NgayTao = DateTime.Now,
                };
                _context.LC_PhoiTron.Add(header);
                await _context.SaveChangesAsync(); // cần ID ngay để gán FK chi tiết

                foreach (var cl in capLieuRows)
                {
                    _context.LC_PhoiTronChiTiet.Add(new LC_PhoiTronChiTiet
                    {
                        ID_PhoiTron = header.ID,
                        Stt = cl.Stt,
                        ID_Silo = cl.ID_Silo,
                        SiloSo = cl.SiloSo,
                        NgayTao = DateTime.Now,
                    });
                }
            }

            await _context.SaveChangesAsync();
        }

        private static LcPhoiTronCardDto BuildEmptyCard(
            string cumThapMa, List<LC_CapLieu> capLieuRows, string loai, byte thuTu)
        {
            var card = new LcPhoiTronCardDto
            {
                Id = 0,
                CumThapMa = cumThapMa,
                LoaiPhoiTron = loai,
                ThuTu = thuTu,
            };
            foreach (var cl in capLieuRows)
            {
                card.ChiTiet.Add(new LcPhoiTronChiTietRowDto
                {
                    SiloSo = cl.SiloSo,
                    Stt = cl.Stt,
                    VatLieuTen = cl.VatLieuTen,
                });
            }
            return card;
        }

        private static decimal? ComputeKlQuyKhoCan(decimal? klCanAm, decimal? doAm)
        {
            if (klCanAm == null || doAm == null) return null;
            return Math.Round(klCanAm.Value * (100 - doAm.Value) / 100m, 3);
        }

        private static decimal? ComputeKlQuyKhoTheoTyLe(decimal? tyLeThucTe, decimal? klQuyKhoCan)
        {
            if (tyLeThucTe == null || klQuyKhoCan == null) return null;
            return Math.Round(tyLeThucTe.Value * klQuyKhoCan.Value / 100m, 3);
        }

        // F03 (Lũy kế theo vật liệu) — MỐC RESET chốt 2026-10-21 (giải quyết OQ-02 LC_01,
        // áp dụng cho MỌI lũy kế của module LC, không chỉ Cấp liệu — xem sp_Init_LC_CapLieu_TonDau):
        // Ca 1 ngày 1 hàng tháng KHÔNG kế thừa từ tháng trước — LuyKe = đúng KLQuyKho của
        // chính ca đó. Các ca khác kế thừa LuyKeQuyKho của dòng CÙNG (Scope, cụm tháp, loại
        // phối trộn, thứ tự, Silo, LỚP, vật tư) ở phiếu liền trước (Scope/NgaySX/Ca) + cộng
        // KLQuyKho ca này — không khớp (chưa từng có, đổi than, hoặc là lớp mới tách) → coi
        // như lũy kế trước = 0. Thêm `stt` vào khoá (2026-10-21, cùng lúc với "Tách liệu") để
        // 2 lớp của cùng 1 silo trong cùng công thức không lẫn lũy kế vào nhau.
        private async Task<decimal?> ComputeLuyKeQuyKhoAsync(
            Guid idPhieu, int scope, DateOnly? ngaySx, int? ca,
            string cumThapMa, string loaiPhoiTron, byte thuTu, int siloSo, byte stt, int? idVatTu, decimal? klQuyKhoThisShift)
        {
            if (klQuyKhoThisShift == null) return null;
            if (ngaySx == null || ca == null) return klQuyKhoThisShift;
            if (ngaySx.Value.Day == 1 && ca == 1) return klQuyKhoThisShift;

            var prev = await (
                from d in _context.LC_PhoiTronChiTiet.AsNoTracking()
                join h in _context.LC_PhoiTron.AsNoTracking() on d.ID_PhoiTron equals h.ID
                where h.Scope == scope
                    && h.CumThapMa == cumThapMa
                    && h.LoaiPhoiTron == loaiPhoiTron
                    && h.ThuTu == thuTu
                    && d.SiloSo == siloSo
                    && d.Stt == stt
                    && d.ID_VatTu == idVatTu
                    && h.IDPhieu != idPhieu
                    && (h.NgaySX < ngaySx || (h.NgaySX == ngaySx && h.Ca < ca))
                orderby h.NgaySX descending, h.Ca descending
                select new { d.LuyKeQuyKho }
            ).FirstOrDefaultAsync();

            return (prev?.LuyKeQuyKho ?? 0) + klQuyKhoThisShift.Value;
        }
    }
}
