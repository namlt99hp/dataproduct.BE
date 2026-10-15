using System.Text.Json.Nodes;
using dataproduct.api.DTOs;
using dataproduct.api.Repositories;
using dataproduct.api.Utils;

namespace dataproduct.api.Services
{
    /// <summary>
    /// Cân bằng nguyên liệu (Kho dữ liệu → Cân bằng NL): Generator (giao nhận) vs Consumer (vào lò).
    /// Mỗi nguồn truy vấn riêng (khác database/server) rồi ghép theo (Ngày SX, Ca) ở tầng C#.
    /// Total / Balance loss / Loss rate của bảng chi tiết do FE tự tính; trend tính sẵn lossRate ở đây.
    /// </summary>
    public class CanBangNLService
    {
        private readonly CanBangNLConfigHelper _configHelper;
        private readonly ICanBangNLSourceRepository _sourceRepo;

        public CanBangNLService(CanBangNLConfigHelper configHelper, ICanBangNLSourceRepository sourceRepo)
        {
            _configHelper = configHelper;
            _sourceRepo = sourceRepo;
        }

        public Task<CanBangNLConfig> GetConfigAsync() => _configHelper.GetAsync();

        public async Task<List<CanBangNLMaterialBalanceDto>> GetCanBangAsync(DateTime ngay, int? ca, bool coAm, int? idLoCao)
        {
            var cfg = await _configHelper.GetAsync();
            var result = new List<CanBangNLMaterialBalanceDto>();
            if (!cfg.Config.Enabled) return result;

            foreach (var rule in cfg.Rules.Where(r => r.Enabled))
            {
                var dto = new CanBangNLMaterialBalanceDto
                {
                    Key = rule.Code,
                    Material = rule.Name,
                    Unit = rule.Unit,
                    StandardLossRate = rule.Standard.LossRate.Value,
                    StandardOperator = rule.Standard.LossRate.Operator,
                    StandardAbsolute = rule.Standard.LossRate.Absolute
                };
                dto.Generators = await BuildItemsAsync(cfg, rule.Generator.Items, ngay, ca, coAm, idLoCao, dto.Notes);
                dto.Consumers = await BuildItemsAsync(cfg, rule.Consumer.Items, ngay, ca, coAm, idLoCao, dto.Notes);
                result.Add(dto);
            }
            return result;
        }

        public async Task<List<CanBangNLTrendPointDto>> GetTrendAsync(
            DateTime ngay, int? ca, bool coAm, int? idLoCao, string material, int soNgay)
        {
            var cfg = await _configHelper.GetAsync();
            var rule = cfg.Rules.FirstOrDefault(r =>
                           r.Code.Equals(material, StringComparison.OrdinalIgnoreCase) ||
                           r.Name.Equals(material, StringComparison.OrdinalIgnoreCase))
                       ?? throw new InvalidOperationException($"Không tìm thấy nguyên liệu '{material}' trong cấu hình");

            soNgay = Math.Clamp(soNgay, 1, 93);
            var denNgay = ngay.Date;
            var tuNgay = denNgay.AddDays(-(soNgay - 1));

            // HTTP_BBGN: 1 request/item cho cả khoảng ngày; LGNL_SILO: song song từng ngày trong LoadItemAsync
            var genRows = new List<CanBangNLSourceRow>();
            foreach (var item in rule.Generator.Items)
                genRows.AddRange((await LoadItemAsync(cfg, item, tuNgay, denNgay, idLoCao)).Rows);
            var consRows = new List<CanBangNLSourceRow>();
            foreach (var item in rule.Consumer.Items)
                consRows.AddRange((await LoadItemAsync(cfg, item, tuNgay, denNgay, idLoCao)).Rows);

            decimal Sum(IEnumerable<CanBangNLSourceRow> rows, DateTime d, int? c) => rows
                .Where(r => r.Ngay.Date == d && (c == null || r.Ca == c))
                .Sum(r => coAm ? r.KhoiLuongAm : r.KhoiLuongKho);

            var points = new List<CanBangNLTrendPointDto>();
            for (var d = tuNgay; d <= denNgay; d = d.AddDays(1))
            {
                foreach (var c in ca.HasValue ? new int?[] { ca } : new int?[] { 1, 2 })
                {
                    var gen = Sum(genRows, d, c);
                    var cons = Sum(consRows, d, c);
                    points.Add(new CanBangNLTrendPointDto
                    {
                        Label = ca.HasValue ? d.ToString("dd/MM") : $"{d:dd/MM} C{c}",
                        Ngay = d,
                        Ca = c,
                        Generator = gen,
                        Consumer = cons,
                        LossRate = gen == 0 ? null : Math.Round((gen - cons) / gen * 100, 2)
                    });
                }
            }
            return points;
        }

