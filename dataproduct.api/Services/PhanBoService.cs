using dataproduct.api.DTOs.NMLG_Dto;
using dataproduct.api.Models;
using dataproduct.api.Repositories;
using dataproduct.api.Utils.Enums;

namespace dataproduct.api.Services
{
    public class PhanBoService
    {
        private readonly INhomPhanBoRepository _nhomRepo;
        private readonly ITyLePhanBoRepository _tyLeRepo;
        private readonly INapLieuPhanBoRepository _napLieuRepo;
        private readonly IBienBanNhanRepository _bienBanRepo;
        private readonly IKetQuaPhanBoRepository _ketQuaRepo;
        private readonly IBienBanGiaoNhanSourceRepository _bbgnRepo;

        private static readonly byte[] TatCaLoaiPhanBo =
        {
            (byte)LoaiPhanBoEnum.Qhlc, (byte)LoaiPhanBoEnum.Cvh, (byte)LoaiPhanBoEnum.ThanCoc10
        };

        public PhanBoService(
            INhomPhanBoRepository nhomRepo,
            ITyLePhanBoRepository tyLeRepo,
            INapLieuPhanBoRepository napLieuRepo,
            IBienBanNhanRepository bienBanRepo,
            IKetQuaPhanBoRepository ketQuaRepo,
            IBienBanGiaoNhanSourceRepository bbgnRepo)
        {
            _nhomRepo = nhomRepo;
            _tyLeRepo = tyLeRepo;
            _napLieuRepo = napLieuRepo;
            _bienBanRepo = bienBanRepo;
            _ketQuaRepo = ketQuaRepo;
            _bbgnRepo = bbgnRepo;
        }

        // ─── Đồng bộ khối lượng nhận về (G) từ 2 nguồn khác DbContext, gộp ở tầng C# ──

        public async Task DongBoBienBanNhanAsync(DateTime ngay, int idNguoiNhap)
        {
            ngay = ngay.Date;
            var mapXuong = await _bienBanRepo.GetMapXuongLoCaoAsync();

            if (mapXuong.Count > 0)
            {
                var nguonQhlcVaThanCoc10 = new (byte LoaiPhanBo, int IdVatTu)[]
                {
                    ((byte)LoaiPhanBoEnum.Qhlc, (int)MaVatTuBienBanEnum.QuangHoiLoCao),
                    ((byte)LoaiPhanBoEnum.ThanCoc10, (int)MaVatTuBienBanEnum.ThanCoc10)
                };

                foreach (var (loaiPhanBo, idVatTu) in nguonQhlcVaThanCoc10)
                {
                    var rows = await _bbgnRepo.GetTongNhanVeAsync(ngay, idVatTu, mapXuong.Keys);
                    var grouped = rows
                        .Where(r => mapXuong.ContainsKey(r.IdXuong))
                        .GroupBy(r => new { IdLoCao = mapXuong[r.IdXuong], r.Ca })
                        .Select(g => new { g.Key.IdLoCao, g.Key.Ca, KhoiLuong = g.Sum(x => x.KhoiLuong) });

                    foreach (var g in grouped)
                    {
                        await _bienBanRepo.UpsertAsync(new LG_PB_BienBanNhanQHLCCVH
                        {
                            Ngay = ngay,
                            Ca = g.Ca,
                            IDLoCao = g.IdLoCao,
                            LoaiPhanBo = loaiPhanBo,
                            KhoiLuongNhanVe = g.KhoiLuong,
                            IDNguoiNhap = idNguoiNhap
                        });
                    }
                }
            }

            var mapCvh = await _napLieuRepo.GetMapNvlCvhLoCaoAsync();
            if (mapCvh.Count > 0)
            {
                var rows = await _napLieuRepo.GetNapLieuTheoNvlListAsync(ngay, mapCvh.Values);
                foreach (var r in rows)
                {
                    await _bienBanRepo.UpsertAsync(new LG_PB_BienBanNhanQHLCCVH
                    {
                        Ngay = ngay,
                        Ca = r.Ca,
                        IDLoCao = r.IdLoCao,
                        LoaiPhanBo = (byte)LoaiPhanBoEnum.Cvh,
                        KhoiLuongNhanVe = r.KhoiLuongNhanVe,
                        IDNguoiNhap = idNguoiNhap
                    });
                }
            }
        }

