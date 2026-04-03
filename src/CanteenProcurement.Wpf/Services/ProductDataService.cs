using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Wpf.Services
{
    public class ProductDataService
    {
        public async Task<List<ProductRecord>> GetProductsAsync(string? categoryCode = null, string? keyword = null)
        {
            var list = new List<ProductRecord>();
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();

            var provider = DatabaseConfig.Provider;
            var likeExpr = provider.LikeConcat("@kw");

            var sql = $@"SELECT p.id, p.name, p.category_code, c.name AS category_name, p.price, p.unit,
                                p.min_interval_days, p.is_active, p.remark, p.updated_at
                        FROM products p
                        LEFT JOIN categories c ON p.category_code = c.code
                        WHERE ( @cat IS NULL OR p.category_code = @cat )
                          AND ( @kw IS NULL OR p.name LIKE {likeExpr} OR p.remark LIKE {likeExpr} )
                        ORDER BY p.id";

            await using var cmd = provider.CreateCommand(sql, conn);
            provider.AddParameter(cmd, "cat", string.IsNullOrWhiteSpace(categoryCode) ? DBNull.Value : categoryCode);
            provider.AddParameter(cmd, "kw", string.IsNullOrWhiteSpace(keyword) ? DBNull.Value : keyword);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new ProductRecord
                {
                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                    Name = reader.GetString(reader.GetOrdinal("name")),
                    CategoryCode = reader.GetString(reader.GetOrdinal("category_code")),
                    CategoryName = reader["category_name"] as string ?? reader.GetString(reader.GetOrdinal("category_code")),
                    Price = reader.GetDecimal(reader.GetOrdinal("price")),
                    Unit = reader.GetString(reader.GetOrdinal("unit")),
                    MinIntervalDays = reader.GetInt32(reader.GetOrdinal("min_interval_days")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    Remark = reader["remark"] as string ?? string.Empty,
                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
                });
            }

            return list;
        }

        public async Task<int> CreateProductAsync(ProductRecord record)
        {
            ValidateProductRecord(record);

            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                var provider = DatabaseConfig.Provider;
                var sql = $@"INSERT INTO products(name, category_code, price, unit, min_interval_days, is_active, remark)
                            VALUES(@name, @category_code, @price, @unit, @min_interval_days, @is_active, @remark);
                            {provider.GetLastInsertIdSql()}";
                
                await using var cmd = provider.CreateCommand(sql, conn, tx);
                provider.AddParameter(cmd, "name", record.Name);
                provider.AddParameter(cmd, "category_code", record.CategoryCode);
                provider.AddParameter(cmd, "price", record.Price);
                provider.AddParameter(cmd, "unit", record.Unit);
                provider.AddParameter(cmd, "min_interval_days", record.MinIntervalDays);
                provider.AddParameter(cmd, "is_active", record.IsActive);
                provider.AddParameter(cmd, "remark", record.Remark ?? string.Empty);

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

        public async Task<int> UpdateProductAsync(ProductRecord record)
        {
            ValidateProductRecord(record);

            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                var provider = DatabaseConfig.Provider;
                var sql = $@"UPDATE products
                            SET name=@name, category_code=@category_code, price=@price, unit=@unit,
                                min_interval_days=@min_interval_days, is_active=@is_active, remark=@remark, 
                                updated_at={provider.GetCurrentTimestampSql()}
                            WHERE id=@id";
                
                await using var cmd = provider.CreateCommand(sql, conn, tx);
                provider.AddParameter(cmd, "name", record.Name);
                provider.AddParameter(cmd, "category_code", record.CategoryCode);
                provider.AddParameter(cmd, "price", record.Price);
                provider.AddParameter(cmd, "unit", record.Unit);
                provider.AddParameter(cmd, "min_interval_days", record.MinIntervalDays);
                provider.AddParameter(cmd, "is_active", record.IsActive);
                provider.AddParameter(cmd, "remark", record.Remark ?? string.Empty);
                provider.AddParameter(cmd, "id", record.Id);

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

        public async Task<int> UpdateProductStatusAsync(int id, bool status)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            var provider = DatabaseConfig.Provider;
            var sql = $"UPDATE products SET is_active=@status, updated_at={provider.GetCurrentTimestampSql()} WHERE id=@id";
            await using var cmd = provider.CreateCommand(sql, conn);
            provider.AddParameter(cmd, "status", status);
            provider.AddParameter(cmd, "id", id);
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string? Message)> CanDeleteProductAsync(int id)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            
            // 检查是否有采购明细引用
            const string detailSql = "SELECT COUNT(*) FROM procurement_details WHERE product_id=@id";
            await using (var cmd = DatabaseConfig.Provider.CreateCommand(detailSql, conn))
            {
                DatabaseConfig.Provider.AddParameter(cmd, "id", id);
                var detailCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (detailCount > 0)
                {
                    return (false, $"该商品有 {detailCount} 条采购明细记录，无法删除。");
                }
            }
            
            // 检查是否有使用历史引用
            const string historySql = "SELECT COUNT(*) FROM product_usage_history WHERE product_id=@id";
            await using (var cmd = DatabaseConfig.Provider.CreateCommand(historySql, conn))
            {
                DatabaseConfig.Provider.AddParameter(cmd, "id", id);
                var historyCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (historyCount > 0)
                {
                    return (false, $"该商品有 {historyCount} 条使用历史记录，无法删除。");
                }
            }
            
            return (true, null!);
        }

        public async Task<int> DeleteProductAsync(int id)
        {
            await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                const string sql = "DELETE FROM products WHERE id=@id";
                await using var cmd = DatabaseConfig.Provider.CreateCommand(sql, conn, tx);
                DatabaseConfig.Provider.AddParameter(cmd, "id", id);
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

        private static void ValidateProductRecord(ProductRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (string.IsNullOrWhiteSpace(record.Name))
            {
                throw new InvalidOperationException("商品名称不能为空。");
            }

            if (string.IsNullOrWhiteSpace(record.CategoryCode))
            {
                throw new InvalidOperationException("商品所属分类不能为空。");
            }

            if (string.IsNullOrWhiteSpace(record.Unit))
            {
                throw new InvalidOperationException("商品单位不能为空。");
            }

            if (record.MinIntervalDays <= 0)
            {
                throw new InvalidOperationException("最小间隔天数必须大于 0。");
            }

            EnsureValidPrice(record.Price);
        }

        private static void EnsureValidPrice(decimal price)
        {
            if (price <= 0)
            {
                throw new InvalidOperationException("商品单价必须大于 0。");
            }
        }

    }


    public class ProductRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int MinIntervalDays { get; set; }
        public bool IsActive { get; set; }
        public string Remark { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
