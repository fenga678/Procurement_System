using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Core.Algorithms
{
    /// <summary>
    /// 基于频率/间隔的通用调度策略
    /// </summary>
    public class FrequencyBasedScheduler : ICategorySchedulingStrategy
    {
        private readonly int _frequencyDays;

        public FrequencyBasedScheduler(int frequencyDays)
        {
            _frequencyDays = Math.Max(1, frequencyDays);
        }

        /// <summary>
        /// 执行分类调度
        /// </summary>
        public Task<CategorySchedule> ScheduleAsync(
            TaskCategoryBudget categoryBudget,
            List<Product> products,
            DateTime startDate,
            DateTime endDate)
        {
            var schedule = new CategorySchedule(categoryBudget.CategoryCode)
            {
                TotalBudget = categoryBudget.Budget
            };

            var lastUsed = new Dictionary<int, DateTime>();
            var random = new Random(Guid.NewGuid().GetHashCode());
            var totalDays = (endDate - startDate).Days + 1;
            var occurrences = Math.Max(1, (int)Math.Ceiling((double)totalDays / _frequencyDays));
            var budgetPerOccur = categoryBudget.Budget / occurrences;

            var currentDate = startDate;
            var usedBudget = 0m;

            for (int i = 0; i < occurrences && currentDate <= endDate; i++)
            {
                var candidates = products
                    .Where(p => !lastUsed.ContainsKey(p.Id) || (currentDate - lastUsed[p.Id]).Days >= p.MinIntervalDays)
                    .OrderBy(_ => random.Next())
                    .ToList();

                if (!candidates.Any())
                {
                    candidates = products.OrderBy(_ => random.Next()).ToList();
                }

                var product = candidates.First();
                lastUsed[product.Id] = currentDate;

                // 计算本次可用预算与数量
                var remainingBudget = categoryBudget.Budget - usedBudget;
                var thisOccurBudget = Math.Min(budgetPerOccur, remainingBudget);
                var qty = Math.Max(1, Math.Round(thisOccurBudget / product.Price, 2, MidpointRounding.ToZero));
                var amount = qty * product.Price;

                // 若金额为0则跳过
                if (amount <= 0)
                {
                    currentDate = currentDate.AddDays(_frequencyDays);
                    continue;
                }

                // 若超出总预算，进行向下取整调整
                if (usedBudget + amount > categoryBudget.Budget)
                {
                    var safeBudget = categoryBudget.Budget - usedBudget;
                    qty = Math.Max(1, Math.Round(safeBudget / product.Price, 2, MidpointRounding.ToZero));
                    amount = qty * product.Price;
                    if (amount <= 0)
                    {
                        currentDate = currentDate.AddDays(_frequencyDays);
                        continue;
                    }
                }

                var item = new ScheduleItem
                {
                    Date = currentDate,
                    Product = product,
                    Quantity = qty,
                    Amount = amount,
                    OriginalAmount = amount,
                    FluctuationFactor = 1.0m
                };

                schedule.AddItem(item);
                usedBudget += amount;

                currentDate = currentDate.AddDays(_frequencyDays);
            }

            return Task.FromResult(schedule);
        }
    }

    /// <summary>
    /// 高频调度策略（每日）
    /// </summary>
    public class HighFrequencyScheduler : FrequencyBasedScheduler
    {
        public HighFrequencyScheduler() : base(1) { }
    }

    /// <summary>
    /// 中频调度策略（默认每2天）
    /// </summary>
    public class MediumFrequencyScheduler : FrequencyBasedScheduler
    {
        public MediumFrequencyScheduler(int frequencyDays) : base(frequencyDays <= 0 ? 2 : frequencyDays) { }
    }

    /// <summary>
    /// 低频调度策略（默认每5-7天）
    /// </summary>
    public class LowFrequencyScheduler : FrequencyBasedScheduler
    {
        public LowFrequencyScheduler(int frequencyDays) : base(frequencyDays <= 0 ? 5 : frequencyDays) { }
    }

    /// <summary>
    /// 预算拆分器
    /// </summary>
    public interface IBudgetSplitter
    {
        List<CategoryBudget> SplitBudget(
            decimal totalBudget,
            List<Category> categories,
            Dictionary<string, decimal>? fixedAmounts = null
        );
    }

    /// <summary>
    /// 预算拆分器实现
    /// </summary>
    public class BudgetSplitter : IBudgetSplitter
    {
        public List<CategoryBudget> SplitBudget(
            decimal totalBudget,
            List<Category> categories,
            Dictionary<string, decimal>? fixedAmounts = null)
        {
            var result = new List<CategoryBudget>();
            var activeCategories = categories.Where(c => c.Status).ToList();

            // Step 1: 处理固定金额分类
            var fixedTotal = 0m;
            var fixedCategoryCodes = new HashSet<string>();

            if (fixedAmounts != null)
            {
                foreach (var category in activeCategories)
                {
                    if (fixedAmounts.TryGetValue(category.Code, out var fixedAmount) && fixedAmount > 0)
                    {
                        result.Add(new CategoryBudget
                        {
                            CategoryCode = category.Code,
                            Ratio = category.Ratio,
                            Budget = fixedAmount,
                            IsFixedAmount = true,
                            ExpectedProductCount = CalculateExpectedProductCount(category)
                        });

                        fixedTotal += fixedAmount;
                        fixedCategoryCodes.Add(category.Code);
                    }
                }
            }

            // Step 2: 计算剩余预算
            var remainingBudget = totalBudget - fixedTotal;
            if (remainingBudget < 0)
            {
                throw new ArgumentException($"固定金额总额({fixedTotal:C2})不能超过总预算({totalBudget:C2})");
            }

            // Step 3: 对非固定分类按比例分配剩余预算
            var variableCategories = activeCategories
                .Where(c => !fixedCategoryCodes.Contains(c.Code))
                .ToList();

            if (variableCategories.Any())
            {
                // 重新归一化比例（只针对非固定分类）
                var totalRatio = variableCategories.Sum(c => c.Ratio);

                foreach (var category in variableCategories)
                {
                    var normalizedRatio = totalRatio > 0 ? category.Ratio / totalRatio : 1m / variableCategories.Count;
                    result.Add(new CategoryBudget
                    {
                        CategoryCode = category.Code,
                        Ratio = normalizedRatio,
                        Budget = Math.Round(remainingBudget * normalizedRatio, 2),
                        IsFixedAmount = false,
                        ExpectedProductCount = CalculateExpectedProductCount(category)
                    });
                }

                // 处理精度误差：调整最后一个非固定分类的金额，确保总和等于总预算
                if (result.Count > 0 && remainingBudget > 0)
                {
                    var lastVariable = result.Last(r => !r.IsFixedAmount);
                    var currentTotal = result.Sum(r => r.Budget);
                    var diff = totalBudget - currentTotal;
                    if (Math.Abs(diff) > 0.001m)
                    {
                        lastVariable.Budget += diff;
                    }
                }
            }

            return result;
        }

        private int CalculateExpectedProductCount(Category category)
        {
            return category.Code switch
            {
                "vegetable" => 15,
                "meat" => 8,
                "egg" => 3,
                "oil" => 4,
                "rice" => 4,
                "noodle" => 5,
                "seasoning" => 12,
                _ => 5
            };
        }
    }

    /// <summary>
    /// 随机波动处理器
    /// </summary>
    public interface IRandomFluctuationProcessor
    {
        void ApplyFluctuation(List<ScheduleItem> schedule, decimal fluctuationRate);
    }

    /// <summary>
    /// 随机波动处理器实现
    /// </summary>
    public class RandomFluctuationProcessor : IRandomFluctuationProcessor
    {
        public void ApplyFluctuation(List<ScheduleItem> schedule, decimal fluctuationRate)
        {
            var random = new Random(Guid.NewGuid().GetHashCode());

            foreach (var item in schedule)
            {
                var randomValue = (random.NextDouble() * 2 - 1) * (double)fluctuationRate;
                var fluctuationFactor = 1.0m + (decimal)randomValue;

                item.FluctuationFactor = fluctuationFactor;
                item.Amount = item.OriginalAmount * fluctuationFactor;

                if (item.Product != null && item.Product.Price > 0)
                {
                    item.Quantity = Math.Round(item.Amount / item.Product.Price, 2, MidpointRounding.ToZero);
                    if (item.Quantity <= 0)
                    {
                        item.Quantity = 1;
                        item.Amount = item.Product.Price;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 规则引擎
    /// </summary>
    public interface IRuleEngine
    {
        IEnumerable<IScheduleRule> GetRules();
        void RegisterRule(IScheduleRule rule);
    }

    public class RuleEngine : IRuleEngine
    {
        private readonly List<IScheduleRule> _rules = new();
        public IEnumerable<IScheduleRule> GetRules() => _rules;
        public void RegisterRule(IScheduleRule rule) => _rules.Add(rule);
    }
}
