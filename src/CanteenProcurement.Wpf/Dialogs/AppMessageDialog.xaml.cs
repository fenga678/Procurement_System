using System.Windows;

namespace CanteenProcurement.Wpf.Dialogs
{
    public partial class AppMessageDialog : Window
    {
        public AppMessageDialog(string title, string message, string tone = "info", bool showCancel = false)
        {
            InitializeComponent();
            Title = title;
            TitleText.Text = title;
            MessageText.Text = message;
            CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
            ConfirmButton.Style = (Style)FindResource(tone == "error" ? "DangerButtonStyle" : "PrimaryButtonStyle");
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
