using System.Data.Common;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Infrastructure.Services;

public sealed class SchemaCapabilitiesService
{
    private readonly IDatabaseProvider _provider;
    private bool? _hasDailyItemColumns;
    private bool? _hasFixedAmountColumns;
    private readonly object _lock = new();

    public SchemaCapabilitiesService(IDatabaseProvider provider)
    {
        _provider = provider;
    }

    public async Task<bool> HasDailyItemColumnsAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (_hasDailyItemColumns.HasValue)
        {
            return _hasDailyItemColumns.Value;
        }

        lock (_lock)
        {
            if (_hasDailyItemColumns.HasValue)
            {
                return _hasDailyItemColumns.Value;
            }
        }

        var count = await _provider.CountColumnsAsync(connection, "categories", "daily_min_items", "daily_max_items");
        _hasDailyItemColumns = count >= 2;
        return _hasDailyItemColumns.Value;
    }

    public async Task<bool> HasFixedAmountColumnsAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        if (_hasFixedAmountColumns.HasValue)
        {
            return _hasFixedAmountColumns.Value;
        }

        lock (_lock)
        {
            if (_hasFixedAmountColumns.HasValue)
            {
                return _hasFixedAmountColumns.Value;
            }
        }

        var count = await _provider.CountColumnsAsync(connection, "task_category_budgets", "is_fixed_amount", "fixed_amount");
        _hasFixedAmountColumns = count >= 2;
        return _hasFixedAmountColumns.Value;
    }

    public void Reset()
    {
        _hasDailyItemColumns = null;
        _hasFixedAmountColumns = null;
    }
}
