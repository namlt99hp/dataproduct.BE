using Microsoft.Data.SqlClient;

namespace dataproduct.mcp;

/// <summary>
/// Tạo SqlConnection read-only tới 1 trong 2 Production DB (PRODUCT_FORM / PRODUCTDATA),
/// cấu hình qua ConnectionStrings:McpReadOnlyConnection và ConnectionStrings:McpReadOnlyMasterConnection
/// (appsettings hoặc biến môi trường ConnectionStrings__McpReadOnlyConnection /
/// ConnectionStrings__McpReadOnlyMasterConnection khi deploy production).
/// Dùng login "mcp_readonly" (role db_datareader) — KHÔNG dùng chung "sa" với dataproduct.api.
/// </summary>
public sealed class SqlConnectionFactory(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;

    /// <summary>Mở kết nối tới DB PRODUCT_FORM (đơn hàng/phiếu sản xuất...).</summary>
    public Task<SqlConnection> OpenProductFormAsync(CancellationToken cancellationToken = default) =>
        OpenAsync("DbConnectionString", cancellationToken);

    /// <summary>Mở kết nối tới DB PRODUCTDATA (master data dùng chung).</summary>
    public Task<SqlConnection> OpenMasterDataAsync(CancellationToken cancellationToken = default) =>
        OpenAsync("MasterDbConnection", cancellationToken);

    private async Task<SqlConnection> OpenAsync(string connectionStringName, CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Thiếu ConnectionStrings:{connectionStringName} (đang rỗng hoặc chưa cấu hình). " +
                $"Kiểm tra đúng TÊN KEY này trong appsettings.{{Environment}}.json " +
                $"hoặc biến môi trường ConnectionStrings__{connectionStringName} — " +
                $"không phải DbConnectionString/MasterDbConnection (đó là key của dataproduct.api).");
        }

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>
    /// Bọc identifier (tên bảng/schema) theo chuẩn SQL Server để build câu lệnh an toàn
    /// SAU KHI đã xác thực identifier đó tồn tại thật trong INFORMATION_SCHEMA (whitelist check).
    /// Không dùng hàm này để bọc trực tiếp input người dùng khi chưa validate.
    /// </summary>
    public static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]")}]";
}
