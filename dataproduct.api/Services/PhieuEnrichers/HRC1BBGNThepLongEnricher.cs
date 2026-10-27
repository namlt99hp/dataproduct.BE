using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;

namespace dataproduct.api.Services.PhieuEnrichers;

public class HRC1BBGNThepLongEnricher : IPhieuSearchEnricher
{
    private readonly IHRC1_BBGNRepository _repo;
    public string MaBm => "HRC1_BBGN_ThepLong";

    public HRC1BBGNThepLongEnricher(IHRC1_BBGNRepository repo) => _repo = repo;

    public async Task EnrichAsync(SearchPhieuResponseModel item)
    {
        if (!item.Scope.HasValue || !item.Ca.HasValue) return;

        // Dùng đúng logic lấy mẻ của GetPhieuAsync (nhánh "duc"): GetMeThepsByMayDucAsync —
        // có tính override CaDucChuyen/NgaySXDucChuyen do TL "Chuyển ca", trước đây tự viết lại
        // query đơn giản (CaTinhLuyen/Ca + khoảng ngày) nên lệch số lượng mẻ với phiếu Đúc thật
        // mỗi khi mẻ bị chuyển ca.
        var mes = await _repo.GetMeThepsByMayDucAsync(item.NgaySX, item.Ca.Value, item.Scope.Value);
        item.SoLuongMe = mes.Count;
        // TrangThaiDuc: 0=chờ xử lý, 1=đã xác nhận, 2=đã chốt — cả 1 và 2 đều coi là máy đúc đã xác nhận mẻ.
        item.SoLuongXNDuc = mes.Count(m => m.TrangThaiDuc >= 1);
    }
}
