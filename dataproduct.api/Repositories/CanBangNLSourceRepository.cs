using System.Data;
using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace dataproduct.api.Repositories
{
    // Gọi stored procedure nguồn của Cân bằng NL (khai báo trong canbang-nl-config.json → sources).
    // Mọi SP nguồn cùng chữ ký: @TuNgay, @DenNgay, @IDLoCao, @Breakdown, @Params (JSON)
    // và cùng kết quả CanBangNLSourceRow. Script SP: dataproduct.api/migrations/canbangnl_sp_*.sql
    // Mỗi SP chạy trên đúng DbContext của database (PRODUCTDATA / PRODUCT_FORM có thể khác server → không JOIN chéo).
    public class CanBangNLSourceRepository : ICanBangNLSourceRepository
    {
        private readonly ProductFormContext _formContext;
        private readonly ProductDataMasterDbContext _masterContext;

        public CanBangNLSourceRepository(ProductFormContext formContext, ProductDataMasterDbContext masterContext)
        {
            _formContext = formContext;
            _masterContext = masterContext;
        }

        public async Task<List<CanBangNLSourceRow>> ExecAsync(
            string database, string procedure, DateTime tuNgay, DateTime denNgay,
            int? idLoCao, string breakdown, string paramsJson)
        {
            // procedure đã được CanBangNLConfigHelper validate dạng định danh — giá trị đi qua tham số
            var sql = $"EXEC {procedure} @TuNgay, @DenNgay, @IDLoCao, @Breakdown, @Params";
            var parameters = new object[]
            {
                new SqlParameter("@TuNgay", SqlDbType.Date) { Value = tuNgay.Date },
                new SqlParameter("@DenNgay", SqlDbType.Date) { Value = denNgay.Date },
                new SqlParameter("@IDLoCao", SqlDbType.Int) { Value = (object?)idLoCao ?? DBNull.Value },
                new SqlParameter("@Breakdown", SqlDbType.VarChar, 20) { Value = breakdown.ToUpperInvariant() },
                new SqlParameter("@Params", SqlDbType.NVarChar, -1) { Value = paramsJson }
            };

            DbContext ctx = database.Equals("PRODUCTDATA", StringComparison.OrdinalIgnoreCase) ? _masterContext : _formContext;
            return await ctx.Database.SqlQueryRaw<CanBangNLSourceRow>(sql, parameters).ToListAsync();
        }
    }
}
