using dataproduct.api.Business;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories;
using DinkToPdf;
using DinkToPdf.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// --- JWT Authentication ---
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero   // không có grace period
        };
    });

// Add services to the container.
// Thêm CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
    {
        builder.AllowAnyOrigin()  // Cho phép tất cả các origin
               .AllowAnyMethod()  // Cho phép tất cả các method (GET, POST, PUT, DELETE...)
               .AllowAnyHeader(); // Cho phép tất cả các header
    });
    //options.AddPolicy("AllowReact",
    //    policy =>
    //    {
    //        policy.WithOrigins("http://localhost:5173") // Cho phép React truy cập
    //              .AllowAnyHeader()
    //              .AllowAnyMethod();
    //    });
});

// Đăng ký các lớp xử lý
//builder.Services.AddScoped<IPhieuRepository, PhieuRepository>();
//builder.Services.AddScoped<PhieuService>();

builder.Services.AddSingleton<IConverter>(
    new SynchronizedConverter(new PdfTools())
);

builder.Services.AddHttpClient();

builder.Services.Scan(scan => scan
    .FromAssemblies(Assembly.GetExecutingAssembly())
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Helper")))
        .AsSelf()
        .WithSingletonLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Repository")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Service")))
        .AsSelf()
        .WithScopedLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Initializer")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Exporter")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("Enricher")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(c => c.Where(t => t.Name.EndsWith("SearchFilterProvider")))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
);



// Đăng ký DbContext với Dependency Injection (DI)
builder.Services.AddDbContext<ProductFormContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DbConnectionString")));

builder.Services.AddDbContext<ProductDataMasterDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MasterDbConnection")));

builder.Services.AddControllers(options =>
{
    // Tất cả endpoint mặc định đều cần xác thực — dùng [AllowAnonymous] để loại trừ
    options.Filters.Add(new AuthorizeFilter(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build()));
});
// Add limit request API
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // GLOBAL LIMIT
    options.GlobalLimiter =
        PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ip = context.Connection
                .RemoteIpAddress?
                .ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: ip,

                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1000, // 1000 request
                    Window = TimeSpan.FromMinutes(1),

                    QueueProcessingOrder =
                        QueueProcessingOrder.OldestFirst,

                    QueueLimit = 0
                });
        });
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description  = "Nhập JWT token. Ví dụ: Bearer eyJhbGci..."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseRateLimiter();
app.UseCors("AllowAllOrigins");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.UseSwagger();
app.UseSwaggerUI();
app.MapFallbackToFile("index.html");

// ❌ KHÔNG HTTPS REDIRECTION
// ❌ KHÔNG UseDefaultFiles
// ❌ KHÔNG MapControllers lần 2

app.Run();

