using System.Globalization;
using CanteenProcurement.Application.DTOs;
using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Core.Entities;

namespace CanteenProcurement.Application.Services;

public sealed class TaskPlanningService : ITaskPlanningService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;

    public TaskPlanningService(
        ITaskRepository taskRepository,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository)
    {
        _taskRepository = taskRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
    }

    public async Task<GeneratePlanResultDto> GenerateAsync(int taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, cancellationToken);
        if (task is null)
        {
            return new GeneratePlanResultDto
            {
                Success = false,
                TaskId = taskId,
                Message = "未找到指定任务。"
            };
        }

        var categories = (await _categoryRepository.GetActiveAsync(cancellationToken))
            .Where(category => category.Ratio > 0)
            .OrderBy(category => category.Sort)
            .ToList();
        var products = (await _productRepository.GetActiveAsync(cancellationToken)).ToList();

        if (categories.Count == 0)
        {
            return new GeneratePlanResultDto
            {
                Success = false,
                TaskId = taskId,
                Message = "没有启用且占比有效的分类，无法生成采购计划。"
            };
        }

        if (products.Count == 0)
        {
            return new GeneratePlanResultDto
            {
                Success = false,
                TaskId = taskId,
                Message = "没有可用商品，无法生成采购计划。"
            };
        }

        var fixedAmounts = await SafeGetFixedAmountsAsync(taskId, cancellationToken);
        var budgetPlans = BuildCategoryBudgetPlans(task.TotalBudget, categories, fixedAmounts);
        var random = new Random(unchecked(Environment.TickCount * 397) ^ taskId);
        var generatedSlots = new List<GeneratedDetailSlot>();
        var monthStart = task.GetStartDate();

        foreach (var plan in budgetPlans)
        {
            var categoryProducts = products
                .Where(product => string.Equals(product.CategoryCode, plan.Category.Code, StringComparison.OrdinalIgnoreCase) && product.Price > 0)
                .OrderBy(product => product.Price)
                .ToList();

            if (categoryProducts.Count == 0)
            {
                return new GeneratePlanResultDto
                {
                    Success = false,
                    TaskId = taskId,
                    Message = $"分类 {plan.Category.Name} 没有可用商品，无法生成采购计划。"
                };
            }

            var categorySlots = BuildCategorySlots(plan.Category, categoryProducts, monthStart, random, task.FloatRate);
            AllocateBudgetToSlots(categorySlots, plan.Budget, random);
            generatedSlots.AddRange(categorySlots);
        }

        if (generatedSlots.Count == 0)
        {
            return new GeneratePlanResultDto
            {
                Success = false,
                TaskId = taskId,
                Message = "没有生成任何有效采购明细，请检查分类频率和商品价格配置。"
            };
        }

        RebalanceTaskBudget(generatedSlots, budgetPlans, random);
        EnforceFixedAmountAccuracy(generatedSlots, budgetPlans, random);
        FinalPrecisionAdjustment(generatedSlots, task.TotalBudget);

        var details = generatedSlots
            .Select(slot => slot.ToEntity(task.Id))
            .Where(IsValidDetail)
            .OrderBy(detail => detail.PurchaseDate)
            .ThenBy(detail => detail.CategoryCode)
            .ThenBy(detail => detail.ProductId)
            .ToList();

        if (details.Count == 0)
        {
            return new GeneratePlanResultDto
            {
                Success = false,
                TaskId = taskId,
                Message = "生成结果为空，无法保存。"
            };
        }

        await _taskRepository.ReplaceDetailsAsync(task.Id, details, cancellationToken);
        await _taskRepository.UpdateStatusAsync(task.Id, Core.Entities.TaskStatus.Completed, cancellationToken);

        var actualTotalAmount = details.Sum(detail => detail.Amount);
        var culture = CultureInfo.GetCultureInfo("zh-CN");
        return new GeneratePlanResultDto
        {
            Success = true,
            TaskId = task.Id,
            GeneratedItemsCount = details.Count,
            ActualTotalAmount = actualTotalAmount,
            Message = $"生成成功，共 {details.Count} 条明细，总金额 {actualTotalAmount.ToString("C2", culture)}。"
        };
    }

    private async Task<Dictionary<string, decimal>> SafeGetFixedAmountsAsync(int taskId, CancellationToken cancellationToken)
    {
        try
        {
            if (!await _taskRepository.HasFixedAmountColumnsAsync(cancellationToken))
            {
                return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            }

            return await _taskRepository.GetFixedAmountsAsync(taskId, cancellationToken);
        }
        catch
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static List<GeneratedDetailSlot> BuildCategorySlots(
        Category category,
        List<Product> products,
        DateTime startDate,
        Random random,
        decimal floatRate)
    {
        var slots = new List<GeneratedDetailSlot>();
        var lastUsedDates = new Dictionary<int, DateTime>();

        foreach (var purchaseDate in GetPurchaseDates(startDate, category.FrequencyDays))
        {
            var minItems = Math.Max(1, category.DailyMinItems);
            var maxItems = Math.Max(minItems, category.DailyMaxItems);
            var pickCount = Math.Min(products.Count, random.Next(minItems, maxItems + 1));

            // 1. 筛选出当前可用的商品池
            // 填充品：间隔为1，永远可用
            // 特产品：间隔>=2，必须度过冷却期
            var readyProducts = products
                .Where(p => p.MinIntervalDays <= 1 || 
                           !lastUsedDates.TryGetValue(p.Id, out var lastDate) || 
                           (purchaseDate - lastDate).TotalDays >= p.MinIntervalDays)
                .ToList();

            // 2. 优先级排序：长间隔(>=14)的特产品如果就绪，优先安排；其余随机
            var orderedReady = readyProducts
                .OrderByDescending(p => p.MinIntervalDays >= 14)
                .ThenBy(_ => random.Next())
                .ToList();

            // 3. 选取商品：只从就绪池中选取
            // 注意：如果就绪池数量不足以满足 pickCount，我们也不再从冷却池抓取 >= 2 的商品
            // 这样可以确保长间隔设置（如25天）得到严格执行
            var selectedProducts = orderedReady.Take(pickCount).ToList();

            // 4. 更新记录最后使用日期并生成 Slots
            foreach (var product in selectedProducts)
            {
                lastUsedDates[product.Id] = purchaseDate;

                var weight = Math.Max(0.1m, 1m + (decimal)((random.NextDouble() * 2d - 1d) * (double)floatRate));
                slots.Add(new GeneratedDetailSlot
                {
                    CategoryCode = category.Code,
                    Product = product,
                    PurchaseDate = purchaseDate,
                    Weight = weight,
                    Quantity = 1
                });
            }
        }

        return slots;
    }

    private static void AllocateBudgetToSlots(List<GeneratedDetailSlot> slots, decimal categoryBudget, Random random)
    {
        if (slots.Count == 0) return;

        // 1. Check if budget covers minimum requirement (1 unit per slot)
        var totalMinCost = GetMinimumRequiredAmount(slots);
        if (categoryBudget < totalMinCost)
        {
            // Fallback: Just buy cheapest items until budget runs out
            var sorted = slots.OrderBy(s => s.Product.Price).ToList();
            foreach (var s in slots) s.Quantity = 0;
            decimal spent = 0;
            foreach (var s in sorted)
            {
                if (spent + s.Product.Price <= categoryBudget)
                {
                    s.Quantity = 1;
                    spent += s.Product.Price;
                }
            }
            return;
        }

        // 2. Even Distribution Strategy with 20% Random Fluctuation
        // Calculate target average amount per slot, then apply random fluctuation
        decimal targetAmountPerSlot = categoryBudget / slots.Count;

        foreach (var slot in slots)
        {
            // Apply 20% random fluctuation to the target amount
            // Fluctuation range: 0.8x to 1.2x of the average
            double fluctuation = 0.8 + (random.NextDouble() * 0.4); // 0.8 to 1.2
            decimal adjustedTarget = targetAmountPerSlot * (decimal)fluctuation;

            // Determine quantity that gets close to the adjusted target
            int qty = (int)(adjustedTarget / slot.Product.Price);
            if (qty < 1) qty = 1; // Must buy at least 1
            slot.Quantity = qty;
        }

        // 3. Distribute the remaining budget (rounding error surplus)
        decimal currentTotal = GetTotalAmount(slots);
        decimal remaining = categoryBudget - currentTotal;

        // Shuffle slots to distribute surplus randomly across different days/items
        var shuffledSlots = slots.OrderBy(x => random.Next()).ToList();

        int guard = 0;
        while (remaining > 0 && guard++ < 10000)
        {
            // Find slots that can accept +1 quantity within remaining budget
            var candidates = shuffledSlots.Where(s => s.Product.Price <= remaining).ToList();
            if (candidates.Count == 0) break; // Remaining amount is less than cheapest item

            // Pick a random candidate to add +1
            var pick = candidates[random.Next(candidates.Count)];
            pick.Quantity++;
            remaining -= pick.Product.Price;
        }

        // 4. Correction: If "Min Qty = 1" forced us over budget, trim the fat
        // (Remove quantity from most expensive items first to save budget)
        if (GetTotalAmount(slots) > categoryBudget)
        {
            var reducible = slots.Where(s => s.Quantity > 1).OrderByDescending(s => s.Product.Price).ToList();
            foreach (var slot in reducible)
            {
                if (GetTotalAmount(slots) <= categoryBudget) break;
                slot.Quantity--;
            }
        }
    }

    private static void RebalanceTaskBudget(List<GeneratedDetailSlot> slots, List<CategoryBudgetPlan> budgetPlans, Random random)
    {
        var target = Math.Round(budgetPlans.Sum(plan => plan.Budget), 1, MidpointRounding.AwayFromZero);
        var current = GetTotalAmount(slots);
        var gap = Math.Round(target - current, 1, MidpointRounding.AwayFromZero);
        if (Math.Abs(gap) < 1m)
        {
            return;
        }

        var categoryCaps = budgetPlans.ToDictionary(plan => plan.Category.Code, plan => plan.Budget, StringComparer.OrdinalIgnoreCase);
        var guard = 0;
        while (Math.Abs(gap) >= 1m && guard++ < 2000)
        {
            if (gap > 0)
            {
                var affordable = slots
                    .Select(slot => new { Slot = slot, Increment = slot.GetNextIncrement() })
                    .Where(item => item.Increment > 0 && CanIncreaseCategoryBudget(item.Slot.CategoryCode, item.Increment, slots, categoryCaps))
                    .OrderBy(item => Math.Abs(item.Increment - gap))
                    .ThenByDescending(item => item.Slot.Weight)
                    .ToList();

                if (affordable.Count == 0)
                {
                    break;
                }

                var selected = affordable[0];
                if (affordable.Count > 1 && random.NextDouble() > 0.7d)
                {
                    selected = affordable[random.Next(Math.Min(affordable.Count, 3))];
                }

                selected.Slot.Quantity++;
                gap = Math.Round(target - GetTotalAmount(slots), 1, MidpointRounding.AwayFromZero);
            }
            else
            {
                var reducible = slots
                    .Where(slot => slot.Quantity > 1)
                    .Select(slot => new { Slot = slot, Decrement = slot.Product.Price })
                    .OrderBy(item => Math.Abs(item.Decrement - Math.Abs(gap)))
                    .ThenBy(item => item.Slot.Weight)
                    .ToList();

                if (reducible.Count == 0)
                {
                    break;
                }

                reducible[0].Slot.Quantity--;
                gap = Math.Round(target - GetTotalAmount(slots), 1, MidpointRounding.AwayFromZero);
            }
        }
    }

    private static void FinalPrecisionAdjustment(List<GeneratedDetailSlot> slots, decimal targetBudget)
    {
        if (slots.Count == 0)
        {
            return;
        }

        var currentTotal = Math.Round(slots.Sum(slot => slot.GetAmount()), 2, MidpointRounding.AwayFromZero);
        var gap = Math.Round(targetBudget - currentTotal, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(gap) < 1m)
        {
            return;
        }

        var orderedSlots = slots.OrderBy(slot => slot.Product.Price).ToList();
        var guard = 0;
        while (Math.Abs(gap) >= 1m && guard++ < 1000)
        {
            if (gap > 0)
            {
                var candidate = orderedSlots
                    .Select(slot => new { Slot = slot, Increment = slot.GetNextIncrement() })
                    .Where(item => item.Increment > 0)
                    .OrderBy(item => Math.Abs(item.Increment - gap))
                    .FirstOrDefault();

                if (candidate is null)
                {
                    break;
                }

                candidate.Slot.Quantity++;
            }
            else
            {
                var candidate = orderedSlots
                    .Where(slot => slot.Quantity > 1)
                    .OrderBy(slot => Math.Abs(slot.Product.Price - Math.Abs(gap)))
                    .FirstOrDefault();

                if (candidate is null)
                {
                    break;
                }

                candidate.Quantity--;
            }

            currentTotal = Math.Round(slots.Sum(slot => slot.GetAmount()), 2, MidpointRounding.AwayFromZero);
            gap = Math.Round(targetBudget - currentTotal, 2, MidpointRounding.AwayFromZero);
        }
    }

    private static List<CategoryBudgetPlan> BuildCategoryBudgetPlans(
        decimal totalBudget,
        IReadOnlyList<Category> categories,
        IReadOnlyDictionary<string, decimal> fixedAmounts)
    {
        var plans = new List<CategoryBudgetPlan>();
        var normalizedTotalBudget = Math.Round(totalBudget, 1, MidpointRounding.AwayFromZero);
        var fixedTotal = 0m;

        foreach (var category in categories)
        {
            if (fixedAmounts.TryGetValue(category.Code, out var fixedAmount) && fixedAmount > 0)
            {
                var normalizedAmount = Math.Round(fixedAmount, 1, MidpointRounding.AwayFromZero);
                fixedTotal += normalizedAmount;
                plans.Add(new CategoryBudgetPlan(category, normalizedAmount, true));
            }
        }

        if (fixedTotal > normalizedTotalBudget)
        {
            throw new InvalidOperationException($"固定金额总额 {fixedTotal:F2} 超过任务预算 {normalizedTotalBudget:F2}。");
        }

        var variableCategories = categories
            .Where(category => plans.All(plan => !string.Equals(plan.Category.Code, category.Code, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (variableCategories.Count == 0)
        {
            return plans;
        }

        var remainingBudget = Math.Round(normalizedTotalBudget - fixedTotal, 1, MidpointRounding.AwayFromZero);
        var ratioSum = variableCategories.Sum(category => category.Ratio);
        if (ratioSum <= 0)
        {
            throw new InvalidOperationException("分类占比无效，无法进行预算分配。");
        }

        var distributable = remainingBudget;
        var remainingRatio = ratioSum;
        for (var index = 0; index < variableCategories.Count; index++)
        {
            var category = variableCategories[index];
            var budget = index == variableCategories.Count - 1
                ? distributable
                : Math.Round(distributable * (category.Ratio / remainingRatio), 1, MidpointRounding.AwayFromZero);
            plans.Add(new CategoryBudgetPlan(category, budget, false));
            distributable = Math.Max(0, Math.Round(distributable - budget, 1, MidpointRounding.AwayFromZero));
            remainingRatio -= category.Ratio;
        }

        return plans;
    }

    private static List<DateTime> GetPurchaseDates(DateTime startDate, int frequencyDays)
    {
        var dates = new List<DateTime>();
        var step = Math.Max(1, frequencyDays);
        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        for (var day = 1; day <= daysInMonth; day += step)
        {
            dates.Add(new DateTime(startDate.Year, startDate.Month, day));
        }

        return dates;
    }

    private static void EnforceFixedAmountAccuracy(List<GeneratedDetailSlot> slots, List<CategoryBudgetPlan> budgetPlans, Random random)
    {
        // Specifically target Fixed Amount categories to ensure high precision (Error <= 2)
        foreach (var plan in budgetPlans.Where(p => p.IsFixed))
        {
            var catSlots = slots.Where(s => s.CategoryCode == plan.Category.Code).ToList();
            if (catSlots.Count == 0) continue;

            decimal target = plan.Budget;
            decimal current = GetTotalAmount(catSlots);
            decimal diff = target - current;

            int attempts = 0;
            while (Math.Abs(diff) > 0.5m && attempts++ < 1000)
            {
                if (diff > 0)
                {
                    // Need to add amount: Add +1 to the cheapest slot in this category
                    var slot = catSlots.OrderBy(s => s.Product.Price).First();
                    slot.Quantity++;
                    diff -= slot.Product.Price;
                }
                else
                {
                    // Need to reduce amount: Remove -1 from the most expensive slot (if Qty > 1)
                    var slot = catSlots.OrderByDescending(s => s.Product.Price).FirstOrDefault(s => s.Quantity > 1);
                    if (slot == null) break; // Can't reduce further
                    slot.Quantity--;
                    diff += slot.Product.Price;
                }
            }
        }
    }

    private static decimal GetMinimumRequiredAmount(IEnumerable<GeneratedDetailSlot> slots)
    {
        return Math.Round(slots.Sum(slot => GeneratedDetailSlot.CalculateAmount(slot.Product.Price, 1)), 1, MidpointRounding.AwayFromZero);
    }

    private static decimal GetTotalAmount(IEnumerable<GeneratedDetailSlot> slots)
    {
        return Math.Round(slots.Sum(slot => slot.GetAmount()), 1, MidpointRounding.AwayFromZero);
    }

    private static bool CanIncreaseCategoryBudget(
        string categoryCode,
        decimal increment,
        IEnumerable<GeneratedDetailSlot> slots,
        IReadOnlyDictionary<string, decimal> categoryCaps)
    {
        if (!categoryCaps.TryGetValue(categoryCode, out var cap))
        {
            return false;
        }

        var currentAmount = Math.Round(
            slots.Where(slot => string.Equals(slot.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
                .Sum(slot => slot.GetAmount()),
            1,
            MidpointRounding.AwayFromZero);

        return currentAmount + increment <= cap;
    }

    private static bool IsValidDetail(ProcurementDetail detail)
    {
        return detail.ProductId > 0
            && !string.IsNullOrWhiteSpace(detail.CategoryCode)
            && detail.Price > 0
            && detail.Quantity > 0
            && detail.Amount > 0;
    }

    private sealed class GeneratedDetailSlot
    {
        public string CategoryCode { get; init; } = string.Empty;
        public Product Product { get; init; } = new();
        public DateTime PurchaseDate { get; init; }
        public decimal Weight { get; init; }
        public int Quantity { get; set; }

        public decimal GetAmount()
        {
            return CalculateAmount(Product.Price, Quantity);
        }

        public decimal GetNextIncrement()
        {
            var currentAmount = GetAmount();
            var nextAmount = CalculateAmount(Product.Price, Quantity + 1);
            return Math.Round(nextAmount - currentAmount, 1, MidpointRounding.AwayFromZero);
        }

        public ProcurementDetail ToEntity(int taskId)
        {
            return new ProcurementDetail
            {
                TaskId = taskId,
                CategoryCode = CategoryCode,
                ProductId = Product.Id,
                Product = Product,
                PurchaseDate = PurchaseDate,
                Price = Product.Price,
                Quantity = Quantity,
                Amount = GetAmount()
            };
        }

        public static decimal CalculateAmount(decimal price, int quantity)
        {
            if (quantity <= 0)
            {
                return 0;
            }

            return Math.Round(price * quantity, 1, MidpointRounding.AwayFromZero);
        }
    }

    private sealed record CategoryBudgetPlan(Category Category, decimal Budget, bool IsFixed);
}
