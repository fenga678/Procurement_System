using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly IDatabaseProvider _provider;
    private readonly Func<Task<System.Data.Common.DbConnection>> _openConnection;

    public ProductRepository(IDatabaseProvider provider, Func<Task<System.Data.Common.DbConnection>> openConnection)
    {
        _provider = provider;
        _openConnection = openConnection;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(string? categoryCode = null, string? keyword = null, CancellationToken cancellationToken = default)
    {
        var products = new List<Product>();
        await using var connection = await _openConnection();
        var sql = $"""
                   SELECT p.id, p.name, p.category_code, p.price, p.unit, p.min_interval_days, p.is_active, p.remark, p.updated_at
                   FROM products p
                   WHERE (@categoryCode IS NULL OR p.category_code = @categoryCode)
                     AND (@keyword IS NULL OR p.name LIKE {_provider.LikeConcat("@keyword")} OR p.remark LIKE {_provider.LikeConcat("@keyword")})
                   ORDER BY p.id
                   """;
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "categoryCode", string.IsNullOrWhiteSpace(categoryCode) ? DBNull.Value : categoryCode);
        _provider.AddParameter(command, "keyword", string.IsNullOrWhiteSpace(keyword) ? DBNull.Value : keyword);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                Name = reader.GetString(reader.GetOrdinal("name")),
                CategoryCode = reader.GetString(reader.GetOrdinal("category_code")),
                Price = reader.GetDecimal(reader.GetOrdinal("price")),
                Unit = reader.GetString(reader.GetOrdinal("unit")),
                MinIntervalDays = reader.GetInt32(reader.GetOrdinal("min_interval_days")),
                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                Remark = reader.IsDBNull(reader.GetOrdinal("remark")) ? string.Empty : reader.GetString(reader.GetOrdinal("remark")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
            });
        }

        return products;
    }

    public async Task<IReadOnlyList<Product>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return (await GetAllAsync(cancellationToken: cancellationToken)).Where(product => product.IsActive && product.Price > 0).ToList();
    }

    public async Task<int> CreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = $"""
                       INSERT INTO products(name, category_code, price, unit, min_interval_days, is_active, remark)
                       VALUES(@name, @categoryCode, @price, @unit, @minIntervalDays, @isActive, @remark);
                       {_provider.GetLastInsertIdSql()}
                       """;
            await using var command = _provider.CreateCommand(sql, connection, transaction);
            AddParameters(command, product);
            var id = await command.ExecuteScalarAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Convert.ToInt32(id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = $"""
                       UPDATE products
                       SET name=@name, category_code=@categoryCode, price=@price, unit=@unit,
                           min_interval_days=@minIntervalDays, is_active=@isActive, remark=@remark,
                           updated_at={_provider.GetCurrentTimestampSql()}
                       WHERE id=@id
                       """;
            await using var command = _provider.CreateCommand(sql, connection, transaction);
            AddParameters(command, product);
            _provider.AddParameter(command, "id", product.Id);
            var rows = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> UpdateStatusAsync(int id, bool status, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        var sql = $"UPDATE products SET is_active=@status, updated_at={_provider.GetCurrentTimestampSql()} WHERE id=@id";
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "status", status);
        _provider.AddParameter(command, "id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(bool CanDelete, string? Message)> CanDeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        var checks = new[]
        {
            ("SELECT COUNT(*) FROM procurement_details WHERE product_id=@id", "该商品已被采购明细引用，不能删除。"),
            ("SELECT COUNT(*) FROM product_usage_history WHERE product_id=@id", "该商品已有使用历史，不能删除。")
        };

        foreach (var (sql, message) in checks)
        {
            await using var command = _provider.CreateCommand(sql, connection);
            _provider.AddParameter(command, "id", id);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (count > 0)
            {
                return (false, message);
            }
        }

        return (true, null);
    }

    public async Task<int> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = _provider.CreateCommand("DELETE FROM products WHERE id=@id", connection, transaction);
            _provider.AddParameter(command, "id", id);
            var rows = await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void AddParameters(System.Data.Common.DbCommand command, Product product)
    {
        _provider.AddParameter(command, "name", product.Name);
        _provider.AddParameter(command, "categoryCode", product.CategoryCode);
        _provider.AddParameter(command, "price", product.Price);
        _provider.AddParameter(command, "unit", product.Unit);
        _provider.AddParameter(command, "minIntervalDays", product.MinIntervalDays);
        _provider.AddParameter(command, "isActive", product.IsActive);
        _provider.AddParameter(command, "remark", product.Remark ?? string.Empty);
    }
}
