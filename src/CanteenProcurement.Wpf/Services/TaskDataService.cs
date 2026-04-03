using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Wpf.Services
{
    public class TaskDataService
    {
        private readonly SchemaCapabilitiesProvider _schemaProvider;

        public TaskDataService(SchemaCapabilitiesProvider schemaProvider)
        {
            _schemaProvider = schemaProvider;
        }

        public async Task<List<TaskRecord>> GetTasksAsync(CancellationToken ct = default)
        {
            var list = new List<TaskRecord>();
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();

            var provider = DatabaseConfig.Provider;
            var sql = $@"SELECT id, {provider.QuoteIdentifier("year_month")}, total_budget, float_rate, status, generated_at, created_by, created_at, updated_at
                                   FROM procurement_tasks
                                   ORDER BY created_at DESC";
            
            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync())
            {
                list.Add(new TaskRecord
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    YearMonth = reader.GetString(reader.GetOrdinal("year_month")),
                    TotalBudget = reader.GetDecimal(reader.GetOrdinal("total_budget")),
                    FloatRate = reader.GetDecimal(reader.GetOrdinal("float_rate")),
                    Status = reader.GetInt32(reader.GetOrdinal("status")),
                    GeneratedAt = reader.IsDBNull(reader.GetOrdinal("generated_at"))
                        ? (DateTime?)null
                        : reader.GetDateTime(reader.GetOrdinal("generated_at")),
                    CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("created_by")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                });
            }

            return list;
        }

        public async Task<int> CreateTaskAsync(TaskRecord record, CancellationToken ct = default)
        {
            ValidateTaskRecord(record);
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                var provider = DatabaseConfig.Provider;
                var sql = $@"INSERT INTO procurement_tasks({provider.QuoteIdentifier("year_month")}, total_budget, float_rate, status, generated_at, created_by)
                              VALUES(@ym, @budget, @float, @status, @generatedAt, @createdBy);
                              {provider.GetLastInsertIdSql()}";
                
                await using var cmd = provider.CreateCommand(sql, conn, tx);
                provider.AddParameter(cmd, "ym", record.YearMonth);
                provider.AddParameter(cmd, "budget", record.TotalBudget);
                provider.AddParameter(cmd, "float", record.FloatRate);
                provider.AddParameter(cmd, "status", record.Status);
                provider.AddParameter(cmd, "generatedAt", record.GeneratedAt.HasValue ? record.GeneratedAt : DBNull.Value);
                provider.AddParameter(cmd, "createdBy", string.IsNullOrWhiteSpace(record.CreatedBy) ? DBNull.Value : record.CreatedBy);

                var idObj = await cmd.ExecuteScalarAsync(ct);
                await tx.CommitAsync(ct);
                return Convert.ToInt32(idObj);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<int> UpdateStatusAsync(int taskId, int status)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            var provider = DatabaseConfig.Provider;
            var sql = $@"UPDATE procurement_tasks
                          SET status=@status, generated_at = CASE WHEN @status = 1 THEN {provider.GetCurrentTimestampSql()} ELSE generated_at END,
                              updated_at = {provider.GetCurrentTimestampSql()}
                          WHERE id=@id";
            
            await using var cmd = provider.CreateCommand(sql, conn);
            provider.AddParameter(cmd, "status", status);
            provider.AddParameter(cmd, "id", taskId);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> DeleteTaskAsync(int taskId)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            const string sql = "DELETE FROM procurement_tasks WHERE id=@id";
            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            DatabaseConfig.Provider.AddParameter(cmd, "id", taskId);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> HasFixedAmountColumnsAsync(CancellationToken ct = default)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            return await _schemaProvider.HasFixedAmountColumnsAsync(conn, ct);
        }

        public async Task<List<CategoryBudgetRecord>> GetActiveCategoriesAsync()
        {
            var list = new List<CategoryBudgetRecord>();
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();

            var hasDaily = await _schemaProvider.HasDailyItemColumnsAsync(conn);
            var sql = hasDaily
                ? "SELECT code, ratio, frequency_days, daily_min_items, daily_max_items FROM categories WHERE status = 1"
                : "SELECT code, ratio, frequency_days, 1 AS daily_min_items, 1 AS daily_max_items FROM categories WHERE status = 1";


            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CategoryBudgetRecord
                {
                    Code = reader.GetString(reader.GetOrdinal("code")),
                    Ratio = reader.GetDecimal(reader.GetOrdinal("ratio")),
                    FrequencyDays = reader.GetInt32(reader.GetOrdinal("frequency_days")),
                    DailyMinItems = reader.GetInt32(reader.GetOrdinal("daily_min_items")),
                    DailyMaxItems = reader.GetInt32(reader.GetOrdinal("daily_max_items"))
                });

            }
            return list;
        }


        public async Task<List<ProductPriceRecord>> GetActiveProductsAsync()
        {
            var list = new List<ProductPriceRecord>();
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            const string sql = "SELECT id, name, price, category_code FROM products WHERE is_active = 1 AND price > 0";

            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ProductPriceRecord
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Price = reader.GetDecimal(reader.GetOrdinal("price")),
                    CategoryCode = reader.GetString(reader.GetOrdinal("category_code"))
                });
            }
            return list;
        }

        public async Task ReplaceTaskDetailsAsync(int taskId, List<ProcurementDetailRecord> details, CancellationToken ct = default)
        {
            var sanitized = details
                .Where(IsValidDetail)
                .ToList();

            if (sanitized.Count == 0)
            {
                throw new InvalidOperationException("生成结果没有可保存的有效明细。请检查商品单价、预算和分类配置。");
            }

            if (sanitized.Count != details.Count)
            {
                throw new InvalidOperationException("生成结果包含价格、数量或金额小于等于 0 的无效明细，已阻止写入数据库。");
            }

            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                var provider = DatabaseConfig.Provider;

                // 清理旧数据
                await using (var del = provider.CreateCommand("DELETE FROM procurement_details WHERE task_id=@id", conn, tx))
                {
                    provider.AddParameter(del, "id", taskId);
                    await del.ExecuteNonQueryAsync(ct);
                }

                // 批量插入（每批200条）
                const int batchSize = 200;
                for (var i = 0; i < sanitized.Count; i += batchSize)
                {
                    var batch = sanitized.Skip(i).Take(batchSize).ToList();

                    var valuePlaceholders = batch.Select((_, idx) =>
                        $"(@tid{idx}, @cat{idx}, @pid{idx}, @date{idx}, @price{idx}, @qty{idx}, @amt{idx})");

                    var sql = $@"INSERT INTO procurement_details(task_id, category_code, product_id, purchase_date, price, quantity, amount)
                                 VALUES {string.Join(",", valuePlaceholders)}";

                    await using var cmd = provider.CreateCommand(sql, conn, tx);

                    for (var j = 0; j < batch.Count; j++)
                    {
                        provider.AddParameter(cmd, $"tid{j}", taskId);
                        provider.AddParameter(cmd, $"cat{j}", batch[j].CategoryCode);
                        provider.AddParameter(cmd, $"pid{j}", batch[j].ProductId);
                        provider.AddParameter(cmd, $"date{j}", batch[j].PurchaseDate);
                        provider.AddParameter(cmd, $"price{j}", batch[j].Price);
                        provider.AddParameter(cmd, $"qty{j}", batch[j].Quantity);
                        provider.AddParameter(cmd, $"amt{j}", batch[j].Amount);
                    }

                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }


        public async Task<List<ProcurementDetailRecord>> GetTaskDetailsAsync(int taskId, CancellationToken ct = default)
        {
            var list = new List<ProcurementDetailRecord>();
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            const string sql = @"SELECT d.task_id,
                                        d.category_code,
                                        c.name AS category_name,
                                        d.product_id,
                                        p.name AS product_name,
                                        p.unit AS unit,
                                        d.purchase_date,
                                        d.price,
                                        d.quantity,
                                        d.amount
                                 FROM procurement_details d
                                 JOIN products p ON d.product_id = p.id
                                 JOIN categories c ON d.category_code = c.code
                                 WHERE d.task_id=@id
                                 ORDER BY d.purchase_date, d.id";

            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            DatabaseConfig.Provider.AddParameter(cmd, "id", taskId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync())
            {
                list.Add(new ProcurementDetailRecord
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("task_id")),
                    CategoryCode = reader.GetString(reader.GetOrdinal("category_code")),
                    CategoryName = reader.GetString(reader.GetOrdinal("category_name")),
                    ProductId = reader.GetInt32(reader.GetOrdinal("product_id")),
                    ProductName = reader.GetString(reader.GetOrdinal("product_name")),
                    Unit = reader.GetString(reader.GetOrdinal("unit")),
                    PurchaseDate = reader.GetDateTime(reader.GetOrdinal("purchase_date")),
                    Price = reader.GetDecimal(reader.GetOrdinal("price")),
                    Quantity = reader.GetDecimal(reader.GetOrdinal("quantity")),
                    Amount = reader.GetDecimal(reader.GetOrdinal("amount"))

                });
            }
            return list;
        }

        private static bool IsValidDetail(ProcurementDetailRecord detail)
        {
            return detail.ProductId > 0
                && !string.IsNullOrWhiteSpace(detail.CategoryCode)
                && detail.Price > 0
                && detail.Quantity > 0
                && detail.Amount > 0;
        }

        public async Task SaveTaskFixedAmountsAsync(int taskId, Dictionary<string, decimal> fixedAmounts, CancellationToken ct = default)
        {
            if (fixedAmounts == null || !fixedAmounts.Any())
                return;

            var hasColumns = await HasFixedAmountColumnsAsync(ct);
            if (!hasColumns)
            {
                throw new InvalidOperationException("数据库缺少固定金额字段（task_category_budgets.is_fixed_amount/fixed_amount），请先执行升级脚本。");
            }

            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                var provider = DatabaseConfig.Provider;

                // 清理旧固定金额配置
                await using (var del = provider.CreateCommand(
                    "DELETE FROM task_category_budgets WHERE task_id = @taskId AND is_fixed_amount = 1",
                    conn, tx))
                {
                    provider.AddParameter(del, "taskId", taskId);
                    await del.ExecuteNonQueryAsync(ct);
                }

                // 写入新固定金额配置（INSERT from categories 确保记录存在）
                const string sql = @"INSERT INTO task_category_budgets(task_id, category_code, ratio, budget, expected_count, actual_count, is_fixed_amount, fixed_amount)
                                     SELECT @taskId, c.code, c.ratio, @amount, 1, 0, 1, @amount
                                     FROM categories c
                                     WHERE c.code = @code AND c.status = 1";

                foreach (var pair in fixedAmounts.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0))
                {
                    await using var cmd = provider.CreateCommand(sql, conn, tx);
                    provider.AddParameter(cmd, "taskId", taskId);
                    provider.AddParameter(cmd, "code", pair.Key.Trim());
                    provider.AddParameter(cmd, "amount", pair.Value);
                    var rows = await cmd.ExecuteNonQueryAsync(ct);
                    if (rows == 0)
                    {
                        throw new InvalidOperationException($"分类编码 {pair.Key} 不存在或未启用，无法保存固定金额配置。");
                    }
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<Dictionary<string, decimal>> GetTaskFixedAmountsAsync(int taskId, CancellationToken ct = default)
        {
            var fixedAmounts = new Dictionary<string, decimal>();

            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();

            const string sql = @"
                SELECT category_code, fixed_amount
                FROM task_category_budgets
                WHERE task_id = @taskId AND is_fixed_amount = 1 AND fixed_amount IS NOT NULL AND fixed_amount > 0
            ";

            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            DatabaseConfig.Provider.AddParameter(cmd, "taskId", taskId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var categoryCode = reader.GetString(reader.GetOrdinal("category_code"));
                var fixedAmount = reader.GetDecimal(reader.GetOrdinal("fixed_amount"));
                fixedAmounts[categoryCode] = fixedAmount;
            }

            return fixedAmounts;
        }

        private static void ValidateTaskRecord(TaskRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.IsNullOrWhiteSpace(record.YearMonth) || record.YearMonth.Length != 6 || !record.YearMonth.All(char.IsDigit))
            {
                throw new InvalidOperationException("任务年月格式必须为 yyyyMM，例如 202604。");
            }

            if (record.TotalBudget <= 0)
            {
                throw new InvalidOperationException("任务总预算必须大于 0。");
            }

            if (record.FloatRate < 0 || record.FloatRate > 1)
            {
                throw new InvalidOperationException("预算波动率必须在 0 到 1 之间。");
            }
        }
    }

    public class TaskRecord
    {
        public int Id { get; set; }
        public string YearMonth { get; set; } = string.Empty;
        public decimal TotalBudget { get; set; }
        public decimal FloatRate { get; set; }
        public int Status { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CategoryBudgetRecord
    {
        public string Code { get; set; } = string.Empty;
        public decimal Ratio { get; set; }
        public int FrequencyDays { get; set; } = 1;
        public int DailyMinItems { get; set; } = 1;
        public int DailyMaxItems { get; set; } = 1;
    }

    public class ProductPriceRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
    }

    public class ProcurementDetailRecord
    {
        public int TaskId { get; set; }
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Amount { get; set; }
    }
}
