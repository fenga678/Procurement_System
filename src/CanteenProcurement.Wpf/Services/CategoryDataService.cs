using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Wpf.Services
{
    public class CategoryDataService
    {
        private readonly SchemaCapabilitiesProvider _schemaProvider;

        public CategoryDataService(SchemaCapabilitiesProvider schemaProvider)
        {
            _schemaProvider = schemaProvider;
        }

        private bool? _hasDailyItemColumns;

        public async Task<List<CategoryRecord>> GetCategoriesAsync()
        {
            var list = new List<CategoryRecord>();
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();

            _hasDailyItemColumns ??= await _schemaProvider.HasDailyItemColumnsAsync(conn);

            var sql = _hasDailyItemColumns == true
                ? @"SELECT id, name, code, ratio, frequency_days, sort, status, updated_at, daily_min_items, daily_max_items
                        FROM categories
                        ORDER BY sort, id"
                : @"SELECT id, name, code, ratio, frequency_days, sort, status, updated_at,
                            1 AS daily_min_items, 1 AS daily_max_items
                        FROM categories
                        ORDER BY sort, id";
            
            await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new CategoryRecord
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    Code = reader.GetString(reader.GetOrdinal("code")),
                    Ratio = reader.GetDecimal(reader.GetOrdinal("ratio")),
                    FrequencyDays = reader.GetInt32(reader.GetOrdinal("frequency_days")),
                    Sort = reader.GetInt32(reader.GetOrdinal("sort")),
                    Status = reader.GetBoolean(reader.GetOrdinal("status")),
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at")),
                    DailyMinItems = reader.GetInt32(reader.GetOrdinal("daily_min_items")),
                    DailyMaxItems = reader.GetInt32(reader.GetOrdinal("daily_max_items"))
                });
            }

            return list;
        }


        public async Task<int> CreateCategoryAsync(CategoryRecord record)
        {
            ValidateCategoryRecord(record);
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            _hasDailyItemColumns ??= await _schemaProvider.HasDailyItemColumnsAsync(conn);
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                var provider = DatabaseConfig.Provider;
                var sql = _hasDailyItemColumns == true
                    ? $@"INSERT INTO categories(name, code, ratio, frequency_days, sort, status, daily_min_items, daily_max_items)
                        VALUES(@name, @code, @ratio, @freq, @sort, @status, @minItems, @maxItems);
                        {provider.GetLastInsertIdSql()}"
                    : $@"INSERT INTO categories(name, code, ratio, frequency_days, sort, status)
                        VALUES(@name, @code, @ratio, @freq, @sort, @status);
                        {provider.GetLastInsertIdSql()}";
                
                await using var cmd = provider.CreateCommand(sql, conn, tx);
                provider.AddParameter(cmd, "name", record.Name);
                provider.AddParameter(cmd, "code", record.Code);
                provider.AddParameter(cmd, "ratio", record.Ratio);
                provider.AddParameter(cmd, "freq", record.FrequencyDays);
                provider.AddParameter(cmd, "sort", record.Sort);
                provider.AddParameter(cmd, "status", record.Status);
                if (_hasDailyItemColumns == true)
                {
                    provider.AddParameter(cmd, "minItems", record.DailyMinItems);
                    provider.AddParameter(cmd, "maxItems", record.DailyMaxItems);
                }

                var idObj = await cmd.ExecuteScalarAsync();
                await tx.CommitAsync();
                return Convert.ToInt32(idObj);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<int> UpdateCategoryAsync(CategoryRecord record)
        {
            ValidateCategoryRecord(record);
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            _hasDailyItemColumns ??= await _schemaProvider.HasDailyItemColumnsAsync(conn);
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                var provider = DatabaseConfig.Provider;
                var sql = _hasDailyItemColumns == true
                    ? $@"UPDATE categories
                        SET name=@name, ratio=@ratio, frequency_days=@freq, sort=@sort, status=@status,
                            daily_min_items=@minItems, daily_max_items=@maxItems, 
                            updated_at={provider.GetCurrentTimestampSql()}
                        WHERE code=@code"
                    : $@"UPDATE categories
                        SET name=@name, ratio=@ratio, frequency_days=@freq, sort=@sort, status=@status, 
                            updated_at={provider.GetCurrentTimestampSql()}
                        WHERE code=@code";
                
                await using var cmd = provider.CreateCommand(sql, conn, tx);
                provider.AddParameter(cmd, "name", record.Name);
                provider.AddParameter(cmd, "ratio", record.Ratio);
                provider.AddParameter(cmd, "freq", record.FrequencyDays);
                provider.AddParameter(cmd, "sort", record.Sort);
                provider.AddParameter(cmd, "status", record.Status);
                provider.AddParameter(cmd, "code", record.Code);
                if (_hasDailyItemColumns == true)
                {
                    provider.AddParameter(cmd, "minItems", record.DailyMinItems);
                    provider.AddParameter(cmd, "maxItems", record.DailyMaxItems);
                }

                var rows = await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return rows;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private static void ValidateCategoryRecord(CategoryRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                throw new InvalidOperationException("分类名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(record.Code))
            {
                throw new InvalidOperationException("分类编码不能为空。");
            }

            if (record.Ratio < 0 || record.Ratio > 1)
            {
                throw new InvalidOperationException("分类占比必须在 0 到 1 之间。");
            }

            if (record.FrequencyDays <= 0)
            {
                throw new InvalidOperationException("出现频率必须大于 0 天。");
            }

            if (record.DailyMinItems <= 0 || record.DailyMaxItems <= 0)
            {
                throw new InvalidOperationException("每日品类上下限必须大于 0。");
            }

            if (record.DailyMinItems > record.DailyMaxItems)
            {
                throw new InvalidOperationException("每日最小品类数不能大于最大品类数。");
            }
        }


        public async Task<int> UpdateCategoryStatusAsync(string code, bool status)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            var provider = DatabaseConfig.Provider;
            var sql = $"UPDATE categories SET status=@status, updated_at={provider.GetCurrentTimestampSql()} WHERE code=@code";
            await using var cmd = provider.CreateCommand(sql, conn);
            provider.AddParameter(cmd, "status", status);
            provider.AddParameter(cmd, "code", code);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string? Message)> CanDeleteCategoryAsync(string code)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            
            // 检查是否有关联商品
            const string productSql = "SELECT COUNT(*) FROM products WHERE category_code=@code";
            await using (var cmd = DatabaseConfig.Provider.CreateCommand(productSql, conn))
            {
                DatabaseConfig.Provider.AddParameter(cmd, "code", code);
                var productCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (productCount > 0)
                {
                    return (false, $"该分类下有 {productCount} 个商品，请先删除或移动这些商品。");
                }
            }
            
            // 检查是否有采购明细引用
            const string detailSql = "SELECT COUNT(*) FROM procurement_details WHERE category_code=@code";
            await using (var cmd = DatabaseConfig.Provider.CreateCommand(detailSql, conn))
            {
                DatabaseConfig.Provider.AddParameter(cmd, "code", code);
                var detailCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (detailCount > 0)
                {
                    return (false, $"该分类有 {detailCount} 条采购明细记录，无法删除。");
                }
            }
            
            // 检查是否有分类预算引用
            const string budgetSql = "SELECT COUNT(*) FROM task_category_budgets WHERE category_code=@code";
            await using (var cmd = DatabaseConfig.Provider.CreateCommand(budgetSql, conn))
            {
                DatabaseConfig.Provider.AddParameter(cmd, "code", code);
                var budgetCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (budgetCount > 0)
                {
                    return (false, $"该分类有 {budgetCount} 条预算记录，无法删除。");
                }
            }
            
            return (true, null!);
        }

        public async Task<int> DeleteCategoryAsync(string code)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                const string sql = "DELETE FROM categories WHERE code=@code";
                await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn, tx);
                DatabaseConfig.Provider.AddParameter(cmd, "code", code);
                var rows = await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return rows;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

    }

    public class CategoryRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal Ratio { get; set; }
        public int FrequencyDays { get; set; }
        public int DailyMinItems { get; set; } = 1;
        public int DailyMaxItems { get; set; } = 1;
        public int Sort { get; set; }
        public bool Status { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
