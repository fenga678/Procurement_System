using System.Collections.Generic;
using System.Windows;
using CanteenProcurement.Wpf.Views;

namespace CanteenProcurement.Wpf.Dialogs
{
    public partial class TaskDetailsDialog : Window
    {
        public TaskDetailsDialog(TaskItem task, IEnumerable<object> details, string summary)
        {
            InitializeComponent();
            Title = $"任务 {task.YearMonth} 明细";
            TitleText.Text = $"任务 {task.YearMonth} 采购明细";
            SummaryText.Text = summary;
            DetailsGrid.ItemsSource = details;
        }
    }
}
