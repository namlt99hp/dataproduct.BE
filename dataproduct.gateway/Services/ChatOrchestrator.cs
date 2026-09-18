using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using AnthropicTool = Anthropic.Models.Messages.Tool;
using Role = Anthropic.Models.Messages.Role;

namespace dataproduct.gateway.Services;

/// <summary>
/// Pha 2 + Pha 5: vòng lặp tool-use thủ công (mục 2 bản phân tích) —
/// Gateway = MCP Client, gọi Claude, forward tool_use sang MCP Server, trả kết quả về Claude, lặp tới end_turn.
/// Câu trả lời cuối được ép theo Response Contract (mục 5) — JSON { type, blocks[] } — validate ở đây,
/// fallback về 1 text block nếu Claude không tuân thủ đúng schema.
/// Chưa có: Scope Guardrail (Pha 6), auth theo user (Pha 7).
/// </summary>
public sealed class ChatOrchestrator
{
    private static readonly HashSet<string> AllowedBlockTypes =
        new(StringComparer.OrdinalIgnoreCase) { "text", "table", "chart", "kpi", "image", "file", "card" };

    private const string SystemPrompt =
        """
        Bạn là trợ lý tra cứu dữ liệu sản xuất nội bộ của HPDQ.
        Chỉ trả lời dựa trên dữ liệu lấy được qua tool được cung cấp, không tự bịa số liệu.
        Nếu câu hỏi không liên quan tới dữ liệu sản xuất: trả lời trực tiếp 1 câu từ chối ngắn gọn,
        KHÔNG tạo bất kỳ tool_use nào cả — không thử gọi tool, kể cả với tên như "none"/"no_tool"/tương tự.
        Chỉ được gọi đúng tên tool có trong danh sách tool đã cung cấp, không tự đặt ra tên tool khác.

        Quy trình truy vấn dữ liệu: tên cột trong DB là tiếng Việt/viết tắt, KHÔNG được đoán tên cột.
        Với 1 bảng chưa biết cấu trúc, luôn gọi describe_table trước để lấy đúng tên cột, rồi mới gọi
        execute_sql. Nếu execute_sql báo lỗi do sai tên cột/tên bảng, gọi describe_table hoặc list_tables
        để xác nhận lại thay vì đoán tiếp.

        Khi đã có đủ dữ liệu để trả lời (không cần gọi thêm tool nữa), câu trả lời CUỐI CÙNG của bạn
        PHẢI BẮT ĐẦU bằng ký tự "{" và KẾT THÚC bằng "}" — là một JSON object DUY NHẤT theo đúng schema
        dưới đây. TUYỆT ĐỐI không viết bất kỳ câu dẫn nào trước JSON (vd "Đã có đủ dữ liệu...",
        "Kết quả như sau:..."), không thêm markdown code fence, không thêm chữ nào trước/sau JSON:

        {
          "type": "composite",
          "blocks": [ ... ]
        }

        Mỗi phần tử trong "blocks" là một trong các dạng sau, chọn dạng phù hợp nhất với dữ liệu:
        - {"type":"text","markdown":"..."} — giải thích, nhận xét ngắn
        - {"type":"table","columns":["Cột 1","Cột 2"],"rows":[["a",1],["b",2]]} — dữ liệu dạng bảng
        - {"type":"chart","chartType":"line|bar|pie","categories":["..."],"series":[{"name":"...","data":[1,2,3]}]} — xu hướng/so sánh theo thời gian hoặc nhóm
        - {"type":"kpi","label":"...","value":"...","trend":"up|down|flat"} — 1 con số nổi bật
        - {"type":"card","title":"...","fields":{"Tên trường":"Giá trị"}} — thông tin 1 đối tượng cụ thể (vd 1 lô hàng)

        Luôn có ít nhất 1 block "text" tóm tắt câu trả lời bằng ngôn ngữ tự nhiên. Chỉ thêm "table"/"chart"/"kpi"/"card"
        khi dữ liệu thực sự phù hợp với dạng đó — không ép dữ liệu đơn giản vào table/chart không cần thiết.
        """;

    private const int MaxToolCallTurns = 10;

    private readonly AnthropicClient _claude;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatOrchestrator> _logger;

    public ChatOrchestrator(IConfiguration configuration, ILogger<ChatOrchestrator> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var apiKey = configuration["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("DAN_CLAUDE_API_KEY", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Thiếu Anthropic:ApiKey (đang rỗng hoặc còn placeholder). Điền key thật trong " +
                "appsettings.Development.json (dev) hoặc biến môi trường Anthropic__ApiKey (production). " +
                "Xem console.anthropic.com để tạo key.");
        }