        // ─── Tính phân bổ cho cả ngày (3 loại, mọi ca/lò cao có biên bản nhận) ────────

        public async Task TinhPhanBoAsync(DateTime ngay, int idNguoiThucThi)
        {
            ngay = ngay.Date;

            await DongBoBienBanNhanAsync(ngay, idNguoiThucThi);

            foreach (var loai in TatCaLoaiPhanBo)
            {
                // (Ca, Lò cao) nào đã chốt thì giữ nguyên, không tính lại/ghi đè — các ca/lò cao khác
                // trong cùng ngày vẫn tính lại bình thường.
                var daChotSet = (await _ketQuaRepo.GetChotSetAsync(ngay, loai)).ToHashSet();
                var rows = await TinhChoLoaiAsync(ngay, loai, idNguoiThucThi, daChotSet);
                await _ketQuaRepo.ReplaceNhapAsync(ngay, loai, rows);
            }
        }

        // Than cốc <10mm dùng CHUNG cấu hình nhóm/NVL/tỷ lệ với CVH (cùng là than cốc, chỉ khác nguồn G) —
        // nên khi tính cho loaiPhanBo=3, tra nhóm dưới "khe" loaiPhanBo=2, không cấu hình nhóm riêng cho 3.
        private static byte LoaiPhanBoChoNhom(byte loaiPhanBo) =>
            loaiPhanBo == (byte)LoaiPhanBoEnum.ThanCoc10 ? (byte)LoaiPhanBoEnum.Cvh : loaiPhanBo;

