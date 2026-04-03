using System;
using CanteenProcurement.Wpf.Controls;

namespace CanteenProcurement.Wpf.Services
{
    public static class ShellFeedbackService
    {
        private static ToastHost? _toastHost;
        private static LoadingOverlay? _loadingOverlay;

        public static void Register(ToastHost toastHost, LoadingOverlay loadingOverlay)
        {
            _toastHost = toastHost;
            _loadingOverlay = loadingOverlay;
        }

        public static void ShowLoading(string message = "正在处理中，请稍候...")
        {
            _loadingOverlay?.Dispatcher.Invoke(() => _loadingOverlay.SetState(true, message));
        }

        public static void HideLoading()
        {
            _loadingOverlay?.Dispatcher.Invoke(() => _loadingOverlay.SetState(false));
        }

        public static void ShowSuccess(string message, string title = "操作成功") => ShowToast(title, message, "success");
        public static void ShowInfo(string message, string title = "提示") => ShowToast(title, message, "info");
        public static void ShowWarning(string message, string title = "注意") => ShowToast(title, message, "warning");
        public static void ShowError(string message, string title = "操作失败") => ShowToast(title, message, "error");

        private static void ShowToast(string title, string message, string tone)
        {
            _toastHost?.Dispatcher.Invoke(() => _toastHost.ShowToast(title, message, tone));
        }
    }
}
