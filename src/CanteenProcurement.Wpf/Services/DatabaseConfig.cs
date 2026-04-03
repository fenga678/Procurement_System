using System;
using System.Data.Common;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Wpf.Providers;

namespace CanteenProcurement.Wpf.Services
{
    /// <summary>
    /// 数据库配置类 - 支持双数据库（MySQL 和 SQLite）
    /// 采用渐进式迁移方案，默认使用 SQLite
    /// </summary>
    public static class DatabaseConfig
    {
        private static IDatabaseProvider? _provider;

        /// <summary>
        /// 当前数据库提供者
        /// </summary>
        public static IDatabaseProvider Provider
        {
            get
            {
                if (_provider == null)
                {
                    _provider = DatabaseProviderFactory.Initialize().Provider;
                }
                return _provider;
            }
        }

        /// <summary>
        /// 获取数据库提供者（异步初始化）
        /// </summary>
        public static async Task<IDatabaseProvider> GetProviderAsync()
        {
            if (_provider == null)
            {
                _provider = DatabaseProviderFactory.Initialize().Provider;
            }
            return await Task.FromResult(_provider);
        }

        /// <summary>
        /// 创建并打开数据库连接
        /// </summary>
        public static async Task<DbConnection> CreateAndOpenConnectionAsync()
        {
            return await Provider.CreateAndOpenConnectionAsync();
        }

        /// <summary>
        /// 获取当前时间戳SQL表达式
        /// </summary>
        public static string GetCurrentTimestampSql()
        {
            return Provider.GetCurrentTimestampSql();
        }

        /// <summary>
        /// 获取当前日期SQL表达式
        /// </summary>
        public static string GetCurrentDateSql()
        {
            return Provider.GetCurrentDateSql();
        }

        /// <summary>
        /// 获取最后插入ID的SQL语句
        /// </summary>
        public static string GetLastInsertIdSql()
        {
            return Provider.GetLastInsertIdSql();
        }

        /// <summary>
        /// 字符串拼接
        /// </summary>
        public static string Concat(params string[] parts)
        {
            return Provider.Concat(parts);
        }

        /// <summary>
        /// LIKE 查询的字符串拼接
        /// </summary>
        public static string LikeConcat(string parameter)
        {
            return Provider.LikeConcat(parameter);
        }

        /// <summary>
        /// 引用标识符（表名、列名等）
        /// </summary>
        public static string QuoteIdentifier(string identifier)
        {
            return Provider.QuoteIdentifier(identifier);
        }

        /// <summary>
        /// 切换数据库提供者（立即生效，无需重启）
        /// </summary>
        /// <param name="providerType">提供者类型 (Sqlite / MySql)</param>
        /// <param name="mysqlConfig">MySQL 配置（可选）</param>
        /// <param name="sqlitePath">SQLite 路径（可选）</param>
        public static void SwitchProvider(string providerType, MySqlConfiguration? mysqlConfig = null, string? sqlitePath = null)
        {
            var factory = DatabaseProviderFactory.Instance;
            factory.SwitchProvider(providerType, mysqlConfig, sqlitePath);
            _provider = factory.Provider;
            
            // 触发数据库切换事件，通知各页面刷新数据
            OnDatabaseSwitched?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// 数据库切换事件（各页面可订阅此事件以刷新数据）
        /// </summary>
        public static event EventHandler? OnDatabaseSwitched;

        /// <summary>
        /// 保存当前数据库配置
        /// </summary>
        public static void SaveConfiguration()
        {
            DatabaseProviderFactory.Instance.SaveConfiguration();
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public static DatabaseConfiguration GetConfiguration()
        {
            return DatabaseProviderFactory.Instance.Configuration;
        }

        /// <summary>
        /// 重置提供者（用于测试或重新初始化）
        /// </summary>
        public static void ResetProvider()
        {
            _provider = null;
            DatabaseProviderFactory.Reset();
        }

        #region 兼容旧代码的方法（已废弃，保留向后兼容）

        /// <summary>
        /// 获取 MySQL 连接字符串（已废弃，保留向后兼容）
        /// </summary>
        [Obsolete("请使用 Provider 属性或 CreateAndOpenConnectionAsync 方法")]
        public static string GetConnectionString()
        {
            var factory = DatabaseProviderFactory.Initialize();
            if (factory.Provider is MySqlProvider mySqlProvider)
            {
                return mySqlProvider.GetDisplayConnectionString();
            }

            // 如果当前不是 MySQL，尝试从配置获取 MySQL 连接字符串
            var config = factory.Configuration;
            var mysql = config.MySql ?? new MySqlConfiguration();
            return $"server={mysql.Server};port={mysql.Port};database={mysql.Database};user={mysql.User};password={mysql.Password};CharSet=utf8mb4;";
        }

        #endregion
    }
}
