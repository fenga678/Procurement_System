using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using CanteenProcurement.Core.Interfaces;

namespace CanteenProcurement.Wpf.Services
{
    /// <summary>
    /// 数据库架构能力检测提供者
    /// 适配双数据库（MySQL 和 SQLite）
    /// </summary>
    public class SchemaCapabilitiesProvider
    {
        private bool? _hasDailyItemColumns;
        private bool? _hasFixedAmountColumns;
        private readonly object _lock = new();

        /// <summary>
        /// 检查 categories 表是否存在 daily_min_items 和 daily_max_items 列
        /// </summary>
        public async Task<bool> HasDailyItemColumnsAsync(
            DbConnection conn,
            CancellationToken ct = default)
        {
            if (_hasDailyItemColumns.HasValue)
                return _hasDailyItemColumns.Value;

            lock (_lock)
            {
                if (_hasDailyItemColumns.HasValue)
                    return _hasDailyItemColumns.Value;
            }

            var provider = DatabaseConfig.Provider;
            var count = await provider.CountColumnsAsync(conn, "categories", "daily_min_items", "daily_max_items");

            _hasDailyItemColumns = count >= 2;
            return _hasDailyItemColumns.Value;
        }

        /// <summary>
        /// 检查 task_category_budgets 表是否存在 is_fixed_amount 和 fixed_amount 列
        /// </summary>
        public async Task<bool> HasFixedAmountColumnsAsync(
            DbConnection conn,
            CancellationToken ct = default)
        {
            if (_hasFixedAmountColumns.HasValue)
                return _hasFixedAmountColumns.Value;

            lock (_lock)
            {
                if (_hasFixedAmountColumns.HasValue)
                    return _hasFixedAmountColumns.Value;
            }

            var provider = DatabaseConfig.Provider;
            var count = await provider.CountColumnsAsync(conn, "task_category_budgets", "is_fixed_amount", "fixed_amount");

            _hasFixedAmountColumns = count >= 2;
            return _hasFixedAmountColumns.Value;
        }

        /// <summary>
        /// 重置缓存
        /// </summary>
        public void ResetCache()
        {
            _hasDailyItemColumns = null;
            _hasFixedAmountColumns = null;
        }
    }
}
