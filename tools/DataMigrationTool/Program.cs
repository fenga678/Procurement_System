using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Microsoft.Data.Sqlite;

namespace DataMigrationTool
{
    /// <summary>
    /// MySQL 到 SQLite 数据迁移工具
    /// 用法: DataMigrationTool <mysql_connection_string> [sqlite_db_path]
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("  MySQL → SQLite 数据迁移工具");
            Console.WriteLine("  食堂采购管理系统");
            Console.WriteLine("========================================");
            Console.WriteLine();

            string mysqlConnStr;
            string sqlitePath;

            if (args.Length >= 1)
            {
                mysqlConnStr = args[0];
                sqlitePath = args.Length >= 2 ? args[1] : "canteen_migration.db";
            }
            else
            {
                // 交互式输入
                Console.WriteLine("请输入 MySQL 连接信息：");
                Console.Write("服务器 [localhost]: ");
                var server = Console.ReadLine()?.Trim() ?? "localhost";
                
                Console.Write("端口 [3306]: ");
                var portStr = Console.ReadLine()?.Trim();
                var port = string.IsNullOrEmpty(portStr) ? 3306 : int.Parse(portStr);
                
                Console.Write("数据库 [canteen_procurement]: ");
                var database = Console.ReadLine()?.Trim() ?? "canteen_procurement";
                
                Console.Write("用户名 [root]: ");
                var user = Console.ReadLine()?.Trim() ?? "root";
                
                Console.Write("密码: ");
                var password = ReadPassword();
                
                mysqlConnStr = $"server={server};port={port};database={database};user={user};password={password};" +
                               "CharSet=utf8mb4;AllowUserVariables=True;TreatTinyAsBoolean=true;ConvertZeroDateTime=True;";
                
                Console.Write("\nSQLite 数据库路径 [data/canteen.db]: ");
                sqlitePath = Console.ReadLine()?.Trim() ?? "data/canteen.db";
            }

            Console.WriteLine();
            Console.WriteLine($"源数据库: MySQL");
            Console.WriteLine($"目标数据库: SQLite ({sqlitePath})");
            Console.WriteLine();

