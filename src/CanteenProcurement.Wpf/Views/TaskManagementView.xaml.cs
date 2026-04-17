using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Core.Entities;
using CanteenProcurement.Wpf.Dialogs;
using CanteenProcurement.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace CanteenProcurement.Wpf.Views;

public partial class TaskManagementView : UserControl, INotifyPropertyChanged
{
    private readonly TaskDataService _taskService;
    private readonly CategoryDataService _categoryService;
    private CancellationTokenSource? _loadCts;
    private bool _isLoading;
    private bool _isApplyingFilters;

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskManagementView()
    {
        InitializeComponent();
        DataContext = this;
        _taskService = AppHost.Services.GetRequiredService<TaskDataService>();
        _categoryService = AppHost.Services.GetRequiredService<CategoryDataService>();
        Loaded += TaskManagementView_Loaded;
        Unloaded += TaskManagementView_Unloaded;
    }

    private async void TaskManagementView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadTaskDataAsync();
    }

    private void TaskManagementView_Unloaded(object sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    private async Task LoadTaskDataAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;

        _isLoading = true;
        ShellFeedbackService.ShowLoading("正在加载任务数据...");
        try
        {
            var tasks = await _taskService.GetTasksAsync(cancellationToken);
            Tasks.Clear();
            foreach (var task in tasks)
            {
                Tasks.Add(new TaskItem
                {
                    Id = task.Id,
                    YearMonth = task.YearMonth,
                    TotalBudget = task.TotalBudget,
                    FloatRate = task.FloatRate,
                    Status = task.Status,
                    StatusDescription = GetStatusDescription(task.Status),
                    StatusTone = GetStatusTone(task.Status),
                    GenerateActionText = task.Status == 0 ? "生成" : "重新生成",
                    GeneratedAt = task.GeneratedAt,
                    CreatedBy = string.IsNullOrWhiteSpace(task.CreatedBy) ? "-" : task.CreatedBy,
                    CreatedAt = task.CreatedAt,
                    UpdatedAt = task.UpdatedAt,
                    CanGenerate = true,
                    CanDelete = task.Status == 0
                });
            }

            dgTasks.ItemsSource = Tasks;
            ApplyFilters();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppDialogService.ShowError(GetOwnerWindow(), "加载失败", $"任务数据加载失败：{exception.Message}");
        }
        finally
        {
            _isLoading = false;
            ShellFeedbackService.HideLoading();
        }
    }

    private static string GetStatusDescription(int status)
    {
        return status switch
        {
            0 => "待生成",
            1 => "已完成",
            2 => "已取消",
            _ => "未知"
        };
    }

    private static string GetStatusTone(int status)
    {
        return status switch
        {
            0 => "warning",
            1 => "success",
            2 => "danger",
            _ => "info"
        };
    }

    private async void btnCreateTask_Click(object sender, RoutedEventArgs e)
    {
        await AsyncCommand.ExecuteAsync(async () =>
        {
            var license = LicensingService.Instance;
            var (allowed, maxTasks) = license.CheckTaskLimit(Tasks.Count);
            if (!allowed)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "试用版限制", $"试用版最多只能创建 {maxTasks} 个任务。");
                return;
            }

            var categories = await _categoryService.GetCategoriesAsync();
            var categoryEntities = categories.Select(category => new Category
            {
                Id = category.Id,
                Name = category.Name,
                Code = category.Code,
                Ratio = category.Ratio,
                Sort = category.Sort,
                Status = category.Status,
                FrequencyDays = category.FrequencyDays,
                DailyMinItems = category.DailyMinItems,
                DailyMaxItems = category.DailyMaxItems
            }).ToList();

            var dialog = new TaskEditorDialog(Tasks.Select(task => task.YearMonth), categoryEntities)
            {
                Owner = GetOwnerWindow()
            };

            if (dialog.ShowDialog() != true || dialog.Result is null) return;

            var request = dialog.Result;
            var createdId = await _taskService.CreateTaskAsync(new TaskRecord
            {
                YearMonth = request.YearMonth,
                TotalBudget = request.TotalBudget,
                FloatRate = request.FloatRate,
                Status = 0,
                CreatedBy = request.CreatedBy
            });

            if (request.CategoryFixedAmounts is { Count: > 0 })
            {
                await _taskService.SaveTaskFixedAmountsAsync(createdId, request.CategoryFixedAmounts);
            }

            ShellFeedbackService.ShowSuccess($"任务已创建，编号：{createdId}");
            await LoadTaskDataAsync();
        }, GetOwnerWindow(), "正在创建任务...");
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isApplyingFilters)
        {
            ApplyFilters();
        }
    }

    private void StatusCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !_isApplyingFilters)
        {
            ApplyFilters();
        }
    }

    private void ApplyFilters()
    {
        _isApplyingFilters = true;
        try
        {
            var keyword = GetSearchBox()?.Text?.Trim();
            var selectedStatus = GetStatusFilter();

            IEnumerable<TaskItem> query = Tasks;
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(task =>
                    task.YearMonth.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    task.Id.ToString().Equals(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedStatus >= 0)
            {
                query = query.Where(task => task.Status == selectedStatus);
            }

            var result = query.ToList();
            dgTasks.ItemsSource = result.Count == Tasks.Count && selectedStatus < 0 && string.IsNullOrWhiteSpace(keyword)
                ? Tasks
                : result;
        }
        finally
        {
            _isApplyingFilters = false;
        }
    }

    private async void btnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TaskItem task }) return;

        if (!AppDialogService.Confirm(GetOwnerWindow(), "确认生成", $"确认对任务 {task.YearMonth} 生成采购计划吗？"))
            return;

        await AsyncCommand.ExecuteAsync(async () =>
        {
            var result = await _taskService.GeneratePlanAsync(task.Id);
            if (!result.Success)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "生成失败", result.Message);
                return;
            }
            ShellFeedbackService.ShowSuccess(result.Message);
            await LoadTaskDataAsync();
        }, GetOwnerWindow(), $"正在为 {task.YearMonth} 生成采购计划...");
    }

    private async void btnView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: TaskItem task })
        {
            await ShowTaskDetailsAsync(task);
        }
    }

    private async void dgTasks_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgTasks.SelectedItem is TaskItem task)
        {
            await ShowTaskDetailsAsync(task);
        }
    }

    private async Task ShowTaskDetailsAsync(TaskItem task)
    {
        var details = await _taskService.GetTaskDetailsAsync(task.Id);
        if (details.Count == 0)
        {
            ShellFeedbackService.ShowInfo("该任务还没有生成采购明细。", "明细提示");
            return;
        }

        var displayRows = details
            .OrderBy(detail => detail.PurchaseDate)
            .Select((detail, index) => new DetailViewRow
            {
                Seq = index + 1,
                CategoryName = string.IsNullOrWhiteSpace(detail.CategoryName) ? detail.CategoryCode : detail.CategoryName,
                ProductName = string.IsNullOrWhiteSpace(detail.ProductName) ? $"商品 {detail.ProductId}" : detail.ProductName,
                PurchaseDate = detail.PurchaseDate,
                Price = detail.Price,
                Quantity = detail.Quantity,
                Amount = detail.Amount,
                Unit = detail.Unit
            })
            .Take(200)
            .Cast<object>()
            .ToList();

        var culture = CultureInfo.GetCultureInfo("zh-CN");
        var summary = $"总预算：{task.TotalBudget.ToString("C2", culture)}，明细 {details.Count} 条，总金额 {details.Sum(detail => detail.Amount).ToString("C2", culture)}";
        var dialog = new TaskDetailsDialog(task, displayRows, summary)
        {
            Owner = GetOwnerWindow()
        };
        dialog.ShowDialog();
    }

    private async void btnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: TaskItem task }) return;
        if (!task.CanDelete)
        {
            ShellFeedbackService.ShowInfo("只有待生成任务允许删除。", "删除限制");
            return;
        }
        if (!AppDialogService.Confirm(GetOwnerWindow(), "确认删除", $"确认删除任务 {task.YearMonth} 吗？")) return;

        await AsyncCommand.ExecuteAsync(async () =>
        {
            var rows = await _taskService.DeleteTaskAsync(task.Id);
            if (rows == 0)
                ShellFeedbackService.ShowWarning("未找到要删除的任务记录。", "删除提示");
            else
                ShellFeedbackService.ShowSuccess("任务已删除。");
            await LoadTaskDataAsync();
        }, GetOwnerWindow(), "正在删除任务...");
    }

    private async void btnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (dgTasks.SelectedItem is not TaskItem task)
        {
            ShellFeedbackService.ShowInfo("请先选择要导出的任务。", "导出提示");
            return;
        }

        var details = await _taskService.GetTaskDetailsAsync(task.Id);
        if (details.Count == 0)
        {
            ShellFeedbackService.ShowInfo("该任务没有明细，无法导出。", "导出提示");
            return;
        }

        var license = LicensingService.Instance;
        var exportRows = details.Take(license.GetExportRowLimit()).ToList();
        if (exportRows.Count < details.Count)
        {
            ShellFeedbackService.ShowInfo($"试用版仅导出前 {exportRows.Count} 条明细。", "试用版限制");
        }

        var dialog = new SaveFileDialog
        {
            Filter = "CSV 文件 (*.csv)|*.csv",
            FileName = $"task_{task.Id}_{task.YearMonth}.csv"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();
            builder.AppendLine("序号,分类,商品名称,单位,单价,数量,金额,日期");
            var orderedRows = exportRows.OrderBy(detail => detail.PurchaseDate).ToList();
            for (var index = 0; index < orderedRows.Count; index++)
            {
                var detail = orderedRows[index];
                builder.AppendLine(
                    $"{index + 1},{detail.CategoryName},\"{detail.ProductName}\",{detail.Unit},{detail.Price:F2},{detail.Quantity:0.##},{detail.Amount:F1},{detail.PurchaseDate:yyyy-MM-dd}");
            }

            File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
            ShellFeedbackService.ShowSuccess("任务明细已导出。", "导出完成");
        }
        catch (Exception exception)
        {
            AppDialogService.ShowError(GetOwnerWindow(), "导出失败", $"导出任务明细失败：{exception.Message}");
        }
    }

    private Window? GetOwnerWindow()
    {
        return Window.GetWindow(this) ?? System.Windows.Application.Current.MainWindow;
    }

    private TextBox? GetSearchBox() => FindByTag<TextBox>("Task.SearchBox");

    private int GetStatusFilter()
    {
        if (FindByTag<ComboBox>("Task.StatusCombo")?.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var value))
        {
            return value;
        }

        return -1;
    }

    private T? FindByTag<T>(string tag) where T : FrameworkElement
    {
        return FindByTagInternal<T>(this, tag);
    }

    private static T? FindByTagInternal<T>(DependencyObject root, string tag) where T : FrameworkElement
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T element && element.Tag is string elementTag && elementTag == tag)
            {
                return element;
            }

            var nested = FindByTagInternal<T>(child, tag);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void ClearData_Click(object sender, RoutedEventArgs e)
    {
        if (!AppDialogService.Confirm(GetOwnerWindow(), "确认清空", "确认清空所有任务数据吗？\n此操作不可恢复！"))
            return;

        await AsyncCommand.ExecuteAsync(async () =>
        {
            var tasks = await _taskService.GetTasksAsync();
            var count = 0;
            foreach (var task in tasks)
            {
                await _taskService.DeleteTaskAsync(task.Id);
                count++;
            }
            ShellFeedbackService.ShowSuccess($"已清空 {count} 条任务数据。", "清空完成");
            await LoadTaskDataAsync();
        }, GetOwnerWindow(), "正在清空数据...");
    }

    private sealed class DetailViewRow
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

public sealed class TaskItem
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

public sealed class CreateTaskRequest
{
    public string YearMonth { get; set; } = string.Empty;
    public decimal TotalBudget { get; set; }
    public decimal FloatRate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public Dictionary<string, decimal>? CategoryFixedAmounts { get; set; }
}
