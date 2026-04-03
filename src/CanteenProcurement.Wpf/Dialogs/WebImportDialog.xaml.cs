using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace CanteenProcurement.Wpf.Dialogs
{
    public partial class WebImportDialog : Window
    {
        public List<WebImportItem> ImportItems { get; private set; } = new();

        // 分类名称到编码的映射（从外部传入）
        private readonly Dictionary<string, string> _categoryMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _categoryCodeToName = new(StringComparer.OrdinalIgnoreCase);

        // 默认分类
        private string _defaultCategoryCode = "vegetable";
        private string _defaultCategoryName = "蔬菜类";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="categories">分类列表 (Code, Name)</param>
        public WebImportDialog(IEnumerable<(string Code, string Name)>? categories = null)
        {
            InitializeComponent();
            
            // 初始化分类映射
            if (categories != null)
            {
                var categoryList = categories.ToList();
                if (categoryList.Count > 0)
                {
                    _defaultCategoryCode = categoryList[0].Code;
                    _defaultCategoryName = categoryList[0].Name;
                }

                foreach (var (code, name) in categoryList)
                {
                    _categoryMap[name] = code;
                    _categoryCodeToName[code] = name;
                }
            }

            // 如果没有传入分类，使用默认映射
            if (_categoryMap.Count == 0)
            {
                SetDefaultCategoryMapping();
            }

            PreviewDataGrid.ItemsSource = new ObservableCollection<PreviewItem>();
        }

        private void SetDefaultCategoryMapping()
        {
            _categoryMap["蔬菜类"] = "vegetable";
            _categoryMap["肉类"] = "meat";
            _categoryMap["蛋类"] = "egg";
            _categoryMap["食用油"] = "oil";
            _categoryMap["米"] = "rice";
            _categoryMap["挂面粉条"] = "noodle";
            _categoryMap["调味品"] = "seasoning";

            _categoryCodeToName["vegetable"] = "蔬菜类";
            _categoryCodeToName["meat"] = "肉类";
            _categoryCodeToName["egg"] = "蛋类";
            _categoryCodeToName["oil"] = "食用油";
            _categoryCodeToName["rice"] = "米";
            _categoryCodeToName["noodle"] = "挂面粉条";
            _categoryCodeToName["seasoning"] = "调味品";
        }

        private void InputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            var input = InputTextBox.Text.Trim();
            var previewItems = new ObservableCollection<PreviewItem>();
            ImportItems.Clear();

            if (string.IsNullOrWhiteSpace(input))
            {
                PreviewDataGrid.ItemsSource = previewItems;
                PreviewCountText.Text = "";
                return;
            }

            var lines = input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var validCount = 0;

            foreach (var line in lines)
            {
                var item = ParseLine(line);
                if (item != null)
                {
                    ImportItems.Add(item);
                    if (previewItems.Count < 5)
                    {
                        previewItems.Add(new PreviewItem
                        {
                            Name = item.Name,
                            CategoryName = item.CategoryName,
                            Price = item.Price.ToString("F2"),
                            Unit = item.Unit,
                            MinIntervalDays = item.MinIntervalDays.ToString()
                        });
                    }
                    validCount++;
                }
            }

            PreviewDataGrid.ItemsSource = previewItems;
            PreviewCountText.Text = $"共识别 {validCount} 条有效数据";
        }

        private WebImportItem? ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // 使用正则表达式分割：制表符、多个空格、逗号
            var parts = Regex.Split(line.Trim(), @"[\t,]+|\s{2,}");
            
            // 过滤空字符串并修剪
            parts = parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();

            if (parts.Length < 1 || string.IsNullOrWhiteSpace(parts[0]))
                return null;

            var item = new WebImportItem
            {
                Name = parts[0]
            };

            // 分类
            if (parts.Length > 1)
            {
                var categoryName = parts[1].Trim();
                if (_categoryMap.TryGetValue(categoryName, out var code))
                {
                    item.CategoryCode = code;
                    item.CategoryName = _categoryCodeToName.TryGetValue(code, out var name) ? name : categoryName;
                }
                else
                {
                    // 尝试模糊匹配分类名称
                    var matchedCategory = _categoryMap.Keys
                        .FirstOrDefault(k => k.Contains(categoryName) || categoryName.Contains(k));
                    
                    if (matchedCategory != null)
                    {
                        item.CategoryCode = _categoryMap[matchedCategory];
                        item.CategoryName = matchedCategory;
                    }
                    else
                    {
                        // 如果分类名称不在映射表中，使用默认分类
                        item.CategoryCode = _defaultCategoryCode;
                        item.CategoryName = _defaultCategoryName;
                    }
                }
            }
            else
            {
                item.CategoryCode = _defaultCategoryCode;
                item.CategoryName = _defaultCategoryName;
            }

            // 单价
            if (parts.Length > 2 && decimal.TryParse(parts[2].Trim(), out var price))
            {
                item.Price = price;
            }

            // 单位
            if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
            {
                item.Unit = parts[3].Trim();
            }
            else
            {
                item.Unit = "斤";
            }

            // 最小间隔天数
            if (parts.Length > 4 && int.TryParse(parts[4].Trim(), out var minInterval))
            {
                item.MinIntervalDays = Math.Max(1, minInterval);
            }
            else
            {
                item.MinIntervalDays = 1;
            }

            return item;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImportItems.Count == 0)
            {
                MessageBox.Show("没有有效的数据可导入，请检查输入格式。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class WebImportItem
    {
        public string Name { get; set; } = "";
        public string CategoryCode { get; set; } = "vegetable";
        public string CategoryName { get; set; } = "";
        public decimal Price { get; set; }
        public string Unit { get; set; } = "斤";
        public int MinIntervalDays { get; set; } = 1;
    }

    public class PreviewItem
    {
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string Price { get; set; } = "";
        public string Unit { get; set; } = "";
        public string MinIntervalDays { get; set; } = "";
    }
}
