using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace CanteenProcurement.Core.Interfaces
{
    /// <summary>
    /// 数据库提供者接口 - 抽象不同数据库的差异
    /// 支持渐进式迁移，同时兼容 MySQL 和 SQLite
    /// </summary>
    public interface IDatabaseProvider
    {
        /// <summary>
        /// 数据库名称标识 (MySql / Sqlite)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 创建并打开数据库连接
        /// </summary>
        Task<DbConnection> CreateAndOpenConnectionAsync();

        /// <summary>
        /// 获取最后插入ID的SQL语句
        /// MySQL: SELECT LAST_INSERT_ID();
        /// SQLite: SELECT last_insert_rowid();
        /// </summary>
        string GetLastInsertIdSql();

        /// <summary>
        /// 获取当前时间戳SQL
        /// MySQL: NOW()
        /// SQLite: datetime('now', 'localtime')
        /// </summary>
        string GetCurrentTimestampSql();

        /// <summary>
        /// 获取当前日期SQL
        /// MySQL: CURDATE()
        /// SQLite: date('now', 'localtime')
        /// </summary>
        string GetCurrentDateSql();

        /// <summary>
        /// 字符串拼接
        /// MySQL: CONCAT(a, b, c)
        /// SQLite: a || b || c
        /// </summary>
        /// <param name="parts">要拼接的字符串部分</param>
        /// <returns>拼接后的SQL表达式</returns>
        string Concat(params string[] parts);

        /// <summary>
        /// 生成 LIKE 查询的字符串拼接表达式
        /// MySQL: LIKE CONCAT('%', parameter, '%')
        /// SQLite: LIKE '%' || parameter || '%'
        /// </summary>
        /// <param name="parameter">参数名</param>
        /// <returns>LIKE 表达式</returns>
        string LikeConcat(string parameter);

        /// <summary>
        /// 检查表是否存在指定列
        /// MySQL: SHOW COLUMNS FROM table WHERE Field = 'column'
        /// SQLite: PRAGMA table_info(table)
        /// </summary>
        Task<bool> HasColumnAsync(DbConnection conn, string tableName, string columnName);

        /// <summary>
        /// 检查表是否存在指定列（批量）
        /// </summary>
        Task<int> CountColumnsAsync(DbConnection conn, string tableName, params string[] columnNames);

        /// <summary>
        /// 连接后初始化
        /// MySQL: SET collation_connection = utf8mb4_unicode_ci;
        /// SQLite: PRAGMA foreign_keys = ON;
        /// </summary>
        Task InitializeConnectionAsync(DbConnection conn);

        /// <summary>
        /// 获取连接字符串（用于显示，隐藏敏感信息）
        /// </summary>
        string GetDisplayConnectionString();

        /// <summary>
        /// 创建数据库命令
        /// </summary>
        DbCommand CreateCommand(string sql, DbConnection conn, DbTransaction? tx = null);

        /// <summary>
        /// 添加参数
        /// </summary>
        void AddParameter(DbCommand cmd, string name, object? value);

        /// <summary>
        /// 引用标识符（表名、列名等）
        /// MySQL: `column_name`
        /// SQLite: "column_name" 或 column_name（SQLite对双引号或无引号都支持）
        /// </summary>
        /// <param name="identifier">标识符名称</param>
        /// <returns>引用后的标识符</returns>
        string QuoteIdentifier(string identifier);
    }
}
