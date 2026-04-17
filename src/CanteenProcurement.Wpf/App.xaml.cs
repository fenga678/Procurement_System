using System.Windows;
using CanteenProcurement.Wpf.Services;
using CanteenProcurement.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenProcurement.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 注册全局异常处理
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        base.OnStartup(e);

        try
        {
            AppHost.ConfigureServices();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"应用启动失败，无法初始化核心服务。\n{exception.Message}",
                "启动失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var (title, message) = ExceptionHandler.Classify(e.Exception);
        MessageBox.Show(
            $"操作失败：{message}\n\n如有疑问，请联系技术支持。",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"程序发生严重错误：{ex.Message}\n\n程序将自动退出。",
                "致命错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
