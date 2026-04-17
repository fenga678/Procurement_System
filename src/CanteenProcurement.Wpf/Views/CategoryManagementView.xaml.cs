using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Wpf.Dialogs;
using CanteenProcurement.Wpf.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenProcurement.Wpf.Views
{
    public partial class CategoryManagementView : UserControl
    {
        public ObservableCollection<CategoryItem> Categories { get; set; } = new();
        private readonly CategoryDataService _dataService;
        private bool _isBusy;

        public CategoryManagementView()
        {
            InitializeComponent();
            DataContext = this;
            _dataService = AppHost.Services.GetRequiredService<CategoryDataService>();
            _ = LoadFromDatabaseAsync();
        }

        private async Task LoadFromDatabaseAsync()
        {
            if (_isBusy) return;

            _isBusy = true;
            ShellFeedbackService.ShowLoading("正在加载分类数据...");
            try
            {
                var list = await _dataService.GetCategoriesAsync();
                Categories.Clear();
                var seq = 0;
                foreach (var c in list)
                {
                    Categories.Add(new CategoryItem(++seq, c.Name, c.Code, c.Ratio, c.FrequencyDays, c.Sort, c.Status)
                    {
                        DailyMinItems = c.DailyMinItems,
                        DailyMaxItems = c.DailyMaxItems,
                        DailyRangeText = $"{c.DailyMinItems}-{c.DailyMaxItems}",
                        UpdatedAt = c.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                    });
                }

                UpdateStats();
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(GetOwnerWindow(), "数据库", $"加载分类失败：{ex.Message}");
            }
            finally
            {
                _isBusy = false;
                ShellFeedbackService.HideLoading();
            }
        }

        private void UpdateStats()
        {
            var active = Categories.Count(c => c.Status);
            var total = Categories.Count;

            var statsCount = GetStatsCountText();
            if (statsCount != null) statsCount.Text = $"{active}";
            var statsTotal = GetStatsTotalText();
            if (statsTotal != null) statsTotal.Text = $"{total}";

            // Update Ratio Display
            var ratioText = FindByTag<TextBlock>("Category.RatioText");
            var ratioHint = FindByTag<TextBlock>("Category.RatioHint");
            if (ratioText != null)
            {
                var totalRatio = Categories.Where(c => c.Status).Sum(c => c.Ratio);
                ratioText.Text = $"{totalRatio:P0}";
                
                // Set color based on allocation
                if (totalRatio == 1.0m)
                {
                    ratioText.Foreground = (Brush)FindResource("Brush.Success");
                }
                else if (totalRatio > 1.0m)
                {
                    ratioText.Foreground = (Brush)FindResource("Brush.Danger");
                }
                else
                {
                    ratioText.Foreground = (Brush)FindResource("Brush.Warning");
                }
            }

            if (ratioHint != null)
            {
                var totalRatio = Categories.Where(c => c.Status).Sum(c => c.Ratio);
                if (totalRatio == 1.0m)
                {
                    ratioHint.Text = "✅ 配置完美";
                    ratioHint.Foreground = (Brush)FindResource("Brush.Success");
                }
                else if (totalRatio > 1.0m)
                {
                    ratioHint.Text = "⚠️ 超过 100%";
                    ratioHint.Foreground = (Brush)FindResource("Brush.Danger");
                }
                else
                {
                    var remaining = 1.0m - totalRatio;
                    ratioHint.Text = $"剩余 {remaining:P0} 待分配";
                    ratioHint.Foreground = (Brush)FindResource("Brush.Warning");
                }
            }
        }

        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            var license = LicensingService.Instance;
            var (allowed, maxCategories) = license.CheckCategoryLimit(Categories.Count);
            if (!allowed)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "试用版限制", $"试用版最多只能创建 {maxCategories} 个分类，请注册后继续使用。");
                return;
            }

            if (TryShowCategoryDialog(null, out var newItem) && newItem != null)
            {
                ShellFeedbackService.ShowLoading("正在保存分类...");
                try
                {
                    await _dataService.CreateCategoryAsync(new CategoryRecord
                    {
                        Name = newItem.Name, Code = newItem.Code, Ratio = newItem.Ratio,
                        FrequencyDays = newItem.FrequencyDays, Sort = newItem.Sort,
                        Status = newItem.Status, DailyMinItems = newItem.DailyMinItems,
                        DailyMaxItems = newItem.DailyMaxItems
                    });
                    ShellFeedbackService.ShowSuccess("分类已新增。", "保存成功");
                    await LoadFromDatabaseAsync();
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "保存失败", $"保存失败：{ex.Message}");
                }
                finally { ShellFeedbackService.HideLoading(); }
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string code) return;
            var item = Categories.FirstOrDefault(c => c.Code == code);
            if (item == null) return;

            if (TryShowCategoryDialog(item, out var updated) && updated != null)
            {
                ShellFeedbackService.ShowLoading("正在更新分类...");
                try
                {
                    var rows = await _dataService.UpdateCategoryAsync(new CategoryRecord
                    {
                        Name = updated.Name, Code = updated.Code, Ratio = updated.Ratio,
                        FrequencyDays = updated.FrequencyDays, Sort = updated.Sort,
                        Status = updated.Status, DailyMinItems = updated.DailyMinItems,
                        DailyMaxItems = updated.DailyMaxItems
                    });
                    if (rows == 0)
                        ShellFeedbackService.ShowWarning("未找到要更新的分类记录。", "更新提醒");
                    else
                        ShellFeedbackService.ShowSuccess("分类已更新。", "更新成功");
                    await LoadFromDatabaseAsync();
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "更新失败", $"更新失败：{ex.Message}");
                }
                finally { ShellFeedbackService.HideLoading(); }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string code) return;
            var item = Categories.FirstOrDefault(c => c.Code == code);
            if (item == null) return;

            ShellFeedbackService.ShowLoading("正在检查...");
            try
            {
                var (canDelete, message) = await _dataService.CanDeleteCategoryAsync(code);
                ShellFeedbackService.HideLoading();
                if (!canDelete)
                {
                    AppDialogService.ShowWarning(GetOwnerWindow(), "无法删除", message);
                    return;
                }
                if (!AppDialogService.Confirm(GetOwnerWindow(), "删除确认", $"确认删除分类 {item.Name}？")) return;

                ShellFeedbackService.ShowLoading("正在删除分类...");
                await _dataService.DeleteCategoryAsync(code);
                ShellFeedbackService.ShowSuccess("分类已删除。", "删除成功");
                await LoadFromDatabaseAsync();
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(GetOwnerWindow(), "删除失败", $"删除失败：{ex.Message}");
            }
            finally { ShellFeedbackService.HideLoading(); }
        }

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string code) return;
            var item = Categories.FirstOrDefault(c => c.Code == code);
            if (item == null) return;

            ShellFeedbackService.ShowLoading("正在更新分类状态...");
            try
            {
                var newStatus = !item.Status;
                var rows = await _dataService.UpdateCategoryStatusAsync(code, newStatus);
                if (rows == 0)
                    ShellFeedbackService.ShowWarning("未找到要更新的分类记录。", "状态提醒");
                else
                    ShellFeedbackService.ShowSuccess(newStatus ? "分类已启用。" : "分类已停用。", "状态已更新");
                await LoadFromDatabaseAsync();
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(GetOwnerWindow(), "更新失败", $"更新失败：{ex.Message}");
            }
            finally { ShellFeedbackService.HideLoading(); }
        }

        private bool TryShowCategoryDialog(CategoryItem? existing, out CategoryItem? result)
        {
            var dialog = new CategoryEditorDialog(existing) { Owner = GetOwnerWindow() };
            var confirmed = dialog.ShowDialog() == true;
            result = dialog.Result;
            return confirmed && result != null;
        }

        private TextBlock? GetStatsCountText() => FindByTag<TextBlock>("Category.StatsCount");
        private TextBlock? GetStatsTotalText() => FindByTag<TextBlock>("Category.StatsTotal");
        private T? FindByTag<T>(string tag) where T : FrameworkElement => FindByTagInternal<T>(this, tag);
        private static T? FindByTagInternal<T>(DependencyObject root, string tag) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T fe && fe.Tag is string t && t == tag) return fe;
                var result = FindByTagInternal<T>(child, tag);
                if (result != null) return result;
            }
            return null;
        }
        private Window? GetOwnerWindow() => Window.GetWindow(this) ?? System.Windows.Application.Current.MainWindow;

        private async void ClearData_Click(object sender, RoutedEventArgs e)
        {
            if (!AppDialogService.Confirm(GetOwnerWindow(), "确认清空", "确认清空所有分类数据吗？\n此操作不可恢复！"))
                return;

            await AsyncCommand.ExecuteAsync(async () =>
            {
                var categories = await _dataService.GetCategoriesAsync();
                var count = 0;
                foreach (var cat in categories)
                {
                    await _dataService.DeleteCategoryAsync(cat.Code);
                    count++;
                }
                ShellFeedbackService.ShowSuccess($"已清空 {count} 条分类数据。", "清空完成");
                await LoadFromDatabaseAsync();
            }, GetOwnerWindow(), "正在清空数据...");
        }
    }

    public class CategoryItem
    {
        public CategoryItem(int seq, string name, string code, decimal ratio, int frequencyDays, int sort, bool status)
        {
            Seq = seq; Name = name; Code = code; Ratio = ratio; RatioText = $"{ratio:P0}";
            FrequencyDays = frequencyDays; Sort = sort; Status = status;
            StatusText = status ? "启用" : "停用"; StatusTone = status ? "success" : "danger";
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }
        public int Seq { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public decimal Ratio { get; set; }
        public string RatioText { get; set; }
        public int FrequencyDays { get; set; }
        public int DailyMinItems { get; set; } = 1;
        public int DailyMaxItems { get; set; } = 1;
        public string DailyRangeText { get; set; } = "1-1";
        public int Sort { get; set; }
        public bool Status { get; set; }
        public string StatusText { get; set; }
        public string StatusTone { get; set; }
        public string UpdatedAt { get; set; }
    }
}
