using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Wpf.Providers
{
    /// <summary>
    /// 数据库配置
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// 数据库提供者类型 (Sqlite / MySql)
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "Sqlite";

        /// <summary>
        /// SQLite 配置
        /// </summary>
        [JsonPropertyName("sqlite")]
        public SqliteConfiguration? Sqlite { get; set; }

        /// <summary>
        /// MySQL 配置
        /// </summary>
        [JsonPropertyName("mysql")]
        public MySqlConfiguration? MySql { get; set; }
    }

    /// <summary>
    /// SQLite 配置
    /// </summary>
    public class SqliteConfiguration
    {
        /// <summary>
        /// 数据库文件路径（相对于应用程序目录）
        /// </summary>
        [JsonPropertyName("databasePath")]
        public string DatabasePath { get; set; } = "data/canteen.db";
    }

    /// <summary>
    /// MySQL 配置
    /// </summary>
    public class MySqlConfiguration
    {
        [JsonPropertyName("server")]
        public string Server { get; set; } = "localhost";

        [JsonPropertyName("port")]
        public int Port { get; set; } = 3306;

        [JsonPropertyName("database")]
        public string Database { get; set; } = "canteen_procurement";

        [JsonPropertyName("user")]
        public string User { get; set; } = "root";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }

    /// <summary>
    /// 数据库提供者工厂
    /// 负责根据配置创建对应的数据库提供者
    /// </summary>
    public class DatabaseProviderFactory
    {
        private static DatabaseProviderFactory? _instance;
        private static readonly object _lock = new();

        private DatabaseConfiguration _configuration;
        private IDatabaseProvider? _provider;

        /// <summary>
        /// 当前提供者实例
        /// </summary>
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

        /// <summary>
        /// 当前配置
        /// </summary>
        public DatabaseConfiguration Configuration => _configuration;

        private DatabaseProviderFactory(DatabaseConfiguration configuration)
        {
            _configuration = configuration ?? new DatabaseConfiguration();
        }

        /// <summary>
        /// 初始化工厂（从配置文件）
        /// </summary>
        public static DatabaseProviderFactory Initialize(string? configPath = null)
        {
            lock (_lock)
            {
                if (_instance != null) return _instance;

                var basePath = AppContext.BaseDirectory;
                var configFilePath = configPath ?? Path.Combine(basePath, "appsettings.json");

                DatabaseConfiguration config;
                if (File.Exists(configFilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(configFilePath);
                        var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        config = new DatabaseConfiguration();

                        // 读取 Database 节点
                        if (root.TryGetProperty("Database", out var dbNode))
                        {
                            if (dbNode.TryGetProperty("provider", out var providerNode))
                            {
                                config.Provider = providerNode.GetString() ?? "Sqlite";
                            }

                            if (dbNode.TryGetProperty("sqlite", out var sqliteNode))
                            {
                                config.Sqlite = JsonSerializer.Deserialize<SqliteConfiguration>(sqliteNode.GetRawText());
                            }

                            if (dbNode.TryGetProperty("mysql", out var mysqlNode))
                            {
                                config.MySql = JsonSerializer.Deserialize<MySqlConfiguration>(mysqlNode.GetRawText());
                            }
                        }
                    }
                    catch
                    {
                        config = new DatabaseConfiguration();
                    }
                }
                else
                {
                    config = new DatabaseConfiguration();
                }

                _instance = new DatabaseProviderFactory(config);
                return _instance;
            }
        }

        /// <summary>
        /// 获取单例实例（如未初始化则自动初始化）
        /// </summary>
        public static DatabaseProviderFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 自动初始化
                    Initialize();
                }
                return _instance!;
            }
        }

        /// <summary>
        /// 创建数据库提供者
        /// </summary>
        private IDatabaseProvider CreateProvider(DatabaseConfiguration config)
        {
            return config.Provider?.ToLowerInvariant() switch
            {
                "mysql" => CreateMySqlProvider(config),
                "sqlite" or _ => CreateSqliteProvider(config)
            };
        }

        private IDatabaseProvider CreateSqliteProvider(DatabaseConfiguration config)
        {
            var dbPath = config.Sqlite?.DatabasePath ?? "data/canteen.db";

            // 处理相对路径
            if (!Path.IsPathRooted(dbPath))
            {
                dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
            }

            return new SqliteProvider(dbPath);
        }

        private IDatabaseProvider CreateMySqlProvider(DatabaseConfiguration config)
        {
            var mysqlConfig = config.MySql ?? new MySqlConfiguration();

            // 支持环境变量覆盖
            var envConn = Environment.GetEnvironmentVariable("CANTEEN_MYSQL_CONN");
            if (!string.IsNullOrWhiteSpace(envConn))
            {
                return new MySqlProvider(envConn);
            }

            // 从配置构建连接字符串
            var connStr = $"server={mysqlConfig.Server};port={mysqlConfig.Port};" +
                          $"database={mysqlConfig.Database};user={mysqlConfig.User};" +
                          $"password={mysqlConfig.Password};" +
                          "AllowUserVariables=True;TreatTinyAsBoolean=true;" +
                          "ConvertZeroDateTime=True;CharSet=utf8mb4;";

            return new MySqlProvider(connStr);
        }

        /// <summary>
        /// 切换数据库提供者
        /// </summary>
        public void SwitchProvider(string providerType, MySqlConfiguration? mysqlConfig = null, string? sqlitePath = null)
        {
            _configuration.Provider = providerType;

            if (mysqlConfig != null)
            {
                _configuration.MySql = mysqlConfig;
            }

            if (!string.IsNullOrEmpty(sqlitePath))
            {
                _configuration.Sqlite = new SqliteConfiguration { DatabasePath = sqlitePath };
            }

            // 重新创建提供者
            _provider = CreateProvider(_configuration);
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void SaveConfiguration(string? configPath = null)
        {
            var basePath = AppContext.BaseDirectory;
            var configFilePath = configPath ?? Path.Combine(basePath, "appsettings.json");

            // 读取现有配置
            JsonDocument? existingDoc = null;
            if (File.Exists(configFilePath))
            {
                try
                {
                    existingDoc = JsonDocument.Parse(File.ReadAllText(configFilePath));
                }
                catch { /* 忽略解析错误 */ }
            }

            // 构建新配置
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();

                // 写入 Database 节点
                writer.WritePropertyName("Database");
                writer.WriteStartObject();
                writer.WriteString("provider", _configuration.Provider);

                // SQLite 配置
                writer.WritePropertyName("sqlite");
                writer.WriteStartObject();
                writer.WriteString("databasePath", _configuration.Sqlite?.DatabasePath ?? "data/canteen.db");
                writer.WriteEndObject();

                // MySQL 配置
                writer.WritePropertyName("mysql");
                writer.WriteStartObject();
                var mysql = _configuration.MySql ?? new MySqlConfiguration();
                writer.WriteString("server", mysql.Server);
                writer.WriteNumber("port", mysql.Port);
                writer.WriteString("database", mysql.Database);
                writer.WriteString("user", mysql.User);
                writer.WriteString("password", mysql.Password);
                writer.WriteEndObject();

                writer.WriteEndObject();

                // 保留其他现有配置
                if (existingDoc != null)
                {
                    foreach (var prop in existingDoc.RootElement.EnumerateObject())
                    {
                        if (prop.NameEquals("Database")) continue;
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            File.WriteAllBytes(configFilePath, stream.ToArray());
        }

        /// <summary>
        /// 重置单例（用于测试或重新初始化）
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }
    }
}
