using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using CanteenProcurement.Wpf.Views;

namespace CanteenProcurement.Wpf.Dialogs
{
    public partial class ProductEditorDialog : Window
    {
        private readonly ProductItem? _existing;
        private readonly int _fallbackId;

        public ProductEditorDialog(ProductItem? existing, IEnumerable<(string Code, string Name)> categories, int fallbackId)
        {
            InitializeComponent();
            _existing = existing;
            _fallbackId = fallbackId;
            TitleText.Text = existing == null ? "新增商品" : "编辑商品";

            var categoryItems = categories.Select(c => new CategoryOption(c.Code, c.Name)).ToList();
            CategoryCombo.ItemsSource = categoryItems;

            NameText.Text = existing?.Name ?? string.Empty;
            PriceText.Text = existing?.Price.ToString("F2") ?? "1.00";
            UnitText.Text = existing?.Unit ?? "斤";
            MinIntervalText.Text = existing?.MinIntervalDays.ToString() ?? "2";
            RemarkText.Text = existing?.Remark ?? string.Empty;
            StatusCheck.IsChecked = existing?.IsActive ?? true;

            CategoryCombo.SelectedValue = existing?.CategoryCode ?? categoryItems.FirstOrDefault()?.Code;
        }

        public ProductItem? Result { get; private set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameText.Text))
            {
                ShowValidation("名称不能为空。");
                return;
            }
            if (CategoryCombo.SelectedItem is not CategoryOption category)
            {
                ShowValidation("请选择分类。");
                return;
            }
            if (!decimal.TryParse(PriceText.Text, out var price) || price <= 0)
            {
                ShowValidation("单价需为大于 0 的数字。");
                return;
            }
            if (!int.TryParse(MinIntervalText.Text, out var minInterval) || minInterval < 0)
            {
                ShowValidation("最小间隔需为非负整数。");
                return;
            }

            Result = new ProductItem(0, _existing?.Id ?? _fallbackId, NameText.Text.Trim(), category.Code, category.Name, price, UnitText.Text.Trim(), minInterval, StatusCheck.IsChecked == true, RemarkText.Text.Trim())
            {
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

        private sealed class CategoryOption
        {
            public CategoryOption(string code, string name)
            {
                Code = code;
                Name = name;
            }

            public string Code { get; }
            public string Name { get; }
        }
    }
}
