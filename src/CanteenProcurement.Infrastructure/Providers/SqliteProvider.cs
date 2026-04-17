using System.Data.Common;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Infrastructure.Migrations;
using Microsoft.Data.Sqlite;

namespace CanteenProcurement.Infrastructure.Providers;

public sealed class SqliteProvider : IDatabaseProvider
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly SqliteSchemaMigrator _migrator = new();

    public SqliteProvider(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={_databasePath}";
    }

    public string Name => "Sqlite";

    public async Task<DbConnection> CreateAndOpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await InitializeConnectionAsync(connection);
        await _migrator.EnsureMigratedAsync(connection);
        return connection;
    }

    public string GetLastInsertIdSql() => "SELECT last_insert_rowid();";

    public string GetCurrentTimestampSql() => "datetime('now', 'localtime')";

    public string GetCurrentDateSql() => "date('now', 'localtime')";

    public string Concat(params string[] parts) => string.Join(" || ", parts);

    public string LikeConcat(string parameter) => $"'%' || {parameter} || '%'";

    public async Task<bool> HasColumnAsync(DbConnection conn, string tableName, string columnName)
    {
        const string sql = "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column";
        await using var command = new SqliteCommand(sql, (SqliteConnection)conn);
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@column", columnName);
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    public async Task<int> CountColumnsAsync(DbConnection conn, string tableName, params string[] columnNames)
    {
        var placeholders = Enumerable.Range(0, columnNames.Length).Select(index => $"@p{index}").ToArray();
        var sql = $"SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name IN ({string.Join(",", placeholders)})";
        await using var command = new SqliteCommand(sql, (SqliteConnection)conn);
        command.Parameters.AddWithValue("@table", tableName);
        for (var index = 0; index < columnNames.Length; index++)
        {
            command.Parameters.AddWithValue($"@p{index}", columnNames[index]);
        }

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task InitializeConnectionAsync(DbConnection conn)
    {
        using var command = conn.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync();
    }

    public string GetDisplayConnectionString() => $"Database: {_databasePath}";

    public DbCommand CreateCommand(string sql, DbConnection conn, DbTransaction? tx = null)
    {
        return new SqliteCommand(sql, (SqliteConnection)conn, tx as SqliteTransaction);
    }

    public void AddParameter(DbCommand cmd, string name, object? value)
    {
        if (cmd is SqliteCommand command)
        {
            command.Parameters.AddWithValue($"@{name}", value ?? DBNull.Value);
        }
    }

    public string QuoteIdentifier(string identifier) => identifier;
}
