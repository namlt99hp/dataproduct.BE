using dataproduct.api.DTOs;
using dataproduct.api.DTOs.CTD_Dto;
using dataproduct.api.DTOs.NMLG_Dto;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using static dataproduct.api.DTOs.CTD_Dto.PhoinhapkhoDto;

namespace dataproduct.api.Repositories
{
    public interface ICtdPhieuXuLyKphRepository
    {
        Task AddRangeAsync(List<CtdPhieuXuLyKph> entities);
        Task DeleteByIdPhieuAsync(Guid idPhieu);
        Task<List<CtdPhieuXuLyKph>> GetByIdPhieuAsync(Guid idPhieu);
    }

    public interface IBKKcscanBbxlSanxuatRepository
    {
        Task<IEnumerable<BkKcscanBbxlSanxuat>> GetAllAsync(DateOnly? ngaySX, string? ca, DateOnly? ngayXL, string? caXL, string? order, int? xuongCan);
        Task<BkKcscanBbxlSanxuat?> GetByIdAsync(long id);
    }

    public interface IBkKcsBbxnSanLuongRepository
    {
        Task<IEnumerable<BkKcsBbxnSanLuong>> GetAllAsync(DateOnly? ngaySX, string? ca, string? sanPham, string? macThep, string? idXuongCan);
        Task<BkKcsBbxnSanLuong?> GetByIdAsync(long id);
        Task<IEnumerable<BkKcsBbxnSanLuong>> GetByIdPhieuAsync(Guid idPhieu);
        Task UpdateAsync(BkKcsBbxnSanLuong entity);
        Task UpdateRangeAsync(IEnumerable<BkKcsBbxnSanLuong> entities);
        Task UpdatePhieuInfoAsync(IEnumerable<long> ids, Guid idPhieu, int tinhTrang);
    }

    public interface IBKNguyenLieuRepository
    {
        Task<IEnumerable<BkNguyenLieu>> GetAllAsync(DateOnly? NgaySX, int? Ca, string? Kip);
        Task<BkNguyenLieu?> GetByIdAsync(int id);
        Task AddAsync(BkNguyenLieu entity);
        Task UpdateAsync(BkNguyenLieu entity);
        Task DeleteAsync(int id);
    }
    public interface IPhieuRepository
    {
        Task<IEnumerable<BmPhieu>> GetAllAsync(string? MaBM, int? NguoiTaoID);
        Task<BmPhieu?> GetByIdAsync(Guid id);
        Task<BmPhieu?> GetByIdPhieuChaAsync(Guid id);
        Task<BmPhieu> AddAsync([FromBody] JsonElement formData);
        Task UpdateAsync(BmPhieu entity);
        Task DeleteAsync(Guid id);
        Task<bool> ExistsAsync(Guid id);

