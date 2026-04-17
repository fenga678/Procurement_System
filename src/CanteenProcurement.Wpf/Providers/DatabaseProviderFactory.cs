using System;
using System.IO;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Wpf.Providers;

/// <summary>
/// SQLite 数据库配置
/// </summary>
public class SqliteConfiguration
{
    public string DatabasePath { get; set; } = "data/canteen.db";
}

/// <summary>
/// SQLite 数据库提供者工厂
/// </summary>
public class DatabaseProviderFactory
{
    private static DatabaseProviderFactory? _instance;
    private static readonly object _lock = new();

    private SqliteConfiguration _configuration;
    private IDatabaseProvider? _provider;

    public IDatabaseProvider Provider
    {
        get
        {
            if (_provider == null)
            {
                _provider = CreateProvider(_configuration);
            }
            return _provider;
        }
    }

    public SqliteConfiguration Configuration => _configuration;

    private DatabaseProviderFactory(SqliteConfiguration configuration)
    {
        _configuration = configuration ?? new SqliteConfiguration();
    }

    public static DatabaseProviderFactory Initialize(string? configPath = null)
    {
        lock (_lock)
        {
            if (_instance != null) return _instance;

            var basePath = AppContext.BaseDirectory;
            var configFilePath = configPath ?? Path.Combine(basePath, "appsettings.json");

            var config = new SqliteConfiguration();

            if (File.Exists(configFilePath))
            {
                try
                {
                    var json = File.ReadAllText(configFilePath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Database", out var dbNode) &&
                        dbNode.TryGetProperty("sqlite", out var sqliteNode))
                    {
                        var path = sqliteNode.GetProperty("databasePath").GetString();
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            config.DatabasePath = path;
                        }
                    }
                }
                catch { /* 忽略配置错误，使用默认值 */ }
            }

            _instance = new DatabaseProviderFactory(config);
            return _instance;
        }
    }

    public static DatabaseProviderFactory Instance
    {
        get
        {
            if (_instance == null) Initialize();
            return _instance!;
        }
    }

    private static IDatabaseProvider CreateProvider(SqliteConfiguration config)
    {
        var dbPath = config.DatabasePath ?? "data/canteen.db";
        if (!Path.IsPathRooted(dbPath))
        {
            dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
        }

        return new SqliteProvider(dbPath);
    }

    public void UpdateSqlitePath(string sqlitePath)
    {
        _configuration.DatabasePath = sqlitePath;
        _provider = CreateProvider(_configuration);
    }

    public void SaveConfiguration(string? configPath = null)
    {
        var basePath = AppContext.BaseDirectory;
        var configFilePath = configPath ?? Path.Combine(basePath, "appsettings.json");

        System.Text.Json.JsonDocument? existingDoc = null;
        if (File.Exists(configFilePath))
        {
            try
            {
                existingDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configFilePath));
            }
            catch { /* 忽略 */ }
        }

        var databaseJson = $@"{{
  ""provider"": ""Sqlite"",
  ""sqlite"": {{
    ""databasePath"": ""{_configuration.DatabasePath}""
  }}
}}";

        if (existingDoc != null)
        {
            var json = File.ReadAllText(configFilePath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 简单替换 Database 节点
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                @"""Database""\s*:\s*\{[^}]*\}",
                $"\"Database\": {databaseJson}",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            File.WriteAllText(configFilePath, json);
        }
        else
        {
            File.WriteAllText(configFilePath, $"{{\n  \"Database\": {databaseJson}\n}}");
        }
    }

    public static void Reset()
    {
        lock (_lock) { _instance = null; }
    }
}
