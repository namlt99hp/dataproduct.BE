using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.ResponseModels;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Data;

namespace dataproduct.api.Repositories
{
    public class DLNMHRC2Repository   : IDLNMHRC2Repository   
    {
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterDataContext;
        public DLNMHRC2Repository (ProductFormContext context, ProductDataMasterDbContext masterDataContext)
        {
            _context = context;
            _masterDataContext = masterDataContext;
        }
        public async Task<IEnumerable<DLNM_HRC2>> GetAllAsync(DateTime? Ngay,int? Ca, string? BieuMau, int? Scope)
        {
            var query = _context.DLNM_HRC2s.Where(x => x.IsDelete != true).AsQueryable();

            if (Ngay.HasValue)
                query = query.Where(x => x.Ngay == Ngay.Value.Date);

            if (Ca.HasValue)
                query = query.Where(x => x.Ca == Ca.Value);

            if (!string.IsNullOrEmpty(BieuMau))
                query = query.Where(x => x.BieuMau == BieuMau);

            if (Scope.HasValue)
                query = query.Where(x => x.Scope == Scope.Value);
            if(BieuMau != "BOF"){
                return await query.OrderBy(x => x.NgaySx).ToListAsync();
            }
            return await query.OrderBy(x => x.MeThoi).ToListAsync();
        }

        public async Task<DLNM_HRC2?> GetByIdAsync(long id)
        {
            return await _context.DLNM_HRC2s.FirstOrDefaultAsync(x => x.ID == id && x.IsDelete != true);
        }

        public async Task<HRC2DetailByReportNoModel?> GetByReportNoAsync(int reportNo)
        {
            try
            {
                // Lấy 1 record DLNM_HRC2 per REPORT_NO
                var baseRecord = await _context.DLNM_HRC2s
                    .Where(x => x.REPORT_NO == reportNo && x.IsDelete != true)
                    .FirstOrDefaultAsync();

                if (baseRecord == null)
                {
                    return null;
                }

                // Lấy tất cả phụ liệu từ bảng PhuLieu_HRC2 (CHỈ lấy phụ liệu thực tế, không lấy phân bổ)
                var phuLieuItems = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.REPORT_NO == reportNo && 
                        x.ID_PhuLieu.HasValue &&
                        (x.IsPhanBo != true)) // ⭐ CHỈ lấy phụ liệu thực tế, không lấy phân bổ
                    .Select(x => new
                    {
                        ID_PhuLieu = x.ID_PhuLieu!.Value,
                        x.TenPhuLieu,
                        x.KLPhuGia,
                        KLPhuGia_Manual = x.KLPhuGia_Manual,
                        IsManual = x.IsManual
                    })
                    .GroupBy(x => x.ID_PhuLieu)
                    .Select(g => g.First())
                    .ToListAsync();

                var phuLieuIds = phuLieuItems
                    .Select(x => x.ID_PhuLieu)
                    .ToList();

                var mappings = await _context.Header_Mappings
                .Where(m => phuLieuIds.Contains(m.ID_PhuLieu))
                .ToListAsync();

                var headerKeyIds = mappings.Select(m => m.ID_HeaderKey).Distinct().ToList();

                var headerKeys = await _context.Header_Keys
                    .Where(k => headerKeyIds.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id);

                var mappingLookup = mappings
                    .GroupBy(m => m.ID_PhuLieu)
                    .ToDictionary(
                        g => g.Key,
                    g => g.Select(m =>
                    {
                        headerKeys.TryGetValue(m.ID_HeaderKey, out var header);
                        return new
                        {
                            m.Id,
                            m.ID_HeaderKey,
                            m.TenNguonDuLieu,
                            KeyGuid = header?.KeyGuid,
                            TenHienThi = header?.TenHienThi,
                            LoaiPhieu = header?.LoaiPhieu,
                        };
                    }).ToList());

                var phuLieus = new List<HeaderKey_ResponeModels>();

                foreach (var item in phuLieuItems)
                {
                    if (mappingLookup.TryGetValue(item.ID_PhuLieu, out var mapEntries) && mapEntries.Any())
                    {
                        foreach (var map in mapEntries)
                        {
                            phuLieus.Add(new HeaderKey_ResponeModels
                            {
                                MappingId = map.Id,
                                ID_PhuLieu = item.ID_PhuLieu,
                                TenPhuLieu = item.TenPhuLieu,
                                KLPhuGia = item.KLPhuGia,
                                ID_HeaderKey = map.ID_HeaderKey,
                                KeyGuid = map.KeyGuid,
                                TenHienThi = map.TenHienThi,
                                TenNguonDuLieu = map.TenNguonDuLieu,
                                ThuTu = null
                            });
                        }
                    }
                    else
                    {
                        phuLieus.Add(new HeaderKey_ResponeModels
                        {
                        MappingId = null,
                            ID_PhuLieu = item.ID_PhuLieu,
                            TenPhuLieu = item.TenPhuLieu,
                            KLPhuGia = item.KLPhuGia
                        });
                    }
                }
                
                var response = new HRC2DetailByReportNoModel
                {
                    data = new DLNM_HRC2_ResponseModels
                    {
                        ID = baseRecord.ID,
                        REPORT_NO = baseRecord.REPORT_NO,
                        NgaySx = baseRecord.NgaySx,
                        Ngay = baseRecord.Ngay,
                        Ca = baseRecord.Ca,
                        BieuMau = baseRecord.BieuMau,
                        Scope = baseRecord.Scope,
                        MeThoi = baseRecord.MeThoi,
                        MacThep = baseRecord.MacThep_Manual ?? baseRecord.MacThep,
                        IsManualMacThep = baseRecord.IsManualMacThep,
                        MacThepGoc = baseRecord.MacThep,
                        O2 = FormatNumber(baseRecord.O2),
                        AR_RH = FormatNumber(baseRecord.AR_RH),
                        N2 = FormatNumber(baseRecord.N2),
                        AR_BOF = FormatNumber(baseRecord.AR_BOF),
                        AR_LF = FormatNumber(baseRecord.AR_LF),
                        KLGangLong = FormatNumber(baseRecord.KLGangLong),
                        KLThepPhe = FormatNumber(baseRecord.KLThepPhe),
                        IsNM = baseRecord.IsNM,
                        KLGangLongCCT = FormatNumber(baseRecord.KLGangLongCCT),
                        KLGangLongCR = FormatNumber( baseRecord.KLGangLongCR),
                        KLThepPheGang = FormatNumber(baseRecord.KLThepPheGang),
                        KLThepLong = FormatNumber(baseRecord.KLThepLong),
                        QueLayMau = baseRecord.QueLayMau,
                        QueDoNhiet = baseRecord.QueDoNhiet,
                        GhiChu = baseRecord.GhiChu
                    },
                    phulieus = phuLieus
                };

                foreach (var item in response.phulieus)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                }