            Console.Write("确认开始迁移? [Y/n]: ");
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm == "n" || confirm == "no")
            {
                Console.WriteLine("已取消迁移。");
                return;
            }

            try
            {
                var migrator = new DataMigrator(mysqlConnStr, sqlitePath);
                await migrator.MigrateAsync();
                Console.WriteLine();
                Console.WriteLine("✅ 数据迁移完成！");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"❌ 迁移失败: {ex.Message}");
                Console.WriteLine($"详细信息: {ex.StackTrace}");
            }
        }

        static string ReadPassword()
        {
            var password = new System.Text.StringBuilder();
            ConsoleKeyInfo key;
            while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (key.KeyChar != '\0')
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return password.ToString();
        }
    }

    /// <summary>
    /// 数据迁移器
    /// </summary>
    public class DataMigrator
    {
        private readonly string _mysqlConnStr;
        private readonly string _sqlitePath;

        // 表迁移顺序（考虑外键依赖）
        private readonly string[] _tableOrder = new[]
        {
            "categories",
            "products",
            "procurement_tasks",
            "task_category_budgets",
            "procurement_details",
            "product_usage_history",
            "system_configs",
            "operation_logs"
        };

        public DataMigrator(string mysqlConnStr, string sqlitePath)
        {
            _mysqlConnStr = mysqlConnStr;
            _sqlitePath = sqlitePath;
        }

        public async Task MigrateAsync()
        {
            // 确保 SQLite 目录存在
            var directory = Path.GetDirectoryName(_sqlitePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var mysqlConn = new MySqlConnection(_mysqlConnStr);
            await mysqlConn.OpenAsync();
            Console.WriteLine("✓ 已连接 MySQL");

            // 创建 SQLite 连接
            var sqliteConnStr = $"Data Source={_sqlitePath}";
            await using var sqliteConn = new SqliteConnection(sqliteConnStr);
            await sqliteConn.OpenAsync();
            
            // 启用外键约束
            await using (var cmd = sqliteConn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA foreign_keys = ON;";
                await cmd.ExecuteNonQueryAsync();
            }
            Console.WriteLine("✓ 已创建 SQLite 数据库");

            // 创建表结构
            await CreateTablesAsync(sqliteConn);
            Console.WriteLine("✓ 已创建表结构");

            // 迁移数据
            foreach (var table in _tableOrder)
            {
                var count = await MigrateTableAsync(mysqlConn, sqliteConn, table);
                Console.WriteLine($"✓ 迁移 {table}: {count} 条记录");
            }

            // 创建索引
            await CreateIndexesAsync(sqliteConn);
            Console.WriteLine("✓ 已创建索引");

            // 创建触发器
            await CreateTriggersAsync(sqliteConn);
            Console.WriteLine("✓ 已创建触发器");

            // 验证数据
            await VerifyDataAsync(mysqlConn, sqliteConn);
            Console.WriteLine("✓ 数据验证通过");
        }

        private async Task CreateTablesAsync(SqliteConnection conn)
        {
            var sql = @"
CREATE TABLE IF NOT EXISTS categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    code TEXT NOT NULL UNIQUE,
    ratio REAL NOT NULL,
    frequency_days INTEGER NOT NULL DEFAULT 1,
    daily_min_items INTEGER NOT NULL DEFAULT 1,
    daily_max_items INTEGER NOT NULL DEFAULT 1,
    sort INTEGER NOT NULL DEFAULT 0,
    status INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    category_code TEXT NOT NULL,
    price REAL NOT NULL,
    unit TEXT NOT NULL,
    min_interval_days INTEGER NOT NULL DEFAULT 2,
    is_active INTEGER NOT NULL DEFAULT 1,
    remark TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (category_code) REFERENCES categories(code),
    CHECK (price > 0)
);

CREATE TABLE IF NOT EXISTS procurement_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    year_month TEXT NOT NULL,
    total_budget REAL NOT NULL,
    float_rate REAL NOT NULL DEFAULT 0.100,
    status INTEGER NOT NULL DEFAULT 0,
    generated_at TEXT,
    created_by TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    CHECK (total_budget > 0),
    CHECK (float_rate >= 0 AND float_rate <= 1)
);

CREATE TABLE IF NOT EXISTS task_category_budgets (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    category_code TEXT NOT NULL,
    ratio REAL NOT NULL,
    budget REAL NOT NULL,
    is_fixed_amount INTEGER NOT NULL DEFAULT 0,
    fixed_amount REAL,
    expected_count INTEGER NOT NULL DEFAULT 1,
    actual_count INTEGER DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (category_code) REFERENCES categories(code)
);

CREATE TABLE IF NOT EXISTS procurement_details (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    category_code TEXT NOT NULL,
    product_id INTEGER NOT NULL,
    purchase_date TEXT NOT NULL,
    price REAL NOT NULL,
    quantity REAL NOT NULL,
    amount REAL NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (category_code) REFERENCES categories(code),
    FOREIGN KEY (product_id) REFERENCES products(id),
    CHECK (price > 0),
    CHECK (quantity > 0),
    CHECK (amount > 0)
);

CREATE TABLE IF NOT EXISTS product_usage_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    task_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    last_used_date TEXT NOT NULL,
    usage_count INTEGER NOT NULL DEFAULT 1,
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    FOREIGN KEY (task_id) REFERENCES procurement_tasks(id) ON DELETE CASCADE,
    FOREIGN KEY (product_id) REFERENCES products(id)
);

CREATE TABLE IF NOT EXISTS system_configs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    config_key TEXT NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    description TEXT,
    is_system INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

CREATE TABLE IF NOT EXISTS operation_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    operation_type TEXT NOT NULL,
    operation_desc TEXT NOT NULL,
    user_id TEXT,
    task_id INTEGER,
    ip_address TEXT,
    user_agent TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);
