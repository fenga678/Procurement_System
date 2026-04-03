using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Core.Entities;
using CanteenProcurement.Wpf.Dialogs;
using CanteenProcurement.Wpf.Services;
using Microsoft.Win32;

namespace CanteenProcurement.Wpf.Views
{
    public partial class TaskManagementView : UserControl
    {
        private TaskDataService? _dataService;
        private CategoryDataService? _categoryDataService;
        private readonly SchemaCapabilitiesProvider _schemaProvider = new();
        private bool _isLoading;

        public ObservableCollection<TaskItem> Tasks { get; set; } = new();
        public TaskItem? SelectedTask { get; set; }
        public int SelectedStatus { get; set; } = -1;
        private bool _isApplyingFilters;

        public TaskManagementView()
        {
            InitializeComponent();
            DataContext = this;
            try
            {
                _dataService = new TaskDataService(_schemaProvider);
                _categoryDataService = new CategoryDataService(_schemaProvider);
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(null, "数据库", $"初始化数据库连接失败：{ex.Message}");
            }

            Loaded += TaskManagementView_Loaded;
        }

        private async void TaskManagementView_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadTaskDataAsync();
        }

        private async System.Threading.Tasks.Task LoadTaskDataAsync()
        {
            if (_dataService == null || _isLoading)
            {
                return;
            }

            _isLoading = true;
            ShellFeedbackService.ShowLoading("正在加载任务数据...");
            try
            {
                var data = await _dataService.GetTasksAsync();
                Tasks.Clear();
                foreach (var item in data)
                {
                    Tasks.Add(new TaskItem
                    {
                        Id = item.Id,
                        YearMonth = item.YearMonth,
                        TotalBudget = item.TotalBudget,
                        FloatRate = item.FloatRate,
                        Status = item.Status,
                        StatusDescription = GetStatusDescription(item.Status),
                        StatusTone = GetStatusTone(item.Status),
                        GenerateActionText = item.Status == 0 ? "生成" : "再次生成",
                        GeneratedAt = item.GeneratedAt,
                        CreatedBy = string.IsNullOrWhiteSpace(item.CreatedBy) ? "-" : item.CreatedBy,
                        CreatedAt = item.CreatedAt,
                        UpdatedAt = item.UpdatedAt,
                        CanGenerate = item.Status == 0,
                        CanDelete = item.Status == 0
                    });
                }

                dgTasks.ItemsSource = Tasks;
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(GetOwnerWindow(), "数据库", $"加载任务失败：{ex.Message}");
            }
            finally
            {
                _isLoading = false;
                ShellFeedbackService.HideLoading();
            }
        }

        private static string GetStatusDescription(int status) => status switch
        {
            0 => "待生成",
            1 => "已完成",
            2 => "已取消",
            _ => "未知"
        };

        private static string GetStatusTone(int status) => status switch
        {
            0 => "warning",
            1 => "success",
            2 => "danger",
            _ => "info"
        };