                return response;
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error while getting report {reportNo}", ex);
            }
        }

        public async Task<HRC2GroupedByReportNoModel?> GetByMeThoiGroupedAsync(string meThoi)
        {
            try
            {
                // Lấy 1 record DLNM_HRC2 per REPORT_NO
                var baseRecord = await _context.DLNM_HRC2s
                    .Where(x => x.MeThoi == meThoi && x.IsDelete != true)
                    .FirstOrDefaultAsync();

                if (baseRecord == null)
                {
                    return null;
                }

                // Lấy tất cả phụ liệu từ bảng PhuLieu_HRC2 (CHỈ lấy phụ liệu thực tế, không lấy phân bổ)
                var allPhuLieuData = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.MeThoi == meThoi && 
                        x.ID_PhuLieu.HasValue &&
                        (x.IsPhanBo != true)) // ⭐ CHỈ lấy phụ liệu thực tế, không lấy phân bổ
                    .Select(x => new
                    {
                        ID_PhuLieu = x.ID_PhuLieu!.Value,
                        x.TenPhuLieu,
                        x.KLPhuGia,
                        x.IsManual,
                        x.KLPhuGia_Manual
                    })
                    .ToListAsync();

                var phuLieuIds = allPhuLieuData
                    .Select(x => x.ID_PhuLieu)
                    .Distinct()
                    .ToList();

                // Lấy mappings
                var mappings = await _context.Header_Mappings
                    .Where(m => phuLieuIds.Contains(m.ID_PhuLieu))
                    .ToListAsync();

                var headerKeyIds = mappings.Select(m => m.ID_HeaderKey).Distinct().ToList();

                var headerKeys = await _context.Header_Keys
                    .Where(k => headerKeyIds.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id);

                // Tạo lookup mapping theo ID_PhuLieu
                var mappingLookup = mappings
                    .GroupBy(m => m.ID_PhuLieu)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(m =>
                        {
                            headerKeys.TryGetValue(m.ID_HeaderKey, out var header);
                            return new
                            {
                                m.Id,
                                m.ID_HeaderKey,
                                m.TenNguonDuLieu,
                                KeyGuid = header?.KeyGuid,
                                TenHienThi = header?.TenHienThi,
                                LoaiPhieu = header?.LoaiPhieu,
                                IsActive = header?.IsActive ?? false,
                            };
                        }).ToList());

                // Group phụ liệu đã mapped theo idHeaderKey
                var groupedMappedPhuLieus = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                // Group phụ liệu chưa mapped theo tên
                var groupedUnmappedPhuLieus = new Dictionary<string, HeaderKeyGroupedByReportNoModel>();

                foreach (var phuLieuItem in allPhuLieuData)
                {
                    if (mappingLookup.TryGetValue(phuLieuItem.ID_PhuLieu, out var mapEntries) && mapEntries.Any())
                    {
                        var activeMaps = mapEntries
                            .Where(m => m.IsActive && m.KeyGuid.HasValue)
                            .ToList();

                        if (activeMaps.Any())
                        {
                            // Phụ liệu đã mapped: group theo idHeaderKey
                            foreach (var map in activeMaps)
                            {
                                var headerKeyId = map.ID_HeaderKey;
                                var formattedValue = FormatNumber(phuLieuItem.KLPhuGia);
                                var formattedManual = FormatNumber(phuLieuItem.KLPhuGia_Manual);
                                if (!groupedMappedPhuLieus.ContainsKey(headerKeyId))
                                {
                                    groupedMappedPhuLieus[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                                    {
                                        ID_HeaderKey = map.ID_HeaderKey,
                                        KeyGuid = map.KeyGuid,
                                        TenHienThi = map.TenHienThi,
                                        TenNguonDuLieu = map.TenNguonDuLieu,
                                        ID_PhuLieu = phuLieuItem.ID_PhuLieu,
                                        TenPhuLieu = phuLieuItem.TenPhuLieu,
                                        KLPhuGia = formattedValue,
                                        KLPhuGia_Manual = formattedManual,
                                        IsManual = phuLieuItem.IsManual,
                                        KLPhuGiaTotal = 0,
                                        LoaiPhuLieu = map.LoaiPhieu,
                                        MappingId = map.Id,
                                        ThuTu = headerKeys.TryGetValue(map.ID_HeaderKey, out var hkMe)
                                            ? (string.Equals(baseRecord.BieuMau, "BOF", StringComparison.OrdinalIgnoreCase) ? hkMe.ThuTu_Excel_BOF
                                               : string.Equals(baseRecord.BieuMau, "RH", StringComparison.OrdinalIgnoreCase) ? hkMe.ThuTu_Excel_RH
                                               : hkMe.ThuTu_Excel_LF)
                                            : null
                                    };
                                }
                                // Sum KLPhuGia
                                groupedMappedPhuLieus[headerKeyId].KLPhuGiaTotal = (groupedMappedPhuLieus[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                            }
                            continue;
                        }

                        // Có mapping nhưng toàn bộ Header Key đã bị vô hiệu → bỏ qua hoàn toàn
                        if (mapEntries.Any())
                        {
                            continue;
                        }
                    }

                    // Phụ liệu chưa mapped (không có bản ghi mapping nào)
                    var groupKey = (phuLieuItem.TenPhuLieu ?? $"PL_{phuLieuItem.ID_PhuLieu}")?.Trim() ?? $"PL_{phuLieuItem.ID_PhuLieu}";
                    if (!groupedUnmappedPhuLieus.ContainsKey(groupKey))
                    {
                        groupedUnmappedPhuLieus[groupKey] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = null,
                            KeyGuid = null,
                            TenHienThi = null,
                            TenNguonDuLieu = phuLieuItem.TenPhuLieu,
                            ID_PhuLieu = phuLieuItem.ID_PhuLieu,
                            TenPhuLieu = phuLieuItem.TenPhuLieu,
                            KLPhuGia = FormatNumber(phuLieuItem.KLPhuGia),
                            KLPhuGiaTotal = 0,
                            LoaiPhuLieu = null // Unmapped phụ liệu không có LoaiPhieu từ Header_Key
                        };
                    }
                    // Sum KLPhuGia
                    groupedUnmappedPhuLieus[groupKey].KLPhuGiaTotal = (groupedUnmappedPhuLieus[groupKey].KLPhuGiaTotal ?? 0) + (FormatNumber(phuLieuItem.KLPhuGia) ?? 0);
                }

                foreach (var item in groupedMappedPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                foreach (var item in groupedUnmappedPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                // ========== Lấy dữ liệu phân bổ (IsPhanBo = true) và nhóm theo HeaderKey ==========
                var phanBoData = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.MeThoi == meThoi && 
                        x.IsPhanBo == true &&
                        x.ID_HeaderKey.HasValue)
                    .Select(x => new
                    {
                        x.ID_HeaderKey,
                        x.KLPhuGia,
                        x.TenHienThi,
                    })
                    .ToListAsync();

                // Nhóm phân bổ theo ID_HeaderKey
                var groupedPhanBoPhuLieus = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                foreach (var phanBoItem in phanBoData)
                {
                    if (!phanBoItem.ID_HeaderKey.HasValue) continue;
                    var headerKeyId = phanBoItem.ID_HeaderKey.Value;
                    var formattedValue = FormatNumber(phanBoItem.KLPhuGia);
                    
                    if (!groupedPhanBoPhuLieus.ContainsKey(headerKeyId))
                    {
                        groupedPhanBoPhuLieus[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = headerKeyId,
                            TenHienThi = phanBoItem.TenHienThi,
                            KLPhuGia = formattedValue,
                            KLPhuGiaTotal = 0,
                        };
                    }
                    // Sum KLPhuGia
                    groupedPhanBoPhuLieus[headerKeyId].KLPhuGiaTotal = (groupedPhanBoPhuLieus[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                }

                foreach (var item in groupedPhanBoPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                // ========== Lấy dữ liệu điều chỉnh tay (IsManual = true, IsPhanBo != true) và nhóm theo HeaderKey ==========
                var manualAdjustData = await _context.PhuLieu_HRC2s
                    .Where(x =>
                        x.MeThoi == meThoi &&
                        x.IsManual == true &&
                        x.IsAddManual == true &&
                        (x.IsPhanBo != true) &&
                        x.ID_HeaderKey.HasValue)
                    .Select(x => new
                    {
                        x.ID_HeaderKey,
                        x.KLPhuGia_Manual,
                        x.TenHienThi,
                        x.IsManual
                    })
                    .ToListAsync();

                var groupedManualAdjust = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                foreach (var item in manualAdjustData)
                {
                    if (!item.ID_HeaderKey.HasValue) continue;
                    var headerKeyId = item.ID_HeaderKey.Value;
                    var formattedValue = FormatNumber(item.KLPhuGia_Manual);

                    if (!groupedManualAdjust.ContainsKey(headerKeyId))
                    {
                        groupedManualAdjust[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = headerKeyId,
                            TenHienThi = item.TenHienThi,
                            KLPhuGia = null,
                            KLPhuGia_Manual = formattedValue,
                            IsManual = true,
                            KLPhuGiaTotal = 0,
                        };
                    }

                    groupedManualAdjust[headerKeyId].KLPhuGiaTotal =
                        (groupedManualAdjust[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                }

                foreach (var item in groupedManualAdjust.Values)
                {
                    item.KLPhuGia_Manual = FormatNumber(item.KLPhuGia_Manual);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                return new HRC2GroupedByReportNoModel
                {
                    data = new DLNM_HRC2_ResponseModels
                    {
                        ID = baseRecord.ID,
                        REPORT_NO = baseRecord.REPORT_NO,
                        NgaySx = baseRecord.NgaySx,
                        Ngay = baseRecord.Ngay,
                        Ca = baseRecord.Ca,
                        BieuMau = baseRecord.BieuMau,
                        Scope = baseRecord.Scope,
                        MeThoi = baseRecord.MeThoi,
                        MacThep = baseRecord.MacThep_Manual ?? baseRecord.MacThep,
                        IsManualMacThep = baseRecord.IsManualMacThep,
                        MacThepGoc = baseRecord.MacThep,
                        IsNM = baseRecord.IsNM,
                        IsChuyenCa = baseRecord.IsChuyenCa,
                        O2 = FormatNumber(baseRecord.O2),
                        AR_RH = FormatNumber(baseRecord.AR_RH),
                        N2 = FormatNumber(baseRecord.N2),
                        AR_BOF = FormatNumber(baseRecord.AR_BOF),
                        AR_LF = FormatNumber(baseRecord.AR_LF),
                        KLGangLong = FormatNumber(baseRecord.KLGangLong),
                        KLThepPhe = FormatNumber(baseRecord.KLThepPhe),
                        KLGangLongCCT = FormatNumber(baseRecord.KLGangLongCCT),
                        KLGangLongCR = FormatNumber(baseRecord.KLGangLongCR),
                        KLThepPheGang = FormatNumber(baseRecord.KLThepPheGang),
                        KLThepLong = FormatNumber(baseRecord.KLThepLong),
                        QueLayMau = baseRecord.QueLayMau,
                        QueDoNhiet = baseRecord.QueDoNhiet,
                        GhiChu = baseRecord.GhiChu
                    },
                    mappedPhulieus = groupedMappedPhuLieus.Values.ToList(),
                    unmappedPhulieus = groupedUnmappedPhuLieus.Values.ToList(),
                    phanBoPhulieus = groupedPhanBoPhuLieus.Values.ToList(),
                    manualAdjustPhulieus = groupedManualAdjust.Values.ToList()
                };
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error while getting grouped report {meThoi}", ex);
            }
        }
        public async Task<HRC2GroupedByReportNoModel?> GetByReportNoGroupedAsync(int reportNo)
        {
            try
            {
                // Lấy 1 record DLNM_HRC2 per REPORT_NO
                var baseRecord = await _context.DLNM_HRC2s
                    .Where(x => x.REPORT_NO == reportNo && x.IsDelete != true)
                    .FirstOrDefaultAsync();

                if (baseRecord == null)
                {
                    return null;
                }

                // Lấy tất cả phụ liệu từ bảng PhuLieu_HRC2 (CHỈ lấy phụ liệu thực tế, không lấy phân bổ)
                var allPhuLieuData = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.REPORT_NO == reportNo && 
                        x.ID_PhuLieu.HasValue &&
                        (x.IsPhanBo != true)) // ⭐ CHỈ lấy phụ liệu thực tế, không lấy phân bổ
                    .Select(x => new
                    {
                        ID_PhuLieu = x.ID_PhuLieu!.Value,
                        x.TenPhuLieu,
                        x.KLPhuGia,
                        x.IsManual,
                        x.KLPhuGia_Manual
                    })
                    .ToListAsync();

                var phuLieuIds = allPhuLieuData
                    .Select(x => x.ID_PhuLieu)
                    .Distinct()
                    .ToList();

                // Lấy mappings
                var mappings = await _context.Header_Mappings
                    .Where(m => phuLieuIds.Contains(m.ID_PhuLieu))
                    .ToListAsync();

                var headerKeyIds = mappings.Select(m => m.ID_HeaderKey).Distinct().ToList();

                var headerKeys = await _context.Header_Keys
                    .Where(k => headerKeyIds.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id);

                // Tạo lookup mapping theo ID_PhuLieu
                var mappingLookup = mappings
                    .GroupBy(m => m.ID_PhuLieu)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(m =>
                        {
                            headerKeys.TryGetValue(m.ID_HeaderKey, out var header);
                            return new
                            {
                                m.Id,
                                m.ID_HeaderKey,
                                m.TenNguonDuLieu,
                                KeyGuid = header?.KeyGuid,
                                TenHienThi = header?.TenHienThi,
                                LoaiPhieu = header?.LoaiPhieu,
                                IsActive = header?.IsActive ?? false,
                            };
                        }).ToList());

                // Group phụ liệu đã mapped theo idHeaderKey
                var groupedMappedPhuLieus = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                // Group phụ liệu chưa mapped theo tên
                var groupedUnmappedPhuLieus = new Dictionary<string, HeaderKeyGroupedByReportNoModel>();

                foreach (var phuLieuItem in allPhuLieuData)
                {
                    if (mappingLookup.TryGetValue(phuLieuItem.ID_PhuLieu, out var mapEntries) && mapEntries.Any())
                    {
                        var activeMaps = mapEntries
                            .Where(m => m.IsActive && m.KeyGuid.HasValue)
                            .ToList();

                        if (activeMaps.Any())
                        {
                            // Phụ liệu đã mapped: group theo idHeaderKey
                            foreach (var map in activeMaps)
                            {
                                var headerKeyId = map.ID_HeaderKey;
                                var formattedValue = FormatNumber(phuLieuItem.KLPhuGia);
                                var formattedManualValue = FormatNumber(phuLieuItem.KLPhuGia_Manual);
                                if (!groupedMappedPhuLieus.ContainsKey(headerKeyId))
                                {
                                    groupedMappedPhuLieus[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                                    {
                                        ID_HeaderKey = map.ID_HeaderKey,
                                        KeyGuid = map.KeyGuid,
                                        TenHienThi = map.TenHienThi,
                                        TenNguonDuLieu = map.TenNguonDuLieu,
                                        ID_PhuLieu = phuLieuItem.ID_PhuLieu,
                                        TenPhuLieu = phuLieuItem.TenPhuLieu,
                                        KLPhuGia = formattedValue,
                                        KLPhuGia_Manual = formattedManualValue,
                                        IsManual = phuLieuItem.IsManual,
                                        KLPhuGiaTotal = 0,
                                        LoaiPhuLieu = map.LoaiPhieu,
                                        MappingId = map.Id,
                                        ThuTu = headerKeys.TryGetValue(map.ID_HeaderKey, out var hkRN)
                                            ? (string.Equals(baseRecord.BieuMau, "BOF", StringComparison.OrdinalIgnoreCase) ? hkRN.ThuTu_Excel_BOF
                                               : string.Equals(baseRecord.BieuMau, "RH", StringComparison.OrdinalIgnoreCase) ? hkRN.ThuTu_Excel_RH
                                               : hkRN.ThuTu_Excel_LF)
                                            : null
                                    };
                                }
                                // Sum KLPhuGia
                                groupedMappedPhuLieus[headerKeyId].KLPhuGiaTotal = (groupedMappedPhuLieus[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                            }
                            continue;
                        }

                        // Có mapping nhưng toàn bộ Header Key đã bị vô hiệu → bỏ qua hoàn toàn
                        if (mapEntries.Any())
                        {
                            continue;
                        }
                    }

                    // Phụ liệu chưa mapped (không có bản ghi mapping nào)
                    var groupKey = (phuLieuItem.TenPhuLieu ?? $"PL_{phuLieuItem.ID_PhuLieu}")?.Trim() ?? $"PL_{phuLieuItem.ID_PhuLieu}";
                    if (!groupedUnmappedPhuLieus.ContainsKey(groupKey))
                    {
                        groupedUnmappedPhuLieus[groupKey] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = null,
                            KeyGuid = null,
                            TenHienThi = null,
                            TenNguonDuLieu = phuLieuItem.TenPhuLieu,
                            ID_PhuLieu = phuLieuItem.ID_PhuLieu,
                            TenPhuLieu = phuLieuItem.TenPhuLieu,
                            KLPhuGia = FormatNumber(phuLieuItem.KLPhuGia),
                            KLPhuGia_Manual = FormatNumber(phuLieuItem.KLPhuGia_Manual),
                            IsManual = phuLieuItem.IsManual,
                            KLPhuGiaTotal = 0,
                            LoaiPhuLieu = null // Unmapped phụ liệu không có LoaiPhieu từ Header_Key
                        };
                    }
                    // Sum KLPhuGia
                    groupedUnmappedPhuLieus[groupKey].KLPhuGiaTotal = (groupedUnmappedPhuLieus[groupKey].KLPhuGiaTotal ?? 0) + (FormatNumber(phuLieuItem.KLPhuGia) ?? 0);
                }

                foreach (var item in groupedMappedPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                foreach (var item in groupedUnmappedPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                // ========== Lấy dữ liệu phân bổ (IsPhanBo = true) và nhóm theo HeaderKey ==========
                var phanBoData = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.REPORT_NO == reportNo && 
                        x.IsPhanBo == true &&
                        x.ID_HeaderKey.HasValue)
                    .Select(x => new
                    {
                        x.ID_HeaderKey,
                        x.KLPhuGia,
                        x.KLPhuGia_Manual,
                        x.IsManual,
                        x.TenHienThi,
                    })
                    .ToListAsync();

                // Nhóm phân bổ theo ID_HeaderKey
                var groupedPhanBoPhuLieus = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                foreach (var phanBoItem in phanBoData)
                {
                    if (!phanBoItem.ID_HeaderKey.HasValue) continue;
                    var headerKeyId = phanBoItem.ID_HeaderKey.Value;
                    var formattedValue = FormatNumber(phanBoItem.KLPhuGia);
                    var formattedManual = FormatNumber(phanBoItem.KLPhuGia_Manual);
                    
                    if (!groupedPhanBoPhuLieus.ContainsKey(headerKeyId))
                    {
                        groupedPhanBoPhuLieus[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = headerKeyId,
                            TenHienThi = phanBoItem.TenHienThi,
                            KLPhuGia = formattedValue,
                            KLPhuGia_Manual = formattedManual,
                            IsManual = phanBoItem.IsManual,
                            KLPhuGiaTotal = 0,
                        };
                    }
                    // Sum KLPhuGia
                    groupedPhanBoPhuLieus[headerKeyId].KLPhuGiaTotal = (groupedPhanBoPhuLieus[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                }

                foreach (var item in groupedPhanBoPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                // ========== Lấy dữ liệu điều chỉnh tay (IsManual = true, IsPhanBo != true) và nhóm theo HeaderKey ==========
                var manualAdjustData = await _context.PhuLieu_HRC2s
                    .Where(x =>
                        x.REPORT_NO == reportNo &&
                        x.IsManual == true &&
                        x.IsAddManual == true &&
                        (x.IsPhanBo != true) &&
                        x.ID_HeaderKey.HasValue)
                    .Select(x => new
                    {
                        x.ID_HeaderKey,
                        x.KLPhuGia_Manual,
                        x.TenHienThi,
                    })
                    .ToListAsync();

                var groupedManualAdjust = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                foreach (var item in manualAdjustData)
                {
                    if (!item.ID_HeaderKey.HasValue) continue;
                    var headerKeyId = item.ID_HeaderKey.Value;
                    var formattedValue = FormatNumber(item.KLPhuGia_Manual);

                    if (!groupedManualAdjust.ContainsKey(headerKeyId))
                    {
                        groupedManualAdjust[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = headerKeyId,
                            TenHienThi = item.TenHienThi,
                            KLPhuGia = null,
                            KLPhuGia_Manual = formattedValue,
                            IsManual = true,
                            KLPhuGiaTotal = 0,
                        };
                    }

                    groupedManualAdjust[headerKeyId].KLPhuGiaTotal =
                        (groupedManualAdjust[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                }

                foreach (var item in groupedManualAdjust.Values)
                {
                    item.KLPhuGia_Manual = FormatNumber(item.KLPhuGia_Manual);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                return new HRC2GroupedByReportNoModel
                {
                    data = new DLNM_HRC2_ResponseModels
                    {
                        ID = baseRecord.ID,
                        REPORT_NO = baseRecord.REPORT_NO,
                        NgaySx = baseRecord.NgaySx,
                        Ngay = baseRecord.Ngay,
                        Ca = baseRecord.Ca,
                        BieuMau = baseRecord.BieuMau,
                        Scope = baseRecord.Scope,
                        MeThoi = baseRecord.MeThoi,
                        MacThep = baseRecord.MacThep_Manual ?? baseRecord.MacThep,
                        IsManualMacThep = baseRecord.IsManualMacThep,
                        MacThepGoc = baseRecord.MacThep,
                        O2 = FormatNumber(baseRecord.O2),
                        AR_RH = FormatNumber(baseRecord.AR_RH),
                        N2 = FormatNumber(baseRecord.N2),
                        AR_BOF = FormatNumber(baseRecord.AR_BOF),
                        AR_LF = FormatNumber(baseRecord.AR_LF),
                        KLGangLong = FormatNumber(baseRecord.KLGangLong),
                        KLThepPhe = FormatNumber(baseRecord.KLThepPhe),
                        KLGangLongCCT = FormatNumber(baseRecord.KLGangLongCCT),
                        KLGangLongCR = FormatNumber(baseRecord.KLGangLongCR),
                        KLThepPheGang = FormatNumber(baseRecord.KLThepPheGang),
                        KLThepLong = FormatNumber(baseRecord.KLThepLong)
                    },
                    mappedPhulieus = groupedMappedPhuLieus.Values.ToList(),
                    unmappedPhulieus = groupedUnmappedPhuLieus.Values.ToList(),
                    phanBoPhulieus = groupedPhanBoPhuLieus.Values.ToList(),
                    manualAdjustPhulieus = groupedManualAdjust.Values.ToList()
                };
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error while getting grouped report {reportNo}", ex);
            }
        }

        public async Task<HRC2GroupedByReportNoModel?>  GetByIdGroupedAsync(int id)
        {
            try
            {
                // Lấy 1 record DLNM_HRC2 per REPORT_NO
                var baseRecord = await _context.DLNM_HRC2s
                    .Where(x => x.ID == id && x.IsDelete != true)
                    .FirstOrDefaultAsync();

                if (baseRecord == null)
                {
                    return null;
                }

                // Lấy tất cả phụ liệu từ bảng PhuLieu_HRC2 (CHỈ lấy phụ liệu thực tế, không lấy phân bổ)
                var allPhuLieuData = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.ID_MeThoi == id && 
                        x.ID_PhuLieu.HasValue &&
                        (x.IsPhanBo != true)) // ⭐ CHỈ lấy phụ liệu thực tế, không lấy phân bổ
                    .Select(x => new
                    {
                        ID_PhuLieu = x.ID_PhuLieu!.Value,
                        x.TenPhuLieu,
                        x.KLPhuGia,
                        x.IsManual,
                        x.KLPhuGia_Manual,
                    })
                    .ToListAsync();

                var phuLieuIds = allPhuLieuData
                    .Select(x => x.ID_PhuLieu)
                    .Distinct()
                    .ToList();

                // Lấy mappings
                var mappings = await _context.Header_Mappings
                    .Where(m => phuLieuIds.Contains(m.ID_PhuLieu))
                    .ToListAsync();

                var headerKeyIds = mappings.Select(m => m.ID_HeaderKey).Distinct().ToList();

                var headerKeys = await _context.Header_Keys
                    .Where(k => headerKeyIds.Contains(k.Id))
                    .ToDictionaryAsync(k => k.Id);

                // Tạo lookup mapping theo ID_PhuLieu
                var mappingLookup = mappings
                    .GroupBy(m => m.ID_PhuLieu)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(m =>
                        {
                            headerKeys.TryGetValue(m.ID_HeaderKey, out var header);
                            return new
                            {
                                m.Id,
                                m.ID_HeaderKey,
                                m.TenNguonDuLieu,
                                KeyGuid = header?.KeyGuid,
                                TenHienThi = header?.TenHienThi,
                                LoaiPhieu = header?.LoaiPhieu,
                                IsActive = header?.IsActive ?? false,
                            };
                        }).ToList());

                // Group phụ liệu đã mapped theo idHeaderKey
                var groupedMappedPhuLieus = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                // Group phụ liệu chưa mapped theo tên
                var groupedUnmappedPhuLieus = new Dictionary<string, HeaderKeyGroupedByReportNoModel>();

                foreach (var phuLieuItem in allPhuLieuData)
                {
                    if (mappingLookup.TryGetValue(phuLieuItem.ID_PhuLieu, out var mapEntries) && mapEntries.Any())
                    {
                        var activeMaps = mapEntries
                            .Where(m => m.IsActive && m.KeyGuid.HasValue)
                            .ToList();

                        if (activeMaps.Any())
                        {
                            // Phụ liệu đã mapped: group theo idHeaderKey
                            foreach (var map in activeMaps)
                            {
                                var headerKeyId = map.ID_HeaderKey;
                                // Header_Key Id=5: KLPhuGia ÷ 0.055, làm tròn không chữ số thập phân
                                var formattedValue = headerKeyId == 5
                                    ? (phuLieuItem.KLPhuGia.HasValue
                                        ? (double?)Math.Round(phuLieuItem.KLPhuGia.Value / 0.055, 0, MidpointRounding.AwayFromZero)
                                        : null)
                                    : FormatNumber(phuLieuItem.KLPhuGia);
                                if (!groupedMappedPhuLieus.ContainsKey(headerKeyId))
                                {
                                    groupedMappedPhuLieus[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                                    {
                                        ID_HeaderKey = map.ID_HeaderKey,
                                        KeyGuid = map.KeyGuid,
                                        TenHienThi = map.TenHienThi,
                                        TenNguonDuLieu = map.TenNguonDuLieu,
                                        ID_PhuLieu = phuLieuItem.ID_PhuLieu,
                                        TenPhuLieu = phuLieuItem.TenPhuLieu,
                                        KLPhuGia = formattedValue,
                                        KLPhuGiaTotal = 0,
                                        IsManual = phuLieuItem.IsManual,
                                        KLPhuGia_Manual = phuLieuItem.KLPhuGia_Manual,
                                        LoaiPhuLieu = map.LoaiPhieu,
                                        MappingId = map.Id,
                                        ThuTu = headerKeys.TryGetValue(map.ID_HeaderKey, out var hkId)
                                            ? (string.Equals(baseRecord.BieuMau, "BOF", StringComparison.OrdinalIgnoreCase) ? hkId.ThuTu_Excel_BOF
                                               : string.Equals(baseRecord.BieuMau, "RH", StringComparison.OrdinalIgnoreCase) ? hkId.ThuTu_Excel_RH
                                               : hkId.ThuTu_Excel_LF)
                                            : null
                                    };
                                }
                                else if (phuLieuItem.IsManual == true)
                                {
                                    // Nếu bất kỳ phụ liệu nào trong nhóm có manual, nhóm được đánh dấu manual
                                    groupedMappedPhuLieus[headerKeyId].IsManual = true;
                                    groupedMappedPhuLieus[headerKeyId].KLPhuGia_Manual = phuLieuItem.KLPhuGia_Manual;
                                }
                                // Sum KLPhuGia
                                groupedMappedPhuLieus[headerKeyId].KLPhuGiaTotal = (groupedMappedPhuLieus[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                            }
                            continue;
                        }

                        // Có mapping nhưng toàn bộ Header Key đã bị vô hiệu → bỏ qua hoàn toàn
                        if (mapEntries.Any())
                        {
                            continue;
                        }
                    }

                    // Phụ liệu chưa mapped (không có bản ghi mapping nào)
                    var groupKey = (phuLieuItem.TenPhuLieu ?? $"PL_{phuLieuItem.ID_PhuLieu}")?.Trim() ?? $"PL_{phuLieuItem.ID_PhuLieu}";
                    if (!groupedUnmappedPhuLieus.ContainsKey(groupKey))
                    {
                        groupedUnmappedPhuLieus[groupKey] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = null,
                            KeyGuid = null,
                            TenHienThi = null,
                            TenNguonDuLieu = phuLieuItem.TenPhuLieu,
                            ID_PhuLieu = phuLieuItem.ID_PhuLieu,
                            TenPhuLieu = phuLieuItem.TenPhuLieu,
                            KLPhuGia = FormatNumber(phuLieuItem.KLPhuGia),
                            KLPhuGiaTotal = 0,
                            LoaiPhuLieu = null // Unmapped phụ liệu không có LoaiPhieu từ Header_Key
                        };
                    }
                    // Sum KLPhuGia
                    groupedUnmappedPhuLieus[groupKey].KLPhuGiaTotal = (groupedUnmappedPhuLieus[groupKey].KLPhuGiaTotal ?? 0) + (FormatNumber(phuLieuItem.KLPhuGia) ?? 0);
                }

                foreach (var item in groupedMappedPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                foreach (var item in groupedUnmappedPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                // ========== Lấy dữ liệu phân bổ (IsPhanBo = true) và nhóm theo HeaderKey ==========
                var phanBoData = await _context.PhuLieu_HRC2s
                    .Where(x => 
                        x.ID_MeThoi == id && 
                        x.IsPhanBo == true &&
                        x.ID_HeaderKey.HasValue)
                    .Select(x => new
                    {
                        x.ID_HeaderKey,
                        x.KLPhuGia,
                        x.KLPhuGia_Manual,
                        x.IsManual,
                        x.TenHienThi,
                    })
                    .ToListAsync();

                // Nhóm phân bổ theo ID_HeaderKey
                var groupedPhanBoPhuLieus = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                foreach (var phanBoItem in phanBoData)
                {
                    if (!phanBoItem.ID_HeaderKey.HasValue) continue;
                    var headerKeyId = phanBoItem.ID_HeaderKey.Value;
                    var formattedValue = FormatNumber(phanBoItem.KLPhuGia);
                    var formattedManual = FormatNumber(phanBoItem.KLPhuGia_Manual);
                    
                    if (!groupedPhanBoPhuLieus.ContainsKey(headerKeyId))
                    {
                        groupedPhanBoPhuLieus[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = headerKeyId,
                            TenHienThi = phanBoItem.TenHienThi,
                            KLPhuGia = formattedValue,
                            KLPhuGia_Manual = formattedManual,
                            IsManual = phanBoItem.IsManual,
                            KLPhuGiaTotal = 0,
                        };
                    }
                    // Sum KLPhuGia
                    groupedPhanBoPhuLieus[headerKeyId].KLPhuGiaTotal = (groupedPhanBoPhuLieus[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                }

                foreach (var item in groupedPhanBoPhuLieus.Values)
                {
                    item.KLPhuGia = FormatNumber(item.KLPhuGia);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                // ========== Lấy dữ liệu điều chỉnh tay (IsManual = true, IsPhanBo != true) và nhóm theo HeaderKey ==========
                var manualAdjustData = await _context.PhuLieu_HRC2s
                    .Where(x =>
                        x.ID_MeThoi == id &&
                        x.IsManual == true &&
                        x.IsAddManual == true &&
                        (x.IsPhanBo != true) &&
                        x.ID_HeaderKey.HasValue)
                    .Select(x => new
                    {
                        x.ID_HeaderKey,
                        x.KLPhuGia_Manual,
                        x.TenHienThi,
                    })
                    .ToListAsync();

                var groupedManualAdjust = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                foreach (var item in manualAdjustData)
                {
                    if (!item.ID_HeaderKey.HasValue) continue;
                    var headerKeyId = item.ID_HeaderKey.Value;
                    var formattedValue = FormatNumber(item.KLPhuGia_Manual);

                    if (!groupedManualAdjust.ContainsKey(headerKeyId))
                    {
                        groupedManualAdjust[headerKeyId] = new HeaderKeyGroupedByReportNoModel
                        {
                            ID_HeaderKey = headerKeyId,
                            TenHienThi = item.TenHienThi,
                            KLPhuGia = null,
                            KLPhuGia_Manual = formattedValue,
                            IsManual = true,
                            KLPhuGiaTotal = 0,
                        };
                    }

                    groupedManualAdjust[headerKeyId].KLPhuGiaTotal =
                        (groupedManualAdjust[headerKeyId].KLPhuGiaTotal ?? 0) + (formattedValue ?? 0);
                }

                foreach (var item in groupedManualAdjust.Values)
                {
                    item.KLPhuGia_Manual = FormatNumber(item.KLPhuGia_Manual);
                    item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal);
                }

                return new HRC2GroupedByReportNoModel
                {
                    data = new DLNM_HRC2_ResponseModels
                    {
                        ID = baseRecord.ID,
                        REPORT_NO = baseRecord.REPORT_NO,
                        NgaySx = baseRecord.NgaySx,
                        Ngay = baseRecord.Ngay,
                        Ca = baseRecord.Ca,
                        BieuMau = baseRecord.BieuMau,
                        Scope = baseRecord.Scope,
                        MeThoi = baseRecord.MeThoi,
                        MacThep = baseRecord.MacThep_Manual ?? baseRecord.MacThep,
                        IsManualMacThep = baseRecord.IsManualMacThep,
                        MacThepGoc = baseRecord.MacThep,
                        IsNM = baseRecord.IsNM,
                        IsChuyenCa = baseRecord.IsChuyenCa,
                        O2 = FormatNumber(baseRecord.O2),
                        AR_RH = FormatNumber(baseRecord.AR_RH),
                        N2 = FormatNumber(baseRecord.N2),
                        AR_BOF = FormatNumber(baseRecord.AR_BOF),
                        AR_LF = FormatNumber(baseRecord.AR_LF),
                        KLGangLong = FormatNumber(baseRecord.KLGangLong),
                        KLThepPhe = FormatNumber(baseRecord.KLThepPhe),
                        KLGangLongCCT = FormatNumber(baseRecord.KLGangLongCCT),
                        KLGangLongCR = FormatNumber(baseRecord.KLGangLongCR),
                        KLThepPheGang = FormatNumber(baseRecord.KLThepPheGang),
                        KLThepLong = FormatNumber(baseRecord.KLThepLong),
                        IsTrungMeThoi = baseRecord.IsTrungMeThoi,
                        QueLayMau = baseRecord.QueLayMau,
                        QueDoNhiet = baseRecord.QueDoNhiet,
                        GhiChu = baseRecord.GhiChu
                    },
                    mappedPhulieus = groupedMappedPhuLieus.Values.ToList(),
                    unmappedPhulieus = groupedUnmappedPhuLieus.Values.ToList(),
                    phanBoPhulieus = groupedPhanBoPhuLieus.Values.ToList(),
                    manualAdjustPhulieus = groupedManualAdjust.Values.ToList()
                };
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error while getting grouped report {id}", ex);
            }
        }
        /// <summary>
        /// Batch version của GetByIdGroupedAsync: thay vì gọi 6 query × N records,
        /// load toàn bộ phụ liệu / mapping / headerKey trong 4 query rồi assemble trong memory.
        /// </summary>
        public async Task<List<HRC2GroupedByReportNoModel>> GetAllGroupedBatchAsync(IEnumerable<DLNM_HRC2> baseList)
        {
            var bases = baseList.ToList();
            if (bases.Count == 0) return new List<HRC2GroupedByReportNoModel>();

            var ids = bases.Select(x => x.ID).ToList();

            // === Query 1: Toàn bộ PhuLieu thực tế (IsPhanBo != true) cho tất cả IDs ===
            // Kéo cả ID_HeaderKey / IsAddManual để dùng lại cho ManualAdjust mà không cần query thêm.
            var allActualRaw = await _context.PhuLieu_HRC2s
                .Where(x => ids.Contains(x.ID_MeThoi) && x.IsPhanBo != true)
                .Select(x => new
                {
                    x.ID_MeThoi,
                    x.ID_PhuLieu,
                    x.TenPhuLieu,
                    x.KLPhuGia,
                    x.IsManual,
                    x.IsAddManual,
                    x.KLPhuGia_Manual,
                    x.ID_HeaderKey,
                    x.TenHienThi,
                })
                .ToListAsync();

            // === Query 2: Toàn bộ PhanBo (IsPhanBo == true) ===
            var allPhanBoRaw = await _context.PhuLieu_HRC2s
                .Where(x => ids.Contains(x.ID_MeThoi) && x.IsPhanBo == true && x.ID_HeaderKey.HasValue)
                .Select(x => new
                {
                    x.ID_MeThoi,
                    x.ID_HeaderKey,
                    x.KLPhuGia,
                    x.KLPhuGia_Manual,
                    x.IsManual,
                    x.TenHienThi,
                })
                .ToListAsync();

            // === Query 3: Header_Mappings cho toàn bộ ID_PhuLieu xuất hiện ===
            var allPhuLieuIds = allActualRaw
                .Where(x => x.ID_PhuLieu.HasValue)
                .Select(x => x.ID_PhuLieu!.Value)
                .Distinct()
                .ToList();

            var allMappings = allPhuLieuIds.Count > 0
                ? await _context.Header_Mappings.Where(m => allPhuLieuIds.Contains(m.ID_PhuLieu)).ToListAsync()
                : new List<Header_Mapping>();

            // === Query 4: Header_Keys cho toàn bộ ID_HeaderKey cần dùng ===
            var allHeaderKeyIds = allMappings.Select(m => m.ID_HeaderKey).Distinct().ToList();
            var allHeaderKeys = allHeaderKeyIds.Count > 0
                ? await _context.Header_Keys.Where(k => allHeaderKeyIds.Contains(k.Id)).ToDictionaryAsync(k => k.Id)
                : new Dictionary<int, Header_Key>();

            // === Build mapping lookup: ID_PhuLieu → entries ===
            var mappingLookup = allMappings
                .GroupBy(m => m.ID_PhuLieu)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(m =>
                    {
                        allHeaderKeys.TryGetValue(m.ID_HeaderKey, out var hk);
                        return new
                        {
                            m.Id,
                            m.ID_HeaderKey,
                            m.TenNguonDuLieu,
                            KeyGuid = hk?.KeyGuid,
                            TenHienThi = hk?.TenHienThi,
                            LoaiPhieu = hk?.LoaiPhieu,
                            IsActive = hk?.IsActive ?? false,
                        };
                    }).ToList());

            // === Group by ID_MeThoi để assemble per-record ===
            var actualByMeThoi  = allActualRaw .GroupBy(x => x.ID_MeThoi).ToDictionary(g => g.Key, g => g.ToList());
            var phanBoByMeThoi  = allPhanBoRaw .GroupBy(x => x.ID_MeThoi).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<HRC2GroupedByReportNoModel>(bases.Count);

            foreach (var baseRecord in bases)
            {
                var id    = baseRecord.ID;
                var isBof = string.Equals(baseRecord.BieuMau, "BOF", StringComparison.OrdinalIgnoreCase);
                var isRh  = string.Equals(baseRecord.BieuMau, "RH", StringComparison.OrdinalIgnoreCase);
                // Bitmask Header_Key.LoaiExcel cần khớp với biểu mẫu hiện tại (BOF=1, LF=2, RH=4)
                // để cột phụ liệu hiển thị trong TaoPhieuBOF/LF/RH + ChiTietBOF/LF/RH — đồng bộ đúng
                // config "Dùng cho Excel" đang dùng cho GetExportDataAsync (Excel export).
                byte requiredExcelBit = isBof ? (byte)1 : (isRh ? (byte)4 : (byte)2);

                // ---- Mapped + Unmapped ----
                var groupedMapped   = new Dictionary<int,    HeaderKeyGroupedByReportNoModel>();
                var groupedUnmapped = new Dictionary<string, HeaderKeyGroupedByReportNoModel>();

                if (actualByMeThoi.TryGetValue(id, out var actualItems))
                {
                    foreach (var pl in actualItems.Where(x => x.ID_PhuLieu.HasValue))
                    {
                        var plId = pl.ID_PhuLieu!.Value;
                        if (mappingLookup.TryGetValue(plId, out var mapEntries) && mapEntries.Any())
                        {
                            var activeMaps = mapEntries.Where(m => m.IsActive && m.KeyGuid.HasValue).ToList();
                            if (activeMaps.Any())
                            {
                                foreach (var map in activeMaps)
                                {
                                    var hkId = map.ID_HeaderKey;

                                    // Chỉ hiển thị cột khi Header_Key có "Dùng cho Excel" và LoaiExcel
                                    // bao gồm đúng bit của biểu mẫu hiện tại (BOF/LF/RH).
                                    if (!allHeaderKeys.TryGetValue(hkId, out var hkInfo) ||
                                        hkInfo.IsUsed_Excel != true ||
                                        ((byte)(hkInfo.LoaiExcel ?? 0) & requiredExcelBit) == 0)
                                        continue;

                                    var fmt  = hkId == 5
                                        ? (pl.KLPhuGia.HasValue
                                            ? (double?)Math.Round(pl.KLPhuGia.Value / 0.055, 0, MidpointRounding.AwayFromZero)
                                            : null)
                                        : FormatNumber(pl.KLPhuGia);

                                    if (!groupedMapped.ContainsKey(hkId))
                                    {
                                        groupedMapped[hkId] = new HeaderKeyGroupedByReportNoModel
                                        {
                                            ID_HeaderKey   = map.ID_HeaderKey,
                                            KeyGuid        = map.KeyGuid,
                                            TenHienThi     = map.TenHienThi,
                                            TenNguonDuLieu = map.TenNguonDuLieu,
                                            ID_PhuLieu     = plId,
                                            TenPhuLieu     = pl.TenPhuLieu,
                                            KLPhuGia       = fmt,
                                            KLPhuGiaTotal  = 0,
                                            IsManual       = pl.IsManual,
                                            KLPhuGia_Manual = pl.KLPhuGia_Manual,
                                            LoaiPhuLieu    = map.LoaiPhieu,
                                            MappingId      = map.Id,
                                            ThuTu          = isBof ? hkInfo.ThuTu_Excel_BOF : (isRh ? hkInfo.ThuTu_Excel_RH : hkInfo.ThuTu_Excel_LF)
                                        };
                                    }
                                    else if (pl.IsManual == true)
                                    {
                                        groupedMapped[hkId].IsManual       = true;
                                        groupedMapped[hkId].KLPhuGia_Manual = pl.KLPhuGia_Manual;
                                    }
                                    groupedMapped[hkId].KLPhuGiaTotal = (groupedMapped[hkId].KLPhuGiaTotal ?? 0) + (fmt ?? 0);
                                }
                                continue;
                            }
                            // Có mapping nhưng toàn inactive → bỏ qua
                            continue;
                        }

                        // Unmapped
                        var groupKey = (pl.TenPhuLieu ?? $"PL_{plId}")?.Trim() ?? $"PL_{plId}";
                        if (!groupedUnmapped.ContainsKey(groupKey))
                        {
                            groupedUnmapped[groupKey] = new HeaderKeyGroupedByReportNoModel
                            {
                                ID_HeaderKey   = null,
                                KeyGuid        = null,
                                TenHienThi     = null,
                                TenNguonDuLieu = pl.TenPhuLieu,
                                ID_PhuLieu     = plId,
                                TenPhuLieu     = pl.TenPhuLieu,
                                KLPhuGia       = FormatNumber(pl.KLPhuGia),
                                KLPhuGiaTotal  = 0,
                                LoaiPhuLieu    = null
                            };
                        }
                        groupedUnmapped[groupKey].KLPhuGiaTotal = (groupedUnmapped[groupKey].KLPhuGiaTotal ?? 0) + (FormatNumber(pl.KLPhuGia) ?? 0);
                    }
                }

                foreach (var item in groupedMapped  .Values) { item.KLPhuGia = FormatNumber(item.KLPhuGia); item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal); }
                foreach (var item in groupedUnmapped.Values) { item.KLPhuGia = FormatNumber(item.KLPhuGia); item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal); }

                // ---- PhanBo ----
                var groupedPhanBo = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                if (phanBoByMeThoi.TryGetValue(id, out var phanBoItems))
                {
                    foreach (var pb in phanBoItems)
                    {
                        if (!pb.ID_HeaderKey.HasValue) continue;
                        var hkId = pb.ID_HeaderKey.Value;
                        var fmt  = FormatNumber(pb.KLPhuGia);
                        if (!groupedPhanBo.ContainsKey(hkId))
                        {
                            groupedPhanBo[hkId] = new HeaderKeyGroupedByReportNoModel
                            {
                                ID_HeaderKey    = hkId,
                                TenHienThi      = pb.TenHienThi,
                                KLPhuGia        = fmt,
                                KLPhuGia_Manual = FormatNumber(pb.KLPhuGia_Manual),
                                IsManual        = pb.IsManual,
                                KLPhuGiaTotal   = 0,
                            };
                        }
                        groupedPhanBo[hkId].KLPhuGiaTotal = (groupedPhanBo[hkId].KLPhuGiaTotal ?? 0) + (fmt ?? 0);
                    }
                }
                foreach (var item in groupedPhanBo.Values) { item.KLPhuGia = FormatNumber(item.KLPhuGia); item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal); }

                // ---- ManualAdjust (lọc từ actualByMeThoi, không cần query riêng) ----
                var groupedManual = new Dictionary<int, HeaderKeyGroupedByReportNoModel>();
                if (actualByMeThoi.TryGetValue(id, out var actualForManual))
                {
                    foreach (var ma in actualForManual.Where(x => x.IsManual == true && x.IsAddManual == true && x.ID_HeaderKey.HasValue))
                    {
                        var hkId = ma.ID_HeaderKey!.Value;
                        var fmt  = FormatNumber(ma.KLPhuGia_Manual);
                        if (!groupedManual.ContainsKey(hkId))
                        {
                            groupedManual[hkId] = new HeaderKeyGroupedByReportNoModel
                            {
                                ID_HeaderKey    = hkId,
                                TenHienThi      = ma.TenHienThi,
                                KLPhuGia        = null,
                                KLPhuGia_Manual = fmt,
                                IsManual        = true,
                                KLPhuGiaTotal   = 0,
                            };
                        }
                        groupedManual[hkId].KLPhuGiaTotal = (groupedManual[hkId].KLPhuGiaTotal ?? 0) + (fmt ?? 0);
                    }
                }
                foreach (var item in groupedManual.Values) { item.KLPhuGia_Manual = FormatNumber(item.KLPhuGia_Manual); item.KLPhuGiaTotal = FormatNumber(item.KLPhuGiaTotal); }

                result.Add(new HRC2GroupedByReportNoModel
                {
                    data = new DLNM_HRC2_ResponseModels
                    {
                        ID              = baseRecord.ID,
                        REPORT_NO       = baseRecord.REPORT_NO,
                        NgaySx          = baseRecord.NgaySx,
                        Ngay            = baseRecord.Ngay,
                        Ca              = baseRecord.Ca,
                        BieuMau         = baseRecord.BieuMau,
                        Scope           = baseRecord.Scope,
                        MeThoi          = baseRecord.MeThoi,
                        MacThep         = baseRecord.MacThep_Manual ?? baseRecord.MacThep,
                        IsManualMacThep = baseRecord.IsManualMacThep,
                        MacThepGoc      = baseRecord.MacThep,
                        IsNM            = baseRecord.IsNM,
                        IsChuyenCa      = baseRecord.IsChuyenCa,
                        O2              = FormatNumber(baseRecord.O2),
                        AR_RH           = FormatNumber(baseRecord.AR_RH),
                        N2              = FormatNumber(baseRecord.N2),
                        AR_BOF          = FormatNumber(baseRecord.AR_BOF),
                        AR_LF           = FormatNumber(baseRecord.AR_LF),
                        KLGangLong      = FormatNumber(baseRecord.KLGangLong),
                        KLThepPhe       = FormatNumber(baseRecord.KLThepPhe),
                        KLGangLongCCT   = FormatNumber(baseRecord.KLGangLongCCT),
                        KLGangLongCR    = FormatNumber(baseRecord.KLGangLongCR),
                        KLThepPheGang   = FormatNumber(baseRecord.KLThepPheGang),
                        KLThepLong      = FormatNumber(baseRecord.KLThepLong),
                        IsTrungMeThoi   = baseRecord.IsTrungMeThoi,
                        QueLayMau       = baseRecord.QueLayMau,
                        QueDoNhiet      = baseRecord.QueDoNhiet,
                        GhiChu          = baseRecord.GhiChu
                    },
                    mappedPhulieus       = groupedMapped  .Values.ToList(),
                    unmappedPhulieus     = groupedUnmapped.Values.ToList(),
                    phanBoPhulieus       = groupedPhanBo  .Values.ToList(),
                    manualAdjustPhulieus = groupedManual  .Values.ToList()
                });
            }

            return result;
        }

        public async Task AddAsync(DLNM_HRC2 entity)
        {
            var existing = await _context.DLNM_HRC2s.Where(x => x.MeThoi == entity.MeThoi && x.BieuMau == entity.BieuMau && x.IsDelete != true).ToListAsync();
            if (existing != null)
            {
                foreach(var item in existing){
                    item.IsTrungMeThoi = true;
                    _context.DLNM_HRC2s.Update(item);
                }
            }
            _context.DLNM_HRC2s.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(DLNM_HRC2 entity)
        {
            _context.DLNM_HRC2s.Update(entity);
            await _context.SaveChangesAsync();
        }

        // public async Task DeleteAsync(long id)
        // {
        //     var item = await _context.DLNM_HRC2s.FindAsync(id);
        //     if (item != null)
        //     {
        //         _context.DLNM_HRC2s.Remove(item);

        //         var relatedPhuLieuItems = await _context.PhuLieu_HRC2s
        //             .Where(x => x.ID_MeThoi == id)
        //             .ToListAsync();
        //         _context.PhuLieu_HRC2s.RemoveRange(relatedPhuLieuItems);
        //         await _context.SaveChangesAsync();
        //     }
        // }
        public async Task DeleteAsync(long id)
        {
            var item = await _context.DLNM_HRC2s.FindAsync(id);
            if (item == null) return;

            // Đếm số record còn lại sau khi xóa (loại chính nó ra)
            var countAfterDelete = await _context.DLNM_HRC2s
                .CountAsync(x => x.MeThoi == item.MeThoi
                            && x.BieuMau == item.BieuMau
                            && x.ID != id
                            && x.IsDelete != true);

            // Xóa chính
            _context.DLNM_HRC2s.Remove(item);

            // Nếu sau khi xóa chỉ còn < 2 (tức là còn 0 hoặc 1)
            if (countAfterDelete < 2)
            {
                var remainItems = await _context.DLNM_HRC2s
                    .Where(x => x.MeThoi == item.MeThoi
                            && x.BieuMau == item.BieuMau
                            && x.ID != id
                            && x.IsDelete != true)
                    .ToListAsync();

                foreach (var x in remainItems)
                {
                    x.IsTrungMeThoi = false;
                }
            }

            // Xóa phụ liệu
            var relatedPhuLieuItems = await _context.PhuLieu_HRC2s
                .Where(x => x.ID_MeThoi == id)
                .ToListAsync();

            _context.PhuLieu_HRC2s.RemoveRange(relatedPhuLieuItems);

            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.DLNM_HRC2s.AnyAsync(e => e.REPORT_NO == id);
        }

        public async Task<(IEnumerable<DLNM_HRC2> Data, int TotalCount)> SearchWithPagingAsync(DateTime? NgaySX, int? Ca, string? LoaiBM, int? Scope, string? searchText, int page, int pageSize)
        {
            var query = _context.DLNM_HRC2s.Where(x => x.IsDelete != true).AsQueryable();

            if (NgaySX.HasValue)
                query = query.Where(x => x.Ngay.HasValue && x.Ngay.Value.Date == NgaySX.Value.Date);

            if (Ca.HasValue)
                query = query.Where(x => x.Ca == Ca.Value);

            if (!string.IsNullOrEmpty(LoaiBM))
                query = query.Where(x => x.BieuMau == LoaiBM);

            if (Scope.HasValue)
                query = query.Where(x => x.Scope == Scope.Value);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var search = searchText!.Trim();
                if (int.TryParse(search, out var searchReportNo))
                {
                    query = query.Where(x => x.REPORT_NO == searchReportNo);
                }
                else
                {
                    query = query.Where(x =>
                        ((x.MacThep_Manual ?? x.MacThep) ?? string.Empty).Contains(search) ||
                        (x.MeThoi ?? string.Empty).Contains(search));
                }
            }

            // Đếm số lượng REPORT_NO duy nhất
            var totalCount = await query.Select(x => x.REPORT_NO).Distinct().CountAsync();

            // Load tất cả dữ liệu vào memory và group ở đó để tránh lỗi EF Core
            var allData = await query
                .OrderBy(x => x.ID)
                .ToListAsync();

            // Group by REPORT_NO và lấy record đầu tiên của mỗi group
            var groupedData = allData
                .GroupBy(x => x.REPORT_NO)
                .Select(g => g.First())
                .OrderByDescending(x => x.Ngay)
                .ThenByDescending(x => x.REPORT_NO)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (groupedData, totalCount);
        }

        private static double? FormatNumber(double? value)
        {
            if (!value.HasValue) return null;

            var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(rounded % 1) < 0.0000001)
            {
                return Math.Truncate(rounded);
            }
            return rounded;
        }

        public async Task<bool> ChuyenMeThoiAsync(ChuyenMeThoiRequest request)
        {
            int? caKQ = null;
            DateOnly? ngayKQ = null;
            if(request.ChuyenToiCa == 1){ // chuyển về ca trước
                if(request.Ca == 1){
                    caKQ = 2;
                    ngayKQ = request.NgaySX.AddDays(-1);
                }
                else{
                    caKQ = 1;
                    ngayKQ = request.NgaySX;
                }
            } else { // chuyển đến ca sau
                if(request.Ca == 2){
                    caKQ = 1;
                    ngayKQ = request.NgaySX.AddDays(1);
                }
                else{
                    caKQ = 2;
                    ngayKQ = request.NgaySX;
                }
            }

            var phieu = await _context.BmPhieus.FirstOrDefaultAsync(x => x.MaBm == request.MaBM && x.NgaySX == ngayKQ && x.Ca == caKQ && x.Scope == request.Scope && x.IsDelete == 0 && x.IsLock == 0);
            

            if(phieu == null)
                throw new ApplicationException("Phiếu chưa được tạo");
            if(phieu.TinhTrang != 0 && phieu.TinhTrang != 3 && phieu.TinhTrang != 7)
                throw new ApplicationException("Phiếu đã được gửi đi nên không nhận mẻ chuyển");
            
            // Cập nhật DLNM_HRC2 records
            var dlnmItems = await _context.DLNM_HRC2s
                .Where(x => x.MeThoi == request.MeThoi && x.BieuMau == request.BieuMau && x.Scope == request.Scope && x.IsDelete != true)
                .ToListAsync();

            if (dlnmItems.Count == 0)
                throw new ApplicationException("Phiếu không có dữ liệu");

            foreach (var item in dlnmItems)
            {
                item.Ngay = ngayKQ?.ToDateTime(TimeOnly.MinValue);
                item.Ca = caKQ;
                item.IsChuyenCa = true;
            }

            // Cập nhật PhuLieu_HRC2 records (nếu có)
            var phuLieuItems = await _context.PhuLieu_HRC2s
                .Where(x => x.MeThoi == request.MeThoi && x.BieuMau == request.BieuMau)
                .ToListAsync();

            await _context.SaveChangesAsync();
            return true;
        }



        /// <summary>
        /// Gọi stored procedure sp_GetHRC2GroupedByMaterial_Test để lấy dữ liệu phụ liệu theo ngày/ca.
        /// Sau đó map với Header_Mapping & Header_Key để quy đổi về HeaderKey và gộp những phụ liệu cùng HeaderKey.
        /// </summary>
        //public async Task<IEnumerable<FilterSTD_NXTResponse>> GetHRC2GroupedByMaterialAsync(DateTime ngaySX, int ca)
        //{
        //    var raw = await _context.STD_NXT_Filters
        //        .FromSqlRaw(
        //            "EXEC sp_GetHRC2GroupedByMaterial @WorkDate, @Shift",
        //            new SqlParameter("@WorkDate", ngaySX.Date),
        //            new SqlParameter("@Shift", ca)
        //        )
        //        .ToListAsync();

        //    if (!raw.Any())
        //        return Enumerable.Empty<FilterSTD_NXTResponse>();

        //    // Mapping
        //    var phuLieuIds = raw.Select(x => (int)x.ID_PhuLieu).Distinct().ToList();

        //    var mappings = await _context.Header_Mappings
        //        .Where(m => phuLieuIds.Contains(m.ID_PhuLieu))
        //        .ToListAsync();

        //    var headerKeys = await _context.Header_Keys
        //        .Where(h => mappings.Select(m => m.ID_HeaderKey).Contains(h.Id))
        //        .ToListAsync();

        //    var mapped = raw.Select(r =>
        //    {
        //        var map = mappings.FirstOrDefault(m => m.ID_PhuLieu == (int)r.ID_PhuLieu);
        //        var hk = headerKeys.FirstOrDefault(h => h.Id == map?.ID_HeaderKey);

        //        return new
        //        {
        //            Raw = r,
        //            HeaderKeyId = map?.ID_HeaderKey,
        //            HeaderKeyName = hk?.TenHienThi
        //        };
        //    });

        //    // 🔥 GROUP ĐÚNG THEO TỔ HỢP
        //    var grouped = mapped.GroupBy(x => new
        //    {
        //        x.Raw.BieuMau,
        //        x.Raw.Scope,
        //        Key = x.HeaderKeyId.HasValue
        //            ? $"HK_{x.HeaderKeyId}"
        //            : $"PL_{x.Raw.ID_PhuLieu}"
        //    });

        //    var result = grouped.Select(g =>
        //    {
        //        var first = g.First();

        //        return new FilterSTD_NXTResponse
        //        {
        //            BieuMau = first.Raw.BieuMau,
        //            Scope = (int)first.Raw.Scope,
        //            HeaderKeyId = first.HeaderKeyId,
        //            HeaderKeyName = first.HeaderKeyName,
        //            TotalKLPhuGia = g.Sum(x => x.Raw.TotalKLPhuGia),
        //            PhuLieus = g
        //                .GroupBy(x => (int)x.Raw.ID_PhuLieu)
        //                .Select(pl => new PhuLieuNM
        //                {
        //                    ID_PhuLieu = pl.Key,
        //                    TenPhuLieu = pl.First().Raw.TenPhuLieu
        //                })
        //                .ToList()
        //        };
        //    }).ToList();

        //    return result;
        //}
        public async Task<IEnumerable<FilterSTD_NXTResponse>> GetHRC2GroupedByMaterialAsync(DateTime ngaySX, int ca)
        {
            var raw = await _context.STD_NXT_Filters
                .FromSqlRaw(
                    "EXEC sp_GetHRC2GroupedByMaterial @WorkDate, @Shift",
                    new SqlParameter("@WorkDate", ngaySX.Date),
                    new SqlParameter("@Shift", ca)
                )
                .ToListAsync();

            if (!raw.Any())
                return Enumerable.Empty<FilterSTD_NXTResponse>();

            // KHÔNG tin ID_HeaderKey do SP trả về — cột đó là snapshot lấy từ PhuLieu_HRC2.ID_HeaderKey,
            // được ghi 1 lần lúc PhuLieu_HRC2 được tạo và KHÔNG bao giờ cập nhật lại khi Header_Mapping
            // đổi sau đó (vd tách phụ liệu A/B từ chung 1 Header_Key A1 ra 2 Header_Key A1/B1 riêng —
            // "Làm mới" vẫn sum A+B vào A1 nếu tin thẳng cột này). Re-map lại theo ID_PhuLieu, LUÔN đọc
            // Header_Mapping HIỆN TẠI mỗi lần gọi để phản ánh đúng mapping mới nhất — mirror đúng cách
            // làm cũ (xem block code comment phía trên, trước khi đổi sang tin thẳng SP).
            var phuLieuIds = raw.Where(x => (x.ID_PhuLieu ?? 0) > 0)
                .Select(x => x.ID_PhuLieu!.Value)
                .Distinct()
                .ToList();

            var mappings = phuLieuIds.Count > 0
                ? await _context.Header_Mappings.Where(m => phuLieuIds.Contains(m.ID_PhuLieu)).ToListAsync()
                : new List<Header_Mapping>();
            // GroupBy + First thay vì ToDictionary trực tiếp: phòng trường hợp 1 ID_PhuLieu lỡ có
            // 2 mapping cùng lúc (ExistsAsync chỉ check trùng đúng cặp PhuLieu+HeaderKey, không chặn
            // 1 phụ liệu trỏ nhiều HeaderKey), tránh crash duplicate-key.
            var mappingByPhuLieu = mappings
                .GroupBy(m => m.ID_PhuLieu)
                .ToDictionary(g => g.Key, g => g.First());

            var headerKeyIds = mappings.Select(m => m.ID_HeaderKey).Distinct().ToList();
            var headerKeys = headerKeyIds.Count > 0
                ? await _context.Header_Keys.Where(k => headerKeyIds.Contains(k.Id)).ToDictionaryAsync(k => k.Id)
                : new Dictionary<int, Header_Key>();

            var resolved = raw.Select(x =>
            {
                int? headerKeyId = null;
                var headerKeyName = x.TenPhuLieu;
                if ((x.ID_PhuLieu ?? 0) > 0 && mappingByPhuLieu.TryGetValue(x.ID_PhuLieu!.Value, out var map))
                {
                    headerKeyId = map.ID_HeaderKey;
                    if (headerKeys.TryGetValue(map.ID_HeaderKey, out var hk))
                        headerKeyName = hk.TenHienThi;
                }
                return new { Raw = x, ID_HeaderKey = headerKeyId, TenPhuLieu = headerKeyName };
            }).ToList();

            // Group theo BieuMau + Scope + ID_HeaderKey — phụ liệu chưa mapping (null) tách riêng
            // theo ID_PhuLieu để không gộp nhầm nhiều phụ liệu chưa map khác nhau vào chung 1 nhóm.
            var grouped = resolved.GroupBy(x => new
            {
                x.Raw.BieuMau,
                x.Raw.Scope,
                Key = x.ID_HeaderKey.HasValue ? $"HK_{x.ID_HeaderKey}" : $"PL_{x.Raw.ID_PhuLieu}"
            });

            //var result = grouped.Select(g =>
            //{
            //    var first = g.First();

            //    return new FilterSTD_NXTResponse
            //    {
            //        BieuMau = first.BieuMau,
            //        Scope = (int)first.Scope,
            //        HeaderKeyId = first.ID_HeaderKey,
            //        HeaderKeyName = first.TenPhuLieu,  // SP đã fallback về TenHienThi nếu IsAddManual
            //        TotalKLPhuGia = g.Sum(x => x.TotalKLPhuGia),
            //        PhuLieus = g
            //            .Where(x => (int)x.ID_PhuLieu > 0)  // bỏ dòng IsAddManual không có PhuLieu thật
            //            .GroupBy(x => (int)x.ID_PhuLieu)
            //            .Select(pl => new PhuLieuNM
            //            {
            //                ID_PhuLieu = pl.Key,
            //                TenPhuLieu = pl.First().TenPhuLieu
            //            })
            //            .ToList()
            //    };
            //}).ToList();
            var result = grouped.Select(g =>
            {
                var first = g.First();

                return new FilterSTD_NXTResponse
                {
                    BieuMau = first.Raw.BieuMau ?? "",
                    Scope = first.Raw.Scope ?? 0,
                    HeaderKeyId = first.ID_HeaderKey,
                    HeaderKeyName = first.TenPhuLieu ?? "",
                    TotalKLPhuGia = g.Sum(x => x.Raw.TotalKLPhuGia ?? 0),
                    PhuLieus = g
                        .Where(x => (x.Raw.ID_PhuLieu ?? 0) > 0)
                        .GroupBy(x => x.Raw.ID_PhuLieu ?? 0)
                        .Select(pl => new PhuLieuNM
                        {
                            ID_PhuLieu = pl.Key,
                            TenPhuLieu = pl.First().Raw.TenPhuLieu ?? ""
                        })
                        .ToList()
                };
            }).ToList();
            return result;
        }

        // public async Task<(IEnumerable<HRC2FilterThongKe> Data, int TotalCount)> SearchThongKeAsync(SearchThongKe dto)
        // {
        //     var query = _context.DLNM_HRC2s.AsQueryable();

        //     if (dto.TuNgay.HasValue && dto.DenNgay.HasValue)
        //     {
        //         query = query.Where(x =>
        //             x.Ngay.HasValue &&
        //             x.Ngay.Value.Date >= dto.TuNgay.Value.Date &&
        //             x.Ngay.Value.Date <= dto.DenNgay.Value.Date);
        //     }

        //     if (dto.Ca.HasValue)
        //         query = query.Where(x => x.Ca == dto.Ca.Value);

        //     if (!string.IsNullOrEmpty(dto.LoaiBM))
        //         query = query.Where(x => x.BieuMau == dto.LoaiBM);

        //     if (dto.Scope.HasValue)
        //         query = query.Where(x => x.Scope == dto.Scope.Value);

        //     if (!string.IsNullOrWhiteSpace(dto.SearchText))
        //     {
        //         var search = dto.SearchText.Trim();

        //         if (int.TryParse(search, out var searchReportNo))
        //         {
        //             query = query.Where(x => x.REPORT_NO == searchReportNo);
        //         }
        //         else
        //         {
        //             query = query.Where(x =>
        //                 (x.MacThep ?? "").Contains(search) ||
        //                 (x.MeThoi ?? "").Contains(search));
        //         }
        //     }

        //     // Đếm tổng bản ghi thống kê:
        //     // - Nhóm theo REPORT_NO với dữ liệu NM (có REPORT_NO)
        //     // - Giữ riêng từng dòng nhập tay IsNM = false dù không có REPORT_NO
        //     var totalByReportNo = await query
        //         .Where(x => x.REPORT_NO.HasValue)
        //         .Select(x => x.REPORT_NO)
        //         .Distinct()
        //         .CountAsync();

        //     var totalManualRows = await query
        //         .Where(x => !x.REPORT_NO.HasValue && x.IsNM == false)
        //         .Select(x => x.ID)
        //         .Distinct()
        //         .CountAsync();

        //     var totalCount = totalByReportNo + totalManualRows;

        //     // lọc loại thống kê theo LoaiBM
        //     var loaiBmKey = (dto.LoaiBM ?? "").Trim().ToUpperInvariant();
        //     HashSet<byte>? allowedLoaiThongKe = null;

        //     if (loaiBmKey.Contains("BOF"))
        //         allowedLoaiThongKe = new HashSet<byte> { 1, 3 };
        //     else if (loaiBmKey.Contains("LF") || loaiBmKey.Contains("RH"))
        //         allowedLoaiThongKe = new HashSet<byte> { 2, 3 };

        //     // danh sách header phụ liệu
        //     bool isBofTk1 = loaiBmKey.Contains("BOF");
        //     var usedThongKeHeaders = (await _context.Header_Keys
        //         .Where(h =>
        //             h.IsUsedThongKe == true &&
        //             (allowedLoaiThongKe == null ||
        //              (h.LoaiThongKe.HasValue && allowedLoaiThongKe.Contains(h.LoaiThongKe.Value))))
        //         .Select(h => new { h.Id, h.TenHienThi, h.LoaiThongKe, h.ThuTu_TK_BOF, h.ThuTu_TK_LFRH })
        //         .ToListAsync())
        //         .OrderBy(h => isBofTk1 ? (h.ThuTu_TK_BOF ?? int.MaxValue) : (h.ThuTu_TK_LFRH ?? int.MaxValue))
        //         .ThenBy(h => h.Id)
        //         .Select(h => new PhuLieuHeaderTable
        //         {
        //             IDHeaderKey = h.Id,
        //             TenPhuLieu = h.TenHienThi,
        //             LoaiThongKe = (byte)(h.LoaiThongKe ?? 0)
        //         })
        //         .ToList();

        //     var usedHeaderKeyIds = usedThongKeHeaders.Select(x => x.IDHeaderKey).ToHashSet();

        //     // Lấy dữ liệu:
        //     // 1) Dòng NM: group theo REPORT_NO, lấy bản ghi mới nhất mỗi report
        //     // 2) Dòng nhập tay IsNM=false không có REPORT_NO: lấy trực tiếp theo ID
        //     var groupedIdsByReportNo = query
        //         .Where(x => x.REPORT_NO.HasValue)
        //         .GroupBy(x => x.REPORT_NO)
        //         .Select(g => g.Max(x => x.ID));

        //     var manualIds = query
        //         .Where(x => !x.REPORT_NO.HasValue && x.IsNM == false)
        //         .Select(x => x.ID);

        //     var selectedIds = groupedIdsByReportNo.Union(manualIds);

        //     IQueryable<DLNM_HRC2> groupedQuery = _context.DLNM_HRC2s
        //         .Where(x => selectedIds.Contains(x.ID))
        //         .OrderByDescending(x => x.Ngay)
        //         .ThenByDescending(x => x.REPORT_NO)
        //         .ThenByDescending(x => x.ID);

        //     if (dto.Page.HasValue && dto.PageSize.HasValue)
        //     {
        //         groupedQuery = groupedQuery
        //             .Skip((dto.Page.Value - 1) * dto.PageSize.Value)
        //             .Take(dto.PageSize.Value);
        //     }

        //     var groupedData = await groupedQuery
        //         .AsNoTracking()
        //         .ToListAsync();

        //     var result = new List<HRC2FilterThongKe>();

        //     foreach (var x in groupedData)
        //     {
        //         HRC2GroupedByReportNoModel? detail = null;
        //         if (x.REPORT_NO.HasValue && x.REPORT_NO.Value != 0)
        //         {
        //             detail = await GetByReportNoGroupedAsync(x.REPORT_NO.Value);
        //         }
        //         else if (x.IsNM == false)
        //         {
        //             detail = await GetByIdGroupedAsync((int)x.ID);
        //         }

        //         if (detail == null)
        //             continue;

        //         // mapped
        //         var mappedById = (detail.mappedPhulieus ?? new List<HeaderKeyGroupedByReportNoModel>())
        //             .Where(p => p.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(p.ID_HeaderKey.Value))
        //             .GroupBy(p => p.ID_HeaderKey!.Value)
        //             .ToDictionary(g => g.Key, g => g.First());

        //         var mappedOrdered = new List<HeaderKeyGroupedByReportNoModel>();

        //         foreach (var h in usedThongKeHeaders)
        //         {
        //             if (mappedById.TryGetValue(h.IDHeaderKey, out var item))
        //             {
        //                 mappedOrdered.Add(item);
        //             }
        //             else
        //             {
        //                 mappedOrdered.Add(new HeaderKeyGroupedByReportNoModel
        //                 {
        //                     ID_HeaderKey = h.IDHeaderKey,
        //                     TenHienThi = h.TenPhuLieu,
        //                     KLPhuGia = null,
        //                     KLPhuGiaTotal = null
        //                 });
        //             }
        //         }

        //         detail.mappedPhulieus = mappedOrdered;

        //         // phân bổ
        //         var phanBoById = (detail.phanBoPhulieus ?? new List<HeaderKeyGroupedByReportNoModel>())
        //             .Where(p => p.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(p.ID_HeaderKey.Value))
        //             .GroupBy(p => p.ID_HeaderKey!.Value)
        //             .ToDictionary(g => g.Key, g => g.First());

        //         var phanBoOrdered = new List<HeaderKeyGroupedByReportNoModel>();

        //         foreach (var h in usedThongKeHeaders)
        //         {
        //             if (phanBoById.TryGetValue(h.IDHeaderKey, out var item))
        //             {
        //                 phanBoOrdered.Add(item);
        //             }
        //             else
        //             {
        //                 phanBoOrdered.Add(new HeaderKeyGroupedByReportNoModel
        //                 {
        //                     ID_HeaderKey = h.IDHeaderKey,
        //                     TenHienThi = h.TenPhuLieu,
        //                     KLPhuGia = null,
        //                     KLPhuGiaTotal = null
        //                 });
        //             }
        //         }

        //         detail.phanBoPhulieus = phanBoOrdered;
        //         detail.unmappedPhulieus = new List<HeaderKeyGroupedByReportNoModel>();

        //         result.Add(new HRC2FilterThongKe
        //         {
        //             dulieu = detail,
        //             phuLieuHeaderTables = usedThongKeHeaders
        //         });
        //     }

        //     return (result, totalCount);


        // }

        /// <summary>
        /// Phiên bản tối ưu cho API search-thongke: batch load toàn bộ phụ liệu của trang hiện tại
        /// bằng JOIN query thay vì N+1 GetByReportNoGroupedAsync. Thêm SumValues khi có khoảng ngày.
        /// </summary>
        public async Task<SearchThongKeApiResponse> SearchThongKeApiAsync(SearchThongKe dto)
        {
            // === 1. Base filter query ===
            var query = _context.DLNM_HRC2s.AsQueryable();

            if (dto.TuNgay.HasValue && dto.DenNgay.HasValue)
                query = query.Where(x =>
                    x.Ngay.HasValue &&
                    x.Ngay.Value.Date >= dto.TuNgay.Value.Date &&
                    x.Ngay.Value.Date <= dto.DenNgay.Value.Date);

            if (dto.Ca.HasValue)
                query = query.Where(x => x.Ca == dto.Ca.Value);

            if (!string.IsNullOrEmpty(dto.LoaiBM))
                query = query.Where(x => x.BieuMau == dto.LoaiBM);

            if (dto.Scope.HasValue)
                query = query.Where(x => x.Scope == dto.Scope.Value);

            if (!string.IsNullOrWhiteSpace(dto.SearchText))
            {
                var search = dto.SearchText.Trim();
                if (int.TryParse(search, out var searchReportNo))
                    query = query.Where(x => x.REPORT_NO == searchReportNo);
                else
                    query = query.Where(x =>
                        ((x.MacThep_Manual ?? x.MacThep) ?? "").Contains(search) ||
                        (x.MeThoi ?? "").Contains(search));
            }
            if (dto.IsTrungMeThoi.HasValue && dto.IsTrungMeThoi.Value)
                query = query.Where(x => x.IsTrungMeThoi == true);

            if (dto.IsDelete.HasValue && dto.IsDelete.Value)
                query = query.Where(x => x.IsDelete == true);
            else
                query = query.Where(x => x.IsDelete != true);

            // === 2. Total count ===
            // - Bản ghi NM: đếm theo REPORT_NO unique
            // - Bản ghi nhập tay IsNM=false không có REPORT_NO: đếm theo từng dòng
            var totalByReportNo = await query
                .Where(x => x.REPORT_NO.HasValue)
                .Select(x => x.REPORT_NO)
                .Distinct()
                .CountAsync();

            var totalManualRows = await query
                .Where(x => !x.REPORT_NO.HasValue && x.IsNM == false)
                .Select(x => x.ID)
                .Distinct()
                .CountAsync();

            var totalCount = totalByReportNo + totalManualRows;

            // === 3. Header columns ===
            // LoaiThongKe là bitmask: BOF=1, LF=2, RH=4 (1 header có thể thuộc nhiều biểu mẫu cùng lúc).
            var loaiBmKey = (dto.LoaiBM ?? "").Trim().ToUpperInvariant();
            bool isBofTk2 = loaiBmKey.Contains("BOF");
            bool isRhTk2  = loaiBmKey.Contains("RH");
            byte? requiredBitTk2 = isBofTk2 ? (byte)1 : isRhTk2 ? (byte)4 : loaiBmKey.Contains("LF") ? (byte)2 : (byte?)null;

            // Load tất cả Header_Key (kể cả children có ID_NhomKey) để fetch đủ data
            var allHeaderKeysRaw = await _context.Header_Keys
                .Where(h =>
                    h.IsUsedThongKe == true &&
                    (requiredBitTk2 == null ||
                     (h.LoaiThongKe.HasValue && (h.LoaiThongKe.Value & requiredBitTk2.Value) != 0)))
                .Select(h => new { h.Id, h.TenHienThi, h.LoaiThongKe, h.ThuTu_TK_BOF, h.ThuTu_TK_LF, h.ThuTu_TK_RH, h.ID_NhomKey })
                .ToListAsync();

            // Load Header_Nhom được tham chiếu bởi children
            var referencedNhomIds = allHeaderKeysRaw
                .Where(h => h.ID_NhomKey.HasValue)
                .Select(h => h.ID_NhomKey!.Value)
                .ToHashSet();
            var allNhoms = referencedNhomIds.Count > 0
                ? await _context.Header_Nhoms
                    .Where(n => referencedNhomIds.Contains(n.Id))
                    .ToListAsync()
                : new List<Header_Nhom>();

            // childToParentMap: Header_Key.Id → -Header_Nhom.Id (âm để phân biệt với Header_Key.Id)
            var childToParentMap = allHeaderKeysRaw
                .Where(h => h.ID_NhomKey.HasValue)
                .ToDictionary(h => h.Id, h => -h.ID_NhomKey!.Value);

            // Derive LoaiThongKe và thứ tự của mỗi Nhom từ các child Header_Keys
            var nhomMeta = allHeaderKeysRaw
                .Where(h => h.ID_NhomKey.HasValue)
                .GroupBy(h => h.ID_NhomKey!.Value)
                .ToDictionary(g => g.Key, g => new
                {
                    LoaiThongKe = g.First().LoaiThongKe,
                    ThuTu_TK_BOF = g.Min(x => x.ThuTu_TK_BOF),
                    ThuTu_TK_LF = g.Min(x => x.ThuTu_TK_LF),
                    ThuTu_TK_RH = g.Min(x => x.ThuTu_TK_RH)
                });

            // Columns: Nhom groups (IDHeaderKey âm) + Header_Keys độc lập, sắp xếp chung theo ThuTu
            var nhomEntries = allNhoms.Select(n =>
            {
                nhomMeta.TryGetValue(n.Id, out var meta);
                return new
                {
                    SortKey = isBofTk2 ? (meta?.ThuTu_TK_BOF ?? int.MaxValue) : (isRhTk2 ? (meta?.ThuTu_TK_RH ?? int.MaxValue) : (meta?.ThuTu_TK_LF ?? int.MaxValue)),
                    SortId = n.Id,
                    Header = new PhuLieuHeaderTable
                    {
                        IDHeaderKey = -n.Id,
                        TenPhuLieu = n.TenHienThi,
                        LoaiThongKe = (byte)(meta?.LoaiThongKe ?? 0)
                    }
                };
            });
            var standaloneEntries = allHeaderKeysRaw
                .Where(h => !h.ID_NhomKey.HasValue)
                .Select(h => new
                {
                    SortKey = isBofTk2 ? (h.ThuTu_TK_BOF ?? int.MaxValue) : (isRhTk2 ? (h.ThuTu_TK_RH ?? int.MaxValue) : (h.ThuTu_TK_LF ?? int.MaxValue)),
                    SortId = h.Id,
                    Header = new PhuLieuHeaderTable
                    {
                        IDHeaderKey = h.Id,
                        TenPhuLieu = h.TenHienThi,
                        LoaiThongKe = (byte)(h.LoaiThongKe ?? 0)
                    }
                });
            var headers = nhomEntries.Concat(standaloneEntries)
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.SortId)
                .Select(x => x.Header)
                .ToList();

            int page = dto.Page ?? 1;
            int pageSize = dto.PageSize ?? 20;
            int totalPages = pageSize > 0 ? (int)Math.Ceiling((double)totalCount / pageSize) : 1;

            if (!headers.Any() || totalCount == 0)
                return new SearchThongKeApiResponse
                {
                    PhuLieuHeaderTables = headers,
                    TotalRecords = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

            // usedHeaderKeyIds: tất cả Header_Key IDs (cả child) để JOIN/filter fetch đủ data
            var usedHeaderKeyIds = allHeaderKeysRaw.Select(x => x.Id).ToHashSet();

            // === 4. Paged DLNM records ===
            // - NM có REPORT_NO: lấy 1 bản ghi đại diện/report (ID lớn nhất)
            // - Nhập tay IsNM=false không REPORT_NO: lấy trực tiếp từng bản ghi
            var groupedIdsByReportNo = query
                .Where(x => x.REPORT_NO.HasValue)
                .GroupBy(x => x.REPORT_NO)
                .Select(g => g.Max(x => x.ID));

            var manualIds = query
                .Where(x => !x.REPORT_NO.HasValue && x.IsNM == false)
                .Select(x => x.ID);

            var selectedIds = groupedIdsByReportNo.Union(manualIds);

            IQueryable<DLNM_HRC2> pagedQuery = _context.DLNM_HRC2s
                .Where(x => selectedIds.Contains(x.ID))
                .OrderByDescending(x => x.Ngay)
                .ThenByDescending(x => x.REPORT_NO)
                .ThenByDescending(x => x.ID);

            if (dto.Page.HasValue && dto.PageSize.HasValue)
                pagedQuery = pagedQuery
                    .Skip((dto.Page.Value - 1) * dto.PageSize.Value)
                    .Take(dto.PageSize.Value);

            var pagedItems = await pagedQuery.AsNoTracking().ToListAsync();

            if (!pagedItems.Any())
                return new SearchThongKeApiResponse
                {
                    PhuLieuHeaderTables = headers,
                    TotalRecords = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

            var reportNos = pagedItems
                .Where(x => x.REPORT_NO.HasValue)
                .Select(x => x.REPORT_NO!.Value)
                .ToList();

            // === 5. Batch load mapped phụ liệu (non-phanBo) cho cả trang ===
            // 5.1. Phụ liệu có mapping (ID_PhuLieu → Header_Mappings → Header_Keys)
            var mappedRaw = await (
                from pl in _context.PhuLieu_HRC2s
                where pl.REPORT_NO.HasValue && reportNos.Contains(pl.REPORT_NO.Value)
                      && (pl.IsPhanBo != true) && pl.ID_PhuLieu.HasValue
                join hm in _context.Header_Mappings on pl.ID_PhuLieu.Value equals hm.ID_PhuLieu
                join hk in _context.Header_Keys on hm.ID_HeaderKey equals hk.Id
                where hk.IsActive && usedHeaderKeyIds.Contains(hk.Id)
                select new
                {
                    ReportNo = pl.REPORT_NO.Value,
                    ID_HeaderKey = hk.Id,
                    pl.KLPhuGia,
                    pl.KLPhuGia_Manual,
                    pl.IsManual
                }
            ).ToListAsync();

            // 5.2. Các phụ liệu điều chỉnh tay thêm mới (manual_col_*) không có ID_PhuLieu, chỉ có ID_HeaderKey
            // Ví dụ các dòng như hình: ID_PhuLieu = NULL, ID_HeaderKey = 16/35, IsManual = 1, KLPhuGia_Manual > 0
            var manualOnlyRaw = await _context.PhuLieu_HRC2s
                .Where(pl =>
                    pl.REPORT_NO.HasValue &&
                    reportNos.Contains(pl.REPORT_NO.Value) &&
                    (pl.IsPhanBo != true) &&
                    !pl.ID_PhuLieu.HasValue &&
                    pl.ID_HeaderKey.HasValue &&
                    usedHeaderKeyIds.Contains(pl.ID_HeaderKey.Value))
                .Select(pl => new
                {
                    ReportNo = pl.REPORT_NO!.Value,
                    ID_HeaderKey = pl.ID_HeaderKey!.Value,
                    // không có số liệu tự động => trả null để FE phân biệt baseline-null
                    KLPhuGia = (double?)null,
                    KLPhuGia_Manual = pl.KLPhuGia_Manual, // toàn bộ là giá trị chỉnh tay
                    pl.IsManual
                })
                .ToListAsync();

            // Gộp cả 2 loại vào cùng một tập mappedRaw để logic group bên dưới không đổi
            mappedRaw.AddRange(manualOnlyRaw);

            // Group in-memory: reportNo → headerKeyId → (KLPhuGiaTotal, first KLPhuGia_Manual, first IsManual)
            // Remap child Header_Key → parent trước khi group (để gộp nhóm theo ID_NhomKey)
            var mappedByReportNo = mappedRaw
                .Select(x => new
                {
                    x.ReportNo,
                    ID_HeaderKey = childToParentMap.TryGetValue(x.ID_HeaderKey, out var pid) ? pid : x.ID_HeaderKey,
                    x.KLPhuGia,
                    x.KLPhuGia_Manual,
                    x.IsManual
                })
                .GroupBy(x => x.ReportNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.ID_HeaderKey)
                          .ToDictionary(
                              hg => hg.Key,
                              hg => (
                                  KLPhuGiaTotal: hg.Any(x => x.KLPhuGia.HasValue)
                                      ? (double?)hg.Sum(x => x.KLPhuGia ?? 0)
                                      : null,
                                  // Nếu bất kỳ phụ liệu nào trong nhóm có IsManual=true → đại diện manual là phụ liệu A (tìm thấy đầu tiên có IsManual=true)
                                  KLPhuGia_Manual: hg.Where(x => x.IsManual == true && x.KLPhuGia_Manual.HasValue)
                                                     .Select(x => x.KLPhuGia_Manual)
                                                     .FirstOrDefault(),
                                  IsManual: (bool?)hg.Any(x => x.IsManual == true)
                              )
                          )
                );

            // === 6. Batch load phanBo phụ liệu cho cả trang ===
            var phanBoRaw = await _context.PhuLieu_HRC2s
                .Where(x =>
                    x.REPORT_NO.HasValue && reportNos.Contains(x.REPORT_NO.Value) &&
                    x.IsPhanBo == true &&
                    x.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(x.ID_HeaderKey.Value))
                .Select(x => new
                {
                    ReportNo = x.REPORT_NO!.Value,
                    ID_HeaderKey = x.ID_HeaderKey!.Value,
                    x.KLPhuGia,
                    x.KLPhuGia_Manual,
                    x.IsManual
                })
                .ToListAsync();

            // Remap child Header_Key → parent cho phanBo (gộp nhóm theo ID_NhomKey)
            var phanBoByReportNo = phanBoRaw
                .Select(x => new
                {
                    x.ReportNo,
                    ID_HeaderKey = childToParentMap.TryGetValue(x.ID_HeaderKey, out var pid) ? pid : x.ID_HeaderKey,
                    x.KLPhuGia,
                    x.KLPhuGia_Manual,
                    x.IsManual
                })
                .GroupBy(x => x.ReportNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.ID_HeaderKey)
                          .ToDictionary(
                              hg => hg.Key,
                              hg => (
                                  KLPhuGia: hg.Any(x => x.KLPhuGia.HasValue)
                                      ? (double?)hg.Sum(x => x.KLPhuGia ?? 0)
                                      : null,
                                  KLPhuGia_Manual: (double?)hg.Sum(x => x.KLPhuGia_Manual ?? 0),
                                  IsManual: hg.First().IsManual
                              )
                          )
                );

            // === 7. Assemble rows ===
            var rows = new List<HRC2ThongKeRow>();
            foreach (var x in pagedItems)
            {
                // Nhập tay không có REPORT_NO: dùng logic theo ID để vẫn lên thống kê
                if (!x.REPORT_NO.HasValue || x.REPORT_NO.Value == 0)
                {
                    if (x.IsNM == false)
                    {
                        var detailById = await GetByIdGroupedAsync((int)x.ID);
                        if (detailById != null)
                        {
                            // Remap child HeaderKey → parent (-NhomId) giống regular path, rồi merge IsManual
                            // Gộp thêm manualAdjustPhulieus (phụ liệu thêm tay trực tiếp vào Header_Key, không qua ID_PhuLieu)
                            // — tương tự nhánh REPORT_NO gộp manualOnlyRaw vào mappedRaw — nếu không, các cột chỉ có
                            // dữ liệu điều chỉnh tay sẽ bị bỏ trống trên bảng thống kê dù đã lưu trong PhuLieu_HRC2.
                            var mappedById = (detailById.mappedPhulieus ?? new List<HeaderKeyGroupedByReportNoModel>())
                                .Concat(detailById.manualAdjustPhulieus ?? new List<HeaderKeyGroupedByReportNoModel>())
                                .Where(p => p.ID_HeaderKey.HasValue)
                                .GroupBy(p => childToParentMap.TryGetValue(p.ID_HeaderKey!.Value, out var pid) ? pid : p.ID_HeaderKey!.Value)
                                .ToDictionary(
                                    g => g.Key,
                                    g =>
                                    {
                                        var hasManual   = g.Any(x => x.IsManual == true);
                                        var manualEntry = g.FirstOrDefault(x => x.IsManual == true);
                                        var first       = g.First();
                                        return new HeaderKeyGroupedByReportNoModel
                                        {
                                            ID_HeaderKey    = g.Key,
                                            KLPhuGia        = hasManual ? null : (double?)g.Sum(x => x.KLPhuGiaTotal ?? 0),
                                            KLPhuGiaTotal   = hasManual ? manualEntry?.KLPhuGia_Manual : g.Sum(x => x.KLPhuGiaTotal ?? 0),
                                            IsManual        = hasManual ? true : null,
                                            KLPhuGia_Manual = manualEntry?.KLPhuGia_Manual,
                                            KeyGuid         = first.KeyGuid,
                                            TenHienThi      = first.TenHienThi,
                                            TenNguonDuLieu  = first.TenNguonDuLieu,
                                            ID_PhuLieu      = first.ID_PhuLieu,
                                            TenPhuLieu      = first.TenPhuLieu,
                                            LoaiPhuLieu     = first.LoaiPhuLieu,
                                            MappingId       = first.MappingId
                                        };
                                    }
                                );

                            var phanBoById = (detailById.phanBoPhulieus ?? new List<HeaderKeyGroupedByReportNoModel>())
                                .Where(p => p.ID_HeaderKey.HasValue)
                                .GroupBy(p => childToParentMap.TryGetValue(p.ID_HeaderKey!.Value, out var pid) ? pid : p.ID_HeaderKey!.Value)
                                .ToDictionary(
                                    g => g.Key,
                                    g => new HeaderKeyGroupedByReportNoModel
                                    {
                                        ID_HeaderKey  = g.Key,
                                        KLPhuGia      = (double?)g.Sum(x => x.KLPhuGiaTotal ?? 0),
                                        KLPhuGiaTotal = g.Sum(x => x.KLPhuGiaTotal ?? 0)
                                    }
                                );

                            var valuesById = headers.Select(h =>
                            {
                                mappedById.TryGetValue(h.IDHeaderKey, out var mapped);
                                phanBoById.TryGetValue(h.IDHeaderKey, out var phanBo);

                                var klPhuGia = FormatNumber(mapped?.KLPhuGia);
                                var klPhuGiaManual = FormatNumber(mapped?.KLPhuGia_Manual);
                                // IsManual=true nhưng KLPhuGia_Manual=null → user đã xóa → effectiveKL = null (0), không dùng KLPhuGia
                                var effectiveKL = (mapped?.IsManual == true) ? klPhuGiaManual : klPhuGia;
                                var klPhanBo = FormatNumber(phanBo?.KLPhuGia);
                                var totalKL = klPhanBo.HasValue
                                    ? FormatNumber((klPhanBo ?? 0) + (effectiveKL ?? 0))
                                    : effectiveKL;

                                return new HRC2ThongKeValue
                                {
                                    IDHeaderKey = h.IDHeaderKey,
                                    KLPhuGia = klPhuGia,
                                    KLPhuGia_Manual = klPhuGiaManual,
                                    IsManual = mapped?.IsManual,
                                    KLPhanBo = klPhanBo,
                                    TotalKLPhuGia = totalKL
                                };
                            }).ToList();

                            rows.Add(new HRC2ThongKeRow
                            {
                                Data = detailById.data,
                                Values = valuesById
                            });
                        }
                    }
                    continue;
                }

                var reportNo = x.REPORT_NO!.Value;
                mappedByReportNo.TryGetValue(reportNo, out var mappedDict);
                phanBoByReportNo.TryGetValue(reportNo, out var phanBoDict);

                var values = headers.Select(h =>
                {
                    double? klPhuGia = null;
                    double? klPhuGia_Manual = null;
                    bool? isManual = null;

                    if (mappedDict != null && mappedDict.TryGetValue(h.IDHeaderKey, out var mapped))
                    {
                        // Header_Key Id=5: cả KLPhuGia và KLPhuGia_Manual đều ÷ 0.055, làm tròn không chữ số thập phân
                        if (h.IDHeaderKey == 5)
                        {
                            klPhuGia = mapped.KLPhuGiaTotal.HasValue
                                ? (double?)Math.Round(mapped.KLPhuGiaTotal.Value / 0.055, 0, MidpointRounding.AwayFromZero)
                                : null;
                            klPhuGia_Manual = mapped.KLPhuGia_Manual.HasValue
                                ? mapped.KLPhuGia_Manual.Value 
                                : null;
                        }
                        else
                        {
                            klPhuGia = FormatNumber(mapped.KLPhuGiaTotal);
                            klPhuGia_Manual = FormatNumber(mapped.KLPhuGia_Manual);
                        }
                        isManual = mapped.IsManual;
                    }

                    // IsManual=true nhưng KLPhuGia_Manual=null → user đã xóa → effectiveKL = null (0), không dùng KLPhuGia
                    var effectiveKL = (isManual == true) ? klPhuGia_Manual : klPhuGia;
                    double? klPhanBo = null;
                    double? totalKLPhuGia;

                    if (phanBoDict != null && phanBoDict.TryGetValue(h.IDHeaderKey, out var phanBo))
                    {
                        klPhanBo = FormatNumber(phanBo.KLPhuGia);
                        totalKLPhuGia = FormatNumber((phanBo.KLPhuGia ?? 0) + (effectiveKL ?? 0));
                    }
                    else
                    {
                        totalKLPhuGia = effectiveKL;
                    }

                    return new HRC2ThongKeValue
                    {
                        IDHeaderKey = h.IDHeaderKey,
                        KLPhuGia = klPhuGia,
                        KLPhuGia_Manual = klPhuGia_Manual,
                        IsManual = isManual,
                        KLPhanBo = klPhanBo,
                        TotalKLPhuGia = totalKLPhuGia
                    };
                }).ToList();

                rows.Add(new HRC2ThongKeRow
                {
                    Data = new DLNM_HRC2_ResponseModels
                    {
                        ID = x.ID,
                        REPORT_NO = x.REPORT_NO,
                        NgaySx = x.NgaySx,
                        Ngay = x.Ngay,
                        Ca = x.Ca,
                        BieuMau = x.BieuMau,
                        Scope = x.Scope,
                        MeThoi = x.MeThoi,
                        MacThep = x.MacThep_Manual ?? x.MacThep,
                        O2 = FormatNumber(x.O2),
                        AR_RH = FormatNumber(x.AR_RH),
                        N2 = FormatNumber(x.N2),
                        AR_BOF = FormatNumber(x.AR_BOF),
                        AR_LF = FormatNumber(x.AR_LF),
                        KLGangLong = FormatNumber(x.KLGangLong),
                        KLThepPhe = FormatNumber(x.KLThepPhe),
                        KLGangLongCCT = FormatNumber(x.KLGangLongCCT),
                        KLGangLongCR = FormatNumber(x.KLGangLongCR),
                        KLThepPheGang = FormatNumber(x.KLThepPheGang),
                        KLThepLong = FormatNumber(x.KLThepLong),
                        IsNM = x.IsNM,
                        IsChuyenCa = x.IsChuyenCa,
                        IsTrungMeThoi = x.IsTrungMeThoi,
                        QueLayMau = x.QueLayMau,
                        QueDoNhiet = x.QueDoNhiet,
                        GhiChu = x.GhiChu
                    },
                    Values = values
                });
            }

            return new SearchThongKeApiResponse
            {
                PhuLieuHeaderTables = headers,
                Data = rows,
                TotalRecords = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        /// <summary>
        /// Tính tổng KLPhuGia theo từng HeaderKey cho toàn bộ khoảng lọc.
        /// Dùng JOIN trực tiếp (không dùng IN subquery) để SQL Server chọn execution plan tốt hơn.
        /// Tách riêng để FE gọi lazy sau khi trang data chính đã load.
        /// </summary>
        public async Task<List<ThongKeSumItem>> GetThongKeSumAsync(SearchThongKe dto)
        {
            // Không có khoảng ngày → FE tự tính sum từ data trang hiện tại
            if (!dto.TuNgay.HasValue || !dto.DenNgay.HasValue)
                return new List<ThongKeSumItem>();

            // === 1. Headers ===
            // LoaiThongKe là bitmask: BOF=1, LF=2, RH=4 (1 header có thể thuộc nhiều biểu mẫu cùng lúc).
            var loaiBmKey = (dto.LoaiBM ?? "").Trim().ToUpperInvariant();
            bool isBofTk3 = loaiBmKey.Contains("BOF");
            bool isRhTk3  = loaiBmKey.Contains("RH");
            byte? requiredBitTk3 = isBofTk3 ? (byte)1 : isRhTk3 ? (byte)4 : loaiBmKey.Contains("LF") ? (byte)2 : (byte?)null;

            // Load tất cả Header_Key (kể cả children có ID_NhomKey)
            var allHeaderKeysRaw = await _context.Header_Keys
                .Where(h =>
                    h.IsUsedThongKe == true &&
                    (requiredBitTk3 == null ||
                     (h.LoaiThongKe.HasValue && (h.LoaiThongKe.Value & requiredBitTk3.Value) != 0)))
                .Select(h => new { h.Id, h.TenHienThi, h.ThuTu_TK_BOF, h.ThuTu_TK_LF, h.ThuTu_TK_RH, h.ID_NhomKey })
                .ToListAsync();

            if (!allHeaderKeysRaw.Any()) return new List<ThongKeSumItem>();

            // Load Header_Nhom được tham chiếu bởi children
            var referencedNhomIds3 = allHeaderKeysRaw
                .Where(h => h.ID_NhomKey.HasValue)
                .Select(h => h.ID_NhomKey!.Value)
                .ToHashSet();
            var allNhoms3 = referencedNhomIds3.Count > 0
                ? await _context.Header_Nhoms
                    .Where(n => referencedNhomIds3.Contains(n.Id))
                    .ToListAsync()
                : new List<Header_Nhom>();

            // childToParentMap: Header_Key.Id → -Header_Nhom.Id
            var childToParentMap = allHeaderKeysRaw
                .Where(h => h.ID_NhomKey.HasValue)
                .ToDictionary(h => h.Id, h => -h.ID_NhomKey!.Value);

            // Derive thứ tự của mỗi Nhom từ các child Header_Keys
            var nhomMeta3 = allHeaderKeysRaw
                .Where(h => h.ID_NhomKey.HasValue)
                .GroupBy(h => h.ID_NhomKey!.Value)
                .ToDictionary(g => g.Key, g => new
                {
                    ThuTu_TK_BOF = g.Min(x => x.ThuTu_TK_BOF),
                    ThuTu_TK_LF = g.Min(x => x.ThuTu_TK_LF),
                    ThuTu_TK_RH = g.Min(x => x.ThuTu_TK_RH)
                });

            // Headers: Nhom groups (IDHeaderKey âm) + standalone Header_Keys, sắp xếp chung theo ThuTu
            var nhomEntries3 = allNhoms3.Select(n =>
            {
                nhomMeta3.TryGetValue(n.Id, out var meta);
                return new
                {
                    SortKey = isBofTk3 ? (meta?.ThuTu_TK_BOF ?? int.MaxValue) : (isRhTk3 ? (meta?.ThuTu_TK_RH ?? int.MaxValue) : (meta?.ThuTu_TK_LF ?? int.MaxValue)),
                    SortId = n.Id,
                    IDHeaderKey = -n.Id,
                    TenHienThi = n.TenHienThi
                };
            });
            var standaloneEntries3 = allHeaderKeysRaw
                .Where(h => !h.ID_NhomKey.HasValue)
                .Select(h => new
                {
                    SortKey = isBofTk3 ? (h.ThuTu_TK_BOF ?? int.MaxValue) : (isRhTk3 ? (h.ThuTu_TK_RH ?? int.MaxValue) : (h.ThuTu_TK_LF ?? int.MaxValue)),
                    SortId = h.Id,
                    IDHeaderKey = h.Id,
                    TenHienThi = h.TenHienThi
                });
            var headers = nhomEntries3.Concat(standaloneEntries3)
                .OrderBy(x => x.SortKey)
                .ThenBy(x => x.SortId)
                .ToList();

            // usedHeaderKeyIds: tất cả Header_Key IDs để fetch đủ data
            var usedHeaderKeyIds = allHeaderKeysRaw.Select(x => x.Id).ToHashSet();

            // === 2. Base filter (giống hệt SearchThongKeApiAsync) ===
            var query = _context.DLNM_HRC2s.AsQueryable();

            query = query.Where(x =>
                x.Ngay.HasValue &&
                x.Ngay.Value.Date >= dto.TuNgay!.Value.Date &&
                x.Ngay.Value.Date <= dto.DenNgay!.Value.Date);

            if (dto.Ca.HasValue)
                query = query.Where(x => x.Ca == dto.Ca.Value);

            if (!string.IsNullOrEmpty(dto.LoaiBM))
                query = query.Where(x => x.BieuMau == dto.LoaiBM);

            if (dto.Scope.HasValue)
                query = query.Where(x => x.Scope == dto.Scope.Value);

            if (!string.IsNullOrWhiteSpace(dto.SearchText))
            {
                var search = dto.SearchText.Trim();
                if (int.TryParse(search, out var searchReportNo))
                    query = query.Where(x => x.REPORT_NO == searchReportNo);
                else
                    query = query.Where(x =>
                        ((x.MacThep_Manual ?? x.MacThep) ?? "").Contains(search) ||
                        (x.MeThoi ?? "").Contains(search));
            }

            if (dto.IsTrungMeThoi.HasValue && dto.IsTrungMeThoi.Value)
                query = query.Where(x => x.IsTrungMeThoi == true);

            if (dto.IsDelete.HasValue && dto.IsDelete.Value)
                query = query.Where(x => x.IsDelete == true);
            else
                query = query.Where(x => x.IsDelete != true);

            // Lấy REPORT_NOs thực tế (de-dup giống SearchThongKeApiAsync: MAX ID per REPORT_NO)
            var filteredReportNos = query
                .Where(x => x.REPORT_NO.HasValue)
                .GroupBy(x => x.REPORT_NO)
                .Select(g => g.Max(x => x.REPORT_NO)!.Value);

            // === 3. Mapped records (IsPhanBo != true) ===
            // 3.1. Phụ liệu có mapping (ID_PhuLieu → Header_Mappings → Header_Keys)
            var mappedRaw = await (
                from pl in _context.PhuLieu_HRC2s
                where pl.REPORT_NO.HasValue && filteredReportNos.Contains(pl.REPORT_NO.Value)
                      && (pl.IsPhanBo != true) && pl.ID_PhuLieu.HasValue
                join hm in _context.Header_Mappings on pl.ID_PhuLieu.Value equals hm.ID_PhuLieu
                join hk in _context.Header_Keys on hm.ID_HeaderKey equals hk.Id
                where hk.IsActive && usedHeaderKeyIds.Contains(hk.Id)
                select new
                {
                    ReportNo = pl.REPORT_NO.Value,
                    ID_HeaderKey = hk.Id,
                    pl.KLPhuGia,
                    pl.KLPhuGia_Manual,
                    pl.IsManual
                }
            ).ToListAsync();

            // 3.2. Phụ liệu nhập tay thêm mới (manual_col_*) không có ID_PhuLieu, chỉ có ID_HeaderKey
            var manualOnlyRaw = await _context.PhuLieu_HRC2s
                .Where(pl =>
                    pl.REPORT_NO.HasValue && filteredReportNos.Contains(pl.REPORT_NO.Value) &&
                    (pl.IsPhanBo != true) &&
                    !pl.ID_PhuLieu.HasValue &&
                    pl.ID_HeaderKey.HasValue &&
                    usedHeaderKeyIds.Contains(pl.ID_HeaderKey.Value))
                .Select(pl => new
                {
                    ReportNo = pl.REPORT_NO!.Value,
                    ID_HeaderKey = pl.ID_HeaderKey!.Value,
                    KLPhuGia = (double?)0,
                    KLPhuGia_Manual = pl.KLPhuGia_Manual,
                    pl.IsManual
                })
                .ToListAsync();

            mappedRaw.AddRange(manualOnlyRaw);

            // === 4. PhanBo records (IsPhanBo = true) — query trực tiếp theo ID_HeaderKey, KHÔNG join mapping ===
            var phanBoRaw = await _context.PhuLieu_HRC2s
                .Where(x =>
                    x.REPORT_NO.HasValue && filteredReportNos.Contains(x.REPORT_NO.Value) &&
                    x.IsPhanBo == true &&
                    x.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(x.ID_HeaderKey.Value))
                .Select(x => new
                {
                    ReportNo = x.REPORT_NO!.Value,
                    ID_HeaderKey = x.ID_HeaderKey!.Value,
                    x.KLPhuGia
                })
                .ToListAsync();

            // === 5. Tính TotalKLPhuGia per (ReportNo, HeaderKey) rồi sum by HeaderKey ===
            // effectiveKL = KLPhuGia_Manual ?? Sum(KLPhuGia)
            // TotalKLPhuGia = phanBo.KLPhuGia + effectiveKL  (nếu có phanBo)
            //               = effectiveKL                     (nếu không có phanBo)

            // Remap child → parent trước khi tính sum (gộp nhóm theo ID_NhomKey)
            var remappedPhanBoRaw = phanBoRaw
                .Select(x => new
                {
                    x.ReportNo,
                    ID_HeaderKey = childToParentMap.TryGetValue(x.ID_HeaderKey, out var pid) ? pid : x.ID_HeaderKey,
                    x.KLPhuGia
                })
                .ToList();

            var remappedMappedRaw = mappedRaw
                .Select(x => new
                {
                    x.ReportNo,
                    ID_HeaderKey = childToParentMap.TryGetValue(x.ID_HeaderKey, out var pid) ? pid : x.ID_HeaderKey,
                    x.KLPhuGia,
                    x.KLPhuGia_Manual,
                    x.IsManual
                })
                .ToList();

            var phanBoLookup = remappedPhanBoRaw
                .GroupBy(x => (x.ReportNo, x.ID_HeaderKey))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.KLPhuGia ?? 0));

            var mappedGrouped = remappedMappedRaw
                .GroupBy(x => (x.ReportNo, x.ID_HeaderKey))
                .Select(g => new
                {
                    g.Key.ReportNo,
                    g.Key.ID_HeaderKey,
                    // Nếu bất kỳ phụ liệu nào trong nhóm có IsManual=true → dùng ManualValue đại diện, bỏ toàn bộ AutoValue
                    HasManual   = g.Any(x => x.IsManual == true),
                    ManualValue = g.Where(x => x.IsManual == true && x.KLPhuGia_Manual.HasValue)
                                   .Select(x => x.KLPhuGia_Manual!.Value)
                                   .DefaultIfEmpty(0).First(),
                    AutoValue   = g.Sum(x => x.KLPhuGia ?? 0)
                })
                .ToList();

            var sumByHeaderKey = new Dictionary<int, double>();

            foreach (var item in mappedGrouped)
            {
                double effectiveKL;
                if (item.HasManual)
                {
                    // ManualValue đã đúng đơn vị (kể cả IDHeaderKey==5), không convert
                    effectiveKL = item.ManualValue;
                }
                else if (item.ID_HeaderKey == 5)
                {
                    effectiveKL = Math.Round(item.AutoValue / 0.055, 0, MidpointRounding.AwayFromZero);
                }
                else
                {
                    effectiveKL = item.AutoValue;
                }

                var total = phanBoLookup.TryGetValue((item.ReportNo, item.ID_HeaderKey), out var phanBoKL)
                    ? phanBoKL + effectiveKL
                    : effectiveKL;

                if (sumByHeaderKey.ContainsKey(item.ID_HeaderKey))
                    sumByHeaderKey[item.ID_HeaderKey] += total;
                else
                    sumByHeaderKey[item.ID_HeaderKey] = total;
            }

            // REPORT_NOs chỉ có phanBo, không có mapped record — hiếm
            foreach (var pb in remappedPhanBoRaw)
            {
                if (!mappedGrouped.Any(m => m.ReportNo == pb.ReportNo && m.ID_HeaderKey == pb.ID_HeaderKey))
                {
                    var val = pb.KLPhuGia ?? 0;
                    if (sumByHeaderKey.ContainsKey(pb.ID_HeaderKey))
                        sumByHeaderKey[pb.ID_HeaderKey] += val;
                    else
                        sumByHeaderKey[pb.ID_HeaderKey] = val;
                }
            }

            // === 6. Manual records (IsNM=false, REPORT_NO=null) — linked via ID_MeThoi ===
            var manualDlnmIds = await query
                .Where(x => !x.REPORT_NO.HasValue && x.IsNM == false)
                .Select(x => x.ID)
                .ToListAsync();

            if (manualDlnmIds.Count > 0)
            {
                // 6a. Phụ liệu mapped (ID_PhuLieu → Header_Mappings → Header_Keys)
                var manualMapped = await (
                    from pl in _context.PhuLieu_HRC2s
                    where manualDlnmIds.Contains(pl.ID_MeThoi) &&
                          (pl.IsPhanBo != true) && pl.ID_PhuLieu.HasValue
                    join hm in _context.Header_Mappings on pl.ID_PhuLieu.Value equals hm.ID_PhuLieu
                    join hk in _context.Header_Keys on hm.ID_HeaderKey equals hk.Id
                    where hk.IsActive && usedHeaderKeyIds.Contains(hk.Id)
                    select new { RowKey = pl.ID_MeThoi, ID_HeaderKey = hk.Id, pl.KLPhuGia, pl.KLPhuGia_Manual, pl.IsManual }
                ).ToListAsync();

                // 6b. Manual-only (không có ID_PhuLieu, chỉ có ID_HeaderKey)
                var manualOnly2 = await _context.PhuLieu_HRC2s
                    .Where(pl =>
                        manualDlnmIds.Contains(pl.ID_MeThoi) &&
                        (pl.IsPhanBo != true) && !pl.ID_PhuLieu.HasValue &&
                        pl.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(pl.ID_HeaderKey.Value))
                    .Select(pl => new { RowKey = pl.ID_MeThoi, ID_HeaderKey = pl.ID_HeaderKey!.Value, KLPhuGia = (double?)0, pl.KLPhuGia_Manual, pl.IsManual })
                    .ToListAsync();

                // Remap child→parent, group by (RowKey, HeaderKey), áp IsManual logic
                var allManual = manualMapped
                    .Select(x => new { x.RowKey, ID_HeaderKey = childToParentMap.TryGetValue(x.ID_HeaderKey, out var p1) ? p1 : x.ID_HeaderKey, x.KLPhuGia, x.KLPhuGia_Manual, x.IsManual })
                    .Concat(manualOnly2.Select(x => new { x.RowKey, ID_HeaderKey = childToParentMap.TryGetValue(x.ID_HeaderKey, out var p2) ? p2 : x.ID_HeaderKey, x.KLPhuGia, x.KLPhuGia_Manual, x.IsManual }))
                    .GroupBy(x => (x.RowKey, x.ID_HeaderKey))
                    .Select(g => new
                    {
                        g.Key.ID_HeaderKey,
                        HasManual   = g.Any(x => x.IsManual == true),
                        ManualValue = g.Where(x => x.IsManual == true && x.KLPhuGia_Manual.HasValue).Select(x => x.KLPhuGia_Manual!.Value).DefaultIfEmpty(0).First(),
                        AutoValue   = g.Sum(x => x.KLPhuGia ?? 0)
                    });

                foreach (var item in allManual)
                {
                    double effectiveKL = item.HasManual
                        ? item.ManualValue
                        : item.ID_HeaderKey == 5
                            ? Math.Round(item.AutoValue / 0.055, 0, MidpointRounding.AwayFromZero)
                            : item.AutoValue;

                    if (sumByHeaderKey.ContainsKey(item.ID_HeaderKey))
                        sumByHeaderKey[item.ID_HeaderKey] += effectiveKL;
                    else
                        sumByHeaderKey[item.ID_HeaderKey] = effectiveKL;
                }

                // 6c. PhanBo của manual records
                var manualPhanBo = await _context.PhuLieu_HRC2s
                    .Where(x =>
                        manualDlnmIds.Contains(x.ID_MeThoi) &&
                        x.IsPhanBo == true &&
                        x.ID_HeaderKey.HasValue && usedHeaderKeyIds.Contains(x.ID_HeaderKey.Value))
                    .Select(x => new { ID_HeaderKey = x.ID_HeaderKey!.Value, x.KLPhuGia })
                    .ToListAsync();

                foreach (var pb in manualPhanBo)
                {
                    var hkId = childToParentMap.TryGetValue(pb.ID_HeaderKey, out var pid) ? pid : pb.ID_HeaderKey;
                    var val = pb.KLPhuGia ?? 0;
                    if (sumByHeaderKey.ContainsKey(hkId))
                        sumByHeaderKey[hkId] += val;
                    else
                        sumByHeaderKey[hkId] = val;
                }
            }

            // === 7. Build flat response ===
            return headers.Select(h =>
            {
                sumByHeaderKey.TryGetValue(h.IDHeaderKey, out var total);
                return new ThongKeSumItem
                {
                    IDHeaderKey = h.IDHeaderKey,
                    TenPhuLieu = h.TenHienThi,
                    TotalKLPhuGia = FormatNumber(total == 0 ? (double?)null : total)
                };
            }).ToList();
        }

    }
}
