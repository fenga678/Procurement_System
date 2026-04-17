using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Infrastructure.Services;

namespace CanteenProcurement.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly IDatabaseProvider _provider;
    private readonly Func<Task<System.Data.Common.DbConnection>> _openConnection;
    private readonly SchemaCapabilitiesService _schemaCapabilities;

    public CategoryRepository(
        IDatabaseProvider provider,
        Func<Task<System.Data.Common.DbConnection>> openConnection,
        SchemaCapabilitiesService schemaCapabilities)
    {
        _provider = provider;
        _openConnection = openConnection;
        _schemaCapabilities = schemaCapabilities;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = new List<Category>();
        await using var connection = await _openConnection();
        var hasDailyColumns = await _schemaCapabilities.HasDailyItemColumnsAsync(connection, cancellationToken);
        var sql = hasDailyColumns
            ? """
              SELECT id, name, code, ratio, frequency_days, sort, status, updated_at, daily_min_items, daily_max_items
              FROM categories
              ORDER BY sort, id
              """
            : """
              SELECT id, name, code, ratio, frequency_days, sort, status, updated_at, 1 AS daily_min_items, 1 AS daily_max_items
              FROM categories
              ORDER BY sort, id
              """;
        await using var command = _provider.CreateCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            categories.Add(new Category
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

        return categories;
    }

    public async Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return (await GetAllAsync(cancellationToken)).Where(category => category.Status).ToList();
    }

    public async Task<int> CreateAsync(Category category, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var hasDailyColumns = await _schemaCapabilities.HasDailyItemColumnsAsync(connection, cancellationToken);
            var sql = hasDailyColumns
                ? $"""
                   INSERT INTO categories(name, code, ratio, frequency_days, sort, status, daily_min_items, daily_max_items)
                   VALUES(@name, @code, @ratio, @frequencyDays, @sort, @status, @dailyMinItems, @dailyMaxItems);
                   {_provider.GetLastInsertIdSql()}
                   """
                : $"""
                   INSERT INTO categories(name, code, ratio, frequency_days, sort, status)
                   VALUES(@name, @code, @ratio, @frequencyDays, @sort, @status);
                   {_provider.GetLastInsertIdSql()}
                   """;
            await using var command = _provider.CreateCommand(sql, connection, transaction);
            AddParameters(command, category);
            if (hasDailyColumns)
            {
                _provider.AddParameter(command, "dailyMinItems", category.DailyMinItems);
                _provider.AddParameter(command, "dailyMaxItems", category.DailyMaxItems);
            }

            var createdId = await command.ExecuteScalarAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Convert.ToInt32(createdId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var hasDailyColumns = await _schemaCapabilities.HasDailyItemColumnsAsync(connection, cancellationToken);
            var sql = hasDailyColumns
                ? $"""
                   UPDATE categories
                   SET name=@name, ratio=@ratio, frequency_days=@frequencyDays, sort=@sort, status=@status,
                       daily_min_items=@dailyMinItems, daily_max_items=@dailyMaxItems, updated_at={_provider.GetCurrentTimestampSql()}
                   WHERE code=@code
                   """
                : $"""
                   UPDATE categories
                   SET name=@name, ratio=@ratio, frequency_days=@frequencyDays, sort=@sort, status=@status,
                       updated_at={_provider.GetCurrentTimestampSql()}
                   WHERE code=@code
                   """;
            await using var command = _provider.CreateCommand(sql, connection, transaction);
            AddParameters(command, category);
            if (hasDailyColumns)
            {
                _provider.AddParameter(command, "dailyMinItems", category.DailyMinItems);
                _provider.AddParameter(command, "dailyMaxItems", category.DailyMaxItems);
            }

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

    public async Task<int> UpdateStatusAsync(string code, bool status, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        var sql = $"UPDATE categories SET status=@status, updated_at={_provider.GetCurrentTimestampSql()} WHERE code=@code";
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "status", status);
        _provider.AddParameter(command, "code", code);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<(bool CanDelete, string? Message)> CanDeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        var checks = new[]
        {
            ("SELECT COUNT(*) FROM products WHERE category_code=@code", "该分类下仍有关联商品，不能删除。"),
            ("SELECT COUNT(*) FROM procurement_details WHERE category_code=@code", "该分类已经被采购明细引用，不能删除。"),
            ("SELECT COUNT(*) FROM task_category_budgets WHERE category_code=@code", "该分类已经被任务预算引用，不能删除。")
        };

        foreach (var (sql, message) in checks)
        {
            await using var command = _provider.CreateCommand(sql, connection);
            _provider.AddParameter(command, "code", code);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (count > 0)
            {
                return (false, message);
            }
        }

        return (true, null);
    }

    public async Task<int> DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = _provider.CreateCommand("DELETE FROM categories WHERE code=@code", connection, transaction);
            _provider.AddParameter(command, "code", code);
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

    private void AddParameters(System.Data.Common.DbCommand command, Category category)
    {
        _provider.AddParameter(command, "name", category.Name);
        _provider.AddParameter(command, "code", category.Code);
        _provider.AddParameter(command, "ratio", category.Ratio);
        _provider.AddParameter(command, "frequencyDays", category.FrequencyDays);
        _provider.AddParameter(command, "sort", category.Sort);
        _provider.AddParameter(command, "status", category.Status);
    }
}
