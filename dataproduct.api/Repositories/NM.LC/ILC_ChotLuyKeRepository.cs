using dataproduct.api.DTOs.NM.LC;

namespace dataproduct.api.Repositories.NM.LC
{
    public interface ILC_ChotLuyKeRepository
    {
        // "Chốt lũy kế" tại phiếu idPhieuChot — ghi 1 mốc LC_LuyKeChotMoc rồi CASCADE tính lại
        // (và lưu lại) mọi phiếu cùng Scope có (NgaySX,Ca) SAU mốc đó, theo đúng thứ tự thời
        // gian, cho cả 8 chuỗi "Lũy kế" của module LC. Xem LC_ChotLuyKeRepository.
        Task<LcChotLuyKeResultDto> ChotLuyKeAsync(Guid idPhieuChot, string? nguoiChot);
    }
}
