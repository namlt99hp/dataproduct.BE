using dataproduct.mcp;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SqlConnectionFactory>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Mỗi request tự chứa đủ thông tin, không cần giữ session giữa nhiều request
        // -> đơn giản hoá scale-out (mục 4 bản phân tích).
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithToolsFromAssembly();

var app = builder.Build();

var apiKey = app.Configuration["Mcp:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "Thiếu cấu hình Mcp:ApiKey. Set trong appsettings.{Environment}.json (dev) " +
        "hoặc biến môi trường Mcp__ApiKey (production). Đây là secret dùng để AI Gateway " +
        "xác thực khi gọi MCP Server — không public MCP Server ra ngoài network nội bộ.");
}

// MCP Server chỉ được gọi bởi AI Gateway (service-to-service), không phải client cuối.
// Middleware này là lớp bảo vệ tối thiểu cho Pha 1 — production nên đặt thêm sau firewall
// chỉ cho phép IP của Gateway (mục 3 bản phân tích).
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.TryGetValue("X-Mcp-Api-Key", out var provided) ||
        provided.ToString() != apiKey)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    await next();
});

app.MapMcp("/mcp");

app.Run();