        private async Task<List<LG_PB_KetQuaPhanBo>> TinhChoLoaiAsync(
            DateTime ngay, byte loaiPhanBo, int idNguoiTao, HashSet<(byte Ca, int IdLoCao)> daChotSet)
        {
            var bienBans = await _bienBanRepo.GetByNgayAsync(ngay, loaiPhanBo);
            if (bienBans.Count == 0) return new List<LG_PB_KetQuaPhanBo>();

            // Giữ lại Mã công đoạn chi phí đã gán tay trước khi dòng cũ bị xoá bởi ReplaceNhapAsync
            var rowsHienTai = await _ketQuaRepo.GetByNgayAsync(ngay, loaiPhanBo, null);
            var maCongDoanMap = new Dictionary<int, string?>();
            foreach (var r in rowsHienTai)
                if (!string.IsNullOrEmpty(r.MaCongDoanChiPhi))
                    maCongDoanMap[r.IDNVL] = r.MaCongDoanChiPhi;

            var loaiPhanBoChoNhom = LoaiPhanBoChoNhom(loaiPhanBo);
            var result = new List<LG_PB_KetQuaPhanBo>();

            foreach (var locaoGroup in bienBans.GroupBy(b => b.IDLoCao))
            {
                var idLoCao = locaoGroup.Key;
                var eList = await _napLieuRepo.GetNapLieuAsync(ngay, idLoCao);

                foreach (var bienBan in locaoGroup)
                {
                    if (bienBan.Ca == null) continue; // không xác định được ca thì không tra được cấu hình nhóm/NVL
                    var ca = bienBan.Ca.Value;

                    // (Ca, Lò cao) này đã chốt riêng — giữ nguyên kết quả đã chốt, không tính lại/ghi đè
                    if (daChotSet.Contains((ca, idLoCao))) continue;

                    // NVL thuộc nhóm là cấu hình RIÊNG cho đúng (ngày, ca, lò cao) này — không kế thừa từ ca/ngày khác
                    var nhomVaThanhVien = await _nhomRepo.GetNhomVaThanhVienAsync(loaiPhanBoChoNhom, ngay, ca, idLoCao);
                    if (nhomVaThanhVien.Count == 0) continue;

                    var pp2Nhoms = nhomVaThanhVien.Where(n => n.Nhom.PhuongThucPhanBo == (byte)PhuongThucPhanBoEnum.TyLeNhapTay).ToList();
                    var pp1Nhoms = nhomVaThanhVien.Where(n => n.Nhom.PhuongThucPhanBo == (byte)PhuongThucPhanBoEnum.TyTrongDongDu).ToList();

                    var eForCa = eList.Where(x => x.Ca == ca).ToDictionary(x => x.IdNvl, x => x.KhoiLuongNapLieu);
                    var g = bienBan.KhoiLuongNhanVe;

                    // Pass 1: PP2 — tỷ lệ nhập tay
                    var pp2Ket = new List<(LG_PB_NhomPhanBo Nhom, LG_PB_NVL_NhomPhanBo Tv, decimal E, decimal F, decimal H)>();
                    decimal tongHPp2 = 0;

                    if (pp2Nhoms.Count > 0)
                    {
                        var allMemberIds = pp2Nhoms.SelectMany(n => n.ThanhVien.Select(t => t.IDNVL)).Distinct().ToList();
                        var tyLeMap = allMemberIds.Count > 0
                            ? await _tyLeRepo.GetHieuLucMapAsync(allMemberIds, ngay, ca)
                            : new Dictionary<int, decimal>();

                        foreach (var (nhom, thanhVien) in pp2Nhoms)
                        {
                            foreach (var tv in thanhVien)
                            {
                                var e = eForCa.GetValueOrDefault(tv.IDNVL, 0m);
                                var f = tyLeMap.GetValueOrDefault(tv.IDNVL, 0m);
                                var h = Math.Round(e * f, 3);
                                pp2Ket.Add((nhom, tv, e, f, h));
                                tongHPp2 += h;
                            }
                        }
                    }

                    // Pass 2: PP1 — tỷ trọng nội bộ + dòng dư, chia phần còn lại (G - tổng H các nhóm PP2)
                    var pp1Ket = new List<(LG_PB_NhomPhanBo Nhom, LG_PB_NVL_NhomPhanBo Tv, decimal E, decimal F, decimal H, bool LaDongDu)>();
                    var conLai = g - tongHPp2;

                    if (pp1Nhoms.Count > 0)
                    {
                        var eNhomMap = pp1Nhoms.ToDictionary(
                            n => n.Nhom.ID,
                            n => n.ThanhVien.Sum(tv => eForCa.GetValueOrDefault(tv.IDNVL, 0m)));
                        var eTongPp1 = eNhomMap.Values.Sum();

                        foreach (var (nhom, thanhVien) in pp1Nhoms)
                        {
                            if (thanhVien.Count == 0) continue;

                            var eNhom = eNhomMap[nhom.ID];
                            var quyNhom = eTongPp1 > 0
                                ? Math.Round(conLai * (eNhom / eTongPp1), 3)
                                : Math.Round(conLai / pp1Nhoms.Count, 3);

                            // Dòng dư (bù trừ do làm tròn) được TỰ ĐỘNG chọn là NVL có khối lượng nạp liệu (E) lớn nhất
                            // trong nhóm tại (ca, lò cao) này — không cần admin cấu hình tay thứ tự ưu tiên.
                            var dongDu = thanhVien
                                .OrderByDescending(t => eForCa.GetValueOrDefault(t.IDNVL, 0m))
                                .First();
                            decimal tongHDongThuong = 0;

                            // F (%) luôn là tỷ trọng nạp liệu E/eNhom — kể cả với dòng dư (không phải H/E),
                            // vì đây là con số mô tả tỷ trọng nạp liệu, độc lập với cơ chế bù trừ làm tròn của H.
                            foreach (var tv in thanhVien.Where(t => t.ID != dongDu.ID))
                            {
                                var e = eForCa.GetValueOrDefault(tv.IDNVL, 0m);
                                var h = eNhom > 0 ? Math.Round(e / eNhom * quyNhom, 3) : 0m;
                                var f = eNhom > 0 ? e / eNhom : 0m;
                                pp1Ket.Add((nhom, tv, e, f, h, false));
                                tongHDongThuong += h;
                            }

                            var eDongDu = eForCa.GetValueOrDefault(dongDu.IDNVL, 0m);
                            var hDongDu = quyNhom - tongHDongThuong;
                            var fDongDu = eNhom > 0 ? eDongDu / eNhom : 0m;
                            pp1Ket.Add((nhom, dongDu, eDongDu, fDongDu, hDongDu, true));
                        }
                    }

                    foreach (var (nhom, tv, e, f, h) in pp2Ket)
                        result.Add(BuildRow(ngay, ca, idLoCao, loaiPhanBo, tv.IDNVL, nhom.ID, bienBan.ID, e, f, h, false, idNguoiTao, maCongDoanMap.GetValueOrDefault(tv.IDNVL)));

                    foreach (var (nhom, tv, e, f, h, laDongDu) in pp1Ket)
                        result.Add(BuildRow(ngay, ca, idLoCao, loaiPhanBo, tv.IDNVL, nhom.ID, bienBan.ID, e, f, h, laDongDu, idNguoiTao, maCongDoanMap.GetValueOrDefault(tv.IDNVL)));
                }
            }

            return result;
        }

