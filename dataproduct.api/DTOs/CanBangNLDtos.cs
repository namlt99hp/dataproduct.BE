using System.Text.Json;

namespace dataproduct.api.DTOs
{
    // ─── Cấu hình động: wwwroot/canbang-nl-config.json ─────────────────────────────
    // Nguồn dữ liệu:
    //   HTTP_BBGN: GET https://report.hoaphatdungquat.vn/BM_11/ThongKeSoLieuData
    //              → { data: [{kip, klBG, klQuyKhoBG, ngay, ...}] }. kip "A"→ca1, "B"→ca2.
    //   LGNL_SILO: ILGNLRepository.GetDuLieuSiloPivotAsync(ngay, ca, idLoCao)
    //              → pivot rows keyed by NVL ID (string).

    public class CanBangNLConfig
    {
        public CanBangNLConfigInfo Config { get; set; } = new();
        public Dictionary<string, CanBangNLSourceConfig> Sources { get; set; } = new();
        public List<CanBangNLRuleConfig> Rules { get; set; } = new();
    }

    public class CanBangNLConfigInfo
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public int Version { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public class CanBangNLSourceConfig
    {
        /// <summary>HTTP_BBGN | LGNL_SILO</summary>
        public string Type { get; set; } = "";
        public string? Description { get; set; }

        // ── HTTP_BBGN ────────────────────────────────────────────────────────────
        /// <summary>Base URL của external BBGN API, vd https://report.hoaphatdungquat.vn/BM_11/ThongKeSoLieuData</summary>
        public string? BaseUrl { get; set; }
        /// <summary>Số bản ghi mỗi trang khi gọi HTTP (mặc định 500 đủ cho 1 ngày)</summary>
        public int PageSize { get; set; } = 500;

        // ── LGNL_SILO ────────────────────────────────────────────────────────────
        /// <summary>Danh sách IDLoCao khi idLoCao=null (tất cả lò). Chỉ dùng cho LGNL_SILO.</summary>
        public List<int> LoCaoList { get; set; } = [1, 2, 3, 4, 5, 6];

        // ── Chung ────────────────────────────────────────────────────────────────
        /// <summary>DIRECT: truyền idLoCao thẳng | MAP: tra loCaoMap ghi đè 1 param | NONE: không tách</summary>
        public string LoCaoMode { get; set; } = "NONE";
    }

    public class CanBangNLRuleConfig
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string Unit { get; set; } = "ton";
        public bool Enabled { get; set; } = true;
        public CanBangNLSideConfig Generator { get; set; } = new();
        public CanBangNLSideConfig Consumer { get; set; } = new();
        public CanBangNLStandardConfig Standard { get; set; } = new();
    }

    public class CanBangNLSideConfig
    {
        public List<CanBangNLItemConfig> Items { get; set; } = new();
    }

    public class CanBangNLItemConfig
    {
        public string Label { get; set; } = "";
        /// <summary>Key trong "sources"</summary>
        public string Source { get; set; } = "";
        /// <summary>Chữ hiển thị ở cột Generate data / Consume data</summary>
        public string? SourceText { get; set; }
        /// <summary>NONE = 1 dòng tổng | MATERIAL_NAME = 1 dòng / tên NVL</summary>
        public string Breakdown { get; set; } = "NONE";
        /// <summary>Params truyền vào nguồn (HTTP query params hoặc LGNL params)</summary>
        public JsonElement Params { get; set; }
        /// <summary>Chỉ dùng khi source.loCaoMode = MAP — ghi đè 1 param theo lò cao đang chọn</summary>
        public CanBangNLLoCaoMapConfig? LoCaoMap { get; set; }
    }

    public class CanBangNLLoCaoMapConfig
    {
        /// <summary>Tên param trong Params bị ghi đè khi chọn lò cao</summary>
        public string Param { get; set; } = "";
        /// <summary>IDLoCao → xưởng ID (single int). Thiếu lò → không filter + cảnh báo.</summary>
        public Dictionary<string, int> Map { get; set; } = new();
    }

    public class CanBangNLStandardConfig
    {
        public CanBangNLLossRateStandard LossRate { get; set; } = new();
    }

    public class CanBangNLLossRateStandard
    {
        /// <summary>&lt; | &lt;=</summary>
        public string Operator { get; set; } = "<=";
        public decimal Value { get; set; } = 2;
        public string Unit { get; set; } = "%";
        /// <summary>true: so sánh |lossRate| (lệch âm quá ngưỡng cũng Không đạt)</summary>
        public bool Absolute { get; set; } = true;
    }

    // ─── BBGN HTTP API response ────────────────────────────────────────────────────

    public class BbgnApiResponse
    {
        public List<BbgnApiItem> Data { get; set; } = new();
    }

    /// <summary>1 bản ghi BBGN từ external API BM_11/ThongKeSoLieuData</summary>
    public class BbgnApiItem
    {
        /// <summary>Ngày biên bản dạng "dd/MM/yyyy"</summary>
        public string? Ngay { get; set; }
        /// <summary>"A" = ca 1 (ngày), "B" = ca 2 (đêm)</summary>
        public string? Kip { get; set; }
        /// <summary>Khối lượng bên giao (ẩm), kiểu string vd "816.126"</summary>
        public string? KlBG { get; set; }
        /// <summary>Khối lượng quy khô bên giao, kiểu string vd "813.106"</summary>
        public string? KlQuyKhoBG { get; set; }
    }

    // ─── Kết quả trả về FE (khớp dataproduct.client/src/services/CanBangNLApi.ts) ──

    public class CanBangNLBalanceItemDto
    {
        public string Name { get; set; } = "";
        public string Source { get; set; } = "";
        public decimal? Value { get; set; }
    }

    public class CanBangNLMaterialBalanceDto
    {
        public string Key { get; set; } = "";
        public string Material { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal StandardLossRate { get; set; }
        public string StandardOperator { get; set; } = "<=";
        public bool StandardAbsolute { get; set; } = true;
        public List<CanBangNLBalanceItemDto> Generators { get; set; } = new();
        public List<CanBangNLBalanceItemDto> Consumers { get; set; } = new();
        /// <summary>Cảnh báo hiển thị cho người dùng (vd nguồn không tách được theo lò cao)</summary>
        public List<string> Notes { get; set; } = new();
    }

    public class CanBangNLTrendPointDto
    {
        public string Label { get; set; } = "";
        public DateTime Ngay { get; set; }
        public int? Ca { get; set; }
        public decimal Generator { get; set; }
        public decimal Consumer { get; set; }
        /// <summary>null khi Generator = 0 (không đủ dữ liệu để kết luận)</summary>
        public decimal? LossRate { get; set; }
    }

    /// <summary>1 dòng dữ liệu thô từ bất kỳ nguồn nào — dùng chung trong CanBangNLSourceRepository</summary>
    public class CanBangNLSourceRow
    {
        public DateTime Ngay { get; set; }
        public int? Ca { get; set; }
        /// <summary>Khóa tách dòng (ID NVL / 0 nếu không tách)</summary>
        public int BreakdownKey { get; set; }
        public string? BreakdownName { get; set; }
        public int BreakdownOrder { get; set; }
        public decimal KhoiLuongAm { get; set; }
        public decimal KhoiLuongKho { get; set; }
    }
}
