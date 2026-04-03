using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CanteenProcurement.Core.Entities;
using Microsoft.Extensions.Logging;

namespace CanteenProcurement.Core.Algorithms
{
    /// <summary>
    /// 采购算法引擎实现
    /// </summary>
    public class ProcurementAlgorithmEngine : IProcurementAlgorithmEngine
    {
        private readonly ILogger<ProcurementAlgorithmEngine> _logger;
        private readonly SchedulingConfiguration _configuration;
        private readonly IBudgetSplitter _budgetSplitter;
        private readonly IRandomFluctuationProcessor _fluctuationProcessor;
        private readonly IRuleEngine _ruleEngine;
        private readonly Dictionary<string, ICategorySchedulingStrategy> _strategies;

        public ProcurementAlgorithmEngine(
            ILogger<ProcurementAlgorithmEngine> logger,
            SchedulingConfiguration configuration,
            IBudgetSplitter budgetSplitter,
            IRandomFluctuationProcessor fluctuationProcessor,
            IRuleEngine ruleEngine)
        {
            _logger = logger;
            _configuration = configuration;
            _budgetSplitter = budgetSplitter;
            _fluctuationProcessor = fluctuationProcessor;
            _ruleEngine = ruleEngine;
            _strategies = new Dictionary<string, ICategorySchedulingStrategy>();
        }

        /// <summary>
        /// 注册分类调度策略
        /// </summary>
        public void RegisterStrategy(string categoryCode, ICategorySchedulingStrategy strategy)
        {
            _strategies[categoryCode] = strategy;
        }

        /// <summary>
        /// 注册默认规则（若未显式注册）
        /// </summary>
        private void EnsureDefaultRules(decimal totalBudget, List<CategoryBudget> categoryBudgets)
        {
            if (!_ruleEngine.GetRules().Any())
            {
                _ruleEngine.RegisterRule(new ProductActivityRule());
                _ruleEngine.RegisterRule(new NoDuplicateSameDayRule());
                _ruleEngine.RegisterRule(new MinIntervalRule());
                _ruleEngine.RegisterRule(new CategoryBudgetRule());
                _ruleEngine.RegisterRule(new TotalBudgetRule(totalBudget));
                _ruleEngine.RegisterRule(new RandomFlucuationRule());
                _ruleEngine.RegisterRule(new BudgetDistributionRule());
            }
        }


        /// <summary>
        /// 执行采购计划生成
        /// </summary>
        public async Task<ScheduleResult> ExecuteAsync(ProcurementTask task)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("开始执行采购计划生成: TaskId={TaskId}, YearMonth={YearMonth}",
                task.Id, task.YearMonth);

            try
            {
                // Step 1: 预算拆分
                var splitResult = await SplitBudgetAsync(task);
                if (!splitResult.IsValid)
                {
                    return splitResult;
                }

                // Step 2: 获取分类数据
                var categories = await GetCategoriesAsync();
                if (categories == null || !categories.Any())
                {
                    return new ScheduleResult(false, "未找到有效的商品分类");
                }

                // Step 3: 注册默认规则
                EnsureDefaultRules(task.TotalBudget, splitResult.CategoryBudgets);

                // Step 4: 并行调度各分类
                var startDate = task.GetStartDate();
                var endDate = task.GetEndDate();

                var categorySchedules = await ExecuteCategorySchedulingAsync(
                    splitResult.CategoryBudgets, categories, startDate, endDate);

                if (categorySchedules == null || !categorySchedules.Any())
                {
                    return new ScheduleResult(false, "未能生成有效的分类调度计划");
                }

                // Step 5: 合并每日数据
                var dailySchedule = MergeDailySchedules(categorySchedules);
                _logger.LogInformation("合并完成: 共{ItemCount}条采购记录", dailySchedule.Count);

                // Step 6: 应用随机波动
                _fluctuationProcessor.ApplyFluctuation(dailySchedule, task.FloatRate);
                _logger.LogInformation("应用随机波动完成: 波动率{FlucuationRate:P1}", task.FloatRate);

                // Step 7: 规则验证
                var validationResult = ValidateSchedule(dailySchedule, task, splitResult.CategoryBudgets);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning("规则验证失败: {Errors}", string.Join(", ", validationResult.Errors));
                    return validationResult;
                }

                _logger.LogInformation("采购计划生成成功: 耗时{ElapsedMs}ms, 共{ItemCount}条记录",
                    stopwatch.ElapsedMilliseconds, dailySchedule.Count);

                return new ScheduleResult(true, "采购计划生成成功")
                {
                    Schedule = dailySchedule,
                    Message = $"成功生成{dailySchedule.Count}条采购记录，总金额{dailySchedule.Sum(i => i.Amount):C2}",
                    CategoryBudgets = splitResult.CategoryBudgets
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "采购计划生成失败: {Message}", ex.Message);
                return new ScheduleResult(false, $"采购计划生成失败: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
            }
        }


