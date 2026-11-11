using dataproduct.api.DTOs;
using dataproduct.api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace dataproduct.api.Services
{
    public class HRC1_NMSyncService
    {
        private readonly ProductFormContext _context;
        private readonly string _gangLongConnStr;

        public HRC1_NMSyncService(ProductFormContext context, IConfiguration config)
        {
            _context = context;

            // GangLong metrics nằm ở DB khác (giống HRC2_NMSyncService) — ưu tiên connection string
            // riêng nếu có, fallback về MasterDbConnection để tương thích config cũ.
            _gangLongConnStr =
                config.GetConnectionString("GangLongDbConnection")
                ?? config.GetConnectionString("MasterDbConnection")
                ?? throw new InvalidOperationException("GangLongDbConnection/MasterDbConnection connection string is not configured");
        }

        /// <summary>
        /// Gọi SP_HRC1_BOF_Sync_Full để đồng bộ mẻ thổi + phụ liệu từ NM cho đúng 1 tổ hợp Ngày+Ca+Lò,
        /// sau đó bổ sung KLGangLongCCT/KLThepPheGang từ DB GangLong (chỉ áp dụng BOF).
        /// Khác HRC2 (sync toàn bộ rồi lọc), SP này đã nhận tham số nên chỉ sync đúng phạm vi cần.
        /// Gồm 2 bước con (SyncHRC1MeThoiOnlyAsync + RefreshGangMetricsForSlotAsync) — tách riêng để
        /// DLNMHRC1Service.FilterGroupedAsync có thể gọi 2 bước này với 2 cooldown độc lập.
        /// </summary>
        public async Task SyncHRC1FromNMAsync(SyncFromNM_HRC1_Request request)
        {
            await SyncHRC1MeThoiOnlyAsync(request);
            await RefreshGangMetricsForSlotAsync(request);
        }

        /// <summary>
        /// Chỉ chạy SP_HRC1_BOF_Sync_Full (đồng bộ mẻ thổi + phụ liệu từ NM) — KHÔNG kèm
        /// KLGangLongCCT/KLThepPheGang, để FilterGroupedAsync có thể throttle riêng phần này.
        /// </summary>
        public async Task SyncHRC1MeThoiOnlyAsync(SyncFromNM_HRC1_Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC dbo.SP_HRC1_BOF_Sync_Full @NgaySanXuat={0}, @Ca={1}, @LoThoi={2}",
                DateOnly.FromDateTime(request.NgaySX), request.Ca, request.Scope);
        }

        /// <summary>
        /// Chỉ bổ sung KLGangLongCCT + KLThepPheGang cho đúng slot (không chạy lại SP_HRC1_BOF_Sync_Full)
        /// — cho phép FilterGroupedAsync làm mới 2 cột này theo cooldown riêng, độc lập với cooldown của
        /// luồng sync mẻ thổi chính.
        /// </summary>
        public async Task RefreshGangMetricsForSlotAsync(SyncFromNM_HRC1_Request request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            await EnsureGangLongMetricsAsync(request);
            await EnsureThepPheGangAsync(request);
        }

        /// <summary>
        /// Bổ sung KLGangLongCCT (usp_Sum_KL_GangLong_By_MeThoi_CCT) cho mọi mẻ trong đúng slot
        /// Ngày+Ca+Lò+BieuMau — mirror HRC2_NMSyncService.EnsureGangLongMetricsAsync. Luôn ghi đè để
        /// phản ánh thay đổi từ phía gang. Chỉ áp dụng BOF (LF chưa có nguồn NM nào để bổ sung metric này).
        /// KHÔNG lọc IsNM: SP chỉ tra cứu theo MeThoi (không phụ thuộc dòng đến từ NM hay thêm tay) —
        /// lọc IsNM==true trước đây khiến mẻ thêm tay (IsNM=false) không bao giờ có KLGangLongCCT dù
        /// MeThoi của nó vẫn tồn tại và tra được ở DB GangLong.
        /// </summary>
        private async Task EnsureGangLongMetricsAsync(SyncFromNM_HRC1_Request request)
        {
            if (!string.Equals(request.BieuMau, "BOF", StringComparison.OrdinalIgnoreCase))
                return;

            var ngay = DateOnly.FromDateTime(request.NgaySX);

            var rows = await _context.Hrc1TieuHaos
                .Where(x =>
                    !x.IsDeleted &&
                    x.NgaySanXuat == ngay &&
                    x.Ca == request.Ca &&
                    x.Scope == request.Scope &&
                    x.BieuMau == request.BieuMau &&
                    x.MeThoi != null)
                .ToListAsync();

            if (rows.Count == 0) return;

            var allMeThoi = rows
                .Select(x => x.MeThoi!)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cache = await ExecuteGangLongStoredProcBatchAsync("usp_Sum_KL_GangLong_By_MeThoi_CCT", allMeThoi);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.MeThoi)) continue;
                if (!cache.TryGetValue(row.MeThoi, out var kl)) continue;

                row.KLGangLongCCT = (decimal?)kl;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Bổ sung KLThepPheGang (usp_Get_KLThepPhe_By_MeThoi) cho mọi mẻ trong đúng slot — mirror
        /// HRC2_NMSyncService.EnsureThepPheGangAsync. Luôn ghi đè. Chỉ áp dụng BOF.
        /// KHÔNG lọc IsNM — cùng lý do với EnsureGangLongMetricsAsync ở trên.
        /// </summary>
        private async Task EnsureThepPheGangAsync(SyncFromNM_HRC1_Request request)
        {
            if (!string.Equals(request.BieuMau, "BOF", StringComparison.OrdinalIgnoreCase))
                return;

            var ngay = DateOnly.FromDateTime(request.NgaySX);

            var rows = await _context.Hrc1TieuHaos
                .Where(x =>
                    !x.IsDeleted &&
                    x.NgaySanXuat == ngay &&
                    x.Ca == request.Ca &&
                    x.Scope == request.Scope &&
                    x.BieuMau == request.BieuMau &&
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

                row.KLThepPheGang = (decimal?)kl;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Làm mới KLGangLongCCT và KLThepPheGang cho danh sách rows đã tải sẵn — mirror
        /// HRC2_NMSyncService.RefreshGangMetricsForRowsAsync. Dùng 2 TVP stored proc batch → 1
        /// SaveChanges, không phụ thuộc filter slot (vd cho 1 tính năng "Làm mới lại chỉ số" sau này).
        /// </summary>
        public async Task<int> RefreshGangMetricsForRowsAsync(List<Hrc1TieuHao> rows)
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
                    row.KLGangLongCCT = (decimal?)kl;
                if (thepPheCache.TryGetValue(row.MeThoi!, out var thepPhe))
                    row.KLThepPheGang = (decimal?)thepPhe;
            }

            await _context.SaveChangesAsync();
            return validRows.Count;
        }

        /// <summary>
        /// Làm mới KLThepLong (LF) cho danh sách rows đã tải sẵn từ HRC1_MeThep.KlThepLong — mirror
        /// RefreshGangMetricsForRowsAsync ở trên nhưng nguồn dữ liệu là HRC1_MeThep (không phải DB
        /// GangLong) vì LF chưa có nguồn NM để bổ sung gang metrics. Luôn ghi đè (kể cả IsEdited) để
        /// phản ánh thay đổi mới nhất từ phía giao nhận thép lỏng, giống hành vi "luôn ghi đè" của
        /// RefreshGangMetricsForRowsAsync.
        /// </summary>
        public async Task<int> RefreshThepLongForRowsAsync(List<Hrc1TieuHao> rows)
        {
            var validRows = rows.Where(x => !string.IsNullOrWhiteSpace(x.MeThoi)).ToList();
            if (validRows.Count == 0) return 0;

            var allMeThoi = validRows
                .Select(x => x.MeThoi!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var meThepByMaMe = await _context.HRC1_MeTheps
                .Where(m => m.MaMe != null && allMeThoi.Contains(m.MaMe))
                .ToDictionaryAsync(m => m.MaMe!, StringComparer.OrdinalIgnoreCase);

            int updated = 0;
            foreach (var row in validRows)
            {
                if (!meThepByMaMe.TryGetValue(row.MeThoi!, out var me)) continue;
                row.KLThepLong = me.KlThepLong;
                updated++;
            }

            await _context.SaveChangesAsync();
            return updated;
        }

        /// <summary>
        /// Bổ sung KLThepLong cho mọi mẻ LF trong đúng slot Ngày/Ca/Lò — tải rows rồi gọi
        /// RefreshThepLongForRowsAsync. Dùng cho DLNMHRC1Service.FilterGroupedAsync (nút "Làm mới dữ
        /// liệu" bên trang Tiêu hao LF) với cooldown riêng, độc lập cooldown Mác thép
        /// (ShouldSyncLfMacThep) của cùng request — 2 nguồn dữ liệu khác nhau (Linked Server vs
        /// HRC1_MeThep) nên không nên gộp chung 1 cooldown.
        /// </summary>
        public async Task EnsureThepLongMetricsAsync(DateOnly ngay, int ca, int scope)
        {
            var rows = await _context.Hrc1TieuHaos
                .Where(x =>
                    !x.IsDeleted &&
                    x.NgaySanXuat == ngay &&
                    x.Ca == (byte)ca &&
                    x.Scope == scope &&
                    x.BieuMau == "LF" &&
                    x.MeThoi != null)
                // Loại dòng thuộc phiếu đã khóa (clone "Đề nghị hiệu chỉnh" đang mở), mirror
                // EnsureGangLongMetricsAsync/RefreshGangMetricsByPhieuIdsAsync.
                .Where(x => x.IDPhieu == null ||
                    _context.BmPhieus.Any(p => p.Idphieu == x.IDPhieu && p.IsLock != 1 && p.IsDelete != 1))
                .ToListAsync();

            if (rows.Count == 0) return;

            await RefreshThepLongForRowsAsync(rows);
        }

        /// <summary>
        /// Gọi 1 stored proc batch trên DB GangLong (TVP dbo.TVP_MeThoi @ListMeThoi) — copy nguyên văn
        /// từ HRC2_NMSyncService.ExecuteGangLongStoredProcBatchAsync (logic hoàn toàn generic theo
        /// MeThoi, không phụ thuộc HRC1/HRC2).
        /// </summary>
        private async Task<Dictionary<string, double?>> ExecuteGangLongStoredProcBatchAsync(
            string procedureName,
            IEnumerable<string> listMeThoi,
            string resultColumnName = "TotalKL")
        {
            var result = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

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
    }
}
