using System.Text.Json;
using dataproduct.api.DTOs;

namespace dataproduct.api.Utils
{
    /// <summary>
    /// Đọc + validate wwwroot/canbang-nl-config.json (Singleton — Scrutor hậu tố Helper).
    /// Cache theo thời điểm sửa file: sửa JSON không cần restart API.
    /// </summary>
    public class CanBangNLConfigHelper
    {
        private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
            { "HTTP_BBGN", "LGNL_SILO" };
        private static readonly HashSet<string> LoCaoModes = new(StringComparer.OrdinalIgnoreCase)
            { "DIRECT", "MAP", "NONE" };
        private static readonly HashSet<string> Breakdowns = new(StringComparer.OrdinalIgnoreCase)
            { "NONE", "MATERIAL_NAME" };

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private readonly string _path;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private CanBangNLConfig? _cache;
        private DateTime _cacheTime;

        public CanBangNLConfigHelper(IWebHostEnvironment env)
        {
            _path = Path.Combine(env.WebRootPath, "canbang-nl-config.json");
        }

        public async Task<CanBangNLConfig> GetAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(_path))
                    throw new InvalidOperationException("Không tìm thấy file cấu hình canbang-nl-config.json");

                var lastWrite = File.GetLastWriteTimeUtc(_path);
                if (_cache == null || lastWrite != _cacheTime)
                {
                    var json = await File.ReadAllTextAsync(_path);
                    var cfg = JsonSerializer.Deserialize<CanBangNLConfig>(json, JsonOpts)
                              ?? throw new InvalidOperationException("File cấu hình cân bằng NL rỗng");
                    Validate(cfg);
                    _cache = cfg;
                    _cacheTime = lastWrite;
                }
                return _cache;
            }
            finally { _lock.Release(); }
        }

        private static void Validate(CanBangNLConfig cfg)
        {
            var errors = new List<string>();

            foreach (var (key, src) in cfg.Sources)
            {
                if (!ValidTypes.Contains(src.Type))
                    errors.Add($"sources.{key}.type '{src.Type}' không hợp lệ ({string.Join(" | ", ValidTypes)})");
                if (src.Type.Equals("HTTP_BBGN", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(src.BaseUrl))
                    errors.Add($"sources.{key}.baseUrl là bắt buộc khi type=HTTP_BBGN");
                if (!LoCaoModes.Contains(src.LoCaoMode))
                    errors.Add($"sources.{key}.loCaoMode '{src.LoCaoMode}' không hợp lệ (DIRECT | MAP | NONE)");
            }

            foreach (var rule in cfg.Rules)
                foreach (var item in rule.Generator.Items.Concat(rule.Consumer.Items))
                {
                    var p = $"rules.{rule.Code}.{item.Label}";
                    var hasSrc = cfg.Sources.Any(s => s.Key.Equals(item.Source, StringComparison.OrdinalIgnoreCase));
                    if (!hasSrc)
                        errors.Add($"{p}: source '{item.Source}' chưa khai báo trong sources");
                    if (!Breakdowns.Contains(item.Breakdown))
                        errors.Add($"{p}: breakdown '{item.Breakdown}' không hợp lệ ({string.Join(" | ", Breakdowns)})");
                    if (item.Params.ValueKind != JsonValueKind.Object)
                        errors.Add($"{p}: params phải là object JSON");
                }

            if (errors.Count > 0)
                throw new InvalidOperationException("Cấu hình cân bằng NL không hợp lệ:\n- " + string.Join("\n- ", errors));
        }
    }
}
