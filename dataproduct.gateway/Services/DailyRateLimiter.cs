using System.Collections.Concurrent;

namespace dataproduct.gateway.Services;

/// <summary>
/// Pha 6, tầng 5 (mục 11 bản phân tích): lưới an toàn cuối, giới hạn số câu hỏi/ngày
/// bất kể các tầng lọc trước có sót hay không — tránh 1 client spam tốn token Claude.
/// Chưa có auth thật (Pha 7) nên tạm dùng IP làm khoá định danh; khi có userId thật
/// (JWT/SSO nội bộ), đổi key truyền vào <see cref="TryConsume"/> sang userId.
/// Đếm in-memory, đủ cho 1 instance Gateway; nếu scale nhiều instance (mục 4) cần
/// chuyển sang Redis để đếm dùng chung.
/// </summary>
public sealed class DailyRateLimiter(IConfiguration configuration)
{
    private readonly int _maxPerDay = configuration.GetValue("Guardrail:MaxMessagesPerDayPerUser", 200);
    private readonly ConcurrentDictionary<string, (DateOnly Day, int Count)> _counters = new();

    public bool TryConsume(string key, out int remaining)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var updated = _counters.AddOrUpdate(
            key,
            _ => (today, 1),
            (_, existing) => existing.Day == today ? (today, existing.Count + 1) : (today, 1));

        remaining = Math.Max(0, _maxPerDay - updated.Count);
        return updated.Count <= _maxPerDay;
    }
}