        Task<bool> CheckExistsAsync(string maBm, DateOnly ngaySX, int ca, int? scope, int? mayduc);
        Task<(IEnumerable<SearchPhieuResponseModel> Data, int TotalCount)> SearchWithPagingAsync(SearchPhieuRequest request);
        Task<IEnumerable<Tbl_LoCao>> GetAllLoCaoAsync();
        Task<(IEnumerable<SearchPhieuResponseModel> Data, int TotalCount)> SearchWithPagingByUserAsync(SearchPhieuByUserRequest request);
        Task<IEnumerable<int?>> GetSoPhieuAsync(string maBm, DateOnly? ngaySX, int? ca);
    }
    public interface IBMPheDuyetRepository
    {
        Task<IEnumerable<BmPheDuyet>> GetAllAsync(int? NguoiDuyetID, int? isCheckDuyet);
        Task<BmPheDuyet> GetByIdAsync(int? id);
        Task<IEnumerable<BmPheDuyet>?> GetByIdPhieuAsync(Guid id);
        Task AddAsync(BmPheDuyet entity);
        Task UpdateAsync(BmPheDuyet entity);
        Task DeleteAsync(int id);
        Task DeleteByPhieuIdAsync(Guid phieuId);
        Task<bool> ExistsAsync(int id);
        Task AddListAsync(List<BmPheDuyet> pheDuyetList, Guid idphieu);
        Task<bool> UpdateTinhTrangAsync(Guid phieuId, int nguoiDuyetId, int tinhTrang);
    }
    public interface IBKPhoiThepRepository
    {
        Task<IEnumerable<BkPhoiThep>> GetAllAsync(DateOnly? NgaySX, DateOnly? TuNgay, DateOnly? DenNgay, int? Ca, string? Kip, int? LoaiPhoi, int? MayDuc);
        Task<BkPhoiThep?> GetByIdAsync(int id);
        Task AddAsync(BkPhoiThep entity);
        Task UpdateAsync(BkPhoiThep entity);
        Task<bool> UpdateStDaChuyenAsync(int id, int stDaChuyen);
        Task<int> UpdateStDaChuyenRangeAsync(List<BKPhoiThepStUpdate> items);
        Task<int?> RevokeStDaChuyenAsync(int id, int soThuHoi);
        Task<int> RevokeStDaChuyenRangeAsync(List<BKPhoiThepStRevoke> items);
        Task DeleteAsync(int id);
    }
    public interface IDLNMHRC2Repository
    {
        Task<IEnumerable<DLNM_HRC2>> GetAllAsync(DateTime? NgaySX, int? Ca, string? BieuMau, int? Scope);
        Task<DLNM_HRC2?> GetByIdAsync(long id);
        Task<HRC2DetailByReportNoModel?> GetByReportNoAsync(int reportNo);
        Task<HRC2GroupedByReportNoModel?> GetByReportNoGroupedAsync(int reportNo);
        Task<HRC2GroupedByReportNoModel?> GetByMeThoiGroupedAsync(string meThoi);
        Task<HRC2GroupedByReportNoModel?> GetByIdGroupedAsync(int id);
        Task<List<HRC2GroupedByReportNoModel>> GetAllGroupedBatchAsync(IEnumerable<DLNM_HRC2> baseList);
        Task AddAsync(DLNM_HRC2 entity);
        Task UpdateAsync(DLNM_HRC2 entity);
        Task DeleteAsync(long id);
        Task<(IEnumerable<DLNM_HRC2> Data, int TotalCount)> SearchWithPagingAsync(DateTime? NgaySX, int? Ca, string? LoaiBM, int? Scope, string? searchText, int page, int pageSize);
        Task<bool> ChuyenMeThoiAsync(ChuyenMeThoiRequest request);
        Task<IEnumerable<FilterSTD_NXTResponse>> GetHRC2GroupedByMaterialAsync(DateTime ngaySX, int ca);
        //Task<(IEnumerable<HRC2FilterThongKe> Data, int TotalCount)> SearchThongKeAsync(SearchThongKe dto);
        Task<SearchThongKeApiResponse> SearchThongKeApiAsync(SearchThongKe dto);
        Task<List<ThongKeSumItem>> GetThongKeSumAsync(SearchThongKe dto);
    }
    public interface IHeaderKeyRepository
    {
        Task<IEnumerable<Header_Key>> GetAllAsync();
        Task<Header_Key?> GetByIdAsync(int id);
        Task AddAsync(Header_Key entity);
        Task UpdateAsync(Header_Key entity);
        Task DeleteAsync(int id);
        Task<(IEnumerable<HeaderKey_ResponseModel> Data, int TotalCount)> SearchWithPagingAsync(string? searchKey, string? LoaiPhieu, int page, int pageSize);
        Task<(IEnumerable<HeaderKeyMapping_ResponseModel> Data, int TotalCount)> SearchMappingsWithPagingAsync(
            string? searchKey,
            string? LoaiPhieu,
            string? TrangThai,
            bool? IsUsedNXT,
            bool? IsUsedThongKe,
            DateTime? FromDate,
            DateTime? ToDate,
            string? SortThuTu,
            int? IdNhom,
            bool? chuaMappingNM,
            int page,
            int pageSize
        );
        Task<bool> ExistsByTenHienThiAsync(string tenHienThi, int? excludeId = null);
        Task<bool> IsInUseAsync(int id);
    }
    public interface IHeaderMappingRepository
    {
        Task<Header_Mapping?> GetByIdAsync(int id);
        Task<Header_Mapping> AddAsync(Header_Mapping entity);
        Task UpdateAsync(Header_Mapping entity);
        Task DeleteAsync(int id);
        Task DeleteByHeaderKeyAsync(int headerKeyId);
        Task<bool> ExistsAsync(int phuLieuId, int headerKeyId, int? excludeId = null);
    }
    public interface ISTD_NXT_HRC2Repository
    {
        Task<STD_NXT_HRC2_UpsertResponse> UpsertAsync(STD_NXT_HRC2_UpsertDto entity);
        Task InitializeHRC2_STD_NXTAsync(BmPhieu phieu);
        Task GetHRC2FilterInitAsync(InitXuatNhapTonHRC2Request request);
        Task<STD_NXT_HRC2_GetDetailResponse> GetByPhieuIdAsync(Guid phieuId);
        Task<STD_NXT_RelatedPhieuStatusResponse> GetRelatedPhieuStatusesAsync(STD_NXT_RelatedPhieuStatusRequest request);
        Task<bool> PhanBoAsync(STD_NXT_HRC2_PhanBoDto entity);
        Task<bool> ThuHoiPhanBoAsync(STD_NXT_HRC2_PhanBoDto entity);
        Task<bool> KhongPhanBoAsync(STD_NXT_HRC2_KhongPhanBoDto entity);
        // Task<STD_NXT_HRC2_GetDetailResponse> GetByIdAsync(Guid idPhieu);
        // Task<STD_NXT_HRC2_GetDetailResponse> FilterAsync(DateTime ngaySX, int ca);
    }
    public interface ICtdPhoiNongRepository
    {
        Task<IEnumerable<CtdPhoiNong>> GetAllAsync(DateOnly? NgaySX, int? Ca, string? Kip, int? Xuong, string? Me);
        Task<CtdPhoiNong?> GetByIdAsync(int id);
        Task<IEnumerable<CtdPhoiNong>> GetByPhieuIdAsync(Guid phieuId);
        Task AddAsync(CtdPhoiNong entity);
        Task AddListAsync(List<CtdPhoiNong> entities);
        Task UpdateAsync(CtdPhoiNong entity);
        Task<int> UpdateStatusRangeAsync(List<CtdPhoiNongStatusUpdate> items);
        Task<int> UpdateStatusDone(DateOnly? NgaySX, int? Ca, string? Kip, int? Xuong, string? Me, int? status);
        Task DeleteAsync(int id);
        Task<(int Created, int Updated)> UpsertListAsync(List<CtdPhoiNong> entities);
    }
    //  Begin NM CTD 
    public interface ICtdBMDucCTDRepository
    {
        Task<List<SanLuongPhoiDto>> GetSanLuongPhoiAsync(string ca, string kip, DateTime ngaySX);
        Task<List<PhoinhapkhoNhanPhoiDto>> GetPhoiNhapKhoAsync(string ca, string kip, DateTime ngaySX, int mayduc);
        Task<List<PhoinhapkhoNhanPhoiDto>> GetPhoiNhapKhoExportRangeAsync(DateOnly? fromDate, DateOnly? toDate);
        Task<List<InsertSanLuongPhoiDto>> GetSanLuongPhoiChiTietAsync(int ca, string kip, DateTime ngaySX, int? mayDuc = null, Guid? idPhieu = null);
        Task AddSanLuongPhoiListAsync(List<BM_SanLuongPhoi> entities);
        Task DeleteSanLuongPhoiByPhieuAsync(Guid idPhieu);
        Task<List<InsertPhoiNhapKhoDto>> GetPhoiNhapKhoChiTietAsync(int ca, string kip, DateTime ngaySX, int? mayDuc = null, Guid? idPhieu = null);
        Task AddPhoiNhapKhoListAsync(List<BM_PhoiNhapKho> entities);
        Task DeletePhoiNhapKhoByPhieuAsync(Guid idPhieu);

