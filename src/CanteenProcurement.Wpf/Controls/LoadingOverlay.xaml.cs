using System.Windows;
using System.Windows.Controls;

namespace CanteenProcurement.Wpf.Controls
{
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }

        public void SetState(bool isActive, string? message = null)
        {
            Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            if (!string.IsNullOrWhiteSpace(message))
            {
                MessageText.Text = message;
            }
            else if (!isActive)
            {
                MessageText.Text = "请稍候...";
            }
        }
    }
}
