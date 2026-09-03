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

    public interface IDLNMHRC1Repository
    {
        Task<IEnumerable<Hrc1TieuHao>> GetAllAsync(DateOnly? ngaySanXuat, int? ca, int? scope, string? bieuMau = "BOF");
        Task<List<Hrc1GroupedByMeThoiModel>> GetAllGroupedBatchAsync(IEnumerable<Hrc1TieuHao> baseList);
        Task<SearchThongKeHrc1ApiResponse> SearchThongKeApiAsync(SearchThongKeHrc1 dto);
        Task<List<ThongKeSumItemHrc1>> GetThongKeSumAsync(SearchThongKeHrc1 dto);
        Task<bool> ChuyenMeThoiAsync(ChuyenMeThoiRequest request);
        Task<List<FilterSTD_NXTResponse_HRC1>> GetHRC1GroupedByMaterialAsync(DateTime ngaySX, int ca);
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
            int? IdNhom,
            bool? chuaMappingNM,
            string? SortBy,
            string? SortOrder,
            int page,
            int pageSize
        );
        Task<bool> ExistsByTenHienThiAsync(string tenHienThi, int? excludeId = null);
        Task<bool> IsInUseAsync(int id);
        /// <summary>Tìm Header_Key khác đang dùng cùng giá trị thứ tự ở cùng cột (nếu có).</summary>
        Task<Header_Key?> FindDuplicateThuTuAsync(ThuTuColumn column, int value, int? excludeId = null);
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
        Task<List<STD_NXT_HRC2_NhapXuatTonRow>> GetNhapXuatTonAsync(STD_NXT_HRC2_NhapXuatTonSearchRequest request);
        // Task<STD_NXT_HRC2_GetDetailResponse> GetByIdAsync(Guid idPhieu);
        // Task<STD_NXT_HRC2_GetDetailResponse> FilterAsync(DateTime ngaySX, int ca);
    }
    public interface ISTD_NXT_HRC1Repository
    {
        Task<STD_NXT_HRC1_UpsertResponse> UpsertAsync(STD_NXT_HRC1_UpsertDto entity);
        Task InitializeHRC1_STD_NXTAsync(BmPhieu phieu);
        Task GetHRC1FilterInitAsync(InitXuatNhapTonHRC1Request request);
        Task<STD_NXT_HRC1_GetDetailResponse> GetByPhieuIdAsync(Guid phieuId);
        Task<STD_NXT_RelatedPhieuStatusResponse> GetRelatedPhieuStatusesAsync(STD_NXT_RelatedPhieuStatusRequest request);
        Task<bool> PhanBoAsync(STD_NXT_HRC1_PhanBoDto entity);
        Task<bool> ThuHoiPhanBoAsync(STD_NXT_HRC1_PhanBoDto entity);
        Task<bool> KhongPhanBoAsync(STD_NXT_HRC1_KhongPhanBoDto entity);
        Task<List<STD_NXT_HRC1_NhapXuatTonRow>> GetNhapXuatTonAsync(STD_NXT_HRC1_NhapXuatTonSearchRequest request);
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

        // Sao chép mapping Silo→NVL từ ca gần nhất có dữ liệu — nếu ca liền kề
        // chưa có mapping thì tự động lùi tiếp cho đến khi tìm được hoặc hết giới hạn tìm kiếm
        Task<CopyMappingFromPreviousShiftResultDto> CopyMappingFromPreviousShiftAsync(
            int idLoCao, DateTime ngay, int idCa);

        // Chi tiết nạp liệu theo phiếu
        Task DeleteChiTietByPhieuIdAsync(Guid idPhieu);
        Task AddChiTietRangeAsync(List<LG_NL_ChiTiet> entities);
        Task ReplaceChiTietAsync(Guid idPhieu, List<LG_NL_ChiTiet> entities);
        Task<List<LGNLChiTietDto>> GetChiTietByPhieuAsync(Guid idPhieu);

        // Phiếu
        Task<BmPhieu?> GetPhieuByIdAsync(Guid idPhieu);
    }

    public interface IBBGN_ThepLongRepository
    {
        // Task<IEnumerable<BBGN_ThepLong>> GetAllAsync();
        Task XuLyDuLieuMeThoiGangLongAsync(List<string> data, FetchMeThoiRequest request);
    }

    // ─── Phân bổ dữ liệu (QHLC / CVH / Than cốc <10mm) ────────────────────────

    public interface INhomPhanBoRepository
    {
        Task<List<LG_PB_NhomPhanBo>> GetListAsync(byte? loaiPhanBo, int? idLoCao);
        Task<LG_PB_NhomPhanBo?> GetByIdAsync(int id);
        Task<LG_PB_NhomPhanBo> AddAsync(LG_PB_NhomPhanBo entity);
        Task<LG_PB_NhomPhanBo?> UpdateAsync(int id, LG_PB_NhomPhanBo entity);
        Task<bool> DeleteAsync(int id);

        // NVL thành viên của nhóm — cấu hình RIÊNG cho từng (Ngay, Ca), không kế thừa
        Task<List<NvlNhomPhanBoDto>> GetNvlByNhomAsync(int idNhomPhanBo, DateTime ngay, byte ca);
        Task<LG_PB_NVL_NhomPhanBo> AddNvlAsync(LG_PB_NVL_NhomPhanBo entity);
        Task<bool> RemoveNvlAsync(int idNhomPhanBo, int idNvl, DateTime ngay, byte ca);

        // Toàn bộ nhóm + NVL thành viên của 1 loại phân bổ tại đúng (Ngay, Ca, Lò cao), dùng cho PhanBoService.TinhPhanBoAsync
        Task<List<(LG_PB_NhomPhanBo Nhom, List<LG_PB_NVL_NhomPhanBo> ThanhVien)>> GetNhomVaThanhVienAsync(byte loaiPhanBo, DateTime ngay, byte ca, int idLoCao);

        Task<Dictionary<int, LG_PB_NhomPhanBo>> GetByIdsAsync(IEnumerable<int> ids);
        Task<Dictionary<int, string?>> GetTenNvlMapAsync(IEnumerable<int> idNvlList);

        Task<(DateTime Ngay, byte Ca)?> GetCaLienKe(int loaiPhanBo, int idLoCao, DateTime ngay, byte ca);
    }

    public interface ITyLePhanBoRepository
    {
        Task<List<LG_PB_TyLePhanBo>> GetHistoryAsync(int idNvl, DateTime? tuNgay, DateTime? denNgay);

        // Tỷ lệ hiệu lực tại (ngay, ca) cho từng NVL: ưu tiên bản ghi đúng ca, fallback bản ghi Ca=NULL (áp dụng chung cả ngày)
        Task<Dictionary<int, decimal>> GetHieuLucMapAsync(IEnumerable<int> idNvlList, DateTime ngay, byte? ca);

        // Bản ghi ĐÚNG (Ngay, Ca) — không fallback Ca=NULL — dùng để so sánh "NVL này đang giữ đúng % nhóm
        // hay đã bị sửa riêng", vì cascade nhóm luôn ghi với Ca cụ thể (không dùng Ca=NULL)
        Task<Dictionary<int, decimal>> GetExactMapAsync(IEnumerable<int> idNvlList, DateTime ngay, byte ca);

        // Ghi đè nếu đã có bản ghi cho đúng (IDNVL, Ngay, Ca) — cho phép sửa nhiều lần trước khi chốt
        Task<LG_PB_TyLePhanBo> UpsertAsync(LG_PB_TyLePhanBo entity);

        // % theo nhóm (chỉ nhóm PP2), scoped (IDNhomPhanBo, Ngay, Ca, IDLoCao) — cascade xuống LG_PB_TyLePhanBo ở tầng Service
        Task<LG_PB_TyLeNhom?> GetTyLeNhomAsync(int idNhomPhanBo, DateTime ngay, byte ca, int idLoCao);
        Task<LG_PB_TyLeNhom> UpsertTyLeNhomAsync(LG_PB_TyLeNhom entity);
    }

    public interface INapLieuPhanBoRepository
    {
        // SUM(QuyKho) GROUP BY IDCa, IDNVL cho 1 ngày + 1 lò cao (bỏ Kíp) — dùng LG_NL_ChiTiet.QuyKho có sẵn, không cần view
        Task<List<NapLieuTheoNvlDto>> GetNapLieuAsync(DateTime ngay, int idLoCao);

        // G của CVH: SUM(QuyKho) cho danh sách NVL "than cốc hoàn" đại diện theo lò cao
        Task<List<TongNhanVeDto>> GetNapLieuTheoNvlListAsync(DateTime ngay, IEnumerable<int> idNvlList);

        Task<Dictionary<int, int>> GetMapNvlCvhLoCaoAsync(); // IDLoCao -> IDNVL
    }

    public interface IBienBanNhanRepository
    {
        Task<List<LG_PB_BienBanNhanQHLCCVH>> GetByNgayAsync(DateTime ngay, byte loaiPhanBo);
        Task UpsertAsync(LG_PB_BienBanNhanQHLCCVH entity); // match theo (Ngay, Ca, IDLoCao, LoaiPhanBo)
        Task<Dictionary<int, int>> GetMapXuongLoCaoAsync(); // ID_Xuong -> IDLoCao
    }

    public interface IKetQuaPhanBoRepository
    {
        // Chốt/kiểm tra chốt luôn theo ĐÚNG (Ngày, Ca, Lò cao, Loại phân bổ) — không ảnh hưởng ca/lò cao khác
        Task<bool> IsNgayDaChotAsync(DateTime ngay, byte loaiPhanBo, byte ca, int idLoCao);
        // LG_PB_TyLePhanBo không lưu Lò cao (tỷ lệ dùng chung cho NVL đó ở mọi lò cao) — kiểm tra chốt ở
        // BẤT KỲ lò cao nào của (Ngày, Ca) này để tránh sửa tỷ lệ ảnh hưởng ngược lò cao đã chốt.
        Task<bool> IsCaDaChotAsync(DateTime ngay, byte loaiPhanBo, byte ca);
        // Tập (Ca, Lò cao) đã chốt của 1 (Ngày, Loại phân bổ) — dùng để bỏ qua khi tính lại cả ngày
        Task<List<(byte Ca, int IdLoCao)>> GetChotSetAsync(DateTime ngay, byte loaiPhanBo);
        Task ReplaceNhapAsync(DateTime ngay, byte loaiPhanBo, List<LG_PB_KetQuaPhanBo> entities); // xóa dòng TrangThai=0 cũ rồi ghi mới, transactional
        Task<List<LG_PB_KetQuaPhanBo>> GetByNgayAsync(DateTime ngay, byte? loaiPhanBo, int? idLoCao, byte? ca = null);
        Task<int> ChotAsync(DateTime ngay, byte loaiPhanBo, byte ca, int idLoCao, int idNguoiXacNhan);
        Task<int> HuyChotAsync(DateTime ngay, byte loaiPhanBo, byte ca, int idLoCao);
        Task<List<LG_PB_KetQuaPhanBo>> GetBaoCaoAsync(DateTime tuNgay, DateTime denNgay, int? idLoCao, byte? loaiPhanBo);

        // Gán/sửa Mã công đoạn chi phí (DQ1/DQ2) cho toàn bộ dòng NHÁP (TrangThai=0) của 1 NVL trong ngày —
        // trả về số dòng đã cập nhật (0 nếu ngày đã chốt hoặc NVL không có dòng kết quả nào).
        Task<int> UpdateMaCongDoanChiPhiAsync(DateTime ngay, int idNvl, string? maCongDoanChiPhi);
    }

    public interface IBienBanGiaoNhanSourceRepository
    {
        // Dùng chung cho QHLC (idVatTu=470) và Than cốc <10mm (idVatTu=484)
        // SUM(KL_QuyKho_BG) GROUP BY Ca, ID_Xuong_BG WHERE ID_TrangThai_BBGN=1 AND ID_Xuong_BG IN idXuongList (bỏ Kíp)
        Task<List<(int IdXuong, byte? Ca, decimal KhoiLuong)>> GetTongNhanVeAsync(DateTime ngay, int idVatTu, IEnumerable<int> idXuongList);
    }

    public interface IHrc1SlabRepository
    {
        Task<Hrc1SlabSyncResult> UpsertFromApiAsync(List<TscSlabItem> items);
        Task<(IEnumerable<Hrc1SlabItem> Data, int TotalCount)> SearchAsync(Hrc1SlabSearchRequest req);
        Task<IEnumerable<Hrc1SlabTongHopItem>> GetTongHopAsync(DateOnly? tuNgay, DateOnly? denNgay, string? ca, string? kip);
        Task<IEnumerable<Hrc1PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca);
        Task<IEnumerable<Hrc1SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu);
        Task<IEnumerable<Hrc1SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu);
        Task<int> ChuyenPhoiAsync(List<int> idSlabs, Guid idPhieuNguon, string huong, int nguoiChuyen);
        Task XacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task HuyXacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task HuyChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task<int> FillMacThepAsync();
        Task<Dictionary<string, string>> GetTenVatTuMapAsync(IEnumerable<string?> macTheps);
        Task UpdateSlabAsync(int id, Hrc1SlabUpdateRequest req);
        Task<Hrc1SlabItem> CreateSlabAsync(Hrc1SlabCreateRequest req);
        Task EditSlabAsync(int id, Hrc1SlabEditRequest req);
        Task<int> DeleteSlabsAsync(List<int> idSlabs, int nguoiThucHien);
        Task<int> RestoreSlabsAsync(List<int> idSlabs, int nguoiThucHien);
        Task<IEnumerable<Hrc1TongHopGhiChuItem>> GetTongHopGhiChuAsync(Guid idPhieu);
        Task SaveTongHopGhiChuAsync(Hrc1SaveTongHopGhiChuRequest req);
    }

    public interface IHrc2SlabRepository
    {
        Task<(IEnumerable<Hrc2SlabItem> Data, int TotalCount)> SearchAsync(Hrc2SlabSearchRequest req);
        Task<IEnumerable<Hrc2SlabTongHopItem>> GetTongHopAsync(string? tuNgay, string? denNgay, string? ca, string? kip);
        Task<IEnumerable<Hrc2PhieuBBSLItem>> GetPhieuBBSLAsync(string? kip, int? ca, string? tuNgay = null, string? denNgay = null);
        Task<IEnumerable<Hrc2SlabTongHopItem>> GetRuotPhieuAsync(Guid idPhieu);
        Task<IEnumerable<Hrc2SlabItem>> GetSlabsByPhieuAsync(Guid idPhieu, int? currentUserId = null);
        Task XacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task HuyXacNhanAsync(List<int> idSlabs, string loaiXacNhan, int nguoiThucHien);
        Task ChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task HuyChotPhieuAsync(Guid idPhieu, int nguoiThucHien);
        Task<int> ChuyenBbslAsync(List<int> idSlabs, Guid idPhieu, int nguoiThucHien, DateTime? thoiDiemThaoTac = null);
        Task<int> ThuHoiAsync(List<int> idSlabs, int nguoiThucHien);
        Task<SyncStatusItem> SyncAsync(DateOnly? ngayBatDau, DateOnly? ngayKetThuc);
        Task CheckAsync(List<int> idSlabs, int idUser);
        Task UnCheckAsync(List<int> idSlabs, int idUser);
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

    public interface IMaVatTuRepository
    {
        Task<(IEnumerable<MaVatTuItem> Data, int TotalCount)> SearchAsync(MaVatTuSearchRequest req);
        Task<MaVatTuItem?> GetByIdAsync(int id);
        Task<MaVatTuItem> CreateAsync(MaVatTuUpsertDto dto);
        Task<MaVatTuBulkCreateResult> BulkCreateAsync(List<MaVatTuUpsertDto> items);
        Task<bool> UpdateAsync(int id, MaVatTuUpsertDto dto);
        Task<bool> DeleteAsync(int id);
        Task<Dictionary<string, string>> GetMaVatTuMapAsync(string nhaMay, IEnumerable<string> macThepNames);
    }

    public interface IMacThep_MayDucRepository
    {
        Task<List<MacThep_MayDuc>> GetByMacThepIdAsync(int idMacThep);
        Task<List<MacThep_MayDuc>> GetByMacThepIdsAsync(List<int> idMacTheps);
        Task SetMayDucsAsync(int idMacThep, List<int> idMayDucs);
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

    public interface IHrc1PhuLieuNmRepository
    {
        Task<IEnumerable<Hrc1PhuLieuNm>> GetAllAsync(bool? dangSuDung, string? searchKey);
        Task<Hrc1PhuLieuNm?> GetByIdAsync(int id);
        Task AddAsync(Hrc1PhuLieuNm entity);
        Task UpdateAsync(Hrc1PhuLieuNm entity);
        Task DeleteAsync(Hrc1PhuLieuNm entity);
        Task<bool> ExistsByTenPhuLieuAsync(string tenPhuLieu, int? excludeId = null);
        /// <summary>Trả về lý do (mô tả nơi đang dùng) nếu phụ liệu đã được tham chiếu ở bất kỳ đâu —
        /// null nếu chưa dùng ở đâu cả, có thể xóa an toàn. Kiểm tra cả 2 nhóm:
        /// (1) phiếu tiêu hao BOF/LF (HRC1_TieuHao/HRC1_PhuLieu — mẻ đã đo/nhập thực tế),
        /// (2) Sổ Xuất-Nhập-Tồn (STD_XUAT_NHAP_TON_HRC1/STD_NXT_TOTAL_HRC1 — chi tiết + tổng hợp).</summary>
        Task<string?> GetInUseReasonAsync(int id);
    }
}
