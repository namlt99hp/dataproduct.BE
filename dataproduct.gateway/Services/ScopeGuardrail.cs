namespace dataproduct.gateway.Services;

/// <summary>
/// Pha 6, tầng 2 (mục 11 bản phân tích): chặn câu hỏi RÕ RÀNG lạc đề bằng keyword
/// thuần code, TRƯỚC khi gọi Claude API — 0 token cho các case rõ ràng nhất.
/// Chỉ chặn khi câu hỏi khớp 1 chủ đề ngoài phạm vi VÀ không hề chứa từ khoá nghiệp vụ nào
/// (tránh chặn nhầm câu hợp lệ dạng "so sánh chất lượng với thời tiết ảnh hưởng thế nào").
/// Case mơ hồ (không khớp cả 2 danh sách) được cho qua tầng 3 — system prompt guardrail
/// trong <see cref="ChatOrchestrator"/> sẽ tự từ chối nếu Claude thấy không liên quan.
/// </summary>
public static class ScopeGuardrail
{
    public const string CannedRefusal =
        "Xin lỗi, trợ lý chỉ hỗ trợ tra cứu dữ liệu sản xuất HPDQ " +
        "(sản lượng, tồn kho, chất lượng, lô hàng, đơn hàng, dây chuyền...). " +
        "Bạn thử đặt câu hỏi liên quan tới dữ liệu sản xuất nhé.";

    private static readonly string[] DomainKeywords =
    [
        "sản lượng", "tồn kho", "lô hàng", "ca sản xuất", "chất lượng", "đơn hàng",
        "dây chuyền", "nguyên vật liệu", "nvl", "kho", "xuất kho", "nhập kho", "phế phẩm",
        "tỷ lệ lỗi", "tỷ lệ đạt", "công đoạn", "phân xưởng", "nhà máy", "sản phẩm", "báo cáo",
        "kpi", "phiếu", "mã lô", "batch", "vật tư", "thiết bị", "bảo trì", "bảng", "cột",
        "dữ liệu", "database", "table", "hrc", "thép", "phôi", "cán", "xưởng",
    ];

    private static readonly string[] OffTopicKeywords =
    [
        "thời tiết", "công thức nấu ăn", "nấu ăn", "phim", "ca sĩ", "bóng đá", "thể thao",
        "chính trị", "tử vi", "horoscope", "dịch giúp", "dịch sang tiếng", "viết thơ",
        "làm thơ", "viết truyện", "kể chuyện cười", "viết code", "lập trình giúp",
        "giải phương trình", "toán học", "yêu đương", "tình yêu", "chơi game", "du lịch",
    ];

    public static bool IsClearlyOffTopic(string message)
    {
        var normalized = message.ToLowerInvariant();

        if (DomainKeywords.Any(normalized.Contains))
        {
            return false;
        }

        return OffTopicKeywords.Any(normalized.Contains);
    }
}
