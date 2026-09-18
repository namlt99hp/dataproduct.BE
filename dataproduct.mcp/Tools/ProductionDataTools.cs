using System.ComponentModel;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;

namespace dataproduct.mcp.Tools;

/// <summary>
/// Bộ tool demo cho Pha 1: chứng minh luồng Gateway ↔ MCP Server ↔ SQL Server hoạt động.
/// Đây là tool "khảo sát schema" an toàn (chỉ đọc INFORMATION_SCHEMA + COUNT), chưa phải
/// business tool thật (mục 6 của bản phân tích) — dùng để test trước khi thay bằng
/// tool nghiệp vụ thật (ví dụ get_production_output, get_quality_defect_rate...).
/// </summary>
[McpServerToolType]
public static class ProductionDataTools
{
    [McpServerTool(Name = "ping_database"),
     Description("Kiểm tra kết nối tới Production DB còn sống không, trả về thời gian hiện tại của SQL Server.")]
    public static async Task<string> PingDatabase(
        SqlConnectionFactory connectionFactory,
        [Description("Chọn DB: 'PRODUCT_FORM' (mặc định) hoặc 'PRODUCTDATA'.")] string database = "PRODUCT_FORM",
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(connectionFactory, database, cancellationToken);
        await using var command = new SqlCommand("SELECT GETDATE(), DB_NAME(), @@SERVERNAME", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return JsonSerializer.Serialize(new
        {
            status = "ok",
            requestedDatabase = database,
            serverTime = reader.GetDateTime(0),
            databaseName = reader.GetString(1),
            serverName = reader.IsDBNull(2) ? null : reader.GetString(2)
        });
    }

    [McpServerTool(Name = "list_tables"),
     Description("Liệt kê bảng trong 1 schema của Production DB (mặc định schema 'dbo'), có phân trang. " +
        "Response trả kèm totalCount (tổng số bảng THẬT trong schema) để biết đã liệt kê hết chưa — " +
        "nếu totalCount > số bảng trả về, gọi lại với skip lớn hơn để lấy tiếp.")]
    public static async Task<string> ListTables(
        SqlConnectionFactory connectionFactory,
        [Description("Chọn DB: 'PRODUCT_FORM' (mặc định) hoặc 'PRODUCTDATA'.")] string database = "PRODUCT_FORM",
        [Description("Tên schema muốn liệt kê, mặc định 'dbo'.")] string schema = "dbo",
        [Description("Số bảng bỏ qua từ đầu (phân trang), mặc định 0.")] int skip = 0,
        [Description("Số bảng tối đa lấy về mỗi lần, mặc định 50, tối đa 200.")] int take = 50,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 200);

        await using var connection = await OpenAsync(connectionFactory, database, cancellationToken);

        int totalCount;
        await using (var countCommand = new SqlCommand(
            """
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'
            """, connection))
        {
            countCommand.Parameters.AddWithValue("@schema", schema);
            totalCount = (int)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0);
        }

