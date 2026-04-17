using System.Data.Common;
using CanteenProcurement.Infrastructure.Services;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 数据库 Schema 能力探测服务（DI 注入）
/// </summary>
public sealed class SchemaCapabilitiesProvider
{
    private readonly SchemaCapabilitiesService _service;

    public SchemaCapabilitiesProvider(SchemaCapabilitiesService service)
    {
        _service = service;
    }

    public Task<bool> HasDailyItemColumnsAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        return _service.HasDailyItemColumnsAsync(connection, cancellationToken);
    }

    public Task<bool> HasFixedAmountColumnsAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        return _service.HasFixedAmountColumnsAsync(connection, cancellationToken);
    }

    public void ResetCache()
    {
        _service.Reset();
    }
}
