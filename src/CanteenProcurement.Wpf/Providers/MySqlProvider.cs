using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;
using MySql.Data.MySqlClient;

namespace CanteenProcurement.Wpf.Providers
{
    /// <summary>
    /// MySQL 数据库提供者实现
    /// </summary>
    public class MySqlProvider : IDatabaseProvider
    {
        private readonly string _connectionString;

        public string Name => "MySql";

        public MySqlProvider(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<DbConnection> CreateAndOpenConnectionAsync()
        {
            var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
            await InitializeConnectionAsync(conn);
            return conn;
        }

        public string GetLastInsertIdSql() => "SELECT LAST_INSERT_ID();";

        public string GetCurrentTimestampSql() => "NOW()";

        public string GetCurrentDateSql() => "CURDATE()";

        public string Concat(params string[] parts)
        {
            return $"CONCAT({string.Join(", ", parts)})";
        }

        public string LikeConcat(string parameter)
        {
            return $"CONCAT('%', {parameter}, '%')";
        }

        public async Task<bool> HasColumnAsync(DbConnection conn, string tableName, string columnName)
        {
            // MySQL 不支持在 SHOW COLUMNS 中使用参数替换表名，必须使用反引号引用
            var sql = $"SHOW COLUMNS FROM `{tableName}` WHERE Field = @column";
            await using var cmd = new MySqlCommand(sql, (MySqlConnection)conn);
            cmd.Parameters.AddWithValue("@column", columnName);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }

        public async Task<int> CountColumnsAsync(DbConnection conn, string tableName, params string[] columnNames)
        {
            // 为每个参数生成唯一的参数名 @p0, @p1, ...
            var paramNames = new List<string>();
            for (int i = 0; i < columnNames.Length; i++)
            {
                paramNames.Add($"@p{i}");
            }
            var placeholders = string.Join(",", paramNames);
            var sql = $"SHOW COLUMNS FROM `{tableName}` WHERE Field IN ({placeholders})";
            await using var cmd = new MySqlCommand(sql, (MySqlConnection)conn);
            for (int i = 0; i < columnNames.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", columnNames[i]);
            }
            await using var reader = await cmd.ExecuteReaderAsync();
            var count = 0;
            while (await reader.ReadAsync()) count++;
            return count;
        }

        public async Task InitializeConnectionAsync(DbConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SET collation_connection = utf8mb4_unicode_ci;";
            await cmd.ExecuteNonQueryAsync();
        }

        public string GetDisplayConnectionString()
        {
            var builder = new MySqlConnectionStringBuilder(_connectionString);
            return $"Server={builder.Server};Database={builder.Database};User={builder.UserID}";
        }

        public DbCommand CreateCommand(string sql, DbConnection conn, DbTransaction? tx = null)
        {
            return new MySqlCommand(sql, (MySqlConnection)conn, tx as MySqlTransaction);
        }

        public void AddParameter(DbCommand cmd, string name, object? value)
        {
            if (cmd is MySqlCommand mySqlCmd)
            {
                mySqlCmd.Parameters.AddWithValue($"@{name}", value ?? DBNull.Value);
            }
        }

        public string QuoteIdentifier(string identifier)
        {
            return $"`{identifier}`";
        }
    }
}
