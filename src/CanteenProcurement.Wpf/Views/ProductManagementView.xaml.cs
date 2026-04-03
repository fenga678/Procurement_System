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
    public partial class ProductManagementView : UserControl
    {
        public ObservableCollection<ProductItem> Products { get; set; } = new();
        public ObservableCollection<CategoryFilterItem> Categories { get; set; } = new();
        private int _nextProductId = 7;
        private bool _isBusy;
        private readonly CategoryDataService? _categoryDataService;

        private readonly (string Code, string Name)[] _categoryOptions =
        {
            ("vegetable", "蔬菜类"),
            ("meat", "肉类"),
            ("egg", "蛋类"),
            ("oil", "食用油"),
            ("rice", "米"),
            ("noodle", "挂面粉条"),
            ("seasoning", "调味品")
        };

        private readonly ProductDataService? _dataService;

        public ProductManagementView()
        {
            InitializeComponent();
            DataContext = this;
            try
            {
                _dataService = new ProductDataService();
                _categoryDataService = new CategoryDataService(new SchemaCapabilitiesProvider());
                _ = InitializeAsync();
            }
            catch (Exception ex)
            {
                _dataService = null;
                AppDialogService.ShowError(null, "数据库", $"初始化数据库连接失败：{ex.Message}\n请检查 appsettings.json 或环境变量 CANTEEN_MYSQL_CONN。");
            }
        }

        private async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await LoadFromDatabaseAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            if (_categoryDataService == null) return;

            try
            {
                Categories.Clear();
                // 添加"全部分类"选项
                Categories.Add(new CategoryFilterItem { Code = null, Name = "全部分类" });

                var categories = await _categoryDataService.GetCategoriesAsync();
                foreach (var cat in categories.Where(c => c.Status).OrderBy(c => c.Sort))
                {
                    Categories.Add(new CategoryFilterItem { Code = cat.Code, Name = cat.Name });
                }

                // 默认选中第一项（全部分类）
                var combo = GetCategoryFilter();
                if (combo != null && combo.Items.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                // 加载分类失败时使用默认选项
                Categories.Clear();
                Categories.Add(new CategoryFilterItem { Code = null, Name = "全部分类" });
            }
        }

        private async Task LoadFromDatabaseAsync(string? categoryCode = null, string? keyword = null)
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            ShellFeedbackService.ShowLoading("正在加载商品数据...");
            try
            {
                if (_dataService == null)
                {
                    return;
                }

                var list = await _dataService.GetProductsAsync(categoryCode, keyword);
                Products.Clear();
                var seq = 0;
                foreach (var p in list)
                {
                    Products.Add(new ProductItem(++seq, p.Id, p.Name, p.CategoryCode, p.CategoryName, p.Price, p.Unit, p.MinIntervalDays, p.IsActive, p.Remark)
                    {
                        UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd HH:mm")
                    });
                }

                _nextProductId = Products.Count == 0 ? 1 : Products.Max(p => p.Id) + 1;
                UpdateStats();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(GetOwnerWindow(), "数据库", $"加载商品失败：{ex.Message}");
            }
            finally
            {
                _isBusy = false;
                ShellFeedbackService.HideLoading();
            }
        }

        private void UpdateStats()
        {
            var activeCount = Products.Count(p => p.IsActive);
            var total = Products.Count;

            var statsCount = GetStatsCountText();
            if (statsCount != null)
            {
                statsCount.Text = $"{activeCount}";
            }

            var statsTotal = GetStatsTotalText();
            if (statsTotal != null)
            {
                statsTotal.Text = $"{total}";
            }
        }

        private async void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            // 检查试用版商品数量限制
            var license = LicensingService.Instance;
            var (allowed, maxProducts) = license.CheckProductLimit(Products.Count);
            if (!allowed)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "试用版限制", 
                    $"试用版最多只能添加 {maxProducts} 个商品，请注册后继续使用。");
                return;
            }

            if (TryShowProductDialog(null, out var newItem) && newItem != null)
            {
                ShellFeedbackService.ShowLoading("正在保存商品...");
                try
                {
                    var newId = await _dataService.CreateProductAsync(new ProductRecord
                    {
                        Name = newItem.Name,
                        CategoryCode = newItem.CategoryCode,
                        Price = newItem.Price,
                        Unit = newItem.Unit,
                        MinIntervalDays = newItem.MinIntervalDays,
                        IsActive = newItem.IsActive,
                        Remark = newItem.Remark
                    });

                    _nextProductId = Math.Max(_nextProductId, newId + 1);
                    ShellFeedbackService.ShowSuccess("商品已新增。", "保存成功");
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

        private async void WebImport_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                AppDialogService.ShowWarning(GetOwnerWindow(), "数据库", "数据库未初始化，无法导入。");
                return;
            }

            // 获取分类列表传给对话框
            var categories = Categories
                .Where(c => c.Code != null)
                .Select(c => (c.Code!, c.Name))
                .ToList();

            var dialog = new WebImportDialog(categories)
            {
                Owner = GetOwnerWindow()
            };

            if (dialog.ShowDialog() != true || dialog.ImportItems.Count == 0)
            {
                return;
            }

            // 检查试用版导入限制
            var license = LicensingService.Instance;
            var maxImportRows = license.GetImportRowLimit();
            var importItems = dialog.ImportItems;
            if (importItems.Count > maxImportRows)
            {
                importItems = importItems.Take(maxImportRows).ToList();
                ShellFeedbackService.ShowInfo($"试用版每次仅导入前 {maxImportRows} 条数据。", "试用版限制");
            }

            ShellFeedbackService.ShowLoading("正在导入商品...");
            try
            {
                var successCount = 0;
                var errorCount = 0;

                foreach (var item in importItems)
                {
                    try
                    {
                        await _dataService.CreateProductAsync(new ProductRecord
                        {
                            Name = item.Name,
                            CategoryCode = item.CategoryCode,
                            Price = item.Price,
                            Unit = item.Unit,
                            MinIntervalDays = item.MinIntervalDays,
                            IsActive = true,
                            Remark = ""
                        });
                        successCount++;
                    }
                    catch
                    {
                        errorCount++;
                    }
                }

                ShellFeedbackService.HideLoading();

                var message = $"导入完成：成功 {successCount} 条";
                if (errorCount > 0)
                {
                    message += $"，失败 {errorCount} 条";
                }

                ShellFeedbackService.ShowSuccess(message, "导入完成");
                await LoadFromDatabaseAsync();
            }
            catch (Exception ex)
            {
                ShellFeedbackService.HideLoading();
                AppDialogService.ShowError(GetOwnerWindow(), "导入失败", $"导入失败：{ex.Message}");
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (_dataService == null)
            {
                return;
            }

            if (sender is Button btn && btn.Tag is int id)
            {
                var item = Products.FirstOrDefault(p => p.Id == id);
                if (item == null)
                {
                    return;
                }

                if (TryShowProductDialog(item, out var updated) && updated != null)
                {
                    ShellFeedbackService.ShowLoading("正在更新商品...");
                    try
                    {
                        var rows = await _dataService.UpdateProductAsync(new ProductRecord
                        {
                            Id = id,
                            Name = updated.Name,
                            CategoryCode = updated.CategoryCode,
                            Price = updated.Price,
                            Unit = updated.Unit,
                            MinIntervalDays = updated.MinIntervalDays,
                            IsActive = updated.IsActive,
                            Remark = updated.Remark
                        });

                        if (rows == 0)
                        {
                            ShellFeedbackService.ShowWarning("未找到要更新的商品记录。", "更新提醒");
                        }
                        else
                        {
                            ShellFeedbackService.ShowSuccess("商品已更新。", "更新成功");
                        }

                        await LoadFromDatabaseAsync(GetSelectedCategoryTag(), GetSearchBox()?.Text?.Trim());

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

            if (sender is Button btn && btn.Tag is int id)
            {
                var item = Products.FirstOrDefault(p => p.Id == id);
                if (item == null)
                {
                    return;
                }

                // 先检查是否可以删除
                ShellFeedbackService.ShowLoading("正在检查...");
                try
                {
                    var (canDelete, message) = await _dataService.CanDeleteProductAsync(id);
                    ShellFeedbackService.HideLoading();

                    if (!canDelete)
                    {
                        AppDialogService.ShowWarning(GetOwnerWindow(), "无法删除", message);
                        return;
                    }

                    if (!AppDialogService.Confirm(GetOwnerWindow(), "删除确认", $"确认删除商品 {item.Name}？"))
                    {
                        return;
                    }

                    ShellFeedbackService.ShowLoading("正在删除商品...");
                    await _dataService.DeleteProductAsync(id);
                    ShellFeedbackService.ShowSuccess("商品已删除。", "删除成功");
                    await LoadFromDatabaseAsync(GetSelectedCategoryTag(), GetSearchBox()?.Text?.Trim());
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

            if (sender is Button btn && btn.Tag is int id)
            {
                var item = Products.FirstOrDefault(p => p.Id == id);
                if (item == null)
                {
                    return;
                }

                ShellFeedbackService.ShowLoading("正在更新商品状态...");
                try
                {
                    var newStatus = !item.IsActive;
                    var rows = await _dataService.UpdateProductStatusAsync(id, newStatus);
                    if (rows == 0)
                    {
                        ShellFeedbackService.ShowWarning("未找到要更新的商品记录。", "状态提醒");
                    }
                    else
                    {
                        ShellFeedbackService.ShowSuccess(newStatus ? "商品已启用。" : "商品已停用。", "状态已更新");
                    }

                    await LoadFromDatabaseAsync(GetSelectedCategoryTag(), GetSearchBox()?.Text?.Trim());

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

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded)
                return;
            await LoadFromDatabaseAsync(GetSelectedCategoryTag(), GetSearchBox()?.Text?.Trim());
        }

        private async void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            await LoadFromDatabaseAsync(GetSelectedCategoryTag(), GetSearchBox()?.Text?.Trim());
        }

        private string? GetSelectedCategoryTag()
        {
            return (GetCategoryFilter()?.SelectedItem as CategoryFilterItem)?.Code;
        }

        private void ApplyFilters()
        {
            var keyword = GetSearchBox()?.Text?.Trim();
            var selectedTag = (GetCategoryFilter()?.SelectedItem as CategoryFilterItem)?.Code;

            var query = Products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p => p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                      || p.Remark.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(selectedTag))
            {
                query = query.Where(p => p.CategoryCode == selectedTag);
            }

            ProductGrid.ItemsSource = string.IsNullOrWhiteSpace(keyword) && string.IsNullOrWhiteSpace(selectedTag)
                ? Products
                : query.ToList();
        }

        private bool TryShowProductDialog(ProductItem? existing, out ProductItem? result)
        {
            // 从数据库加载的分类列表转换为对话框需要的格式
            var categories = Categories
                .Where(c => c.Code != null)  // 排除"全部分类"选项
                .Select(c => (c.Code!, c.Name))
                .ToList();

            // 如果没有从数据库加载到分类，使用默认选项作为备用
            if (categories.Count == 0)
            {
                categories = _categoryOptions.ToList();
            }

            var dialog = new ProductEditorDialog(existing, categories, _nextProductId)
            {
                Owner = GetOwnerWindow()
            };

            var confirmed = dialog.ShowDialog() == true;
            result = dialog.Result;
            return confirmed && result != null;
        }

        private TextBox? GetSearchBox() => FindByTag<TextBox>("Product.SearchBox");
        private ComboBox? GetCategoryFilter() => FindByTag<ComboBox>("Product.CategoryFilter");
        private TextBlock? GetStatsCountText() => FindByTag<TextBlock>("Product.StatsCount");
        private TextBlock? GetStatsTotalText() => FindByTag<TextBlock>("Product.StatsTotal");

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

    public class ProductItem
    {
        public ProductItem(int seq, int id, string name, string categoryCode, string categoryName, decimal price, string unit, int minIntervalDays, bool isActive, string remark)
        {
            Seq = seq;
            Id = id;
            Name = name;
            CategoryCode = categoryCode;
            CategoryName = categoryName;
            Price = price;
            PriceText = price.ToString("F2");
            Unit = unit;
            MinIntervalDays = minIntervalDays;
            IsActive = isActive;
            StatusText = isActive ? "启用" : "停用";
            StatusTone = isActive ? "success" : "danger";
            Remark = remark;
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        }

        public int Seq { get; set; }
        public int Id { get; set; }
        public string Name { get; set; }
        public string CategoryCode { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public string PriceText { get; set; }
        public string Unit { get; set; }
        public int MinIntervalDays { get; set; }
        public bool IsActive { get; set; }
        public string StatusText { get; set; }
        public string StatusTone { get; set; }
        public string Remark { get; set; }
        public string UpdatedAt { get; set; }
    }

    /// <summary>
    /// 分类筛选项
    /// </summary>
    public class CategoryFilterItem
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