        private static LG_PB_KetQuaPhanBo BuildRow(
            DateTime ngay, byte? ca, int idLoCao, byte loaiPhanBo, int idNvl, int idNhom, int idBienBan,
            decimal e, decimal f, decimal h, bool laDongDu, int idNguoiTao, string? maCongDoanChiPhi) => new()
        {
            Ngay = ngay,
            Ca = ca,
            IDLoCao = idLoCao,
            LoaiPhanBo = loaiPhanBo,
            IDNVL = idNvl,
            MaCongDoanChiPhi = maCongDoanChiPhi,
            IDNhomPhanBo = idNhom,
            IDBienBanNhan = idBienBan,
            KhoiLuongNapLieu = e,
            TyLePhanBo = f,
            KhoiLuongPhanBo = h,
            KhoiLuongChotCuoi = e + h,
            LaDongConLai = laDongDu,
            TrangThai = 0,
            IDNguoiTao = idNguoiTao,
            NgayTao = DateTime.Now
        };

        // ─── Đọc kết quả + đối chiếu tổng (dùng cho ValidateBanner) ───────────────────

        public async Task<KetQuaPhanBoQueryResultDto> LayKetQuaAsync(DateTime ngay, byte loaiPhanBo, int idLoCao, byte? ca)
        {
            ngay = ngay.Date;
            var rows = await _ketQuaRepo.GetByNgayAsync(ngay, loaiPhanBo, idLoCao, ca);

            var nhomMap = await _nhomRepo.GetByIdsAsync(rows.Select(r => r.IDNhomPhanBo));
            var tenNvlMap = await _nhomRepo.GetTenNvlMapAsync(rows.Select(r => r.IDNVL));
            var eNhomMap = BuildENhomMap(rows.Select(r => (r.IDNhomPhanBo, r.KhoiLuongNapLieu)));

            // G — khối lượng nhận về từ biên bản giao nhận, dùng chung cho toàn bộ bảng (1 loại/ca/lò cao)
            var bienBans = await _bienBanRepo.GetByNgayAsync(ngay, loaiPhanBo);
            var tongNhanVe = bienBans
                .Where(b => b.IDLoCao == idLoCao && (ca == null || b.Ca == ca))
                .Sum(b => b.KhoiLuongNhanVe);

            var ketQua = rows.Select(r => new KetQuaPhanBoDto
            {
                Id = r.ID,
                Ngay = r.Ngay,
                Ca = r.Ca,
                IdLoCao = r.IDLoCao,
                LoaiPhanBo = r.LoaiPhanBo,
                IdNvl = r.IDNVL,
                TenNvl = tenNvlMap.GetValueOrDefault(r.IDNVL),
                MaCongDoanChiPhi = r.MaCongDoanChiPhi,
                IdNhomPhanBo = r.IDNhomPhanBo,
                TenNhomPhanBo = nhomMap.TryGetValue(r.IDNhomPhanBo, out var nhom) ? nhom.TenNhom : null,
                PhuongThucPhanBo = nhomMap.TryGetValue(r.IDNhomPhanBo, out var nhom2) ? nhom2.PhuongThucPhanBo : (byte)0,
                KhoiLuongNapLieu = r.KhoiLuongNapLieu,
                KhoiLuongNapLieuNhom = eNhomMap.GetValueOrDefault(r.IDNhomPhanBo, 0m),
                TyLePhanBo = r.TyLePhanBo,
                KhoiLuongNhanVe = tongNhanVe,
                KhoiLuongPhanBo = r.KhoiLuongPhanBo,
                KhoiLuongChotCuoi = r.KhoiLuongChotCuoi,
                LaDongConLai = r.LaDongConLai,
                TrangThai = r.TrangThai
            }).ToList();

            var validate = await BuildValidateAsync(ngay, loaiPhanBo, idLoCao, ca, ketQua.Sum(k => k.KhoiLuongPhanBo));

            return new KetQuaPhanBoQueryResultDto { KetQua = ketQua, Validate = validate };
        }