        private async void btnCreateTask_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "数据库", "数据库未初始化，无法创建任务。");
                return;
            }

            // 检查试用版任务数量限制
            var license = LicensingService.Instance;
            var tasks = await _dataService.GetTasksAsync();
            var (allowed, maxTasks) = license.CheckTaskLimit(tasks.Count);
            if (!allowed)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "试用版限制", 
                    $"试用版最多只能创建 {maxTasks} 个任务，请注册后继续使用。");
                return;
            }

            if (_isLoading)
            {
                return;
            }

            if (TryShowCreateTaskDialog(out var request) && request != null)
            {
                ShellFeedbackService.ShowLoading("正在创建任务...");
                try
                {
                    var newId = await _dataService.CreateTaskAsync(new TaskRecord
                    {
                        YearMonth = request.YearMonth,
                        TotalBudget = request.TotalBudget,
                        FloatRate = request.FloatRate,
                        Status = 0,
                        GeneratedAt = null,
                        CreatedBy = request.CreatedBy
                    });

                    // 保存固定金额配置
                    if (request.CategoryFixedAmounts != null && request.CategoryFixedAmounts.Any())
                    {
                        try
                        {
                            var hasFixedAmountColumns = await _dataService.HasFixedAmountColumnsAsync();
                            if (!hasFixedAmountColumns)
                            {
                                AppDialogService.ShowWarning(GetOwnerWindow(), "数据库升级提示", "数据库缺少固定金额功能的字段，请执行 database/04_add_fixed_amount_feature.sql 脚本进行升级。");
                            }
                            else
                            {
                                await _dataService.SaveTaskFixedAmountsAsync(newId, request.CategoryFixedAmounts);
                            }
                        }
                        catch (Exception ex)
                        {
                            AppDialogService.ShowWarning(GetOwnerWindow(), "保存固定金额配置失败", $"保存固定金额配置时出现错误：{ex.Message}");
                        }
                    }

                    ShellFeedbackService.ShowSuccess($"任务已创建，任务编号：{newId}");
                    await LoadTaskDataAsync();
                }
                catch (Exception ex) when (ex.Message.Contains("Duplicate") || ex.Message.Contains("duplicate") || ex.Message.Contains("UNIQUE"))
                {
                    AppDialogService.ShowWarning(GetOwnerWindow(), "任务重复", "该年月的任务已存在，请勿重复创建。");
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "创建失败", $"创建任务失败：{ex.Message}");
                }
                finally
                {
                    ShellFeedbackService.HideLoading();
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isApplyingFilters)
                return;
            ApplyFilters();
        }

        private void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingFilters || !IsLoaded)
                return;
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            _isApplyingFilters = true;
            try
            {
                if (Tasks.Count == 0)
                {
                    dgTasks.ItemsSource = Tasks;
                    return;
                }

                var keyword = GetSearchBox()?.Text?.Trim();
                int? statusFilter = null;
                var statusCombo = GetStatusCombo();
                if (statusCombo?.SelectedItem is ComboBoxItem selected && int.TryParse(selected.Tag?.ToString(), out var statusValue) && statusValue >= 0)
                {
                    statusFilter = statusValue;
                }

                var query = Tasks.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(t => t.YearMonth.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                          || t.Id.ToString().Equals(keyword, StringComparison.OrdinalIgnoreCase));
                }

                if (statusFilter.HasValue)
                {
                    query = query.Where(t => t.Status == statusFilter.Value);
                }

                var filtered = query.ToList();
                dgTasks.ItemsSource = filtered.Count == Tasks.Count && statusFilter == null && string.IsNullOrWhiteSpace(keyword)
                    ? Tasks
                    : filtered;
            }
            finally
            {
                _isApplyingFilters = false;
            }
        }

        private async void btnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (dgTasks.SelectedItem is not TaskItem item)
            {
                ShellFeedbackService.ShowInfo("请先选择要导出的任务。", "导出提醒");
                return;
            }

            var details = await _dataService.GetTaskDetailsAsync(item.Id);
            if (details.Count == 0)
            {
                ShellFeedbackService.ShowInfo("该任务暂无生成明细，暂时无法导出。", "导出提醒");
                return;
            }

            // 检查试用版导出限制
            var license = LicensingService.Instance;
            var maxExportRows = license.GetExportRowLimit();
            var exportDetails = details;
            if (details.Count > maxExportRows)
            {
                exportDetails = details.Take(maxExportRows).ToList();
                ShellFeedbackService.ShowInfo($"试用版仅导出前 {maxExportRows} 条数据。", "试用版限制");
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"task_{item.Id}_{item.YearMonth}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("序号,分类,商品名称,单位,单价,数量,金额,日期");
                    var ordered = exportDetails.OrderBy(d => d.PurchaseDate).ToList();
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var d = ordered[i];
                        var catName = string.IsNullOrWhiteSpace(d.CategoryName) ? d.CategoryCode : d.CategoryName;
                        var prodName = string.IsNullOrWhiteSpace(d.ProductName) ? $"商品{d.ProductId}" : d.ProductName;
                        var unit = string.IsNullOrWhiteSpace(d.Unit) ? string.Empty : d.Unit;
                        var qtyStr = Math.Round(d.Quantity, 0, MidpointRounding.AwayFromZero).ToString("0");
                        var amtStr = Math.Round(d.Amount, 1, MidpointRounding.AwayFromZero).ToString("0.0");
                        sb.AppendLine($"{i + 1},{catName},\"{prodName}\",{unit},{d.Price},{qtyStr},{amtStr},{d.PurchaseDate:yyyy-MM-dd}");
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    ShellFeedbackService.ShowSuccess("任务明细已导出为 CSV 文件。", "导出完成");
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "导出失败", $"导出失败：{ex.Message}");
                }
            }
        }

        private async void dgTasks_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgTasks.SelectedItem is TaskItem item)
            {
                await ShowTaskDetailsAsync(item);
            }
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (sender is Button btn && btn.DataContext is TaskItem item)
            {
                if (!AppDialogService.Confirm(GetOwnerWindow(), "确认生成", $"确认对任务 {item.YearMonth} 执行生成？"))
                {
                    return;
                }

                ShellFeedbackService.ShowLoading($"正在为 {item.YearMonth} 生成采购计划...");
                try
                {
                    var result = await GeneratePlanAsync(item);
                    ShellFeedbackService.ShowSuccess(result, "生成完成");
                    await LoadTaskDataAsync();
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "生成失败", $"生成失败：{ex.Message}");
                }
                finally
                {
                    ShellFeedbackService.HideLoading();
                }
            }
        }

        private async System.Threading.Tasks.Task<string> GeneratePlanAsync(TaskItem task)

        {
            if (_dataService == null) return "数据服务未初始化";

            var categories = (await _dataService.GetActiveCategoriesAsync())
                .Where(c => c.Ratio > 0)
                .ToList();
            var products = await _dataService.GetActiveProductsAsync();
            if (categories.Count == 0) throw new InvalidOperationException("无有效分类占比，无法生成计划。");
            if (products.Count == 0) throw new InvalidOperationException("无单价大于 0 的启用商品，无法生成计划。");

            // 读取固定金额配置
            Dictionary<string, decimal> fixedAmounts = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                var hasFixedAmountColumns = await _dataService.HasFixedAmountColumnsAsync();
                if (hasFixedAmountColumns)
                {
                    fixedAmounts = await _dataService.GetTaskFixedAmountsAsync(task.Id);
                }
            }
            catch (Exception ex)
            {
                // 忽略固定金额读取错误，继续按比例分配
                Console.WriteLine($"读取固定金额配置失败: {ex.Message}");
            }

            var budgetPlans = BuildCategoryBudgetPlans(task.TotalBudget, categories, fixedAmounts);

            var rand = new Random();
            var generatedSlots = new List<GeneratedDetailSlot>();
            var startDate = ParseYearMonthStart(task.YearMonth);

            foreach (var plan in budgetPlans)
            {
                var catProducts = products
                    .Where(p => p.CategoryCode == plan.Category.Code && p.Price > 0)
                    .OrderBy(p => p.Price)
                    .ToList();

                if (catProducts.Count == 0)
                {
                    throw new InvalidOperationException($"分类 {plan.Category.Code} 没有可用的有效商品，请检查启用状态和单价。");
                }

                var categorySlots = BuildCategorySlots(plan.Category, catProducts, startDate, rand, task.FloatRate);
                AllocateBudgetToSlots(categorySlots, plan.Budget, rand);
                generatedSlots.AddRange(categorySlots);
            }

            if (generatedSlots.Count == 0)
            {
                throw new InvalidOperationException("未生成任何有效明细，请检查分类预算、采购频率和商品单价配置。");
            }

            RebalanceTaskBudget(generatedSlots, budgetPlans, rand);
            
            // 最终精确校正：确保总金额精确等于目标预算
            FinalPrecisionAdjustment(generatedSlots, task.TotalBudget);

            var details = generatedSlots
                .Select(slot => slot.ToRecord(task.Id))
                .Where(IsValidDetail)
                .OrderBy(d => d.PurchaseDate)
                .ThenBy(d => d.CategoryCode)
                .ThenBy(d => d.ProductId)
                .ToList();

            if (details.Count == 0)
            {
                throw new InvalidOperationException("未生成任何可保存的有效明细，请检查商品单价、预算和分类配置。");
            }

            await _dataService.ReplaceTaskDetailsAsync(task.Id, details);
            await _dataService.UpdateStatusAsync(task.Id, 1);

            var culture = CultureInfo.GetCultureInfo("zh-CN");
            var totalAmount = details.Sum(d => d.Amount);
            var gap = Math.Round(task.TotalBudget - totalAmount, 1, MidpointRounding.AwayFromZero);
            return $"生成成功：{details.Count} 条明细，总金额 {totalAmount.ToString("C2", culture)}，与预算差额 {gap.ToString("C2", culture)}";
        }

        private static List<GeneratedDetailSlot> BuildCategorySlots(CategoryBudgetRecord category, List<ProductPriceRecord> products, DateTime startDate, Random rand, decimal floatRate)
        {
            var slots = new List<GeneratedDetailSlot>();
            foreach (var purchaseDate in GetPurchaseDates(startDate, category.FrequencyDays))
            {
                var minItems = Math.Max(1, category.DailyMinItems);
                var maxItems = Math.Max(minItems, category.DailyMaxItems);
                var pickCount = Math.Min(products.Count, rand.Next(minItems, maxItems + 1));
                var selectedProducts = products
                    .OrderBy(_ => rand.Next())
                    .Take(pickCount)
                    .ToList();

                foreach (var product in selectedProducts)
                {
                    var randomWeight = (decimal)(0.6 + rand.NextDouble() * 1.4);
                    var fluctuationWeight = Math.Max(0.10m, 1 + (decimal)((rand.NextDouble() * 2 - 1) * (double)floatRate));
                    slots.Add(new GeneratedDetailSlot
                    {
                        CategoryCode = category.Code,
                        Product = product,
                        PurchaseDate = purchaseDate,
                        Weight = Math.Max(0.10m, randomWeight * fluctuationWeight)
                    });
                }
            }

            return slots;
        }

        private static void AllocateBudgetToSlots(List<GeneratedDetailSlot> slots, decimal categoryBudget, Random rand)
        {
            slots.RemoveAll(slot => slot.Product.Price <= 0);
            if (slots.Count == 0 || categoryBudget <= 0)
            {
                slots.Clear();
                return;
            }

            categoryBudget = Math.Round(categoryBudget, 1, MidpointRounding.AwayFromZero);
            while (slots.Count > 0 && GetMinimumRequiredAmount(slots) > categoryBudget)
            {
                var removable = slots
                    .GroupBy(slot => slot.PurchaseDate)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.OrderByDescending(slot => slot.Product.Price).First())
                    .OrderByDescending(slot => slot.Product.Price)
                    .FirstOrDefault()
                    ?? slots.OrderByDescending(slot => slot.Product.Price).First();

                slots.Remove(removable);
            }

            if (slots.Count == 0)
            {
                return;
            }

            foreach (var slot in slots)
            {
                slot.Quantity = 1;
            }

            var remainingBudget = Math.Round(categoryBudget - GetTotalAmount(slots), 1, MidpointRounding.AwayFromZero);
            var guard = 0;
            while (remainingBudget > 0 && guard++ < 200000)
            {
                var affordable = slots
                    .Select(slot => new { Slot = slot, Increment = slot.GetNextIncrement() })
                    .Where(x => x.Increment > 0 && x.Increment <= remainingBudget)
                    .ToList();
                if (affordable.Count == 0)
                {
                    break;
                }

                var totalWeight = affordable.Sum(x => Math.Max(0.1m, x.Slot.Weight / x.Increment));
                var threshold = (decimal)rand.NextDouble() * totalWeight;
                decimal cumulative = 0;
                var selected = affordable[^1];
                foreach (var option in affordable)
                {
                    cumulative += Math.Max(0.1m, option.Slot.Weight / option.Increment);
                    if (threshold <= cumulative)
                    {
                        selected = option;
                        break;
                    }
                }

                selected.Slot.Quantity++;
                remainingBudget = Math.Round(remainingBudget - selected.Increment, 1, MidpointRounding.AwayFromZero);
            }
        }

        private static void RebalanceTaskBudget(List<GeneratedDetailSlot> slots, List<CategoryBudgetPlan> budgetPlans, Random rand)
        {
            if (slots.Count == 0 || budgetPlans.Count == 0)
            {
                return;
            }

            var categoryCaps = budgetPlans.ToDictionary(p => p.Category.Code, p => p.Budget, StringComparer.OrdinalIgnoreCase);
            var targetBudget = Math.Round(categoryCaps.Values.Sum(), 1, MidpointRounding.AwayFromZero);
            var remainingGap = Math.Round(targetBudget - GetTotalAmount(slots), 1, MidpointRounding.AwayFromZero);
            var guard = 0;
            while (remainingGap > 0 && guard++ < 200000)
            {
                var affordable = slots
                    .Select(slot => new { Slot = slot, Increment = slot.GetNextIncrement() })
                    .Where(x => x.Increment > 0
                                && x.Increment <= remainingGap
                                && CanIncreaseCategoryBudget(x.Slot.CategoryCode, x.Increment, slots, categoryCaps))
                    .OrderBy(x => x.Increment)
                    .ThenByDescending(x => x.Slot.Weight / (x.Slot.Quantity + 1m))
                    .ToList();
                if (affordable.Count == 0)
                {
                    break;
                }

                var selected = affordable[0];
                if (affordable.Count > 1 && rand.NextDouble() > 0.7)
                {
                    selected = affordable[rand.Next(Math.Min(affordable.Count, 3))];
                }

                selected.Slot.Quantity++;
                remainingGap = Math.Round(remainingGap - selected.Increment, 1, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        /// 最终精确校正：确保总金额精确等于目标预算（误差控制在1元以内）
        /// </summary>
        private static void FinalPrecisionAdjustment(List<GeneratedDetailSlot> slots, decimal targetBudget)
        {
            if (slots.Count == 0)
            {
                return;
            }

            // 使用更高精度计算当前总金额
            var currentTotal = Math.Round(slots.Sum(s => s.GetAmount()), 2, MidpointRounding.AwayFromZero);
            var gap = Math.Round(targetBudget - currentTotal, 2, MidpointRounding.AwayFromZero);
            
            // 如果误差已经在1元以内，无需调整
            if (Math.Abs(gap) < 1m)
            {
                return;
            }

            // 按价格排序，方便找到最佳调整项
            var sortedSlots = slots.OrderBy(s => s.Product.Price).ToList();
            var guard = 0;
            
            while (Math.Abs(gap) >= 1m && guard++ < 1000)
            {
                if (gap > 0)
                {
                    // 需要增加金额：优先找一个增量最接近gap的项
                    var candidates = sortedSlots
                        .Select(s => new { Slot = s, Increment = s.GetNextIncrement() })
                        .Where(x => x.Increment > 0)
                        .ToList();

                    if (candidates.Count == 0)
                    {
                        break;
                    }

                    // 优先选择增量不超过gap+2元的项（允许小量超调）
                    var bestMatch = candidates
                        .Where(x => x.Increment <= gap + 2m)
                        .OrderBy(x => Math.Abs(x.Increment - gap))
                        .ThenByDescending(x => x.Slot.Weight)
                        .FirstOrDefault();

                    if (bestMatch == null)
                    {
                        // 如果没有合适的选择，选择增量最小的项
                        bestMatch = candidates.OrderBy(x => x.Increment).First();
                    }

                    bestMatch.Slot.Quantity++;
                    currentTotal = Math.Round(slots.Sum(s => s.GetAmount()), 2, MidpointRounding.AwayFromZero);
                    gap = Math.Round(targetBudget - currentTotal, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    // 需要减少金额
                    var reducibleCandidates = sortedSlots
                        .Where(s => s.Quantity > 1)
                        .Select(s => new { Slot = s, Decrement = s.Product.Price })
                        .ToList();

                    if (reducibleCandidates.Count == 0)
                    {
                        // 如果没有可减少的项（所有数量都为1），则无法继续减少
                        break;
                    }

                    // 优先选择减少金额接近|gap|的项
                    var targetDecrement = Math.Abs(gap);
                    var bestReduce = reducibleCandidates
                        .Where(x => x.Decrement >= targetDecrement - 2m && x.Decrement <= targetDecrement + 2m)
                        .OrderBy(x => Math.Abs(x.Decrement - targetDecrement))
                        .ThenBy(x => x.Slot.Weight)
                        .FirstOrDefault();

                    if (bestReduce == null)
                    {
                        // 如果没有精确匹配，选择金额最接近的
                        bestReduce = reducibleCandidates
                            .OrderBy(x => Math.Abs(x.Decrement - targetDecrement))
                            .First();
                    }

                    bestReduce.Slot.Quantity--;
                    currentTotal = Math.Round(slots.Sum(s => s.GetAmount()), 2, MidpointRounding.AwayFromZero);
                    gap = Math.Round(targetBudget - currentTotal, 2, MidpointRounding.AwayFromZero);
                }
            }
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
                slots.Where(s => string.Equals(s.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase))
                    .Sum(s => s.GetAmount()),
                1,
                MidpointRounding.AwayFromZero);

            return currentAmount + increment <= cap;
        }

        private static List<CategoryBudgetPlan> BuildCategoryBudgetPlans(
            decimal totalBudget,
            List<CategoryBudgetRecord> categories,
            Dictionary<string, decimal> fixedAmounts)
        {
            var plans = new List<CategoryBudgetPlan>();
            var normalizedTotalBudget = Math.Round(totalBudget, 1, MidpointRounding.AwayFromZero);

            var fixedTotal = 0m;
            foreach (var cat in categories)
            {
                if (fixedAmounts != null && fixedAmounts.TryGetValue(cat.Code, out var fixedAmount) && fixedAmount > 0)
                {
                    var normalizedFixed = Math.Round(fixedAmount, 1, MidpointRounding.AwayFromZero);
                    fixedTotal += normalizedFixed;
                    plans.Add(new CategoryBudgetPlan(cat, normalizedFixed, true));
                }
            }

            if (fixedTotal > normalizedTotalBudget)
            {
                throw new InvalidOperationException($"固定金额总额({fixedTotal:C2})超过任务总预算({normalizedTotalBudget:C2})。");
            }

            var nonFixedCategories = categories
                .Where(c => plans.All(p => !string.Equals(p.Category.Code, c.Code, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            var remainingBudget = Math.Round(normalizedTotalBudget - fixedTotal, 1, MidpointRounding.AwayFromZero);

            if (nonFixedCategories.Count == 0)
            {
                return plans;
            }

            var ratioSum = nonFixedCategories.Sum(c => c.Ratio);
            if (ratioSum <= 0)
            {
                throw new InvalidOperationException("非固定分类占比总和无效，请检查分类配置。");
            }

            var distributable = remainingBudget;
            var remainingRatio = ratioSum;
            for (int i = 0; i < nonFixedCategories.Count; i++)
            {
                var cat = nonFixedCategories[i];
                var budget = i == nonFixedCategories.Count - 1
                    ? distributable
                    : Math.Round(distributable * (cat.Ratio / remainingRatio), 1, MidpointRounding.AwayFromZero);
                plans.Add(new CategoryBudgetPlan(cat, budget, false));
                distributable = Math.Max(0, Math.Round(distributable - budget, 1, MidpointRounding.AwayFromZero));
                remainingRatio -= cat.Ratio;
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

        private static decimal GetMinimumRequiredAmount(IEnumerable<GeneratedDetailSlot> slots)
        {
            return Math.Round(slots.Sum(slot => GeneratedDetailSlot.CalculateAmount(slot.Product.Price, 1)), 1, MidpointRounding.AwayFromZero);
        }

        private static decimal GetTotalAmount(IEnumerable<GeneratedDetailSlot> slots)
        {
            return Math.Round(slots.Sum(slot => slot.GetAmount()), 1, MidpointRounding.AwayFromZero);
        }

        private static bool IsValidDetail(ProcurementDetailRecord detail)
        {
            return detail.ProductId > 0
                && !string.IsNullOrWhiteSpace(detail.CategoryCode)
                && detail.Price > 0
                && detail.Quantity > 0
                && detail.Amount > 0;
        }

        private static DateTime ParseYearMonthStart(string ym)
        {
            var year = int.Parse(ym.Substring(0, 4));
            var month = int.Parse(ym.Substring(4, 2));
            return new DateTime(year, month, 1);
        }

        private async void btnView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TaskItem item)
            {
                await ShowTaskDetailsAsync(item);
            }
        }

        private async System.Threading.Tasks.Task ShowTaskDetailsAsync(TaskItem item)
        {
            if (_dataService == null)
            {
                return;
            }

            var details = await _dataService.GetTaskDetailsAsync(item.Id);
            if (details.Count == 0)
            {
                ShellFeedbackService.ShowInfo("该任务暂未生成采购明细。", "明细提醒");
                return;
            }

            var display = details
                .OrderBy(d => d.PurchaseDate)
                .Select((d, i) => new DetailViewRow
                {
                    Seq = i + 1,
                    CategoryName = string.IsNullOrWhiteSpace(d.CategoryName) ? d.CategoryCode : d.CategoryName,
                    ProductName = string.IsNullOrWhiteSpace(d.ProductName) ? $"商品{d.ProductId}" : d.ProductName,
                    PurchaseDate = d.PurchaseDate,
                    Price = d.Price,
                    Quantity = d.Quantity,
                    Amount = d.Amount,
                    Unit = d.Unit
                })
                .Take(80)
                .Cast<object>()
                .ToList();

            var summary = $"总预算：{item.TotalBudget.ToString("C2", CultureInfo.GetCultureInfo("zh-CN"))}，明细 {details.Count} 条，总金额 {details.Sum(d => d.Amount).ToString("C2", CultureInfo.GetCultureInfo("zh-CN"))}";
            var dialog = new TaskDetailsDialog(item, display, summary)
            {
                Owner = GetOwnerWindow()
            };
            dialog.ShowDialog();
        }

        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (sender is Button btn && btn.DataContext is TaskItem item)
            {
                if (!item.CanDelete)
                {
                    ShellFeedbackService.ShowInfo("仅待生成任务允许删除。", "删除限制");
                    return;
                }

                if (!AppDialogService.Confirm(GetOwnerWindow(), "删除确认", $"确认删除任务 {item.YearMonth}？"))
                {
                    return;
                }

                ShellFeedbackService.ShowLoading("正在删除任务...");
                try
                {
                    var rows = await _dataService.DeleteTaskAsync(item.Id);
                    if (rows == 0)
                    {
                        ShellFeedbackService.ShowWarning("未找到任务记录，可能已被删除。", "删除提醒");
                    }
                    else
                    {
                        ShellFeedbackService.ShowSuccess("任务已删除。", "删除完成");
                    }

                    await LoadTaskDataAsync();
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "删除失败", $"删除失败：{ex.Message}");
                }
                finally
                {
                    ShellFeedbackService.HideLoading();
                }
            }
        }

        private bool TryShowCreateTaskDialog(out CreateTaskRequest? result)
        {
            result = null;
            
            // 从数据库加载分类数据
            List<CategoryRecord>? categories = null;
            if (_categoryDataService != null)
            {
                try
                {
                    var categoryRecords = _categoryDataService.GetCategoriesAsync().GetAwaiter().GetResult();
                    if (categoryRecords != null && categoryRecords.Count > 0)
                    {
                        categories = categoryRecords;
                    }
                }
                catch
                {
                    // 加载失败时使用默认分类
                }
            }

            // 转换为Category对象列表
            var categoryList = categories?.Select(c => new Category
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Ratio = c.Ratio,
                Sort = c.Sort,
                Status = c.Status
            }).ToList();

            var dialog = new TaskEditorDialog(Tasks.Select(t => t.YearMonth), categoryList)
            {
                Owner = GetOwnerWindow()
            };

            var confirmed = dialog.ShowDialog() == true;
            result = dialog.Result;
            return confirmed && result != null;
        }

        private Window? GetOwnerWindow()
        {
            return Window.GetWindow(this) ?? System.Windows.Application.Current.MainWindow;
        }

        private TextBox? GetSearchBox() => FindByTag<TextBox>("Task.SearchBox");

        private ComboBox? GetStatusCombo() => FindByTag<ComboBox>("Task.StatusCombo");

        private T? FindByTag<T>(string tag) where T : FrameworkElement
        {
            return FindByTagInternal<T>(this, tag);
        }

        private static T? FindByTagInternal<T>(DependencyObject root, string tag) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T fe && fe.Tag is string t && t == tag)
                {
                    return fe;
                }

                var result = FindByTagInternal<T>(child, tag);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private sealed class GeneratedDetailSlot

        {
            public string CategoryCode { get; init; } = string.Empty;
            public ProductPriceRecord Product { get; init; } = new ProductPriceRecord();
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

            public ProcurementDetailRecord ToRecord(int taskId)
            {
                return new ProcurementDetailRecord
                {
                    TaskId = taskId,
                    CategoryCode = CategoryCode,
                    CategoryName = string.Empty,
                    ProductId = Product.Id,
                    ProductName = Product.Name,
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

        private class DetailViewRow
        {
            public int Seq { get; set; }
            public string CategoryName { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public string Unit { get; set; } = string.Empty;
            public DateTime PurchaseDate { get; set; }
            public decimal Price { get; set; }
            public decimal Quantity { get; set; }
            public decimal Amount { get; set; }
        }



    }

    public class TaskItem
    {
        public int Id { get; set; }
        public string YearMonth { get; set; } = string.Empty;
        public decimal TotalBudget { get; set; }
        public decimal FloatRate { get; set; }
        public int Status { get; set; }
        public string StatusDescription { get; set; } = string.Empty;
        public string StatusTone { get; set; } = "info";
        public string GenerateActionText { get; set; } = "生成";
        public DateTime? GeneratedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool CanGenerate { get; set; }
        public bool CanDelete { get; set; }
    }


    public class CreateTaskRequest
    {
        public string YearMonth { get; set; } = string.Empty;
        public decimal TotalBudget { get; set; }
        public decimal FloatRate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;

        /// <summary>
        /// 分类固定金额配置（可选）
        /// Key: 分类编码, Value: 固定金额
        /// </summary>
        public Dictionary<string, decimal>? CategoryFixedAmounts { get; set; }
    }

    public class CategoryBudgetPlan
    {
        public CategoryBudgetPlan(CategoryBudgetRecord category, decimal budget, bool isFixed)
        {
            Category = category;
            Budget = budget;
            IsFixed = isFixed;
        }

        public CategoryBudgetRecord Category { get; }
        public decimal Budget { get; }
        public bool IsFixed { get; }
    }
}