        await using var command = new SqlCommand(
            """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME
            OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
            """, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@skip", skip);
        command.Parameters.AddWithValue("@take", take);

        var tables = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new { schema = reader.GetString(0), table = reader.GetString(1) });
        }

        return JsonSerializer.Serialize(new
        {
            database,
            schema,
            totalCount,
            skip,
            take,
            returnedCount = tables.Count,
            hasMore = skip + tables.Count < totalCount,
            tables,
        });
    }

    [McpServerTool(Name = "get_table_row_count"),
     Description("Đếm số dòng của 1 bảng cụ thể trong Production DB. Chỉ chấp nhận bảng có thật (kiểm tra qua INFORMATION_SCHEMA trước khi query) để tránh SQL injection qua tên bảng.")]
    public static async Task<string> GetTableRowCount(
        SqlConnectionFactory connectionFactory,
        [Description("Tên bảng cần đếm dòng.")] string tableName,
        [Description("Chọn DB: 'PRODUCT_FORM' (mặc định) hoặc 'PRODUCTDATA'.")] string database = "PRODUCT_FORM",
        [Description("Tên schema chứa bảng, mặc định 'dbo'.")] string schema = "dbo",
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(connectionFactory, database, cancellationToken);

        // Bước 1: whitelist check — xác nhận bảng tồn tại thật qua parameterized query
        await using (var checkCommand = new SqlCommand(
            """
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND TABLE_TYPE = 'BASE TABLE'
            """, connection))
        {
            checkCommand.Parameters.AddWithValue("@schema", schema);
            checkCommand.Parameters.AddWithValue("@table", tableName);
            var exists = (int)(await checkCommand.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
            if (!exists)
            {
                return JsonSerializer.Serialize(new { error = $"Không tìm thấy bảng {schema}.{tableName}." });
            }
        }

        // Bước 2: identifier đã được xác thực tồn tại thật -> an toàn để build câu lệnh COUNT
        var quotedSchema = SqlConnectionFactory.QuoteIdentifier(schema);
        var quotedTable = SqlConnectionFactory.QuoteIdentifier(tableName);
        await using var countCommand = new SqlCommand($"SELECT COUNT(*) FROM {quotedSchema}.{quotedTable}", connection);
        var rowCount = (int)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0);

        return JsonSerializer.Serialize(new { database, schema, table = tableName, rowCount });
    }

    [McpServerTool(Name = "describe_table"),
     Description("Liệt kê danh sách cột (tên, kiểu dữ liệu, có NULL được không) của 1 bảng cụ thể. " +
        "BẮT BUỘC gọi tool này trước khi viết execute_sql cho 1 bảng chưa biết cấu trúc — " +
        "không đoán tên cột, vì tên cột trong DB này bằng tiếng Việt/viết tắt, đoán sai sẽ làm SQL lỗi.")]
    public static async Task<string> DescribeTable(
        SqlConnectionFactory connectionFactory,
        [Description("Tên bảng cần xem cấu trúc cột.")] string tableName,
        [Description("Chọn DB: 'PRODUCT_FORM' (mặc định) hoặc 'PRODUCTDATA'.")] string database = "PRODUCT_FORM",
        [Description("Tên schema chứa bảng, mặc định 'dbo'.")] string schema = "dbo",
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(connectionFactory, database, cancellationToken);

        await using (var checkCommand = new SqlCommand(
            """
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table AND TABLE_TYPE = 'BASE TABLE'
            """, connection))
        {
            checkCommand.Parameters.AddWithValue("@schema", schema);
            checkCommand.Parameters.AddWithValue("@table", tableName);
            var exists = (int)(await checkCommand.ExecuteScalarAsync(cancellationToken) ?? 0) > 0;
            if (!exists)
            {
                return JsonSerializer.Serialize(new { error = $"Không tìm thấy bảng {schema}.{tableName}." });
            }
        }

        await using var command = new SqlCommand(
            """
            SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
            ORDER BY ORDINAL_POSITION
            """, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", tableName);

        var columns = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new
            {
                column = reader.GetString(0),
                dataType = reader.GetString(1),
                nullable = reader.GetString(2) == "YES",
                maxLength = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
            });
        }

        return JsonSerializer.Serialize(new { database, schema, table = tableName, columns });
    }

    [McpServerTool(Name = "execute_sql"),
     Description("Chạy trực tiếp 1 câu lệnh SQL bất kỳ (SELECT/INSERT/UPDATE/DELETE/DDL...) trên Production DB, " +
        "không giới hạn cú pháp — dùng để truy vấn/tuỳ biến dữ liệu theo yêu cầu tự do từ chatbox, " +
        "tương tự cách Claude Desktop kết nối thẳng DB. An toàn dựa vào quyền của user DB đang cấu hình " +
        "(hiện là DB backup demo; khi triển khai thật hãy trỏ McpReadOnlyConnection tới user chỉ có quyền SELECT).")]
    public static async Task<string> ExecuteSql(
        SqlConnectionFactory connectionFactory,
        [Description("Câu lệnh SQL cần chạy.")] string sql,
        [Description("Chọn DB: 'PRODUCT_FORM' (mặc định) hoặc 'PRODUCTDATA'.")] string database = "PRODUCT_FORM",
        [Description("Số dòng tối đa trả về cho câu SELECT (tránh response quá lớn), mặc định 500, tối đa 5000.")] int maxRows = 500,
        CancellationToken cancellationToken = default)
    {
        maxRows = Math.Clamp(maxRows, 1, 5000);

        await using var connection = await OpenAsync(connectionFactory, database, cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 30 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (reader.FieldCount == 0)
        {
            return JsonSerializer.Serialize(new { database, sql, recordsAffected = reader.RecordsAffected });
        }

        var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
        var rows = new List<Dictionary<string, object?>>();
        var truncated = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (rows.Count >= maxRows)
            {
                truncated = true;
                break;
            }

            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return JsonSerializer.Serialize(new { database, columns, rowCount = rows.Count, truncated, rows });
    }

    private static Task<SqlConnection> OpenAsync(SqlConnectionFactory factory, string database, CancellationToken cancellationToken) =>
        database.Trim().ToUpperInvariant() switch
        {
            "PRODUCT_FORM" => factory.OpenProductFormAsync(cancellationToken),
            "PRODUCTDATA" => factory.OpenMasterDataAsync(cancellationToken),
            _ => throw new ArgumentException($"database phải là 'PRODUCT_FORM' hoặc 'PRODUCTDATA', nhận được '{database}'.")
        };
}
