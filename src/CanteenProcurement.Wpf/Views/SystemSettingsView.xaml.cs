using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Wpf.Dialogs;
using CanteenProcurement.Wpf.Services;
using Microsoft.Win32;

namespace CanteenProcurement.Wpf.Views
{
    public partial class SystemSettingsView : UserControl
    {
        public SystemSettingsView()
        {
            InitializeComponent();
            LoadCurrentConfiguration();
            LoadLicenseInfo();
        }

        private void LoadLicenseInfo()
        {
            var license = LicensingService.Instance;
            MachineCodeText.Text = license.MachineCode;

            if (license.IsRegistered)
            {
                LicenseStatusText.Text = license.GetStatusDescription();
                LicenseStatusText.Foreground = (Brush)FindResource("Brush.Success");
                TrialLimitsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                LicenseStatusText.Text = "试用版";
                LicenseStatusText.Foreground = (Brush)FindResource("Brush.Warning");
                TrialLimitsText.Text = license.GetTrialLimitsDescription()
                    .Replace("试用版限制：\n", string.Empty)
                    .Replace("•", string.Empty);
            }
        }

        private void CopyMachineCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var machineCode = MachineCodeText.Text;
                if (!string.IsNullOrWhiteSpace(machineCode))
                {
                    Clipboard.SetText(machineCode);
                    ShellFeedbackService.ShowSuccess("机器码已复制到剪贴板，可用于获取注册码。", "复制成功");
                }
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "复制失败", $"复制失败：{ex.Message}");
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var licenseKey = LicenseKeyText.Text?.Trim();
            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                AppDialogService.ShowWarning(Window.GetWindow(this), "注册失败", "请输入注册码。");
                return;
            }

            var license = LicensingService.Instance;
            var result = license.ValidateLicenseKey(licenseKey);

            if (result.Success)
            {
                AppDialogService.ShowSuccess(Window.GetWindow(this), "注册成功", result.Message);
                LoadLicenseInfo();
                LicenseKeyText.Text = string.Empty;
                MainWindow.Instance?.UpdateLicenseStatus();
            }
            else
            {
                AppDialogService.ShowError(Window.GetWindow(this), "注册失败", result.Message);
            }
        }

        private void LoadCurrentConfiguration()
        {
            try
            {
                var config = DatabaseConfig.GetConfiguration();
                SqlitePathText.Text = config.DatabasePath ?? "data/canteen.db";
            }
            catch
            {
                SqlitePathText.Text = "data/canteen.db";
            }
        }

        private async void TestDb_Click(object sender, RoutedEventArgs e)
        {
            ShellFeedbackService.ShowLoading("正在测试数据库连接...");
            try
            {
                await using var conn = await DatabaseConfig.CreateAndOpenConnectionAsync();
                ShellFeedbackService.ShowSuccess("数据库连接成功。", "连接测试通过");
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "连接测试失败", $"连接失败：{ex.Message}\n\n请检查配置或网络连接。");
            }
            finally
            {
                ShellFeedbackService.HideLoading();
            }
        }

        private void SaveDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sqlitePath = SqlitePathText.Text?.Trim() ?? "data/canteen.db";
                DatabaseConfig.UpdateSqlitePath(sqlitePath);
                DatabaseConfig.SaveConfiguration();
                ShellFeedbackService.ShowSuccess($"数据库配置已保存并立即生效。\nSQLite: {sqlitePath}", "配置保存成功");
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "保存失败", $"保存失败：{ex.Message}");
            }
        }

        private void BackupDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = DatabaseConfig.GetConfiguration();
                var dbPath = config.DatabasePath ?? "data/canteen.db";
                if (!Path.IsPathRooted(dbPath))
                {
                    dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
                }

                if (!File.Exists(dbPath))
                {
                    AppDialogService.ShowWarning(Window.GetWindow(this), "备份失败", "数据库文件不存在，无法备份。");
                    return;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var defaultFileName = $"canteen_backup_{timestamp}.db";
                
                var dialog = new SaveFileDialog
                {
                    Filter = "SQLite 数据库 (*.db)|*.db",
                    FileName = defaultFileName,
                    Title = "选择备份保存位置"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.Copy(dbPath, dialog.FileName, true);
                    ShellFeedbackService.ShowSuccess($"数据已备份到：\n{dialog.FileName}", "备份完成");
                }
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "备份失败", $"备份失败：{ex.Message}");
            }
        }

        private void RestoreDb_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "SQLite 数据库 (*.db)|*.db",
                    Title = "选择备份文件进行恢复"
                };

                if (dialog.ShowDialog() != true) return;

                if (!AppDialogService.Confirm(Window.GetWindow(this), "确认恢复",
                    "恢复数据将覆盖当前数据库，所有未保存的更改将丢失。\n\n确定要继续吗？"))
                {
                    return;
                }

                var config = DatabaseConfig.GetConfiguration();
                var dbPath = config.DatabasePath ?? "data/canteen.db";
                if (!Path.IsPathRooted(dbPath))
                {
                    dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
                }

                // 确保目标目录存在
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.Copy(dialog.FileName, dbPath, true);
                
                AppDialogService.ShowWarning(Window.GetWindow(this), "恢复完成",
                    "数据已恢复。请重启程序以使更改生效。");
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "恢复失败", $"恢复失败：{ex.Message}");
            }
        }
    }
}