        // Tổng E của cả nhóm = SUM(E) các NVL thành viên đang có trong tập kết quả (đã lọc theo ca/lò cao)
        private static Dictionary<int, decimal> BuildENhomMap(IEnumerable<(int IdNhomPhanBo, decimal E)> rows)
        {
            var map = new Dictionary<int, decimal>();
            foreach (var (idNhom, e) in rows)
                map[idNhom] = map.GetValueOrDefault(idNhom, 0m) + e;
            return map;
        }

        private async Task<ValidatePhanBoResultDto> BuildValidateAsync(
            DateTime ngay, byte loaiPhanBo, int idLoCao, byte? ca, decimal tongPhanBo)
        {
            var bienBans = await _bienBanRepo.GetByNgayAsync(ngay, loaiPhanBo);
            var tongNhanVe = bienBans
                .Where(b => b.IDLoCao == idLoCao && (ca == null || b.Ca == ca))
                .Sum(b => b.KhoiLuongNhanVe);
            var chenhLech = tongNhanVe - tongPhanBo;

            return new ValidatePhanBoResultDto
            {
                IsMatched = Math.Abs(chenhLech) < 0.001m,
                TongNhanVe = tongNhanVe,
                TongPhanBo = tongPhanBo,
                ChenhLech = chenhLech
            };
        }

        // ─── Kết quả gộp CVH + Than cốc <10mm — cùng nhóm/NVL/tỷ lệ, khác nguồn G ──────

