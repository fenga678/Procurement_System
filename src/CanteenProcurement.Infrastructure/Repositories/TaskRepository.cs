using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Infrastructure.Services;

namespace CanteenProcurement.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly IDatabaseProvider _provider;
    private readonly Func<Task<System.Data.Common.DbConnection>> _openConnection;
    private readonly SchemaCapabilitiesService _schemaCapabilities;
    private const int BatchSize = 200;

    public TaskRepository(
        IDatabaseProvider provider,
        Func<Task<System.Data.Common.DbConnection>> openConnection,
        SchemaCapabilitiesService schemaCapabilities)
    {
        _provider = provider;
        _openConnection = openConnection;
        _schemaCapabilities = schemaCapabilities;
    }

    public async Task<IReadOnlyList<ProcurementTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<ProcurementTask>();
        await using var connection = await _openConnection();
        var sql = $"""
                   SELECT id, {_provider.QuoteIdentifier("year_m")} AS year_month, total_budget, float_rate, status, generated_at, created_by, created_at, updated_at
                   FROM procurement_tasks
                   ORDER BY created_at DESC
                   """;
        await using var command = _provider.CreateCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tasks.Add(MapTask(reader));
        }

        return tasks;
    }

    public async Task<ProcurementTask?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        var sql = $"""
                   SELECT id, {_provider.QuoteIdentifier("year_m")} AS year_month, total_budget, float_rate, status, generated_at, created_by, created_at, updated_at
                   FROM procurement_tasks
                   WHERE id=@id
                   """;
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapTask(reader) : null;
    }

    public async Task<int> CreateAsync(ProcurementTask task, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var sql = $"""
                       INSERT INTO procurement_tasks({_provider.QuoteIdentifier("year_m")}, total_budget, float_rate, status, generated_at, created_by)
                       VALUES(@yearMonth, @totalBudget, @floatRate, @status, @generatedAt, @createdBy);
                       {_provider.GetLastInsertIdSql()}
                       """;
            await using var command = _provider.CreateCommand(sql, connection, transaction);
            _provider.AddParameter(command, "yearMonth", task.YearMonth);
            _provider.AddParameter(command, "totalBudget", task.TotalBudget);
            _provider.AddParameter(command, "floatRate", task.FloatRate);
            _provider.AddParameter(command, "status", (int)task.Status);
            _provider.AddParameter(command, "generatedAt", task.GeneratedAt.HasValue ? task.GeneratedAt : DBNull.Value);
            _provider.AddParameter(command, "createdBy", string.IsNullOrWhiteSpace(task.CreatedBy) ? DBNull.Value : task.CreatedBy);
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

    public async Task<int> UpdateStatusAsync(int taskId, Core.Entities.TaskStatus status, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        var sql = $"""
                   UPDATE procurement_tasks
                   SET status=@status,
                       generated_at = CASE WHEN @status = 1 THEN {_provider.GetCurrentTimestampSql()} ELSE generated_at END,
                       updated_at = {_provider.GetCurrentTimestampSql()}
                   WHERE id=@id
                   """;
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "status", (int)status);
        _provider.AddParameter(command, "id", taskId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(int taskId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        await using var command = _provider.CreateCommand("DELETE FROM procurement_tasks WHERE id=@id", connection);
        _provider.AddParameter(command, "id", taskId);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementDetail>> GetDetailsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var details = new List<ProcurementDetail>();
        await using var connection = await _openConnection();
        const string sql = """
                           SELECT d.id, d.task_id, d.category_code, d.product_id, d.purchase_date, d.price, d.quantity, d.amount, d.created_at,
                                  p.name AS product_name, p.unit, c.name AS category_name
                           FROM procurement_details d
                           JOIN products p ON d.product_id = p.id
                           JOIN categories c ON d.category_code = c.code
                           WHERE d.task_id=@id
                           ORDER BY d.purchase_date, d.id
                           """;
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "id", taskId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            details.Add(new ProcurementDetail
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                TaskId = reader.GetInt32(reader.GetOrdinal("task_id")),
                CategoryCode = reader.GetString(reader.GetOrdinal("category_code")),
                ProductId = reader.GetInt32(reader.GetOrdinal("product_id")),
                PurchaseDate = reader.GetDateTime(reader.GetOrdinal("purchase_date")),
                Price = reader.GetDecimal(reader.GetOrdinal("price")),
                Quantity = reader.GetDecimal(reader.GetOrdinal("quantity")),
                Amount = reader.GetDecimal(reader.GetOrdinal("amount")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                Product = new Product
                {
                    Id = reader.GetInt32(reader.GetOrdinal("product_id")),
                    Name = reader.GetString(reader.GetOrdinal("product_name")),
                    Unit = reader.GetString(reader.GetOrdinal("unit"))
                },
                Category = new Category
                {
                    Code = reader.GetString(reader.GetOrdinal("category_code")),
                    Name = reader.GetString(reader.GetOrdinal("category_name"))
                }
            });
        }

        return details;
    }

    public async Task ReplaceDetailsAsync(int taskId, IReadOnlyList<ProcurementDetail> details, CancellationToken cancellationToken = default)
    {
        var sanitized = details.Where(detail => detail.ProductId > 0 && detail.Price > 0 && detail.Quantity > 0 && detail.Amount > 0).ToList();
        if (sanitized.Count == 0 || sanitized.Count != details.Count)
        {
            throw new InvalidOperationException("采购明细包含无效数据，已阻止写入。");
        }

        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var deleteCommand = _provider.CreateCommand("DELETE FROM procurement_details WHERE task_id=@id", connection, transaction))
            {
                _provider.AddParameter(deleteCommand, "id", taskId);
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            for (var index = 0; index < sanitized.Count; index += BatchSize)
            {
                var batch = sanitized.Skip(index).Take(BatchSize).ToList();
                var placeholders = batch.Select((_, batchIndex) => $"(@taskId{batchIndex}, @categoryCode{batchIndex}, @productId{batchIndex}, @purchaseDate{batchIndex}, @price{batchIndex}, @quantity{batchIndex}, @amount{batchIndex})");
                var sql = $"""
                           INSERT INTO procurement_details(task_id, category_code, product_id, purchase_date, price, quantity, amount)
                           VALUES {string.Join(",", placeholders)}
                           """;
                await using var command = _provider.CreateCommand(sql, connection, transaction);
                for (var batchIndex = 0; batchIndex < batch.Count; batchIndex++)
                {
                    var detail = batch[batchIndex];
                    _provider.AddParameter(command, $"taskId{batchIndex}", taskId);
                    _provider.AddParameter(command, $"categoryCode{batchIndex}", detail.CategoryCode);
                    _provider.AddParameter(command, $"productId{batchIndex}", detail.ProductId);
                    _provider.AddParameter(command, $"purchaseDate{batchIndex}", detail.PurchaseDate);
                    _provider.AddParameter(command, $"price{batchIndex}", detail.Price);
                    _provider.AddParameter(command, $"quantity{batchIndex}", detail.Quantity);
                    _provider.AddParameter(command, $"amount{batchIndex}", detail.Amount);
                }

                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> HasFixedAmountColumnsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _openConnection();
        return await _schemaCapabilities.HasFixedAmountColumnsAsync(connection, cancellationToken);
    }

    public async Task SaveFixedAmountsAsync(int taskId, IReadOnlyDictionary<string, decimal> fixedAmounts, CancellationToken cancellationToken = default)
    {
        if (fixedAmounts.Count == 0)
        {
            return;
        }

        if (!await HasFixedAmountColumnsAsync(cancellationToken))
        {
            throw new InvalidOperationException("当前数据库版本缺少固定金额字段，请先完成数据库升级。");
        }

        await using var connection = await _openConnection();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var deleteCommand = _provider.CreateCommand(
                             "DELETE FROM task_category_budgets WHERE task_id=@taskId AND is_fixed_amount = 1",
                             connection,
                             transaction))
            {
                _provider.AddParameter(deleteCommand, "taskId", taskId);
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string sql = """
                               INSERT INTO task_category_budgets(task_id, category_code, ratio, budget, expected_count, actual_count, is_fixed_amount, fixed_amount)
                               SELECT @taskId, c.code, c.ratio, @amount, 1, 0, 1, @amount
                               FROM categories c
                               WHERE c.code = @code AND c.status = 1
                               """;
            foreach (var pair in fixedAmounts.Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0))
            {
                await using var command = _provider.CreateCommand(sql, connection, transaction);
                _provider.AddParameter(command, "taskId", taskId);
                _provider.AddParameter(command, "code", pair.Key.Trim());
                _provider.AddParameter(command, "amount", pair.Value);
                var rows = await command.ExecuteNonQueryAsync(cancellationToken);
                if (rows == 0)
                {
                    throw new InvalidOperationException($"分类编码 {pair.Key} 不存在或未启用，无法保存固定金额。");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Dictionary<string, decimal>> GetFixedAmountsAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var fixedAmounts = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        await using var connection = await _openConnection();
        const string sql = """
                           SELECT category_code, fixed_amount
                           FROM task_category_budgets
                           WHERE task_id = @taskId AND is_fixed_amount = 1 AND fixed_amount IS NOT NULL AND fixed_amount > 0
                           """;
        await using var command = _provider.CreateCommand(sql, connection);
        _provider.AddParameter(command, "taskId", taskId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            fixedAmounts[reader.GetString(reader.GetOrdinal("category_code"))] = reader.GetDecimal(reader.GetOrdinal("fixed_amount"));
        }

        return fixedAmounts;
    }

    private static ProcurementTask MapTask(System.Data.Common.DbDataReader reader)
    {
        return new ProcurementTask
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            YearMonth = reader.GetString(reader.GetOrdinal("year_month")),
            TotalBudget = reader.GetDecimal(reader.GetOrdinal("total_budget")),
            FloatRate = reader.GetDecimal(reader.GetOrdinal("float_rate")),
            Status = (Core.Entities.TaskStatus)reader.GetInt32(reader.GetOrdinal("status")),
            GeneratedAt = reader.IsDBNull(reader.GetOrdinal("generated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("generated_at")),
            CreatedBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? string.Empty : reader.GetString(reader.GetOrdinal("created_by")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
            UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
        };
    }
}
