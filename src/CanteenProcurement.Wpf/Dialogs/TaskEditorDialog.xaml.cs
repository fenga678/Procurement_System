using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Core.Entities;
using CanteenProcurement.Wpf.Views;

namespace CanteenProcurement.Wpf.Dialogs
{
    public partial class TaskEditorDialog : Window
    {
        private readonly HashSet<string> _existingYearMonths;
        private List<Category> _categories;

        public TaskEditorDialog(IEnumerable<string> existingYearMonths, List<Category> categories = null)
        {
            InitializeComponent();
            _existingYearMonths = existingYearMonths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _categories = categories ?? GetDefaultCategories();

            YearMonthText.Text = DateTime.Now.ToString("yyyyMM");
            BudgetText.Text = "100000";
            FloatRateText.Text = "0.00";
            CreatedByText.Text = "system";

            // 动态生成分类配置行
            GenerateCategoryConfigRows(_categories);
        }

        public CreateTaskRequest? Result { get; private set; }

        /// <summary>
        /// 获取默认分类列表（用于向后兼容）
        /// </summary>
        private static List<Category> GetDefaultCategories()
        {
            return new List<Category>
            {
                new Category { Id = 1, Code = "vegetable", Name = "蔬菜类", Ratio = 0.45m, Sort = 1 },
                new Category { Id = 2, Code = "meat", Name = "肉类", Ratio = 0.30m, Sort = 2 },
                new Category { Id = 3, Code = "egg", Name = "蛋类", Ratio = 0.05m, Sort = 3 },
                new Category { Id = 4, Code = "oil", Name = "食用油", Ratio = 0.048m, Sort = 4 },
                new Category { Id = 5, Code = "rice", Name = "米", Ratio = 0.104m, Sort = 5 },
                new Category { Id = 6, Code = "noodle", Name = "挂面粉条", Ratio = 0.036m, Sort = 6 },
                new Category { Id = 7, Code = "seasoning", Name = "调味品", Ratio = 0.012m, Sort = 7 }
            };
        }

        /// <summary>
        /// 动态生成分类配置UI行
        /// </summary>
        private void GenerateCategoryConfigRows(List<Category> categories)
        {
            foreach (var category in categories.OrderBy(c => c.Sort))
            {
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 分类名称和比例标签
                var label = new Label
                {
                    Content = $"{category.Name} ({category.Ratio:P0})",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12
                };
                Grid.SetColumn(label, 0);

                // 固定金额复选框
                var checkBox = new CheckBox
                {
                    Content = "固定",
                    Tag = category.Code,
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = $"勾选后可手动指定{category.Name}的预算金额"
                };
                Grid.SetColumn(checkBox, 1);

                // 固定金额输入框（默认禁用）
                // 使用 TryFindResource 获取样式，防止资源缺失时抛出
                var textBox = new TextBox
                {
                    Style = System.Windows.Application.Current.TryFindResource("DefaultTextBoxStyle") as Style,
                    IsEnabled = false,
                    Tag = category.Code,
                    ToolTip = $"输入{category.Name}的固定预算金额"
                };
                Grid.SetColumn(textBox, 2);

                // 缓存所需画刷（避免在 TextChanged 中每次查找并为缺失资源提供回退值）
                var brushText = System.Windows.Application.Current.TryFindResource("Brush.Text") as Brush ?? SystemColors.ControlTextBrush;
                var brushDanger = System.Windows.Application.Current.TryFindResource("Brush.Danger") as Brush ?? Brushes.Red;

                // 绑定复选框事件：勾选时启用输入框，取消勾选时禁用并清空
                checkBox.Checked += (s, e) =>
                {
                    textBox.IsEnabled = true;
                    textBox.Focus();
                };
                checkBox.Unchecked += (s, e) =>
                {
                    textBox.IsEnabled = false;
                    textBox.Text = string.Empty;
                };

                // 输入框文本变化时实时验证
                textBox.TextChanged += (s, e) =>
                {
                    if (checkBox.IsChecked == true && !string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        if (!decimal.TryParse(textBox.Text, out var amount) || amount <= 0)
                        {
                            textBox.Foreground = brushDanger;
                        }
                        else
                        {
                            textBox.Foreground = brushText;
                        }
                    }
                };

                grid.Children.Add(label);
                grid.Children.Add(checkBox);
                grid.Children.Add(textBox);

                CategoryConfigPanel.Children.Add(grid);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var ym = YearMonthText.Text.Trim();
            if (string.IsNullOrWhiteSpace(ym) || ym.Length != 6 || !ym.All(char.IsDigit))
            {
                ShowValidation("请输入 6 位年月（YYYYMM）。");
                return;
            }

            if (!decimal.TryParse(BudgetText.Text, out var budget) || budget <= 0)
            {
                ShowValidation("总预算需为大于 0 的数字。");
                return;
            }

            if (!decimal.TryParse(FloatRateText.Text, out var floatRate) || floatRate < 0 || floatRate > 1)
            {
                ShowValidation("波动率需在 0 到 1 之间。");
                return;
            }

            if (_existingYearMonths.Contains(ym))
            {
                ShowValidation("该年月的任务已存在，请调整原任务或更换年月。");
                return;
            }

            // 收集固定金额配置
            var fixedAmounts = CollectFixedAmounts();

            // 验证固定金额总和不超过总预算
            if (fixedAmounts.Any())
            {
                var totalFixed = fixedAmounts.Values.Sum();
                if (totalFixed > budget)
                {
                    ShowValidation($"固定金额总额({totalFixed:C2})不能超过总预算({budget:C2})。");
                    return;
                }
            }

            Result = new CreateTaskRequest
            {
                YearMonth = ym,
                TotalBudget = budget,
                FloatRate = floatRate,
                CreatedBy = string.IsNullOrWhiteSpace(CreatedByText.Text) ? "system" : CreatedByText.Text.Trim(),
                CategoryFixedAmounts = fixedAmounts.Any() ? fixedAmounts : null
            };

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 收集用户设置的固定金额配置
        /// </summary>
        private Dictionary<string, decimal> CollectFixedAmounts()
        {
            var fixedAmounts = new Dictionary<string, decimal>();

            foreach (var child in CategoryConfigPanel.Children.OfType<Grid>())
            {
                var checkBox = child.Children.OfType<CheckBox>().FirstOrDefault();
                var textBox = child.Children.OfType<TextBox>().FirstOrDefault();

                if (checkBox?.IsChecked == true &&
                    textBox != null &&
                    decimal.TryParse(textBox.Text, out var amount) &&
                    amount > 0)
                {
                    var categoryCode = checkBox.Tag.ToString();
                    if (!string.IsNullOrEmpty(categoryCode))
                    {
                        fixedAmounts[categoryCode] = amount;
                    }
                }
            }

            return fixedAmounts;
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