        Task<List<BmPhieu>> GetDataAsync(DateOnly? fromDate, DateOnly? toDate);
        Task<List<BmPhieu>> GetDataSanLuongPhoiAsync(DateOnly? fromDate, DateOnly? toDate);

    }
    //  End NM CTD 

    public interface ICtdPhoiNguoiRepository
    {
        Task<IEnumerable<CtdPhoiNguoi>> GetByPhieuIdAsync(Guid phieuId);
        Task AddAsync(CtdPhoiNguoi entity);
        Task AddListAsync(List<CtdPhoiNguoi> entities);
        Task DeleteByPhieuIdAsync(Guid phieuId);
    }

    public interface ICtdSoTheoDoiRepository
    {
        Task AddSoTheoDoiListAsync(List<CtdSoTheoDoi> entities);
        Task AddDienBienListAsync(List<CtdStdDienBien> entities);
        Task DeleteSoTheoDoiByPhieuIdAsync(Guid phieuId);
        Task DeleteDienBienByPhieuIdAsync(Guid phieuId);
        Task<List<CtdSoTheoDoi>> GetSoTheoDoiByPhieuIdAsync(Guid phieuId);
        Task<List<CtdStdDienBien>> GetDienBienByPhieuIdAsync(Guid phieuId);
    }

