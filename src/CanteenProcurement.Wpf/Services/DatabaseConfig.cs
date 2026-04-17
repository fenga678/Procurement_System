using System.Data.Common;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Wpf.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 数据库配置入口（仅支持 SQLite）
/// </summary>
public static class DatabaseConfig
{
    public static IDatabaseProvider Provider => AppHost.Services.GetRequiredService<IDatabaseProvider>();

    public static Task<IDatabaseProvider> GetProviderAsync()
    {
        return Task.FromResult(Provider);
    }

    public static Task<DbConnection> CreateAndOpenConnectionAsync()
    {
        return Provider.CreateAndOpenConnectionAsync();
    }

    public static string GetCurrentTimestampSql() => Provider.GetCurrentTimestampSql();
    public static string GetCurrentDateSql() => Provider.GetCurrentDateSql();
    public static string GetLastInsertIdSql() => Provider.GetLastInsertIdSql();
    public static string Concat(params string[] parts) => Provider.Concat(parts);
    public static string LikeConcat(string parameter) => Provider.LikeConcat(parameter);
    public static string QuoteIdentifier(string identifier) => Provider.QuoteIdentifier(identifier);

    public static void UpdateSqlitePath(string sqlitePath)
    {
        var factory = AppHost.Services.GetRequiredService<DatabaseProviderFactory>();
        factory.UpdateSqlitePath(sqlitePath);
    }

    public static event EventHandler? OnDatabaseChanged;

    public static void SaveConfiguration()
    {
        var factory = AppHost.Services.GetRequiredService<DatabaseProviderFactory>();
        factory.SaveConfiguration();
        OnDatabaseChanged?.Invoke(null, EventArgs.Empty);
    }

    public static SqliteConfiguration GetConfiguration()
    {
        var factory = AppHost.Services.GetRequiredService<DatabaseProviderFactory>();
        return factory.Configuration;
    }

    public static void ResetProvider()
    {
        _ = AppHost.Services.GetRequiredService<IDatabaseProvider>();
    }
}
