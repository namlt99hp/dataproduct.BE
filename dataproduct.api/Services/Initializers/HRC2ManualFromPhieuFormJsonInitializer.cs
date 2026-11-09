using dataproduct.api.Models;
using dataproduct.api.Services;

namespace dataproduct.api.Services.Initializers
{
    public class HRC2ManualFromPhieuFormJsonInitializer : IPhieuJsonInitializer
    {
        private readonly DLNMHRC2Service _dlnmHrc2Service;

        public HRC2ManualFromPhieuFormJsonInitializer(DLNMHRC2Service dlnmHrc2Service)
        {
            _dlnmHrc2Service = dlnmHrc2Service;
        }

        public bool CanHandle(string? maBm)
        {
            if (string.IsNullOrWhiteSpace(maBm))
                return false;

            return maBm.Equals("HRC2_BB_NauLuyen_BOF", StringComparison.OrdinalIgnoreCase)
                || maBm.Equals("HRC2_BB_NauLuyen_LF", StringComparison.OrdinalIgnoreCase)
                || maBm.Equals("HRC2_BB_NauLuyen_RH", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<string>> InitializeAsync(BmPhieu phieu)
        {
            await _dlnmHrc2Service.SaveHRC2ManualFromPhieuFormAsync(phieu);
            return new List<string>();
        }
    }
}

