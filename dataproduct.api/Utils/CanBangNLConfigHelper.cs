using System.Text.Json;
using System.Text.RegularExpressions;
using dataproduct.api.DTOs;

namespace dataproduct.api.Utils
{
    /// <summary>
    /// Đọc + validate wwwroot/canbang-nl-config.json (Singleton — Scrutor hậu tố Helper).
    /// Cache theo thời điểm sửa file: sửa JSON không cần restart API.
    /// </summary>
    public class CanBangNLConfigHelper
    {
        /// <summary>schema.procedure — tên SP được ghép vào câu EXEC nên chỉ cho phép ký tự định danh</summary>
        public static readonly Regex ProcedureRef = new(@"^([A-Za-z_]\w*\.)?[A-Za-z_]\w*$", RegexOptions.Compiled);

        public static readonly HashSet<string> Databases = new(StringComparer.OrdinalIgnoreCase) { "PRODUCTDATA", "PRODUCT_FORM" };
        private static readonly HashSet<string> LoCaoModes = new(StringComparer.OrdinalIgnoreCase) { "DIRECT", "MAP", "NONE" };
        private static readonly HashSet<string> Breakdowns = new(StringComparer.OrdinalIgnoreCase) { "NONE", "MATERIAL", "MATERIAL_NAME", "GROUP" };

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
                if (!Databases.Contains(src.Database))
                    errors.Add($"sources.{key}.database '{src.Database}' không hợp lệ (PRODUCTDATA | PRODUCT_FORM)");
                if (!ProcedureRef.IsMatch(src.Procedure))
                    errors.Add($"sources.{key}.procedure '{src.Procedure}' không hợp lệ (dạng schema.tenSP)");
                if (!LoCaoModes.Contains(src.LoCaoMode))
                    errors.Add($"sources.{key}.loCaoMode '{src.LoCaoMode}' không hợp lệ (DIRECT | MAP | NONE)");
            }

            foreach (var rule in cfg.Rules)
                foreach (var item in rule.Generator.Items.Concat(rule.Consumer.Items))
                {
                    var p = $"rules.{rule.Code}.{item.Label}";
                    var src = cfg.Sources.FirstOrDefault(s => s.Key.Equals(item.Source, StringComparison.OrdinalIgnoreCase)).Value;
                    if (src == null)
                        errors.Add($"{p}: source '{item.Source}' chưa khai báo trong sources");
                    if (!Breakdowns.Contains(item.Breakdown))
                        errors.Add($"{p}: breakdown '{item.Breakdown}' không hợp lệ (NONE | MATERIAL | MATERIAL_NAME | GROUP)");
                    if (item.Params.ValueKind != JsonValueKind.Object)
                        errors.Add($"{p}: params phải là object JSON");
                    if (src != null && src.LoCaoMode.Equals("MAP", StringComparison.OrdinalIgnoreCase)
                        && item.LoCaoMap != null && string.IsNullOrWhiteSpace(item.LoCaoMap.Param))
                        errors.Add($"{p}: loCaoMap.param là bắt buộc");
                }

            if (errors.Count > 0)
                throw new InvalidOperationException("Cấu hình cân bằng NL không hợp lệ:\n- " + string.Join("\n- ", errors));
        }
    }
}
