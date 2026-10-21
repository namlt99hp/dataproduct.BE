using dataproduct.api.DTOs.Export;
using dataproduct.api.Services.NMTKVV;

namespace dataproduct.api.Services.Exporters
{
    public class TKVVTonSiloExcelExporter : IPhieuExcelExporter
    {
        private readonly TKVV_TonSiloService _service;

        public TKVVTonSiloExcelExporter(TKVV_TonSiloService service)
        {
            _service = service;
        }

        public bool CanHandle(string? maBm)
        {
            if (string.IsNullOrWhiteSpace(maBm))
                return false;

            return maBm.Equals("TKVV_TONSILO", StringComparison.OrdinalIgnoreCase)
                || maBm.Equals("BM.05-QT.05.03", StringComparison.OrdinalIgnoreCase)
                || maBm.Equals("BM.05/QT.05.03", StringComparison.OrdinalIgnoreCase);
        }

        public Task<ExportFileResult> ExportTongHopExcelAsync(DateOnly? fromDate, DateOnly? toDate)
            => throw new NotSupportedException("Chưa hỗ trợ export tổng hợp Excel cho sổ theo dõi tồn silo.");

        public Task<ExportFileResult> ExportExcelPhieuAsync(Guid phieuId)
            => _service.ExportTonSiloExcelAsync(phieuId);
    }
}
