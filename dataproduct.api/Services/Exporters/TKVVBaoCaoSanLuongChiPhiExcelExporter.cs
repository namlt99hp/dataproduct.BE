using dataproduct.api.DTOs.Export;
using dataproduct.api.Services.NMTKVV;

namespace dataproduct.api.Services.Exporters
{
    public class TKVVBaoCaoSanLuongChiPhiExcelExporter : IPhieuExcelExporter
    {
        private readonly TKVV_BCSL_ChiPhiService _service;

        public TKVVBaoCaoSanLuongChiPhiExcelExporter(TKVV_BCSL_ChiPhiService service)
        {
            _service = service;
        }

        public bool CanHandle(string? maBm)
        {
            if (string.IsNullOrWhiteSpace(maBm))
                return false;

            return maBm.Equals("TKVV_BC_SanLuongChiPhi", StringComparison.OrdinalIgnoreCase)
                || maBm.Equals("BM.06-QT.05.03", StringComparison.OrdinalIgnoreCase)
                || maBm.Equals("BM.06/QT.05.03", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ExportFileResult> ExportTongHopExcelAsync(DateOnly? fromDate, DateOnly? toDate)
            => throw new NotSupportedException("Chưa hỗ trợ export tổng hợp Excel cho báo cáo sản lượng & chi phí.");

        public Task<ExportFileResult> ExportExcelPhieuAsync(Guid phieuId)
            => _service.ExportBaoCaoExcelAsync(phieuId);
    }
}
