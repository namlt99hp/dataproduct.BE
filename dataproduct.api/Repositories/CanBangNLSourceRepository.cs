using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using dataproduct.api.DTOs;

namespace dataproduct.api.Repositories
{
    // Nguồn dữ liệu Cân bằng NL:
    //   HTTP_BBGN  → GET BM_11/ThongKeSoLieuData, parse { data: [] }, kip A→ca1 B→ca2
    //   LGNL_SILO  → ILGNLRepository.GetDuLieuSiloPivotAsync, sum rows theo nvlIds
    // FetchSingleAsync tạo scope mới mỗi lần để tránh concurrent DbContext trên cùng 1 instance.
    public class CanBangNLSourceRepository : ICanBangNLSourceRepository
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };
        private static readonly SemaphoreSlim _siloSem = new(8, 8);

        public CanBangNLSourceRepository(IHttpClientFactory httpFactory, IServiceScopeFactory scopeFactory)
        {
            _httpFactory = httpFactory;
            _scopeFactory = scopeFactory;
        }

        // ─── HTTP_BBGN ────────────────────────────────────────────────────────────

        public async Task<List<CanBangNLSourceRow>> ExecHttpBbgnAsync(
            CanBangNLSourceConfig src, DateTime tuNgay, DateTime denNgay,
            JsonObject itemParams)
        {
            var queryParts = new List<string>
            {
                $"begind={Uri.EscapeDataString(tuNgay.ToString("yyyy-MM-dd"))}",
                $"endd={Uri.EscapeDataString(denNgay.ToString("yyyy-MM-dd"))}",
                "page=1",
                $"pageSize={src.PageSize}"
            };

            foreach (var kv in itemParams)
            {
                if (kv.Value is null || kv.Value.GetValueKind() == JsonValueKind.Null) continue;
                queryParts.Add($"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value.ToString()!)}");
            }

            var url = $"{src.BaseUrl!.TrimEnd('/')}?{string.Join("&", queryParts)}";
            var client = _httpFactory.CreateClient();
            var resp = await client.GetFromJsonAsync<BbgnApiResponse>(url, _jsonOpts);
            if (resp?.Data == null) return [];

            return resp.Data
                .Select(item => new CanBangNLSourceRow
                {
                    Ngay = ParseNgay(item.Ngay, tuNgay),
                    Ca = MapKip(item.Kip),
                    BreakdownKey = 0,
                    BreakdownName = null,
                    BreakdownOrder = 0,
                    KhoiLuongAm = ParseDecimal(item.KlBG),
                    KhoiLuongKho = ParseDecimal(item.KlQuyKhoBG),
                })
                .ToList();
        }

        private static int? MapKip(string? kip) => kip?.Trim().ToUpperInvariant() switch
        {
            "A" => 1,
            "B" => 2,
            _ => null
        };

        private static DateTime ParseNgay(string? raw, DateTime fallback)
        {
            if (raw != null && DateTime.TryParseExact(raw, "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
            return fallback.Date;
        }

        private static decimal ParseDecimal(string? raw)
            => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;

        // ─── LGNL_SILO ────────────────────────────────────────────────────────────

        public async Task<List<CanBangNLSourceRow>> ExecLgnlSiloAsync(
            DateTime ngay, int? ca, int? idLoCao, CanBangNLSourceConfig src, List<int> nhomNVLIds)
        {
            var caList = ca.HasValue ? [ca.Value] : new[] { 1, 2 };
            var loCaoList = idLoCao.HasValue ? [idLoCao.Value] : src.LoCaoList.ToArray();

            var tasks = (
                from c in caList
                from lc in loCaoList
                select FetchSingleAsync(ngay, c, lc, nhomNVLIds)
            ).ToList();

            var allRows = (await Task.WhenAll(tasks)).SelectMany(r => r).ToList();

            // Group by (Ca, NVL ID), sum KL across lò cao
            return allRows
                .GroupBy(r => (r.Ca, r.BreakdownKey))
                .Select(g => new CanBangNLSourceRow
                {
                    Ngay = ngay,
                    Ca = g.Key.Ca,
                    BreakdownKey = g.Key.BreakdownKey,
                    BreakdownName = g.First().BreakdownName,
                    BreakdownOrder = g.First().BreakdownOrder,
                    KhoiLuongAm = g.Sum(r => r.KhoiLuongAm),
                    KhoiLuongKho = g.Sum(r => r.KhoiLuongKho),
                })
                .OrderBy(r => r.BreakdownOrder)
                .ThenBy(r => r.BreakdownKey)
                .ToList();
        }

        private async Task<List<CanBangNLSourceRow>> FetchSingleAsync(
            DateTime ngay, int ca, int idLoCao, IReadOnlyList<int> nhomNVLIds)
        {
            await _siloSem.WaitAsync();
            try
            {
                // Scope riêng → DbContext riêng, tránh "second operation on same context" khi Task.WhenAll
                await using var scope = _scopeFactory.CreateAsyncScope();
                var lgnlRepo = scope.ServiceProvider.GetRequiredService<ILGNLRepository>();

                // Resolve NVL IDs cho lò cao này từ nhóm — mỗi lò có NVL riêng (lò 5: cốc vụn, lò 1-4,6: than cốc)
                var nvlIds = await lgnlRepo.GetNvlIdsByNhomAsync(nhomNVLIds, idLoCao);
                if (nvlIds.Count == 0) return [];

                var pivot = await lgnlRepo.GetDuLieuSiloPivotAsync(ngay, ca, idLoCao);

                // Build name + order lookup from flattened columns
                var nameMap = new Dictionary<string, string>();
                var orderMap = new Dictionary<string, int>();
                var order = 0;
                foreach (var col in pivot.Columns.Where(c => c.Children != null))
                    foreach (var child in col.Children!.Where(c => !c.IsEmpty && c.DataIndex != null))
                    {
                        nameMap[child.DataIndex!] = child.Title;
                        orderMap[child.DataIndex!] = order++;
                    }

                return nvlIds.Select(nvlId =>
                {
                    var key = nvlId.ToString();
                    var sum = pivot.Rows.Sum(row =>
                        row.TryGetValue(key, out var v) && v != null ? Convert.ToDecimal(v) : 0m);
                    return new CanBangNLSourceRow
                    {
                        Ngay = ngay,
                        Ca = ca,
                        BreakdownKey = nvlId,
                        BreakdownName = nameMap.GetValueOrDefault(key, key),
                        BreakdownOrder = orderMap.GetValueOrDefault(key, 999),
                        KhoiLuongAm = sum,
                        KhoiLuongKho = sum,  // SCADA không có độ ẩm, KhôLượng = Ẩm
                    };
                }).ToList();
            }
            finally { _siloSem.Release(); }
        }
    }
}
