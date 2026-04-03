using System.Windows;
using CanteenProcurement.Wpf.Dialogs;

namespace CanteenProcurement.Wpf.Services
{
    public static class AppDialogService
    {
        public static bool Confirm(Window? owner, string title, string message)
        {
            var dialog = new AppMessageDialog(title, message, "info", showCancel: true)
            {
                Owner = owner ?? System.Windows.Application.Current.MainWindow
            };
            return dialog.ShowDialog() == true;
        }

        public static void ShowInfo(Window? owner, string title, string message)
        {
            Show(owner, title, message, "info");
        }

        public static void ShowWarning(Window? owner, string title, string message)
        {
            Show(owner, title, message, "warning");
        }


        public static void ShowError(Window? owner, string title, string message)
        {
            Show(owner, title, message, "error");
        }

        public static void ShowSuccess(Window? owner, string title, string message)
        {
            Show(owner, title, message, "success");
        }

        private static void Show(Window? owner, string title, string message, string tone)
        {
            var dialog = new AppMessageDialog(title, message, tone)
            {
                Owner = owner ?? System.Windows.Application.Current.MainWindow
            };
            dialog.ShowDialog();
        }
    }
}