        public async Task<KetQuaThanCocQueryResultDto> LayKetQuaThanCocAsync(DateTime ngay, int idLoCao, byte? ca)
        {
            ngay = ngay.Date;

            var rowsCvh = await _ketQuaRepo.GetByNgayAsync(ngay, (byte)LoaiPhanBoEnum.Cvh, idLoCao, ca);
            var rowsThanCoc10 = await _ketQuaRepo.GetByNgayAsync(ngay, (byte)LoaiPhanBoEnum.ThanCoc10, idLoCao, ca);

            var idNhomAll = rowsCvh.Select(r => r.IDNhomPhanBo).Concat(rowsThanCoc10.Select(r => r.IDNhomPhanBo));
            var idNvlAll = rowsCvh.Select(r => r.IDNVL).Concat(rowsThanCoc10.Select(r => r.IDNVL));
            var nhomMap = await _nhomRepo.GetByIdsAsync(idNhomAll);
            var tenNvlMap = await _nhomRepo.GetTenNvlMapAsync(idNvlAll);

            var mapCvh = rowsCvh.ToDictionary(r => (r.Ca, r.IDNVL));
            var mapThanCoc10 = rowsThanCoc10.ToDictionary(r => (r.Ca, r.IDNVL));
            var allKeys = mapCvh.Keys.Concat(mapThanCoc10.Keys).Distinct();

            var ketQua = allKeys.Select(key =>
            {
                mapCvh.TryGetValue(key, out var rCvh);
                mapThanCoc10.TryGetValue(key, out var rThanCoc10);
                var goc = rCvh ?? rThanCoc10!;

                return new KetQuaThanCocDto
                {
                    IdLoCao = idLoCao,
                    Ca = goc.Ca,
                    IdNvl = goc.IDNVL,
                    TenNvl = tenNvlMap.GetValueOrDefault(goc.IDNVL),
                    MaCongDoanChiPhi = goc.MaCongDoanChiPhi,
                    IdNhomPhanBo = goc.IDNhomPhanBo,
                    TenNhomPhanBo = nhomMap.TryGetValue(goc.IDNhomPhanBo, out var nhom) ? nhom.TenNhom : null,
                    PhuongThucPhanBo = nhomMap.TryGetValue(goc.IDNhomPhanBo, out var nhom2) ? nhom2.PhuongThucPhanBo : (byte)0,
                    KhoiLuongNapLieu = goc.KhoiLuongNapLieu,
                    TyLePhanBo = goc.TyLePhanBo,
                    KhoiLuongPhanBoCvh = rCvh?.KhoiLuongPhanBo ?? 0,
                    KhoiLuongPhanBoThanCoc10 = rThanCoc10?.KhoiLuongPhanBo ?? 0,
                    KhoiLuongChotCuoi = goc.KhoiLuongNapLieu + (rCvh?.KhoiLuongPhanBo ?? 0) + (rThanCoc10?.KhoiLuongPhanBo ?? 0),
                    LaDongConLai = (rCvh?.LaDongConLai ?? false) || (rThanCoc10?.LaDongConLai ?? false),
                    TrangThai = rCvh?.TrangThai ?? rThanCoc10?.TrangThai ?? (byte)0
                };
            })
            .OrderBy(k => k.IdNhomPhanBo).ThenBy(k => k.IdNvl)
            .ToList();

            // Tổng E của nhóm = SUM(E) theo từng NVL DUY NHẤT (E dùng chung giữa CVH/ThanCoc10, không cộng dồn 2 lần)
            var eNhomMap = BuildENhomMap(ketQua.Select(k => (k.IdNhomPhanBo, k.KhoiLuongNapLieu)));
            foreach (var k in ketQua) k.KhoiLuongNapLieuNhom = eNhomMap.GetValueOrDefault(k.IdNhomPhanBo, 0m);

            var validateCvh = await BuildValidateAsync(ngay, (byte)LoaiPhanBoEnum.Cvh, idLoCao, ca, rowsCvh.Sum(r => r.KhoiLuongPhanBo));
            var validateThanCoc10 = await BuildValidateAsync(ngay, (byte)LoaiPhanBoEnum.ThanCoc10, idLoCao, ca, rowsThanCoc10.Sum(r => r.KhoiLuongPhanBo));

            // G của CVH/Than cốc <10mm — dùng chung cho toàn bộ bảng, lấy lại từ kết quả validate đã tính
            foreach (var k in ketQua)
            {
                k.KhoiLuongNhanVeCvh = validateCvh.TongNhanVe;
                k.KhoiLuongNhanVeThanCoc10 = validateThanCoc10.TongNhanVe;
            }

            return new KetQuaThanCocQueryResultDto
            {
                KetQua = ketQua,
                ValidateCvh = validateCvh,
                ValidateThanCoc10 = validateThanCoc10
            };
        }

        // ─── Gán/sửa Mã công đoạn chi phí (DQ1/DQ2) cho 1 NVL trong ngày ───────────────

        public async Task UpdateMaCongDoanChiPhiAsync(DateTime ngay, int idNvl, string? maCongDoanChiPhi)
        {
            var soDongCapNhat = await _ketQuaRepo.UpdateMaCongDoanChiPhiAsync(ngay.Date, idNvl, maCongDoanChiPhi);
            if (soDongCapNhat == 0)
                throw new InvalidOperationException("Không tìm thấy dòng kết quả phân bổ để cập nhật (có thể ngày đã chốt hoặc NVL chưa có kết quả).");
        }

        // ─── Chốt (khóa cả 3 loại phân bổ của 1 (Ngày, Ca, Lò cao)) ────────────────────

        public async Task ChotPhanBoAsync(DateTime ngay, byte ca, int idLoCao, int idNguoiXacNhan)
        {
            ngay = ngay.Date;

            // Validate trước cho cả 3 loại — chỉ chốt khi tất cả đều khớp tổng
            foreach (var loai in TatCaLoaiPhanBo)
                await ValidateTruocKhiChotAsync(ngay, loai, ca, idLoCao);

            foreach (var loai in TatCaLoaiPhanBo)
            {
                if (await _ketQuaRepo.IsNgayDaChotAsync(ngay, loai, ca, idLoCao)) continue;
                await _ketQuaRepo.ChotAsync(ngay, loai, ca, idLoCao, idNguoiXacNhan);
            }
        }