";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task<int> MigrateTableAsync(MySqlConnection mysqlConn, SqliteConnection sqliteConn, string tableName)
        {
            // 获取 MySQL 数据
            var mysqlCmd = new MySqlCommand($"SELECT * FROM `{tableName}`", mysqlConn);
            await using var reader = await mysqlCmd.ExecuteReaderAsync();

            if (!reader.HasRows)
                return 0;

            var columns = new List<string>();
            var parameters = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
                parameters.Add($"@{reader.GetName(i)}");
            }

            var insertSql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters)})";
            var columnTypes = new Dictionary<string, Type>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnTypes[reader.GetName(i)] = reader.GetDataTypeName(i).ToLower() switch
                {
                    "datetime" or "timestamp" => typeof(DateTime),
                    "date" => typeof(DateTime),
                    "tinyint" => typeof(bool),
                    "int" or "integer" => typeof(int),
                    "bigint" => typeof(long),
                    "decimal" or "double" or "float" => typeof(decimal),
                    _ => typeof(string)
                };
            }

            int count = 0;
            await using var tx = await sqliteConn.BeginTransactionAsync();

            while (await reader.ReadAsync())
            {
                await using var sqliteCmd = sqliteConn.CreateCommand();
                sqliteCmd.CommandText = insertSql;
                sqliteCmd.Transaction = tx as SqliteTransaction;

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var value = reader.GetValue(i);
                    
                    if (value == null || value == DBNull.Value)
                    {
                        sqliteCmd.Parameters.AddWithValue($"@{colName}", DBNull.Value);
                    }
                    else if (columnTypes[colName] == typeof(DateTime))
                    {
                        // 转换为 SQLite ISO8601 格式
                        var dt = reader.GetDateTime(i);
                        sqliteCmd.Parameters.AddWithValue($"@{colName}", dt.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                    else if (columnTypes[colName] == typeof(bool))
                    {
                        // MySQL TINYINT(1) → SQLite INTEGER (0/1)
                        sqliteCmd.Parameters.AddWithValue($"@{colName}", reader.GetBoolean(i) ? 1 : 0);
                    }
                    else
                    {
                        sqliteCmd.Parameters.AddWithValue($"@{colName}", value);
                    }
                }

                await sqliteCmd.ExecuteNonQueryAsync();
                count++;
            }

            await tx.CommitAsync();
            return count;
        }

        private async Task CreateIndexesAsync(SqliteConnection conn)
        {
            var sql = @"
CREATE INDEX IF NOT EXISTS idx_categories_code ON categories(code);
CREATE INDEX IF NOT EXISTS idx_categories_status ON categories(status);
CREATE INDEX IF NOT EXISTS idx_categories_sort ON categories(sort);
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_code);
CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);
CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_year_month ON procurement_tasks(year_month);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON procurement_tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON procurement_tasks(created_at);
CREATE INDEX IF NOT EXISTS idx_budgets_task ON task_category_budgets(task_id);
CREATE INDEX IF NOT EXISTS idx_budgets_category ON task_category_budgets(category_code);
CREATE INDEX IF NOT EXISTS idx_task_category_budgets_fixed ON task_category_budgets(task_id, is_fixed_amount);
CREATE INDEX IF NOT EXISTS idx_details_task_date ON procurement_details(task_id, purchase_date);
CREATE INDEX IF NOT EXISTS idx_details_category ON procurement_details(category_code);
CREATE INDEX IF NOT EXISTS idx_details_product ON procurement_details(product_id);
CREATE INDEX IF NOT EXISTS idx_details_date ON procurement_details(purchase_date);
CREATE UNIQUE INDEX IF NOT EXISTS idx_history_task_product ON product_usage_history(task_id, product_id);
CREATE INDEX IF NOT EXISTS idx_history_last_used ON product_usage_history(last_used_date);
CREATE INDEX IF NOT EXISTS idx_configs_key ON system_configs(config_key);
CREATE INDEX IF NOT EXISTS idx_logs_operation ON operation_logs(operation_type);
CREATE INDEX IF NOT EXISTS idx_logs_user ON operation_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_logs_created_at ON operation_logs(created_at);
";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task CreateTriggersAsync(SqliteConnection conn)
        {
            var sql = @"
CREATE TRIGGER IF NOT EXISTS trigger_categories_updated_at
AFTER UPDATE ON categories
BEGIN
    UPDATE categories SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS trigger_products_updated_at
AFTER UPDATE ON products
BEGIN
    UPDATE products SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS trigger_procurement_tasks_updated_at
AFTER UPDATE ON procurement_tasks
BEGIN
    UPDATE procurement_tasks SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS trigger_system_configs_updated_at
AFTER UPDATE ON system_configs
BEGIN
    UPDATE system_configs SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;
";
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task VerifyDataAsync(MySqlConnection mysqlConn, SqliteConnection sqliteConn)
        {
            Console.WriteLine("\n数据验证:");
            
            foreach (var table in _tableOrder)
            {
                // MySQL 记录数
                var mysqlCmd = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", mysqlConn);
                var mysqlCount = Convert.ToInt32(await mysqlCmd.ExecuteScalarAsync());

                // SQLite 记录数
                await using var sqliteCmd = sqliteConn.CreateCommand();
                sqliteCmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                var sqliteCount = Convert.ToInt32(await sqliteCmd.ExecuteScalarAsync());

                if (mysqlCount != sqliteCount)
                {
                    throw new Exception($"表 {table} 数据不一致: MySQL={mysqlCount}, SQLite={sqliteCount}");
                }
                
                Console.WriteLine($"  {table}: {sqliteCount} 条记录 ✓");
            }
        }
    }
}
