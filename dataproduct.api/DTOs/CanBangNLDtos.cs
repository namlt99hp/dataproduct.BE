using System.Text.Json;

namespace dataproduct.api.DTOs
{
    // ─── Cấu hình động: wwwroot/canbang-nl-config.json ─────────────────────────────
    // Mỗi nguồn = 1 stored procedure (script: dataproduct.api/migrations/canbangnl_sp_*.sql), cùng chữ ký:
    //   EXEC {procedure} @TuNgay, @DenNgay, @IDLoCao, @Breakdown, @Params (JSON = item.params)
    // trả về: Ngay, Ca, BreakdownKey, BreakdownName, BreakdownOrder, KhoiLuongAm, KhoiLuongKho

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
        public string? Description { get; set; }
        /// <summary>PRODUCTDATA → ProductDataMasterDbContext; PRODUCT_FORM → ProductFormContext</summary>
        public string Database { get; set; } = "";
        /// <summary>schema.procedure, vd dbo.sp_CanBangNL_NapLieu</summary>
        public string Procedure { get; set; } = "";
        /// <summary>
        /// DIRECT: SP tự lọc theo @IDLoCao.
        /// MAP: SP không có lò cao — API tra item.loCaoMap để ghi đè 1 tham số trong @Params (vd idXuongBN), @IDLoCao = NULL.
        /// NONE: không tách được theo lò cao (luôn lấy tổng, kèm cảnh báo).
        /// </summary>
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
        /// <summary>NONE = 1 dòng tổng (tên = label) | MATERIAL | MATERIAL_NAME | GROUP — truyền vào @Breakdown</summary>
        public string Breakdown { get; set; } = "NONE";
        /// <summary>Object JSON truyền nguyên vào @Params của SP</summary>
        public JsonElement Params { get; set; }
        /// <summary>Chỉ dùng khi source.loCaoMode = MAP</summary>
        public CanBangNLLoCaoMapConfig? LoCaoMap { get; set; }
    }

    public class CanBangNLLoCaoMapConfig
    {
        /// <summary>Tên key trong params bị ghi đè khi chọn lò cao, vd "idXuongBN"</summary>
        public string Param { get; set; } = "";
        /// <summary>IDLoCao → giá trị mảng thay cho params[Param]. Thiếu lò → lấy tổng + cảnh báo</summary>
        public Dictionary<string, List<int>> Map { get; set; } = new();
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

    // ─── Kết quả trả về FE (khớp dataproduct.client/src/services/CanBangNLApi.ts) ─

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
        /// <summary>Cảnh báo hiển thị cho người dùng (vd BBGN không tách được theo lò cao)</summary>
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

    /// <summary>1 dòng dữ liệu thô đã gom theo (Ngày SX, Ca, khóa tách dòng) — dùng chung cho mọi nguồn</summary>
    public class CanBangNLSourceRow
    {
        public DateTime Ngay { get; set; }
        public int? Ca { get; set; }
        /// <summary>Khóa tách dòng (ID NVL / ID nhóm), 0 nếu không tách</summary>
        public int BreakdownKey { get; set; }
        public string? BreakdownName { get; set; }
        public int BreakdownOrder { get; set; }
        public decimal KhoiLuongAm { get; set; }
        public decimal KhoiLuongKho { get; set; }
    }
}
