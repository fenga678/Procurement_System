using System;
using System.Windows;
using CanteenProcurement.Wpf.Views;

namespace CanteenProcurement.Wpf.Dialogs
{
    public partial class CategoryEditorDialog : Window
    {
        private readonly CategoryItem? _existing;

        public CategoryEditorDialog(CategoryItem? existing)
        {
            InitializeComponent();
            _existing = existing;
            TitleText.Text = existing == null ? "新增分类" : "编辑分类";
            NameText.Text = existing?.Name ?? string.Empty;
            CodeText.Text = existing?.Code ?? string.Empty;
            
            // 编辑模式下编码不可修改（编码是业务主键，关联商品和采购数据）
            if (existing != null)
            {
                CodeText.IsReadOnly = true;
                CodeText.Background = (System.Windows.Media.Brush)FindResource("Brush.Surface");
                CodeText.ToolTip = "分类编码已绑定商品和采购数据，不可修改";
            }
            else
            {
                CodeText.ToolTip = "编码创建后不可修改，建议使用小写英文";
            }
            
            RatioText.Text = existing?.Ratio.ToString("F2") ?? "0.10";
            FrequencyText.Text = existing?.FrequencyDays.ToString() ?? "2";
            MinItemsText.Text = existing?.DailyMinItems.ToString() ?? "1";
            MaxItemsText.Text = existing?.DailyMaxItems.ToString() ?? "1";
            SortText.Text = existing?.Sort.ToString() ?? "1";
            StatusCheck.IsChecked = existing?.Status ?? true;
        }

        public CategoryItem? Result { get; private set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ValidationText.Visibility = Visibility.Collapsed;

            var name = NameText.Text?.Trim();
            var code = CodeText.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidation("分类名称不能为空。");
                NameText.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                ShowValidation("分类编码不能为空。");
                CodeText.Focus();
                return;
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
            {
                ShowValidation("编码格式不正确。\n必须以字母开头，只能包含字母、数字和下划线。");
                CodeText.Focus();
                return;
            }

            if (!decimal.TryParse(RatioText.Text, out var ratio) || ratio < 0 || ratio > 1)
            {
                ShowValidation("预算占比需在 0 到 1 之间（例如 0.45 表示 45%）。");
                RatioText.Focus();
                return;
            }

            if (!int.TryParse(FrequencyText.Text, out var freq) || freq <= 0)
            {
                ShowValidation("采购频率需为正整数（天数）。");
                FrequencyText.Focus();
                return;
            }

            if (!int.TryParse(MinItemsText.Text, out var minItems) || minItems <= 0)
            {
                ShowValidation("每日最小品类数需为正整数。");
                MinItemsText.Focus();
                return;
            }

            if (!int.TryParse(MaxItemsText.Text, out var maxItems) || maxItems <= 0)
            {
                ShowValidation("每日最大品类数需为正整数。");
                MaxItemsText.Focus();
                return;
            }

            if (minItems > maxItems)
            {
                ShowValidation("每日最小品类数不能大于最大品类数。");
                MinItemsText.Focus();
                return;
            }

            if (!int.TryParse(SortText.Text, out var sort) || sort < 0)
            {
                ShowValidation("排序值需为非负整数。");
                SortText.Focus();
                return;
            }

            Result = new CategoryItem(0, name, code, ratio, freq, sort, StatusCheck.IsChecked == true)
            {
                DailyMinItems = minItems,
                DailyMaxItems = maxItems,
                DailyRangeText = $"{minItems}-{maxItems}",
                UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowValidation(string message)
        {
            ValidationText.Text = message;
            ValidationText.Visibility = Visibility.Visible;
        }
    }
}
