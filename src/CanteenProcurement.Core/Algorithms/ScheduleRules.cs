using System;
using System.Collections.Generic;
using System.Linq;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Core.Algorithms
{
    /// <summary>
    /// 同日内不重复规则
    /// </summary>
    public class NoDuplicateSameDayRule : IScheduleRule
    {

        public string RuleName => "同日内不重复规则";

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            // 检查同日内是否有相同的商品
            var sameDayItems = context.GetItemsForDate(item.Date);
            var duplicateItems = sameDayItems
                .Where(i => i.Product.Id == item.Product.Id && i != item)
                .ToList();

            if (duplicateItems.Any())
            {
                return new RuleValidationResult(false, 
                    $"日期{item.Date:yyyy-MM-dd}内商品{item.Product.Name}重复出现");
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 最小间隔天数规则
    /// </summary>
    public class MinIntervalRule : IScheduleRule
    {

        public string RuleName => "最小间隔天数规则";

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            var minInterval = item.Product.MinIntervalDays;
            
            // 获取该商品的所有使用记录（按日期排序）
            var productItems = context.GetItemsForProduct(item.Product.Id)
                .OrderBy(i => i.Date)
                .ToList();

            // 找到当前项的索引
            var currentIndex = productItems.FindIndex(i => i == item);
            
            if (currentIndex > 0) // 不是第一次出现
            {
                var previousItem = productItems[currentIndex - 1];
                var daysDiff = (item.Date - previousItem.Date).Days;

                if (daysDiff < minInterval)
                {
                    return new RuleValidationResult(false,
                        $"商品{item.Product.Name}间隔{daysDiff}天，小于最小间隔{minInterval}天");
                }
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 分类预算规则
    /// </summary>
    public class CategoryBudgetRule : IScheduleRule
    {

        public string RuleName => "分类预算规则";

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            var categoryBudget = context.GetCategoryBudget(item.Product.CategoryCode);
            if (categoryBudget == null)
            {
                return new RuleValidationResult(false,
                    $"找不到分类{item.Product.CategoryCode}的预算信息");
            }

            var usedBudget = context.GetUsedBudgetForCategory(item.Product.CategoryCode);
            
            if (usedBudget > categoryBudget.Budget)
            {
                var overspend = usedBudget - categoryBudget.Budget;
                return new RuleValidationResult(false,
                    $"分类{item.Product.CategoryCode}超预算{overspend:C2}，总使用{usedBudget:C2}，预算{categoryBudget.Budget:C2}");
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 商品价格有效性规则
    /// </summary>
    public class ProductPriceValidityRule : IScheduleRule
    {

        public string RuleName => "商品价格有效性规则";

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            if (item.Product.Price <= 0)
            {
                return new RuleValidationResult(false,
                    $"商品{item.Product.Name}的价格无效：{item.Product.Price}");
            }

            if (item.Quantity <= 0)
            {
                return new RuleValidationResult(false,
                    $"商品{item.Product.Name}的数量无效：{item.Quantity}");
            }

            if (item.Amount <= 0)
            {
                return new RuleValidationResult(false,
                    $"商品{item.Product.Name}的金额无效：{item.Amount}");
            }

            // 检查单价 * 数量 是否等于 金额（考虑波动因子）
            var expectedAmount = item.Product.Price * item.Quantity;
            var actualAmount = item.Amount;
            var tolerance = 0.01m; // 允许的误差

            if (Math.Abs(expectedAmount - actualAmount) > tolerance)
            {
                return new RuleValidationResult(false,
                    $"商品{item.Product.Name}的单价*数量({expectedAmount})与金额({actualAmount})不匹配");
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 预算分布均匀性规则
    /// </summary>
    public class BudgetDistributionRule : IScheduleRule
    {

        public string RuleName => "预算分布均匀性规则";

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            // 获取该商品所在分类的所有调度项
            var categoryItems = context.ScheduleItems
                .Where(i => i.Product.CategoryCode == item.Product.CategoryCode)
                .ToList();

            if (categoryItems.Count < 2) return new RuleValidationResult(true);

            // 计算平均间隔
            var dates = categoryItems.Select(i => i.Date).OrderBy(d => d).ToList();
            var intervals = new List<int>();

            for (int i = 1; i < dates.Count; i++)
            {
                intervals.Add((dates[i] - dates[i - 1]).Days);
            }

            if (intervals.Count > 0)
            {
                var avgInterval = intervals.Average();
                var maxDeviation = 3; // 允许的最大偏差

                foreach (var interval in intervals)
                {
                    if (Math.Abs(interval - avgInterval) > maxDeviation)
                    {
                        return new RuleValidationResult(false,
                            $"分类{item.Product.CategoryCode}的商品分布不够均匀，间隔偏差过大");
                    }
                }
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 总预算限制规则
    /// </summary>
    public class TotalBudgetRule : IScheduleRule
    {
        private readonly decimal _totalBudget;

        public string RuleName => "总预算限制规则";

        public TotalBudgetRule(decimal totalBudget)
        {
            _totalBudget = totalBudget;
        }

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            var usedBudget = context.ScheduleItems.Sum(i => i.Amount);
            
            if (usedBudget > _totalBudget)
            {
                var overspend = usedBudget - _totalBudget;
                return new RuleValidationResult(false,
                    $"总预算超支{overspend:C2}，总使用{usedBudget:C2}，预算{_totalBudget:C2}");
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 随机波动范围规则
    /// </summary>
    public class RandomFluctuationRule : IScheduleRule
    {
        private readonly decimal _maxFluctuationRate;

        public string RuleName => "随机波动范围规则";

        public RandomFluctuationRule(decimal maxFluctuationRate = 0.1m)
        {
            _maxFluctuationRate = maxFluctuationRate;
        }

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            var minAmount = item.OriginalAmount * (1 - _maxFluctuationRate);
            var maxAmount = item.OriginalAmount * (1 + _maxFluctuationRate);

            if (item.Amount < minAmount || item.Amount > maxAmount)
            {
                return new RuleValidationResult(false,
                    $"商品{item.Product.Name}的波动金额{item.Amount:C2}超出允许范围[{minAmount:C2}, {maxAmount:C2}]");
            }

            return new RuleValidationResult(true);
        }
    }

    /// <summary>
    /// 商品活动状态规则
    /// </summary>
    public class ProductActivityRule : IScheduleRule
    {

        public string RuleName => "商品活动状态规则";

        /// <summary>
        /// 验证规则
        /// </summary>
        public RuleValidationResult Validate(ScheduleItem item, ScheduleContext context)
        {
            if (!item.Product.IsActive)
            {
                return new RuleValidationResult(false,
                    $"商品{item.Product.Name}已停用，不应出现在采购计划中");
            }

            return new RuleValidationResult(true);
        }
    }
}