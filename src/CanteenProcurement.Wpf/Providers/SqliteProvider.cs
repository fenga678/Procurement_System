using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;
using Microsoft.Data.Sqlite;

namespace CanteenProcurement.Wpf.Providers
{
    /// <summary>
    /// SQLite 数据库提供者实现
    /// </summary>
    public class SqliteProvider : IDatabaseProvider
    {
        private readonly string _databasePath;
        private readonly string _connectionString;

        public string Name => "Sqlite";

        public SqliteProvider(string databasePath)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            
            // 确保数据库目录存在
            var directory = Path.GetDirectoryName(_databasePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"Data Source={_databasePath}";
        }

        public async Task<DbConnection> CreateAndOpenConnectionAsync()
        {
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await InitializeConnectionAsync(conn);
            
            // 检查是否需要初始化数据库结构
            await EnsureDatabaseSchemaAsync(conn);
            
            return conn;
        }

        public string GetLastInsertIdSql() => "SELECT last_insert_rowid();";

        public string GetCurrentTimestampSql() => "datetime('now', 'localtime')";

        public string GetCurrentDateSql() => "date('now', 'localtime')";

        public string Concat(params string[] parts)
        {
            return string.Join(" || ", parts);
        }

        public string LikeConcat(string parameter)
        {
            return "'%' || " + parameter + " || '%'";
        }

        public async Task<bool> HasColumnAsync(DbConnection conn, string tableName, string columnName)
        {
            const string sql = "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column";
            await using var cmd = new SqliteCommand(sql, (SqliteConnection)conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            cmd.Parameters.AddWithValue("@column", columnName);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        public async Task<int> CountColumnsAsync(DbConnection conn, string tableName, params string[] columnNames)
        {
            var placeholderList = new List<string>();
            for (int i = 0; i < columnNames.Length; i++)
            {
                placeholderList.Add($"@p{i}");
            }
            var placeholders = string.Join(",", placeholderList);
            var sql = $"SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name IN ({placeholders})";
            await using var cmd = new SqliteCommand(sql, (SqliteConnection)conn);
            cmd.Parameters.AddWithValue("@table", tableName);
            for (var i = 0; i < columnNames.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", columnNames[i]);
            }
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task InitializeConnectionAsync(DbConnection conn)
        {
            // 启用外键约束
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            await cmd.ExecuteNonQueryAsync();
        }

        public string GetDisplayConnectionString()
        {
            return $"Database: {_databasePath}";
        }

        public DbCommand CreateCommand(string sql, DbConnection conn, DbTransaction? tx = null)
        {
            return new SqliteCommand(sql, (SqliteConnection)conn, tx as SqliteTransaction);
        }

        public void AddParameter(DbCommand cmd, string name, object? value)
        {
            if (cmd is SqliteCommand sqliteCmd)
            {
                sqliteCmd.Parameters.AddWithValue($"@{name}", value ?? DBNull.Value);
            }
        }

        public string QuoteIdentifier(string identifier)
        {
            // SQLite 支持双引号或不加引号，这里返回原值即可（对于非保留字）
            // 如果需要也可以返回 $"\"{identifier}\""
            return identifier;
        }

        /// <summary>
        /// 确保数据库结构存在
        /// </summary>
        private async Task EnsureDatabaseSchemaAsync(DbConnection conn)
        {
            // 检查是否存在 categories 表
            const string checkSql = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='categories'";
            using (var cmd = new SqliteCommand(checkSql, (SqliteConnection)conn))
            {
                var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (count > 0) return; // 数据库已初始化
            }

            // 创建基础表结构
            var createTablesSql = GetCreateTablesSql();
            using (var cmd = new SqliteCommand(createTablesSql, (SqliteConnection)conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 创建触发器
            var createTriggersSql = GetCreateTriggersSql();
            using (var cmd = new SqliteCommand(createTriggersSql, (SqliteConnection)conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // 创建索引
            var createIndexesSql = GetCreateIndexesSql();
            using (var cmd = new SqliteCommand(createIndexesSql, (SqliteConnection)conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private string GetCreateTablesSql()
        {
            return @"
-- 分类表
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

-- 商品表
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

-- 采购任务表
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

-- 任务分类预算表
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

-- 采购明细表
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

-- 商品使用历史表
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

-- 系统配置表
CREATE TABLE IF NOT EXISTS system_configs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    config_key TEXT NOT NULL UNIQUE,
    config_value TEXT NOT NULL,
    description TEXT,
    is_system INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- 操作日志表
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
        }

        private string GetCreateTriggersSql()
        {
            return @"
-- categories 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_categories_updated_at
AFTER UPDATE ON categories
BEGIN
    UPDATE categories SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- products 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_products_updated_at
AFTER UPDATE ON products
BEGIN
    UPDATE products SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- procurement_tasks 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_procurement_tasks_updated_at
AFTER UPDATE ON procurement_tasks
BEGIN
    UPDATE procurement_tasks SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;

-- system_configs 表自动更新时间触发器
CREATE TRIGGER IF NOT EXISTS trigger_system_configs_updated_at
AFTER UPDATE ON system_configs
BEGIN
    UPDATE system_configs SET updated_at = datetime('now', 'localtime') WHERE id = NEW.id;
END;
";
        }

        private string GetCreateIndexesSql()
        {
            return @"
-- 分类表索引
CREATE INDEX IF NOT EXISTS idx_categories_code ON categories(code);
CREATE INDEX IF NOT EXISTS idx_categories_status ON categories(status);
CREATE INDEX IF NOT EXISTS idx_categories_sort ON categories(sort);

-- 商品表索引
CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_code);
CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);

-- 采购任务表索引
CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_year_month ON procurement_tasks(year_month);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON procurement_tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON procurement_tasks(created_at);

-- 分类预算表索引
CREATE INDEX IF NOT EXISTS idx_budgets_task ON task_category_budgets(task_id);
CREATE INDEX IF NOT EXISTS idx_budgets_category ON task_category_budgets(category_code);
CREATE INDEX IF NOT EXISTS idx_task_category_budgets_fixed ON task_category_budgets(task_id, is_fixed_amount);

-- 采购明细表索引
CREATE INDEX IF NOT EXISTS idx_details_task_date ON procurement_details(task_id, purchase_date);
CREATE INDEX IF NOT EXISTS idx_details_category ON procurement_details(category_code);
CREATE INDEX IF NOT EXISTS idx_details_product ON procurement_details(product_id);
CREATE INDEX IF NOT EXISTS idx_details_date ON procurement_details(purchase_date);

-- 商品使用历史表索引
CREATE UNIQUE INDEX IF NOT EXISTS idx_history_task_product ON product_usage_history(task_id, product_id);
CREATE INDEX IF NOT EXISTS idx_history_last_used ON product_usage_history(last_used_date);

-- 系统配置表索引
CREATE INDEX IF NOT EXISTS idx_configs_key ON system_configs(config_key);

-- 操作日志表索引
CREATE INDEX IF NOT EXISTS idx_logs_operation ON operation_logs(operation_type);
CREATE INDEX IF NOT EXISTS idx_logs_user ON operation_logs(user_id);
CREATE INDEX IF NOT EXISTS idx_logs_created_at ON operation_logs(created_at);
";
        }
    }
}
