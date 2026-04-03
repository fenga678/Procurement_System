using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Wpf.Dialogs;
using CanteenProcurement.Wpf.Services;


namespace CanteenProcurement.Wpf.Views
{
    public partial class CategoryManagementView : UserControl
    {
        public ObservableCollection<CategoryItem> Categories { get; set; } = new();
        private readonly CategoryDataService? _dataService;
        private readonly SchemaCapabilitiesProvider _schemaProvider = new();
        private bool _isBusy;

        public CategoryManagementView()
        {
            InitializeComponent();
            DataContext = this;
            try
            {
                _dataService = new CategoryDataService(_schemaProvider);
                _ = LoadFromDatabaseAsync();
            }
            catch (Exception ex)
            {
                _dataService = null;
                AppDialogService.ShowError(null, "数据库", $"初始化数据库连接失败：{ex.Message}\n请检查 appsettings.json 或环境变量 CANTEEN_MYSQL_CONN。");
            }
        }

        private async Task LoadFromDatabaseAsync()
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            ShellFeedbackService.ShowLoading("正在加载分类数据...");
            try
            {
                if (_dataService == null)
                {
                    return;
                }

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
            if (statsCount != null)
            {
                statsCount.Text = $"{active}";
            }

            var statsTotal = GetStatsTotalText();
            if (statsTotal != null)
            {
                statsTotal.Text = $"{total}";
            }
        }

        private async void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            // 检查试用版分类数量限制
            var license = LicensingService.Instance;
            var (allowed, maxCategories) = license.CheckCategoryLimit(Categories.Count);
            if (!allowed)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "试用版限制", 
                    $"试用版最多只能创建 {maxCategories} 个分类，请注册后继续使用。");
                return;
            }

            if (TryShowCategoryDialog(null, out var newItem) && newItem != null)
            {
                ShellFeedbackService.ShowLoading("正在保存分类...");
                try
                {
                    await _dataService.CreateCategoryAsync(new CategoryRecord
                    {
                        Name = newItem.Name,
                        Code = newItem.Code,
                        Ratio = newItem.Ratio,
                        FrequencyDays = newItem.FrequencyDays,
                        Sort = newItem.Sort,
                        Status = newItem.Status,
                        DailyMinItems = newItem.DailyMinItems,
                        DailyMaxItems = newItem.DailyMaxItems
                    });

                    ShellFeedbackService.ShowSuccess("分类已新增。", "保存成功");
                    await LoadFromDatabaseAsync();
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "保存失败", $"保存失败：{ex.Message}");
                }
                finally
                {
                    ShellFeedbackService.HideLoading();
                }
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (sender is Button btn && btn.Tag is string code)
            {
                var item = Categories.FirstOrDefault(c => c.Code == code);
                if (item == null)
                {
                    return;
                }

                if (TryShowCategoryDialog(item, out var updated) && updated != null)
                {
                    ShellFeedbackService.ShowLoading("正在更新分类...");
                    try
                    {
                        var rows = await _dataService.UpdateCategoryAsync(new CategoryRecord
                        {
                            Name = updated.Name,
                            Code = updated.Code,
                            Ratio = updated.Ratio,
                            FrequencyDays = updated.FrequencyDays,
                            Sort = updated.Sort,
                            Status = updated.Status,
                            DailyMinItems = updated.DailyMinItems,
                            DailyMaxItems = updated.DailyMaxItems
                        });

                        if (rows == 0)
                        {
                            ShellFeedbackService.ShowWarning("未找到要更新的分类记录。", "更新提醒");
                        }
                        else
                        {
                            ShellFeedbackService.ShowSuccess("分类已更新。", "更新成功");
                        }

                        await LoadFromDatabaseAsync();
                    }
                    catch (Exception ex)
                    {
                        AppDialogService.ShowError(GetOwnerWindow(), "更新失败", $"更新失败：{ex.Message}");
                    }
                    finally
                    {
                        ShellFeedbackService.HideLoading();
                    }
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (sender is Button btn && btn.Tag is string code)
            {
                var item = Categories.FirstOrDefault(c => c.Code == code);
                if (item == null)
                {
                    return;
                }

                // 先检查是否可以删除
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

                    if (!AppDialogService.Confirm(GetOwnerWindow(), "删除确认", $"确认删除分类 {item.Name}？"))
                    {
                        return;
                    }

                    ShellFeedbackService.ShowLoading("正在删除分类...");
                    await _dataService.DeleteCategoryAsync(code);
                    ShellFeedbackService.ShowSuccess("分类已删除。", "删除成功");
                    await LoadFromDatabaseAsync();
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

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (sender is Button btn && btn.Tag is string code)
            {
                var item = Categories.FirstOrDefault(c => c.Code == code);
                if (item == null)
                {
                    return;
                }

                ShellFeedbackService.ShowLoading("正在更新分类状态...");
                try
                {
                    var newStatus = !item.Status;
                    var rows = await _dataService.UpdateCategoryStatusAsync(code, newStatus);
                    if (rows == 0)
                    {
                        ShellFeedbackService.ShowWarning("未找到要更新的分类记录。", "状态提醒");
                    }
                    else
                    {
                        ShellFeedbackService.ShowSuccess(newStatus ? "分类已启用。" : "分类已停用。", "状态已更新");
                    }

                    await LoadFromDatabaseAsync();
                }
                catch (Exception ex)
                {
                    AppDialogService.ShowError(GetOwnerWindow(), "更新失败", $"更新失败：{ex.Message}");
                }
                finally
                {
                    ShellFeedbackService.HideLoading();
                }
            }
        }

        private bool TryShowCategoryDialog(CategoryItem? existing, out CategoryItem? result)
        {
            var dialog = new CategoryEditorDialog(existing)
            {
                Owner = GetOwnerWindow()
            };

            var confirmed = dialog.ShowDialog() == true;
            result = dialog.Result;
            return confirmed && result != null;
        }

        private TextBlock? GetStatsCountText() => FindByTag<TextBlock>("Category.StatsCount");
        private TextBlock? GetStatsTotalText() => FindByTag<TextBlock>("Category.StatsTotal");

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

        private Window? GetOwnerWindow()

        {
            return Window.GetWindow(this) ?? System.Windows.Application.Current.MainWindow;
        }
    }

    public class CategoryItem
    {
        public CategoryItem(int seq, string name, string code, decimal ratio, int frequencyDays, int sort, bool status)
        {
            Seq = seq;
            Name = name;
            Code = code;
            Ratio = ratio;
            RatioText = $"{ratio:P0}";
            FrequencyDays = frequencyDays;
            Sort = sort;
            Status = status;
            StatusText = status ? "启用" : "停用";
            StatusTone = status ? "success" : "danger";
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
