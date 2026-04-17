using Microsoft.Data.Sqlite;

namespace CanteenProcurement.Infrastructure.Migrations;

public sealed class SqliteSchemaMigrator
{
    public async Task EnsureMigratedAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        await EnsureVersionTableAsync(connection, cancellationToken);
        
        // 0. 强制修复：如果存在 year_month 但不存在 year_m，则重命名（无视版本号）
        await RenameYearMonthColumnIfNecessary(connection, cancellationToken);
        
        var appliedVersions = await GetAppliedVersionsAsync(connection, cancellationToken);

        if (!appliedVersions.Contains(1))
        {
            await ApplyMigrationAsync(connection, 1, "Initial schema", GetInitialSchemaSql(), cancellationToken);
        }

        if (!appliedVersions.Contains(2))
        {
            try
            {
                await ApplyMigrationAsync(connection, 2, "Task fixed amount columns", GetFixedAmountMigrationSql(), cancellationToken);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                await using var command = new SqliteCommand("INSERT OR IGNORE INTO schema_version(version, description) VALUES(2, 'Task fixed amount columns (column already exists)')", connection);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        
        // 记录 v3 版本（如果尚未记录）
        if (!appliedVersions.Contains(3))
        {
            await using var command = new SqliteCommand("INSERT OR IGNORE INTO schema_version(version, description) VALUES(3, 'Rename year_month to year_m')", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task RenameYearMonthColumnIfNecessary(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        await using var pragmaCmd = new SqliteCommand("PRAGMA table_info(procurement_tasks)", connection);
        await using var reader = await pragmaCmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        // 如果旧字段存在且新字段不存在，执行重命名
        if (columns.Contains("year_month") && !columns.Contains("year_m"))
        {
            await using var renameCmd = new SqliteCommand("ALTER TABLE procurement_tasks RENAME COLUMN year_month TO year_m", connection);
            await renameCmd.ExecuteNonQueryAsync(cancellationToken);
            
            await using var dropCmd = new SqliteCommand("DROP INDEX IF EXISTS idx_tasks_year_month", connection);
            await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            
            await using var createCmd = new SqliteCommand("CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_year_m ON procurement_tasks(year_m)", connection);
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureVersionTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS schema_version (
                               version INTEGER PRIMARY KEY,
                               description TEXT NOT NULL,
                               applied_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
                           );
                           """;
        await using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<int>> GetAppliedVersionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var versions = new HashSet<int>();
        const string sql = "SELECT version FROM schema_version";
        await using var command = new SqliteCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task ApplyMigrationAsync(
        SqliteConnection connection,
        int version,
        string description,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var command = new SqliteCommand(sql, connection, (SqliteTransaction)transaction))
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var versionCommand = new SqliteCommand("INSERT INTO schema_version(version, description) VALUES(@version, @description)", connection, (SqliteTransaction)transaction))
            {
                versionCommand.Parameters.AddWithValue("@version", version);
                versionCommand.Parameters.AddWithValue("@description", description);
                await versionCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string GetInitialSchemaSql()
    {
        return """
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
                   year_m TEXT NOT NULL,
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
               CREATE INDEX IF NOT EXISTS idx_categories_code ON categories(code);
               CREATE INDEX IF NOT EXISTS idx_categories_status ON categories(status);
               CREATE INDEX IF NOT EXISTS idx_categories_sort ON categories(sort);
               CREATE INDEX IF NOT EXISTS idx_products_category ON products(category_code);
               CREATE INDEX IF NOT EXISTS idx_products_active ON products(is_active);
               CREATE INDEX IF NOT EXISTS idx_products_name ON products(name);
               CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_year_m ON procurement_tasks(year_m);
               CREATE INDEX IF NOT EXISTS idx_tasks_status ON procurement_tasks(status);
               CREATE INDEX IF NOT EXISTS idx_tasks_created_at ON procurement_tasks(created_at);
               CREATE INDEX IF NOT EXISTS idx_budgets_task ON task_category_budgets(task_id);
               CREATE INDEX IF NOT EXISTS idx_budgets_category ON task_category_budgets(category_code);
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
               """;
    }

    private static string GetFixedAmountMigrationSql()
    {
        return """
               ALTER TABLE task_category_budgets ADD COLUMN is_fixed_amount INTEGER NOT NULL DEFAULT 0;
               ALTER TABLE task_category_budgets ADD COLUMN fixed_amount REAL NULL;
               CREATE INDEX IF NOT EXISTS idx_task_category_budgets_fixed ON task_category_budgets(task_id, is_fixed_amount);
               """;
    }

    private static string GetYearMonthRenameSql()
    {
        return """
               -- 重命名 year_month 为 year_m（避免 MySQL 保留字冲突）
               ALTER TABLE procurement_tasks RENAME COLUMN year_month TO year_m;
               DROP INDEX IF EXISTS idx_tasks_year_month;
               CREATE UNIQUE INDEX IF NOT EXISTS idx_tasks_year_m ON procurement_tasks(year_m);
               """;
    }

    /// <summary>
    /// 手动修复：如果 v2 迁移失败（列已存在但版本未记录），尝试修复
    /// </summary>
    public static async Task RepairIfColumnExistsAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        try
        {
            var hasColumn = await HasColumnAsync(connection, "task_category_budgets", "is_fixed_amount", cancellationToken);
            if (hasColumn)
            {
                var versions = await GetAppliedVersionsAsync(connection, cancellationToken);
                if (!versions.Contains(2))
                {
                    await using var command = new SqliteCommand("INSERT OR IGNORE INTO schema_version(version, description) VALUES(2, 'Task fixed amount columns (repaired)')", connection);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }
        catch
        {
            // 静默失败，不影响正常流程
        }
    }

    private static async Task<bool> HasColumnAsync(SqliteConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column";
        await using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@column", columnName);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) > 0;
    }
}