    public interface IBmQuyenXlRepository
    {
        Task<IEnumerable<BmQuyenXl>> GetAllAsync(int? idTaiKhoan, string? maBm, string? maKhuVuc);
        Task<BmQuyenXl?> GetByIdAsync(int id);
        Task AddAsync(BmQuyenXl entity);
        Task AddRangeAsync(List<BmQuyenXl> entities);
        Task UpdateAsync(BmQuyenXl entity);
        Task DeleteAsync(int id);
        Task DeleteRangeAsync(List<int> ids);
        Task DeleteByTaiKhoanAsync(int idTaiKhoan);
        Task<bool> ExistsAsync(int id);
        Task<IEnumerable<BmQuyenXl>> GetByTaiKhoanIdAsync(int idTaiKhoan);
        /// <summary>
        /// Kiểm tra trùng lặp theo IdTaiKhoan + MaBm + MaKhuVuc + QuyenChucNang + KhuVucPhu.
        /// </summary>
        Task<bool> CheckDuplicateAsync(int? idTaiKhoan, string? maBm, string? maKhuVuc, byte? quyenChucNang, string? khuVucPhu = null, int? excludeId = null);
    }

    //  Begin láy thông tin người và chữ ký 

    public interface IPheDuyetRepository
    {
        Task<List<BmPheDuyet>> GetBmPheDuyetByPhieuIdAsync(Guid phieuId);
        Task<List<TaiKhoan>> GetTaiKhoanByIdsAsync(List<int> ids);
        Task<List<PhongBan>> GetAllPhongBanAsync();
        Task<List<ViTri>> GetAllViTriAsync();
        Task<BmPheDuyet> InitializePheDuyetAsync(Guid phieuId, int capDuyet, int idNguoiDuyet);
    }

    // End
    public interface ISiloRepository
    {
        Task<IEnumerable<Silo>> GetAllAsync();
        Task<Silo?> GetByIdAsync(int id);
        Task AddAsync(Silo entity);
        Task UpdateAsync(Silo entity);
        Task DeleteAsync(int id);
        Task<(IEnumerable<Silo> Data, int TotalCount)> SearchMappingsWithPagingAsync(
            string? searchKey,
            int? scope,
            bool? tinhTrang,
            string? BieuMau,
            int? nhaMay,
            int page,
            int pageSize
        );
        Task<bool> ExistsByTenSiloAsync(string tenSilo, int nhaMay, string bieuMau, int? scope, int? excludeId = null);
        Task<List<SiloWithPhuLieuNmDto>> GetValidSilosAsync(
                List<int> phuLieuNMIds,
                DateTime ngaySX
            );
        Task<List<int>> GetPhuLieuNMInSiloAsync(
            int siloId,
            DateTime ngaySX
        );
        Task<bool> IsSiloContainPhuLieuNMAsync(
            int siloId,
            int phuLieuNMId,
            DateTime ngaySX
        );
        // MapSiloPhuLieuNM methods
        Task<List<MapSiloPhuLieuNMResponse>> GetMappingsBySiloIdAsync(int siloId);
        Task<MapSiloPhuLieuNM?> GetMappingByIdAsync(int id);
        Task<MapSiloPhuLieuNM> AddMappingAsync(MapSiloPhuLieuNM entity);
        Task UpdateMappingAsync(MapSiloPhuLieuNM entity);
        Task DeleteMappingAsync(int id);
        Task<List<PhuLieu_NM>> GetAllPhuLieuNMAsync();
        Task<(IEnumerable<PhuLieu_NM> Data, int TotalCount)> SearchPhuLieuNMWithPagingAsync(
            string? searchKey,
            int page,
            int pageSize
        );
        Task<List<DTOs.SiloByHeaderKeyDto>> GetSilosByHeaderKeyAsync(
            int headerKeyId,
            DateTime ngaySX,
            int nhaMay,
            string? bieuMau
        );

