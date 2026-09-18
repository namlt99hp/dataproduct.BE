using dataproduct.gateway.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ChatOrchestrator>();
builder.Services.AddSingleton<DailyRateLimiter>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAllOrigins");

app.MapPost("/api/chat", async (
    ChatRequest request,
    ChatOrchestrator orchestrator,
    DailyRateLimiter rateLimiter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new { error = "message không được để trống." });
    }

    // Tầng 5 (mục 11): lưới an toàn cuối, chặn bất kể tầng lọc trước có sót hay không.
    // Chưa có userId thật (Pha 7) -> tạm dùng IP client làm khoá định danh.
    var rateLimitKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    if (!rateLimiter.TryConsume(rateLimitKey, out var remaining))
    {
        return Results.Json(
            new { error = "Bạn đã dùng hết số câu hỏi cho phép trong hôm nay. Vui lòng thử lại vào ngày mai." },
            statusCode: StatusCodes.Status429TooManyRequests);
    }

    // Tầng 2 (mục 11): chặn câu hỏi rõ ràng lạc đề bằng keyword thuần code, 0 token.
    if (ScopeGuardrail.IsClearlyOffTopic(request.Message))
    {
        return Results.Ok(new
        {
            type = "composite",
            blocks = new object[] { new { type = "text", markdown = ScopeGuardrail.CannedRefusal } },
        });
    }

    var response = await orchestrator.AskAsync(request.Message, cancellationToken);
    return Results.Ok(response);
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

record ChatRequest(string Message);
