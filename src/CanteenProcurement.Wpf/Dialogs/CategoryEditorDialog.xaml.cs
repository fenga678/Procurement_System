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
            if (string.IsNullOrWhiteSpace(NameText.Text) || string.IsNullOrWhiteSpace(CodeText.Text))
            {
                ShowValidation("名称和编码不能为空。");
                return;
            }
            if (!decimal.TryParse(RatioText.Text, out var ratio) || ratio < 0 || ratio > 1)
            {
                ShowValidation("预算占比需在 0 到 1 之间。");
                return;
            }
            if (!int.TryParse(FrequencyText.Text, out var freq) || freq <= 0)
            {
                ShowValidation("频率需为正整数。");
                return;
            }
            if (!int.TryParse(MinItemsText.Text, out var minItems) || minItems <= 0)
            {
                ShowValidation("最小品类数需为正整数。");
                return;
            }
            if (!int.TryParse(MaxItemsText.Text, out var maxItems) || maxItems < minItems)
            {
                ShowValidation("最大品类数不能小于最小品类数。");
                return;
            }
            if (!int.TryParse(SortText.Text, out var sort) || sort < 0)
            {
                ShowValidation("排序需为非负整数。");
                return;
            }

            Result = new CategoryItem(0, NameText.Text.Trim(), CodeText.Text.Trim(), ratio, freq, sort, StatusCheck.IsChecked == true)
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