        /*============= NMLG==================*/

    }

    // ─── LG_TSL: SiLo, NVL, Mapping (Tồn Silo Lò Cao) ──────────────────────────
    public interface ILGTSLRepository
    {
        // SiLo
        Task<List<LGTSSiLoDto>> GetSiLoListAsync(int? idLoCao);
        Task<LG_TSL_SiLo?> GetSiLoByIdAsync(int id);
        Task<LG_TSL_SiLo> AddSiLoAsync(LG_TSL_SiLo entity);
        Task<LG_TSL_SiLo?> UpdateSiLoAsync(int id, LG_TSL_SiLo entity);
        Task<bool> DeleteSiLoAsync(int id);

        // NVL
        Task<List<LGTSNvlDto>> GetNvlListAsync(int? idLoCao);
        Task<LG_TSL_NVL?> GetNvlByIdAsync(int id);
        Task<LG_TSL_NVL> AddNvlAsync(LG_TSL_NVL entity);
        Task<LG_TSL_NVL?> UpdateNvlAsync(int id, LG_TSL_NVL entity);
        Task<bool> DeleteNvlAsync(int id);
        Task<bool> UpdateXacNhanAsync(int id, bool xacNhan);

        // Mapping
        Task<List<LGTSMappingDto>> GetMappingListAsync(int? idLoCao, DateTime? ngay, int? ca);
        Task<LG_TSL_SiLo_Mapping?> GetMappingByIdAsync(int id);
        Task<LG_TSL_SiLo_Mapping?> GetExistingMappingAsync(int thuTuCoDinh, int idLoCao, DateTime ngay, int ca);
        Task<LG_TSL_SiLo_Mapping> AddMappingAsync(LG_TSL_SiLo_Mapping entity);
        Task<LG_TSL_SiLo_Mapping?> UpdateMappingAsync(int id, LG_TSL_SiLo_Mapping entity);
        Task<bool> DeleteMappingAsync(int id);

        // Lấy ThuTuCoDinh của silo theo ID + IDLoCao (dùng để lưu ThuTuCoDinh vào IDSiLo của Mapping)
        Task<int?> GetSiLoThuTuCoDinhAsync(int siloId, int idLoCao);

        // View: SiLo + NVL theo Ngày/Ca/LoCao (dùng tạo phiếu tồn silo)
        Task<List<LGTSSiLoMappingViewDto>> GetSiLoByMappingAsync(int? idLoCao, DateTime? ngay, int? ca);

        // Chi tiết tồn silo theo phiếu
        Task DeleteByPhieuIdAsync(Guid idPhieu);
        Task UpsertChiTietAsync(UpsertLGTSChiTietDto dto);
        Task<List<LGTSChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu);

