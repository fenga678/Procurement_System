using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CanteenProcurement.Wpf.Services;

/// <summary>
/// 异步命令执行器 - 统一处理按钮防重入、Loading 状态、异常捕获
/// 用法：await AsyncCommand.ExecuteAsync(async () => { ... }, this, "正在保存...");
/// </summary>
public static class AsyncCommand
{
    private static readonly object _lock = new();
    private static bool _isRunning;

    /// <summary>
    /// 执行异步命令（防重入 + Loading + 异常捕获）
    /// </summary>
    /// <param name="action">异步操作</param>
    /// <param name="owner">所属窗口（用于异常提示）</param>
    /// <param name="loadingMessage">Loading 提示文本（null 则不显示 Loading）</param>
    public static async Task ExecuteAsync(Func<Task> action, Window? owner = null, string? loadingMessage = null)
    {
        // 防重入
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
        }

        try
        {
            if (loadingMessage != null)
            {
                ShellFeedbackService.ShowLoading(loadingMessage);
            }

            await action();
        }
        catch (OperationCanceledException)
        {
            // 取消操作，静默处理
        }
        catch (Exception ex)
        {
            ExceptionHandler.ShowInWindow(ex, owner);
        }
        finally
        {
            if (loadingMessage != null)
            {
                ShellFeedbackService.HideLoading();
            }

            lock (_lock) { _isRunning = false; }
        }
    }

    /// <summary>
    /// 为控件设置/移除 Loading 遮罩
    /// </summary>
    public static void ShowOverlay(Panel container, string message = "加载中...")
    {
        HideOverlay(container);

        var overlay = new Grid
        {
            Name = "BusyOverlay",
            Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var textBlock = new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        overlay.Children.Add(textBlock);
        container.Children.Add(overlay);
    }

    public static void HideOverlay(Panel container)
    {
        for (var i = container.Children.Count - 1; i >= 0; i--)
        {
            if (container.Children[i] is Grid grid && grid.Name == "BusyOverlay")
            {
                container.Children.RemoveAt(i);
                break;
            }
        }
    }
}