        private async Task<List<CanBangNLBalanceItemDto>> BuildItemsAsync(
            CanBangNLConfig cfg, List<CanBangNLItemConfig> items,
            DateTime ngay, int? ca, bool coAm, int? idLoCao, List<string> notes)
        {
            var result = new List<CanBangNLBalanceItemDto>();
            foreach (var item in items)
            {
                var (rows, note) = await LoadItemAsync(cfg, item, ngay, ngay, idLoCao);
                if (note != null) notes.Add(note);
                if (ca.HasValue) rows = rows.Where(r => r.Ca == ca).ToList();

                var sourceText = item.SourceText ?? item.Source;
                decimal Kl(CanBangNLSourceRow r) => coAm ? r.KhoiLuongAm : r.KhoiLuongKho;

                if (item.Breakdown.Equals("NONE", StringComparison.OrdinalIgnoreCase) || rows.Count == 0)
                {
                    // Không có dòng nào → null (FE hiện "—"), phân biệt với "có dữ liệu = 0"
                    result.Add(new CanBangNLBalanceItemDto
                    {
                        Name = item.Label,
                        Source = sourceText,
                        Value = rows.Count == 0 ? null : rows.Sum(Kl)
                    });
                    continue;
                }

                result.AddRange(rows
                    .GroupBy(r => r.BreakdownKey)
                    .OrderBy(g => g.Min(r => r.BreakdownOrder))
                    .ThenBy(g => g.Key)
                    .Select(g => new CanBangNLBalanceItemDto
                    {
                        Name = g.Select(r => r.BreakdownName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim()
                               ?? $"{item.Label} #{g.Key}",
                        Source = sourceText,
                        Value = g.Sum(Kl)
                    }));
            }
            return result;
        }

        private async Task<(List<CanBangNLSourceRow> Rows, string? Note)> LoadItemAsync(
            CanBangNLConfig cfg, CanBangNLItemConfig item, DateTime tuNgay, DateTime denNgay, int? idLoCao)
        {
            var src = cfg.Sources.First(s => s.Key.Equals(item.Source, StringComparison.OrdinalIgnoreCase)).Value;
            string? note = null;

            switch (src.Type.ToUpperInvariant())
            {
                case "HTTP_BBGN":
                {
                    var prms = JsonNode.Parse(item.Params.GetRawText()) as JsonObject ?? new JsonObject();
                    if (idLoCao.HasValue && item.LoCaoMap != null)
                    {
                        if (item.LoCaoMap.Map.TryGetValue(idLoCao.Value.ToString(), out var xId))
                            prms[item.LoCaoMap.Param] = JsonValue.Create(xId);
                        else
                            note = $"\"{item.Label}\" chưa cấu hình loCaoMap cho lò cao {idLoCao} — đang lấy tổng tất cả.";
                    }
                    var rows = await _sourceRepo.ExecHttpBbgnAsync(src, tuNgay, denNgay, prms);
                    return (rows, note);
                }
                case "LGNL_SILO":
                {
                    var nvlIds = ExtractNhomNVLIds(item.Params);
                    // LGNL_SILO chỉ hỗ trợ 1 ngày/lần — gọi song song từng ngày trong khoảng
                    var days = Enumerable.Range(0, (int)(denNgay.Date - tuNgay.Date).TotalDays + 1)
                                        .Select(i => tuNgay.Date.AddDays(i));
                    var results = await Task.WhenAll(
                        days.Select(d => _sourceRepo.ExecLgnlSiloAsync(d, null, idLoCao, src, nvlIds)));
                    return (results.SelectMany(r => r).ToList(), note);
                }
                default:
                    throw new InvalidOperationException($"Loại nguồn '{src.Type}' chưa được hỗ trợ.");
            }
        }

        private static List<int> ExtractNhomNVLIds(System.Text.Json.JsonElement paramsEl)
        {
            if (paramsEl.TryGetProperty("idNhomNVLs", out var arr)
                && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                return arr.EnumerateArray().Select(e => e.GetInt32()).ToList();
            return [];
        }
    }
}