        _claude = new AnthropicClient { ApiKey = apiKey };
    }

    public async Task<JsonElement> AskAsync(string userMessage, CancellationToken cancellationToken)
    {
        await using var mcpClient = await ConnectMcpAsync(cancellationToken);

        var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        var validToolNames = mcpTools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var tools = mcpTools.Select(t => (ToolUnion)ToAnthropicTool(t)).ToList();

        List<MessageParam> messages = [new() { Role = Role.User, Content = userMessage }];
        var usage = new TokenUsageAccumulator();

        for (var turn = 0; turn < MaxToolCallTurns; turn++)
        {
            var response = await _claude.Messages.Create(new MessageCreateParams
            {
                Model = "claude-sonnet-5",
                MaxTokens = 4096,
                System = new List<TextBlockParam>
                {
                    // Cache system prompt + tool schema (mục 12) — không đổi giữa các request
                    // nên cache read chỉ tính 10% giá gốc.
                    new() { Text = SystemPrompt, CacheControl = new CacheControlEphemeral() },
                },
                Tools = tools,
                Messages = messages,
            }, cancellationToken);

            usage.Add(response.Usage, turn + 1);

            _logger.LogInformation(
                "Claude turn {Turn}: stop_reason={StopReason}, input_tokens={Input}, output_tokens={Output}, cache_read={CacheRead}",
                turn, response.StopReason, response.Usage.InputTokens, response.Usage.OutputTokens,
                response.Usage.CacheReadInputTokens);

            if (response.StopReason != "tool_use")
            {
                var finalText = string.Join('\n', response.Content
                    .Select(b => b.Value)
                    .OfType<TextBlock>()
                    .Select(t => t.Text));

                return WithUsage(ParseResponseContract(finalText), usage);
            }

            List<ContentBlockParam> assistantContent = [];
            List<ContentBlockParam> toolResults = [];

            foreach (var block in response.Content)
            {
                if (block.TryPickText(out TextBlock? text))
                {
                    assistantContent.Add(new TextBlockParam { Text = text.Text });
                }
                else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    assistantContent.Add(new ToolUseBlockParam
                    {
                        ID = toolUse.ID,
                        Name = toolUse.Name,
                        Input = toolUse.Input,
                    });

                    var isKnownTool = validToolNames.Contains(toolUse.Name);
                    string resultText;
                    if (!isKnownTool)
                    {
                        // Claude hallucinate tên tool không có thật (vd "none") — KHÔNG gửi xuống MCP Server,
                        // trả lỗi có kiểm soát để Claude tự sửa ở lượt sau thay vì làm request vỡ.
                        _logger.LogWarning("Claude gọi tool không tồn tại: '{ToolName}'", toolUse.Name);
                        resultText = $"Lỗi: tool '{toolUse.Name}' không tồn tại. " +
                            $"Các tool hợp lệ: {string.Join(", ", validToolNames)}.";
                    }
                    else
                    {
                        resultText = await CallMcpToolAsync(mcpClient, toolUse, cancellationToken);
                    }

                    toolResults.Add(new ToolResultBlockParam
                    {
                        ToolUseID = toolUse.ID,
                        Content = resultText,
                        IsError = !isKnownTool,
                    });
                }
            }

            messages.Add(new MessageParam { Role = Role.Assistant, Content = assistantContent });
            messages.Add(new MessageParam { Role = Role.User, Content = toolResults });
        }

        return WithUsage(
            BuildTextOnlyEnvelope("Xin lỗi, câu hỏi cần quá nhiều bước để trả lời được. Bạn thử hỏi cụ thể/ngắn gọn hơn."),
            usage);
    }

    /// <summary>Cộng dồn token usage qua các turn gọi Claude trong 1 lượt hỏi-đáp, để trả về cho FE hiển thị.</summary>
    private sealed class TokenUsageAccumulator
    {
        public long InputTokens { get; private set; }
        public long OutputTokens { get; private set; }
        public long CacheReadInputTokens { get; private set; }
        public long CacheCreationInputTokens { get; private set; }
        public int Turns { get; private set; }

        public void Add(Usage turnUsage, int turnNumber)
        {
            InputTokens += turnUsage.InputTokens;
            OutputTokens += turnUsage.OutputTokens;
            CacheReadInputTokens += turnUsage.CacheReadInputTokens ?? 0;
            CacheCreationInputTokens += turnUsage.CacheCreationInputTokens ?? 0;
            Turns = turnNumber;
        }
    }

    /// <summary>Gắn tổng token usage (mục 12 — theo dõi chi phí) vào envelope trước khi trả về FE.</summary>
    private static JsonElement WithUsage(JsonElement envelope, TokenUsageAccumulator usage) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "composite",
            blocks = envelope.GetProperty("blocks").Clone(),
            usage = new
            {
                inputTokens = usage.InputTokens,
                outputTokens = usage.OutputTokens,
                cacheReadInputTokens = usage.CacheReadInputTokens,
                cacheCreationInputTokens = usage.CacheCreationInputTokens,
                totalTokens = usage.InputTokens + usage.OutputTokens,
                turns = usage.Turns,
            },
        });

    /// <summary>
    /// Validate câu trả lời cuối của Claude theo Response Contract (mục 5): phải là JSON
    /// { type, blocks[] } với mỗi block có "type" nằm trong danh sách cho phép.
    /// Nếu Claude không tuân thủ (parse lỗi, thiếu "blocks", type lạ) -> fallback bọc thành 1 text block,
    /// để FE luôn nhận được đúng 1 hình dạng response duy nhất, không bao giờ vỡ.
    /// </summary>
    private JsonElement ParseResponseContract(string rawText)
    {
        var trimmed = rawText.Trim();

        // Claude đôi khi vẫn bọc JSON trong ```json ... ``` dù đã dặn không làm vậy — gỡ ra trước khi parse.
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        if (TryParseEnvelope(trimmed, out var envelope))
        {
            return envelope;
        }

        // Claude đôi khi viết thêm câu dẫn kiểu "Đã có đủ dữ liệu, tổng hợp kết quả:" trước JSON
        // dù đã dặn không làm vậy — thử cắt lấy đúng khối JSON object (từ '{' đầu tới '}' cuối)
        // trước khi bỏ cuộc, để không mất luôn các block table/chart/kpi vì 1 câu dẫn thừa.
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            var candidate = trimmed[firstBrace..(lastBrace + 1)];
            if (TryParseEnvelope(candidate, out var recovered))
            {
                _logger.LogWarning("Claude viết thêm chữ ngoài JSON, đã tự cắt lấy phần JSON hợp lệ. Raw: {Raw}", rawText);
                return recovered;
            }
        }

        _logger.LogWarning("Claude không trả JSON hợp lệ ở lượt cuối, fallback về text. Raw: {Raw}", rawText);
        return BuildTextOnlyEnvelope(rawText);
    }

    private bool TryParseEnvelope(string json, out JsonElement envelope)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("blocks", out var blocksElement) &&
                blocksElement.ValueKind == JsonValueKind.Array &&
                blocksElement.EnumerateArray().All(IsValidBlock))
            {
                envelope = root.Clone();
                return true;
            }
        }
        catch (JsonException)
        {
        }

        envelope = default;
        return false;
    }

    private static bool IsValidBlock(JsonElement block) =>
        block.ValueKind == JsonValueKind.Object &&
        block.TryGetProperty("type", out var typeElement) &&
        typeElement.ValueKind == JsonValueKind.String &&
        AllowedBlockTypes.Contains(typeElement.GetString() ?? string.Empty);

    private static JsonElement BuildTextOnlyEnvelope(string text) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "composite",
            blocks = new object[] { new { type = "text", markdown = text } },
        });

    private async Task<string> CallMcpToolAsync(McpClient mcpClient, ToolUseBlock toolUse, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = toolUse.Input.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
            var result = await mcpClient.CallToolAsync(toolUse.Name, arguments, cancellationToken: cancellationToken);
            return string.Join('\n', result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gọi MCP tool {ToolName}", toolUse.Name);
            return $"Lỗi khi gọi tool '{toolUse.Name}': {ex.Message}";
        }
    }

    private async Task<McpClient> ConnectMcpAsync(CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["Mcp:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Thiếu Mcp:BaseUrl (URL của dataproduct.mcp, ví dụ http://localhost:5127/mcp).");
        }

        var mcpApiKey = _configuration["Mcp:ApiKey"];
        if (string.IsNullOrWhiteSpace(mcpApiKey))
        {
            throw new InvalidOperationException("Thiếu Mcp:ApiKey — phải khớp với Mcp:ApiKey đang cấu hình bên dataproduct.mcp.");
        }

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(baseUrl),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["X-Mcp-Api-Key"] = mcpApiKey },
        });

        return await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }

    /// <summary>Chuyển tool schema MCP (JSON Schema thô) sang định dạng Tool của Anthropic SDK.</summary>
    private static AnthropicTool ToAnthropicTool(McpClientTool mcpTool)
    {
        var schema = mcpTool.JsonSchema;

        var properties = new Dictionary<string, JsonElement>();
        if (schema.TryGetProperty("properties", out var propsElement))
        {
            foreach (var prop in propsElement.EnumerateObject())
            {
                properties[prop.Name] = prop.Value.Clone();
            }
        }

        var required = new List<string>();
        if (schema.TryGetProperty("required", out var reqElement))
        {
            foreach (var item in reqElement.EnumerateArray())
            {
                if (item.GetString() is { } name)
                {
                    required.Add(name);
                }
            }
        }

        return new AnthropicTool
        {
            Name = mcpTool.Name,
            Description = mcpTool.Description ?? mcpTool.Name,
            InputSchema = new() { Properties = properties, Required = [.. required] },
        };
    }
}