        // Lấy thông tin phiếu (để export PDF)
        Task<BmPhieu?> GetPhieuByIdAsync(Guid idPhieu);
    }

    public interface ILGPTLCRepository
    {
        Task<List<LG_NKVHPT_DuLieuAuto>> GetAutoDataAsync(int idLoCao, DateTime ngayVanHanh, Guid? idPhieu = null);
        Task<List<LGPTLCChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu);
        Task DeleteChiTietByPhieuAsync(Guid idPhieu);
        Task AddChiTietRangeAsync(List<LG_NKVHPT_ChiTiet> entities);
        Task UpdateManualValuesAsync(UpdateLGPTLCManualDto dto);
        Task UpdatePhieuSummaryAsync(Guid idPhieu, UpdateLGPTLCSummaryDto dto);
        Task<BmPhieu?> GetPhieuByIdAsync(Guid idPhieu);
    }

    public interface ILGNLRepository
    {
        // TS Mapping lookup
        Task<List<LGNLTsMappingDto>> GetTsMappingListAsync();

        Task<List<LG1_DuLieuNL>> GetDuLieuRawAsync(
            IEnumerable<string> tagKeys, int idLoCao, DateTime timeFrom, DateTime timeTo);

        // SiLo Master
        Task<List<LG_NL_SiLo>> GetSiLoMasterListAsync(int? idLoCao);
        Task<LG_NL_SiLo?> GetSiLoMasterByIdAsync(int id);
        Task<LG_NL_SiLo> AddSiLoMasterAsync(LG_NL_SiLo entity);
        Task<LG_NL_SiLo?> UpdateSiLoMasterAsync(int id, LG_NL_SiLo entity);
        Task<bool> DeleteSiLoMasterAsync(int id);

        // Mapping
        Task<List<LGNLMappingDto>> GetMappingListAsync(DateTime? ngay, int? idCa, int? idLoCao);
        Task<LG_NL_Mapping?> GetMappingByIdAsync(int id);
        Task<LG_NL_Mapping> AddMappingAsync(LG_NL_Mapping entity);
        Task<LG_NL_Mapping?> UpdateMappingAsync(int id, LG_NL_Mapping entity);
        Task<bool> DeleteMappingAsync(int id);

        // Nhóm NVL
        Task<List<LG_NL_NhomNVL>> GetNhomNvlListAsync(int? idLoCao);
        Task<LG_NL_NhomNVL?> GetNhomNvlByIdAsync(int id);
        Task<LG_NL_NhomNVL> AddNhomNvlAsync(LG_NL_NhomNVL entity);
        Task<LG_NL_NhomNVL?> UpdateNhomNvlAsync(int id, LG_NL_NhomNVL entity);
        Task<bool> DeleteNhomNvlAsync(int id);

        // NVL
        Task<List<LGNLNvlDto>> GetNvlListAsync(int? idLoCao);
        Task<LG_NL_NVL?> GetNvlByIdAsync(int id);
        Task<LG_NL_NVL> AddNvlAsync(LG_NL_NVL entity);
        Task<LG_NL_NVL?> UpdateNvlAsync(int id, LG_NL_NVL entity);
        Task<bool> DeleteNvlAsync(int id);

        // Dữ liệu SCADA filtered by LoCao, Ngày
        Task<List<LGNLDuLieuScadaDto>> GetDataByFilterAsync(
            int? idLoCao, DateTime? ngayBatDau, DateTime? ngayKetThuc);

        // Pivot dữ liệu nạp liệu theo Silo mapping (ngày/ca/lò cao) — LO 1-4
        Task<LGNLDuLieuSiLoResult> GetDuLieuSiloPivotAsync(
            DateTime ngay, int idCa, int idLoCao);

        // Đổi NVL cho một silo tại thời điểm cụ thể trong ca (giữ data cũ đúng NVL)
        Task<LG_NL_Mapping> ChangeSiLoNVLAsync(
            int idLoCao, DateTime ngay, int idCa, int idSiLo, int idNVLMoi,
            DateTime thoiDiem, string? ghiChu);

        // Hoàn tác đổi NVL giữa ca — xóa tất cả mid-shift row của silo trong ca này
        Task<int> UndoChangeSiLoNVLAsync(int idLoCao, DateTime ngay, int idCa, int idSiLo);

        // Snapshot trạng thái hiện tại của từng Silo (NVL đang chứa)
        Task<List<LGNLSiloSnapshotDto>> GetSiloSnapshotAsync(
            int idLoCao, DateTime ngay, int idCa);

        // Chi tiết nạp liệu theo phiếu
        Task DeleteChiTietByPhieuIdAsync(Guid idPhieu);
        Task AddChiTietRangeAsync(List<LG_NL_ChiTiet> entities);
        Task<List<LGNLChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu);

        // Phiếu
        Task<BmPhieu?> GetPhieuByIdAsync(Guid idPhieu);
    }

    public interface IBBGN_ThepLongRepository
    {
        // Task<IEnumerable<BBGN_ThepLong>> GetAllAsync();
        Task XuLyDuLieuMeThoiGangLongAsync(List<string> data, FetchMeThoiRequest request);
    }

    public interface IMacThepRepository
    {
        Task<IEnumerable<MacThep>> GetAllAsync(byte? nhaMay, bool? isLock, string? tenMacThep, List<int>? idMayDucs = null);
        Task<MacThep?> GetByIdAsync(int id);
        Task AddAsync(MacThep entity);
        Task UpdateAsync(MacThep entity);
        Task DeleteAsync(int id);
        Task<bool> ExistsByTenAndMayDucAsync(string tenMacThep, List<int> idMayDucs, int? excludeId = null);
    }

    public interface IHrc1MaVatTuRepository
    {
        Task<(IEnumerable<Hrc1MaVatTuItem> Data, int TotalCount)> SearchAsync(Hrc1MaVatTuSearchRequest req);
        Task<Hrc1MaVatTuItem?> GetByIdAsync(int id);
        Task<Hrc1MaVatTuItem> CreateAsync(Hrc1MaVatTuUpsertDto dto);
        Task<Hrc1MaVatTuBulkCreateResult> BulkCreateAsync(List<Hrc1MaVatTuUpsertDto> items);
        Task<bool> UpdateAsync(int id, Hrc1MaVatTuUpsertDto dto);
        Task<bool> DeleteAsync(int id);
    }

    public interface IMacThep_MayDucRepository
    {
        Task<List<MacThep_MayDuc>> GetByMacThepIdAsync(int idMacThep);
        Task<List<MacThep_MayDuc>> GetByMacThepIdsAsync(List<int> idMacTheps);
        Task SetMayDucsAsync(int idMacThep, List<int> idMayDucs);
    }

    public interface IHrc2SlabWorkflowRepository
    {
        Task<(IEnumerable<BkHrc2SlabItem> Data, int TotalCount)> SearchWithTrangThaiAsync(HrcSlabSearchRequest request);
        Task<IEnumerable<SlabTongHopItem>> GetTongHopAsync(DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip);
        Task<IEnumerable<PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca);
        Task<IEnumerable<SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu);
        Task<IEnumerable<BkHrc2SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu);
        Task ChuyenBBSLAsync(List<int> idSlabs, Guid idPhieu, int nguoiThucHien);
        Task ThuHoiAsync(List<int> idSlabs, int nguoiThucHien);
        Task XacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task HuyXacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task HuyChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task<SyncStatusItem> SyncAsync(DateOnly? ngayBatDau, DateOnly? ngayKetThuc);
        Task<SyncStatusItem?> GetSyncStatusAsync();
    }

    public interface IMayDucRepository
    {
        Task<IEnumerable<MayDuc>> GetAllAsync(byte? nhaMay, bool? isLock, string? tenMayDuc);
        Task<MayDuc?> GetByIdAsync(int id);
        Task AddAsync(MayDuc entity);
        Task UpdateAsync(MayDuc entity);
        Task DeleteAsync(int id);
        Task<bool> ExistsByTenAsync(string tenMayDuc, byte nhaMay, int? excludeId = null);
    }

    public interface IHrc1SlabRepository
    {
        Task<(IEnumerable<Hrc1SlabItem> Data, int TotalCount)> SearchAsync(Hrc1SlabSearchRequest request);
        Task<IEnumerable<Hrc1SlabTongHopItem>> GetTongHopAsync(DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip);
        Task<IEnumerable<Hrc1PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca);
        Task<IEnumerable<Hrc1SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu);
        Task<IEnumerable<Hrc1SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu);
        Task<int> ChuyenPhoiAsync(List<int> idSlabs, Guid idPhieuNguon, string huong, int nguoiChuyen);
        Task XacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task HuyXacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task HuyChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task<Hrc1SlabSyncResult> UpsertFromApiAsync(List<TscSlabItem> items);
        Task<int> FillMacThepAsync();
        Task UpdateSlabAsync(int id, Hrc1SlabUpdateRequest req);
        Task<int> BulkUpdateMaVatTuAsync(Hrc1BulkUpdateMaVatTuRequest req);
        Task<IEnumerable<Hrc1TongHopGhiChuItem>> GetTongHopGhiChuAsync(Guid idPhieu);
        Task SaveTongHopGhiChuAsync(Hrc1SaveTongHopGhiChuRequest req);
    }
}
