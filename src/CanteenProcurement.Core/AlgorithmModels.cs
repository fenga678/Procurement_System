using System;
using System.Collections.Generic;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Core.Algorithms
{
    /// <summary>
    /// 调度结果
    /// </summary>
    public class ScheduleResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 调度详情
        /// </summary>
        public List<ScheduleItem> Schedule { get; set; } = new List<ScheduleItem>();

        /// <summary>
        /// 分类预算分配
        /// </summary>
        public List<CategoryBudget> CategoryBudgets { get; set; } = new List<CategoryBudget>();

        /// <summary>
        /// 执行信息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        public ScheduleResult(bool isValid = true, string? message = null)
        {
            IsValid = isValid;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>
    /// 调度项
    /// </summary>
    public class ScheduleItem
    {
        /// <summary>
        /// 日期
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// 商品
        /// </summary>
        public Product Product { get; set; } = new();

        /// <summary>
        /// 采购数量
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 采购金额
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 原始金额（波动前）
        /// </summary>
        public decimal OriginalAmount { get; set; }

        /// <summary>
        /// 波动因子
        /// </summary>
        public decimal FluctuationFactor { get; set; } = 1.0m;
    }

    /// <summary>
    /// 分类调度结果
    /// </summary>
    public class CategorySchedule
    {
        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; } = string.Empty;

        /// <summary>
        /// 调度项列表
        /// </summary>
        public List<ScheduleItem> Items { get; set; } = new List<ScheduleItem>();

        /// <summary>
        /// 总预算
        /// </summary>
        public decimal TotalBudget { get; set; }

        /// <summary>
        /// 实际使用预算
        /// </summary>
        public decimal UsedBudget => Items?.Sum(i => i.Amount) ?? 0;

        public CategorySchedule(string categoryCode)
        {
            CategoryCode = categoryCode;
        }

        /// <summary>
        /// 添加调度项
        /// </summary>
        public void AddItem(ScheduleItem item)
        {
            Items.Add(item);
        }
    }

    /// <summary>
    /// 规则验证结果
    /// </summary>
    public class RuleValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        public RuleValidationResult(bool isValid = true, string? errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage ?? string.Empty;
        }
    }

    /// <summary>
    /// 调度上下文
    /// </summary>
    public class ScheduleContext
    {
        /// <summary>
        /// 任务ID
        /// </summary>
        public int TaskId { get; set; }

        /// <summary>
        /// 当前调度项列表
        /// </summary>
        public List<ScheduleItem> ScheduleItems { get; set; } = new List<ScheduleItem>();

        /// <summary>
        /// 商品使用历史记录
        /// </summary>
        public Dictionary<int, DateTime> ProductLastUsedDates { get; set; } = new Dictionary<int, DateTime>();

        /// <summary>
        /// 各分类预算信息
        /// </summary>
        public Dictionary<string, TaskCategoryBudget> CategoryBudgets { get; set; } = new Dictionary<string, TaskCategoryBudget>();

        /// <summary>
        /// 获取指定日期的调度项
        /// </summary>
        public List<ScheduleItem> GetItemsForDate(DateTime date)
        {
            return ScheduleItems.FindAll(item => item.Date.Date == date.Date);
        }

        /// <summary>
        /// 获取指定商品的使用记录
        /// </summary>
        public List<ScheduleItem> GetItemsForProduct(int productId)
        {
            return ScheduleItems.FindAll(item => item.Product.Id == productId);
        }

        /// <summary>
        /// 获取指定分类的预算
        /// </summary>
        public TaskCategoryBudget? GetCategoryBudget(string categoryCode)
        {
            return CategoryBudgets.TryGetValue(categoryCode, out var budget) ? budget : null;
        }

        /// <summary>
        /// 获取指定分类已使用的预算
        /// </summary>
        public decimal GetUsedBudgetForCategory(string categoryCode)
        {
            return ScheduleItems
                .Where(item => item.Product.CategoryCode == categoryCode)
                .Sum(item => item.Amount);
        }
    }

    /// <summary>
    /// 预算拆分结果
    /// </summary>
    public class CategoryBudget
    {
        /// <summary>
        /// 分类编码
        /// </summary>
        public string CategoryCode { get; set; } = string.Empty;

        /// <summary>
        /// 预算占比
        /// </summary>
        public decimal Ratio { get; set; }

        /// <summary>
        /// 分配预算
        /// </summary>
        public decimal Budget { get; set; }

        /// <summary>
        /// 预期商品数量
        /// </summary>
        public int ExpectedProductCount { get; set; }

        /// <summary>
        /// 是否为固定金额（true=使用固定金额，false=使用比例计算）
        /// </summary>
        public bool IsFixedAmount { get; set; } = false;
    }

    /// <summary>
    /// 算法配置
    /// </summary>
    public class SchedulingConfiguration
    {
        /// <summary>
        /// 随机波动率
        /// </summary>
        public decimal FloatRate { get; set; } = 0.1m;

        /// <summary>
        /// 最小间隔天数
        /// </summary>
        public int DefaultMinInterval { get; set; } = 2;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// 超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// 分类调度策略配置
        /// </summary>
        public Dictionary<string, CategoryStrategyConfig> CategoryStrategies { get; set; } = new Dictionary<string, CategoryStrategyConfig>();
    }

    /// <summary>
    /// 分类策略配置
    /// </summary>
    public class CategoryStrategyConfig
    {
        /// <summary>
        /// 策略类型
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 调度频率（天）
        /// </summary>
        public int FrequencyDays { get; set; } = 1;

        /// <summary>
        /// 每日商品数量
        /// </summary>
        public string DailyProductCount { get; set; } = "auto";
    }
}