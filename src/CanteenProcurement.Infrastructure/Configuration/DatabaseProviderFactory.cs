using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Infrastructure.Providers;

namespace CanteenProcurement.Infrastructure.Configuration;

public sealed class DatabaseProviderFactory
{
    private readonly string _configPath;
    private DatabaseConfiguration _configuration;
    private IDatabaseProvider? _provider;

    public DatabaseProviderFactory(string? configPath = null)
    {
        _configPath = configPath ?? System.IO.Path.Combine(System.AppContext.BaseDirectory, "appsettings.json");
        _configuration = LoadConfiguration(_configPath);
    }

    public DatabaseConfiguration Configuration => _configuration;

    public IDatabaseProvider Provider => _provider ??= CreateSqliteProvider(_configuration.Sqlite);

    public void SwitchProvider(string? sqlitePath = null)
    {
        if (!string.IsNullOrWhiteSpace(sqlitePath))
        {
            _configuration.Sqlite = new SqliteConfiguration { DatabasePath = sqlitePath };
        }

        _provider = CreateSqliteProvider(_configuration.Sqlite);
    }

    public void SaveConfiguration()
    {
        var root = new Dictionary<string, object?>
        {
            ["Database"] = new { provider = "Sqlite", sqlite = _configuration.Sqlite }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(root, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.File.WriteAllText(_configPath, json);
    }

    private static DatabaseConfiguration LoadConfiguration(string configPath)
    {
        if (!System.IO.File.Exists(configPath))
        {
            return new DatabaseConfiguration();
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(configPath));
            if (!document.RootElement.TryGetProperty("Database", out var databaseNode))
            {
                return new DatabaseConfiguration();
            }

            return System.Text.Json.JsonSerializer.Deserialize<DatabaseConfiguration>(databaseNode.GetRawText()) ?? new DatabaseConfiguration();
        }
        catch
        {
            return new DatabaseConfiguration();
        }
    }

    private static IDatabaseProvider CreateSqliteProvider(SqliteConfiguration configuration)
    {
        var databasePath = configuration.DatabasePath;
        if (!System.IO.Path.IsPathRooted(databasePath))
        {
            databasePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, databasePath);
        }

        return new SqliteProvider(databasePath);
    }
}
