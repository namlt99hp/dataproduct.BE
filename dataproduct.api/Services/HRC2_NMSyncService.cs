using dataproduct.api.DTOs;
using dataproduct.api.Models;
using dataproduct.api.Models.MasterData;
using dataproduct.api.Repositories;
using dataproduct.api.ResponseModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace dataproduct.api.Services
{
    public class HRC2_NMSyncService
    {
        private readonly ProductFormContext _context;
        private readonly ProductDataMasterDbContext _masterDataContext;
        private readonly string _gangLongConnStr;

        public HRC2_NMSyncService(ProductFormContext context, ProductDataMasterDbContext masterDataContext, IConfiguration config)
        {
            _context = context;
            _masterDataContext = masterDataContext;

            // GangLong metrics nằm ở DB khác. Ưu tiên connection string riêng nếu có,
            // fallback về MasterDbConnection để tương thích config cũ.
            _gangLongConnStr =
                config.GetConnectionString("GangLongDbConnection")
                ?? config.GetConnectionString("MasterDbConnection")
                ?? throw new InvalidOperationException("GangLongDbConnection/MasterDbConnection connection string is not configured");
        }

        /// <summary>
        /// Gọi store để sync dữ liệu HRC2 mới nhất từ NM về DB hiện tại (DLNM_HRC2 + PhuLieu_HRC2).
        /// Store này do bạn tạo và chịu trách nhiệm insert/update dữ liệu.
        /// LoaiBM/Scope truyền NULL để không giới hạn (dùng cho các API cần gộp toàn bộ BieuMau/Scope
        /// trong 1 Ngày/Ca, ví dụ filterSTD_NXT).
        /// </summary>
        public async Task SyncFromNmStoredProcAsync(DateTime ngay, int ca, string? loaiBM, int? scope)
        {
            await _context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.sp_Sync_HRC2_FromNM @Ngay, @Ca, @LoaiBM, @Scope",
                new SqlParameter("@Ngay", ngay.Date),
                new SqlParameter("@Ca", ca),
                new SqlParameter("@LoaiBM", (object?)loaiBM ?? DBNull.Value),
                new SqlParameter("@Scope", (object?)scope ?? DBNull.Value));
        }

        // private async Task EnsureGangLongMetricsAsync(SyncFromNM_HRC2_Request request)
        // {
        //     // Chỉ áp dụng cho BOF
        //     if (!string.Equals(request.LoaiBM, "BOF", StringComparison.OrdinalIgnoreCase))
        //         return;

        //     // Chỉ tính cho record NM trong slot hiện tại
        //     var rows = await _context.DLNM_HRC2s
        //         .Where(x =>
        //             x.IsNM == true &&
        //             x.Ngay == request.NgaySX &&
        //             x.Ca == request.Ca &&
        //             x.Scope == request.Scope &&
        //             x.BieuMau == request.LoaiBM &&
        //             x.MeThoi != null)
        //         .ToListAsync();

        //     // Chỉ gọi store (DB khác) cho những mẻ chưa có KLGangLongCCT
        //     var needMeThoi = rows
        //         .Where(x => x.KLGangLongCCT == null)
        //         .Select(x => x.MeThoi!)
        //         .Where(x => !string.IsNullOrWhiteSpace(x))
        //         .Distinct(StringComparer.OrdinalIgnoreCase)
        //         .ToList();

        //     if (needMeThoi.Count == 0) return;

        //     var cache = new Dictionary<string, (double? CCT, double? CR)>(StringComparer.OrdinalIgnoreCase);

        //     foreach (var meThoi in needMeThoi)
        //     {
        //         var cct = await ExecuteGangLongStoredProcAsync("usp_Sum_KL_GangLong_By_MeThoi_CCT", meThoi);
        //         // var cr = await ExecuteGangLongStoredProcAsync("usp_Sum_KL_GangLong_By_MeThoi_CR", meThoi);
        //         var cr = 0d;
        //         cache[meThoi] = (cct, cr);
        //     }

        //     foreach (var row in rows)
        //     {
        //         if (string.IsNullOrWhiteSpace(row.MeThoi)) continue;
        //         if (row.KLGangLongCCT != null) continue;
        //         if (!cache.TryGetValue(row.MeThoi, out var m)) continue;
        //         row.KLGangLongCCT = m.CCT;
        //         row.KLGangLongCR = m.CR;
        //     }

        //     await _context.SaveChangesAsync();
        // }

        // private async Task<double?> ExecuteGangLongStoredProcAsync(string procedureName, string meThoi)
        // {
        //     if (string.IsNullOrWhiteSpace(meThoi))
        //         return null;

        //     await using var connection = new SqlConnection(_gangLongConnStr);
        //     await connection.OpenAsync();

        //     try
        //     {
        //         using var command = new SqlCommand(procedureName, connection);
        //         command.CommandType = CommandType.StoredProcedure;
        //         command.CommandTimeout = 30;

        //         command.Parameters.Add(new SqlParameter("@MeThoi", SqlDbType.NVarChar, 20)
        //         {
        //             Value = meThoi
        //         });

        //         var result = await command.ExecuteScalarAsync();

        //         if (result == null || result == DBNull.Value)
        //             return null;

        //         return Convert.ToDouble(result);
        //     }
        //     catch (Exception ex)
        //     {
        //         throw new InvalidOperationException(
        //             $"Error executing {procedureName} for MeThoi '{meThoi}': {ex.Message}", ex);
        //     }
        // }
        
        /// <summary>
        /// Public để DLNMHRC2Service.FilterGroupedAsync gọi với cooldown riêng (ShouldSyncGangLong),
        /// tách khỏi cooldown của EnsureThepPheGangAsync — 2 cột khác nhau, không nên gộp chung 1 throttle.
        /// </summary>
        public async Task EnsureGangLongMetricsAsync(SyncFromNM_HRC2_Request request)
        {
            if (!string.Equals(request.LoaiBM, "BOF", StringComparison.OrdinalIgnoreCase))
                return;

            var rows = await _context.DLNM_HRC2s
                .Where(x =>
                    x.IsNM == true &&
                    x.IsDelete != true &&
                    x.Ngay == request.NgaySX &&
                    x.Ca == request.Ca &&
                    x.Scope == request.Scope &&
                    x.BieuMau == request.LoaiBM &&
                    x.MeThoi != null)
                .ToListAsync();

            if (rows.Count == 0) return;

            var allMeThoi = rows
                .Select(x => x.MeThoi!)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Gọi 1 lần, lấy toàn bộ — luôn overwrite để phản ánh thay đổi từ gang
            var cache = await ExecuteGangLongStoredProcBatchAsync("usp_Sum_KL_GangLong_By_MeThoi_CCT", allMeThoi);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.MeThoi)) continue;
                if (!cache.TryGetValue(row.MeThoi, out var kl)) continue;

                row.KLGangLongCCT = kl;
                row.KLGangLongCR = 0;
            }

            await _context.SaveChangesAsync();
        }

        private async Task<Dictionary<string, double?>> ExecuteGangLongStoredProcBatchAsync(
            string procedureName,
            IEnumerable<string> listMeThoi,
            string resultColumnName = "TotalKL")
        {
            var result = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

            // Build TVP
            var tvp = new DataTable();
            tvp.Columns.Add("MaMeThoi", typeof(string));
            foreach (var mt in listMeThoi)
                tvp.Rows.Add(mt);

            await using var connection = new SqlConnection(_gangLongConnStr);
            await connection.OpenAsync();

            try
            {
                using var command = new SqlCommand(procedureName, connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 30;

                var param = command.Parameters.AddWithValue("@ListMeThoi", tvp);
                param.SqlDbType = SqlDbType.Structured;
                param.TypeName = "dbo.TVP_MeThoi";

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var meThoi = reader["MaMeThoi"]?.ToString();
                    var kl = reader[resultColumnName] == DBNull.Value ? (double?)null : Convert.ToDouble(reader[resultColumnName]);

                    if (!string.IsNullOrWhiteSpace(meThoi))
                        result[meThoi] = kl;
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error executing {procedureName}: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// Public để DLNMHRC2Service.FilterGroupedAsync gọi với cooldown riêng (ShouldSyncThepPhe),
        /// tách khỏi cooldown của EnsureGangLongMetricsAsync — xem comment ở đó.
        /// </summary>
        public async Task EnsureThepPheGangAsync(SyncFromNM_HRC2_Request request)
        {
            if (!string.Equals(request.LoaiBM, "BOF", StringComparison.OrdinalIgnoreCase))
                return;

            var rows = await _context.DLNM_HRC2s
                .Where(x =>
                    x.IsNM == true &&
                    x.IsDelete != true &&
                    x.Ngay == request.NgaySX &&
                    x.Ca == request.Ca &&
                    x.Scope == request.Scope &&
                    x.BieuMau == request.LoaiBM &&
                    x.MeThoi != null)
                .ToListAsync();

            if (rows.Count == 0) return;

            var allMeThoi = rows
                .Select(x => x.MeThoi!)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cache = await ExecuteGangLongStoredProcBatchAsync(
                "usp_Get_KLThepPhe_By_MeThoi", allMeThoi, "KLThepPhe");

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.MeThoi)) continue;
                if (!cache.TryGetValue(row.MeThoi, out var kl)) continue;
                row.KLThepPheGang = kl;
            }

            await _context.SaveChangesAsync();
        }

        private static bool AreEqual(string? value1, string? value2)
        {
            return string.Equals(value1 ?? string.Empty, value2 ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Làm mới KLGangLongCCT và KLThepPheGang cho danh sách rows đã tải sẵn.
        /// Dùng 2 TVP stored proc batch → 1 SaveChanges, không phụ thuộc vào filter slot.
        /// </summary>
        public async Task<int> RefreshGangMetricsForRowsAsync(List<DLNM_HRC2> rows)
        {
            var validRows = rows.Where(x => !string.IsNullOrWhiteSpace(x.MeThoi)).ToList();
            if (validRows.Count == 0) return 0;

            var allMeThoi = validRows
                .Select(x => x.MeThoi!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var gangLongCache = await ExecuteGangLongStoredProcBatchAsync(
                "usp_Sum_KL_GangLong_By_MeThoi_CCT", allMeThoi);

            var thepPheCache = await ExecuteGangLongStoredProcBatchAsync(
                "usp_Get_KLThepPhe_By_MeThoi", allMeThoi, "KLThepPhe");

            foreach (var row in validRows)
            {
                if (gangLongCache.TryGetValue(row.MeThoi!, out var kl))
                {
                    row.KLGangLongCCT = kl;
                    row.KLGangLongCR = 0;
                }
                if (thepPheCache.TryGetValue(row.MeThoi!, out var thepPhe))
                    row.KLThepPheGang = thepPhe;
            }

            await _context.SaveChangesAsync();
            return validRows.Count;
        }

        public async Task SyncHRC2FromNMAsync(SyncFromNM_HRC2_Request request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // 1) Sync dữ liệu mới nhất từ NM về DB hiện tại
            // await SyncFromNmStoredProcAsync(request.NgaySX, request.Ca, request.LoaiBM, request.Scope);

            // 2) Bổ sung GangLong metrics (DB khác) khi cần
            await EnsureGangLongMetricsAsync(request);

            // 3) Đồng bộ thép phế từ gang (luôn ghi đè)
            await EnsureThepPheGangAsync(request);
        }
        public async Task DeleteRowByKeyAsync(int rowKey)
        {
            var row = await _context.DLNM_HRC2s.FindAsync(rowKey);
            if (row == null)
            {
                throw new InvalidOperationException($"Row with key {rowKey} not found");
            }
            _context.DLNM_HRC2s.Remove(row);
            await _context.SaveChangesAsync();
        }
    }
}