        // ─── Hủy chốt (mở khóa lại cả 3 loại phân bổ của 1 (Ngày, Ca, Lò cao) để sửa/tính lại) ─

        public async Task HuyChotPhanBoAsync(DateTime ngay, byte ca, int idLoCao, int idNguoiThucHien)
        {
            ngay = ngay.Date;

            foreach (var loai in TatCaLoaiPhanBo)
                await _ketQuaRepo.HuyChotAsync(ngay, loai, ca, idLoCao);
        }

        private async Task ValidateTruocKhiChotAsync(DateTime ngay, byte loaiPhanBo, byte ca, int idLoCao)
        {
            var rows = await _ketQuaRepo.GetByNgayAsync(ngay, loaiPhanBo, idLoCao, ca);
            if (rows.Count == 0) return;

            var bienBans = await _bienBanRepo.GetByNgayAsync(ngay, loaiPhanBo);
            var tongPhanBo = rows.Sum(r => r.KhoiLuongPhanBo);
            var tongNhanVe = bienBans
                .Where(b => b.IDLoCao == idLoCao && b.Ca == ca)
                .Sum(b => b.KhoiLuongNhanVe);
            if (Math.Abs(tongNhanVe - tongPhanBo) >= 0.001m)
                throw new InvalidOperationException(
                    $"Chưa thể chốt: lệch tổng phân bổ tại lò cao {idLoCao}, ca {ca} (loại phân bổ {loaiPhanBo}).");
        }

        public async Task<List<KetQuaPhanBoDto>> LayBaoCaoAsync(DateTime tuNgay, DateTime denNgay, int? idLoCao, byte? loaiPhanBo)
        {
            var rows = await _ketQuaRepo.GetBaoCaoAsync(tuNgay, denNgay, idLoCao, loaiPhanBo);
            var nhomMap = await _nhomRepo.GetByIdsAsync(rows.Select(r => r.IDNhomPhanBo));
            var tenNvlMap = await _nhomRepo.GetTenNvlMapAsync(rows.Select(r => r.IDNVL));

            // Tổng E của nhóm tính riêng theo từng (Ngày, Ca, Lò cao) — báo cáo trải nhiều ngày/ca/lò cao
            var eNhomMap = rows
                .GroupBy(r => (r.Ngay, r.Ca, r.IDLoCao, r.IDNhomPhanBo))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.KhoiLuongNapLieu));

            return rows.Select(r => new KetQuaPhanBoDto
            {
                Id = r.ID,
                Ngay = r.Ngay,
                Ca = r.Ca,
                IdLoCao = r.IDLoCao,
                LoaiPhanBo = r.LoaiPhanBo,
                IdNvl = r.IDNVL,
                TenNvl = tenNvlMap.GetValueOrDefault(r.IDNVL),
                MaCongDoanChiPhi = r.MaCongDoanChiPhi,
                IdNhomPhanBo = r.IDNhomPhanBo,
                TenNhomPhanBo = nhomMap.TryGetValue(r.IDNhomPhanBo, out var nhom) ? nhom.TenNhom : null,
                PhuongThucPhanBo = nhomMap.TryGetValue(r.IDNhomPhanBo, out var nhom2) ? nhom2.PhuongThucPhanBo : (byte)0,
                KhoiLuongNapLieu = r.KhoiLuongNapLieu,
                KhoiLuongNapLieuNhom = eNhomMap.GetValueOrDefault((r.Ngay, r.Ca, r.IDLoCao, r.IDNhomPhanBo), 0m),
                TyLePhanBo = r.TyLePhanBo,
                KhoiLuongPhanBo = r.KhoiLuongPhanBo,
                KhoiLuongChotCuoi = r.KhoiLuongChotCuoi,
                LaDongConLai = r.LaDongConLai,
                TrangThai = r.TrangThai
            }).ToList();
        }
    }
}