        /// <summary>
        /// 预算拆分
        /// </summary>
        private async Task<ScheduleResult> SplitBudgetAsync(ProcurementTask task)
        {
            try
            {
                var categories = await GetCategoriesAsync();

                // 从任务中获取固定金额配置（如果有）
                Dictionary<string, decimal>? fixedAmounts = null;
                if (task.CategoryFixedAmounts != null && task.CategoryFixedAmounts.Any())
                {
                    fixedAmounts = task.CategoryFixedAmounts;
                }

                var categoryBudgets = _budgetSplitter.SplitBudget(
                    task.TotalBudget,
                    categories,
                    fixedAmounts  // 传入固定金额配置
                );

                _logger.LogInformation("预算拆分完成: 总预算{TotalBudget:C2}", task.TotalBudget);
                foreach (var budget in categoryBudgets)
                {
                    var mode = budget.IsFixedAmount ? "固定金额" : "比例分配";
                    _logger.LogDebug("分类{CategoryCode}: 预算{Budget:C2}, 模式{Mode}",
                        budget.CategoryCode, budget.Budget, mode);
                }

                return new ScheduleResult(true)
                {
                    CategoryBudgets = categoryBudgets
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预算拆分失败");
                return new ScheduleResult(false, $"预算拆分失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 并行执行分类调度
        /// </summary>
        private async Task<List<CategorySchedule>> ExecuteCategorySchedulingAsync(
            List<CategoryBudget> categoryBudgets,
            List<Category> categories,
            DateTime startDate,
            DateTime endDate)
        {
            var tasks = new List<Task<CategorySchedule>>();

            foreach (var categoryBudget in categoryBudgets)
            {
                var task = ExecuteSingleCategorySchedulingAsync(
                    categoryBudget, categories, startDate, endDate);
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        /// <summary>
        /// 执行单个分类调度
        /// </summary>
        private async Task<CategorySchedule> ExecuteSingleCategorySchedulingAsync(
            CategoryBudget categoryBudget,
            List<Category> categories,
            DateTime startDate,
            DateTime endDate)
        {
            try
            {
                // 获取该分类的商品
                var products = await GetProductsByCategoryAsync(categoryBudget.CategoryCode);
                if (products == null || !products.Any())
                {
                    _logger.LogWarning("分类{CategoryCode}没有可用的商品", categoryBudget.CategoryCode);
                    return new CategorySchedule(categoryBudget.CategoryCode);
                }

                // 获取调度策略
                if (!_strategies.TryGetValue(categoryBudget.CategoryCode, out var strategy))
                {
                    // 使用默认策略
                    strategy = GetDefaultStrategy(categoryBudget.CategoryCode);
                }

                var categoryTaskBudget = new TaskCategoryBudget
                {
                    CategoryCode = categoryBudget.CategoryCode,
                    Ratio = categoryBudget.Ratio,
                    Budget = categoryBudget.Budget,
                    ExpectedCount = categoryBudget.ExpectedProductCount
                };

                var result = await strategy.ScheduleAsync(
                    categoryTaskBudget, products, startDate, endDate);

                _logger.LogInformation("分类{CategoryCode}调度完成: {ItemCount}条记录, 预算使用{BudgetUsed:C2}/{BudgetTotal:C2}",
                    categoryBudget.CategoryCode, result.Items.Count, result.UsedBudget, categoryBudget.Budget);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分类{CategoryCode}调度失败", categoryBudget.CategoryCode);
                return new CategorySchedule(categoryBudget.CategoryCode);
            }
        }

        /// <summary>
        /// 获取默认调度策略
        /// </summary>
        private ICategorySchedulingStrategy GetDefaultStrategy(string categoryCode)
        {
            return categoryCode switch
            {
                "vegetable" => new HighFrequencyScheduler(),
                "meat" => new HighFrequencyScheduler(),
                "egg" => new MediumFrequencyScheduler(2),
                "oil" => new LowFrequencyScheduler(7),
                "rice" => new LowFrequencyScheduler(5),
                "noodle" => new LowFrequencyScheduler(6),
                "seasoning" => new LowFrequencyScheduler(3),
                _ => new HighFrequencyScheduler()
            };
        }

        /// <summary>
        /// 合并每日调度数据（并保持同日不重复）
        /// </summary>
        private List<ScheduleItem> MergeDailySchedules(List<CategorySchedule> categorySchedules)
        {
            var allItems = new List<ScheduleItem>();

            foreach (var schedule in categorySchedules)
            {
                allItems.AddRange(schedule.Items);
            }

            // 按日期排序
            return allItems
                .GroupBy(i => new { i.Date.Date, i.Product?.Id })
                .Select(g => g.First())
                .OrderBy(item => item.Date)
                .ToList();
        }


        /// <summary>
        /// 验证调度结果
        /// </summary>
        private ScheduleResult ValidateSchedule(List<ScheduleItem> schedule, ProcurementTask task, List<CategoryBudget> categoryBudgets)
        {
            var errors = new List<string>();
            var context = CreateScheduleContext(schedule, task, categoryBudgets);

            foreach (var item in schedule)
            {
                foreach (var rule in _ruleEngine.GetRules())
                {
                    var result = rule.Validate(item, context);
                    if (!result.IsValid)
                    {
                        errors.Add($"{rule.RuleName}: {result.ErrorMessage}");
                    }
                }
            }

            if (errors.Any())
            {
                return new ScheduleResult(false, "规则验证失败")
                {
                    Errors = errors.Distinct().ToList()
                };
            }

            return new ScheduleResult(true, "规则验证通过");
        }

        /// <summary>
        /// 创建调度上下文
        /// </summary>
        private ScheduleContext CreateScheduleContext(List<ScheduleItem> schedule, ProcurementTask task, List<CategoryBudget> categoryBudgets)
        {
            var context = new ScheduleContext
            {
                TaskId = task.Id,
                ScheduleItems = schedule,
                ProductLastUsedDates = new Dictionary<int, DateTime>(),
                CategoryBudgets = categoryBudgets.ToDictionary(
                    b => b.CategoryCode,
                    b => new TaskCategoryBudget
                    {
                        CategoryCode = b.CategoryCode,
                        Budget = b.Budget,
                        Ratio = b.Ratio,
                        ExpectedCount = b.ExpectedProductCount
                    })
            };

            foreach (var item in schedule)
            {
                context.ProductLastUsedDates[item.Product.Id] = item.Date;
            }

            return context;
        }


        /// <summary>
        /// 获取所有分类（模拟数据访问）
        /// </summary>
        private Task<List<Category>> GetCategoriesAsync()
        {
            // 这里应该从数据库获取，现在返回模拟数据
            return Task.FromResult(new List<Category>
            {
                new Category { Id = 1, Code = "vegetable", Name = "蔬菜类", Ratio = 0.45m },
                new Category { Id = 2, Code = "meat", Name = "肉类", Ratio = 0.30m },
                new Category { Id = 3, Code = "egg", Name = "蛋类", Ratio = 0.05m },
                new Category { Id = 4, Code = "oil", Name = "食用油", Ratio = 0.048m },
                new Category { Id = 5, Code = "rice", Name = "米", Ratio = 0.104m },
                new Category { Id = 6, Code = "noodle", Name = "挂面粉条", Ratio = 0.036m },
                new Category { Id = 7, Code = "seasoning", Name = "调味品", Ratio = 0.012m }
            });
        }

        /// <summary>
        /// 按分类获取商品（模拟数据访问）
        /// </summary>
        private Task<List<Product>> GetProductsByCategoryAsync(string categoryCode)
        {
            // 这里应该从数据库获取，现在返回模拟数据
            var products = new List<Product>();

            switch (categoryCode)
            {
                case "vegetable":
                    products.AddRange(new[] {
                        new Product { Id = 1, Name = "白菜", Price = 2.50m, CategoryCode = "vegetable", Unit = "斤", MinIntervalDays = 2 },
                        new Product { Id = 2, Name = "土豆", Price = 2.70m, CategoryCode = "vegetable", Unit = "斤", MinIntervalDays = 3 },
                        new Product { Id = 3, Name = "西红柿", Price = 3.50m, CategoryCode = "vegetable", Unit = "斤", MinIntervalDays = 2 }
                    });
                    break;
                case "meat":
                    products.AddRange(new[] {
                        new Product { Id = 4, Name = "五花肉", Price = 28.00m, CategoryCode = "meat", Unit = "斤", MinIntervalDays = 4 },
                        new Product { Id = 5, Name = "鸡胸肉", Price = 18.00m, CategoryCode = "meat", Unit = "斤", MinIntervalDays = 3 }
                    });
                    break;
                case "egg":
                    products.Add(new Product { Id = 6, Name = "鸡蛋", Price = 6.00m, CategoryCode = "egg", Unit = "斤", MinIntervalDays = 2 });
                    break;
                default:
                    products.Add(new Product { Id = 7, Name = "默认商品", Price = 10.00m, CategoryCode = categoryCode, Unit = "斤", MinIntervalDays = 2 });
                    break;
            }

            return Task.FromResult(products);
        }
    }
}
