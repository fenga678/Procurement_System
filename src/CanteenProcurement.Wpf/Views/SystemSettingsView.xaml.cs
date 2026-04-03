using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Wpf.Providers;
using CanteenProcurement.Wpf.Services;

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

        /// <summary>
        /// 加载授权信息
        /// </summary>
        private void LoadLicenseInfo()
        {
            var license = LicensingService.Instance;
            
            // 显示机器码
            MachineCodeText.Text = license.MachineCode;
            
            // 显示授权状态
            if (license.IsRegistered)
            {
                LicenseStatusText.Text = license.GetStatusDescription();
                LicenseStatusText.Foreground = (Brush)FindResource("Brush.Success");
                
                // 隐藏试用限制面板
                TrialLimitsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                LicenseStatusText.Text = "试用版";
                LicenseStatusText.Foreground = (Brush)FindResource("Brush.Warning");
                
                // 显示试用限制
                TrialLimitsText.Text = license.GetTrialLimitsDescription().Replace("试用版限制：\n", "").Replace("• ", "");
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
                    ShellFeedbackService.ShowSuccess("机器码已复制到剪贴板，可以分享给管理员获取注册码。", "复制成功");
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
                
                // 通知主窗口更新标题
                MainWindow.Instance?.UpdateLicenseStatus();
            }
            else
            {
                AppDialogService.ShowError(Window.GetWindow(this), "注册失败", result.Message);
            }
        }

        /// <summary>
        /// 加载当前配置
        /// </summary>
        private void LoadCurrentConfiguration()
        {
            try
            {
                var config = DatabaseConfig.GetConfiguration();
                
                // 设置数据库类型
                if (config.Provider?.ToLowerInvariant() == "mysql")
                {
                    RbMySql.IsChecked = true;
                    MySqlConfigPanel.Visibility = Visibility.Visible;
                    SqliteConfigPanel.Visibility = Visibility.Collapsed;
                    DbTypeHint.Text = "MySQL 需要配置服务器连接信息，请确保 MySQL 服务已启动。";
                }
                else
                {
                    RbSqlite.IsChecked = true;
                    MySqlConfigPanel.Visibility = Visibility.Collapsed;
                    SqliteConfigPanel.Visibility = Visibility.Visible;
                    DbTypeHint.Text = "SQLite 无需额外配置，数据库文件自动创建在程序目录的 data 文件夹中。";
                }

                // SQLite 配置
                SqlitePathText.Text = config.Sqlite?.DatabasePath ?? "data/canteen.db";

                // MySQL 配置
                var mysql = config.MySql ?? new MySqlConfiguration();
                DbServerText.Text = mysql.Server;
                DbPortText.Text = mysql.Port.ToString();
                DbNameText.Text = mysql.Database;
                DbUserText.Text = mysql.User;
                DbPasswordBox.Password = mysql.Password;
            }
            catch (Exception)
            {
                // 使用默认配置
                RbSqlite.IsChecked = true;
                SqlitePathText.Text = "data/canteen.db";
                DbPortText.Text = "3306";
            }
        }

        private void DbType_Changed(object sender, RoutedEventArgs e)
        {
            if (RbSqlite == null || RbMySql == null || MySqlConfigPanel == null || SqliteConfigPanel == null)
                return;

            if (RbSqlite.IsChecked == true)
            {
                MySqlConfigPanel.Visibility = Visibility.Collapsed;
                SqliteConfigPanel.Visibility = Visibility.Visible;
                DbTypeHint.Text = "SQLite 无需额外配置，数据库文件自动创建在程序目录的 data 文件夹中。";
            }
            else
            {
                MySqlConfigPanel.Visibility = Visibility.Visible;
                SqliteConfigPanel.Visibility = Visibility.Collapsed;
                DbTypeHint.Text = "MySQL 需要配置服务器连接信息，请确保 MySQL 服务已启动。";
            }
        }

        private async void TestDb_Click(object sender, RoutedEventArgs e)
        {
            ShellFeedbackService.ShowLoading("正在测试数据库连接...");
            try
            {
                // 创建临时提供者进行测试
                IDatabaseProvider testProvider;
                
                if (RbSqlite.IsChecked == true)
                {
                    var dbPath = SqlitePathText.Text?.Trim() ?? "data/canteen.db";
                    if (!Path.IsPathRooted(dbPath))
                    {
                        dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);
                    }
                    testProvider = new SqliteProvider(dbPath);
                }
                else
                {
                    var connStr = BuildMySqlConnectionString();
                    testProvider = new MySqlProvider(connStr);
                }

                await using var conn = await testProvider.CreateAndOpenConnectionAsync();
                
                ShellFeedbackService.ShowSuccess(
                    $"数据库连接成功！\n类型: {testProvider.Name}\n{testProvider.GetDisplayConnectionString()}", 
                    "连接测试通过");
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "连接测试失败", 
                    $"连接失败：{ex.Message}\n\n请检查配置或网络连接。");
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
                var providerType = RbSqlite.IsChecked == true ? "Sqlite" : "MySql";

                // 解析端口
                int port = 3306;
                if (!string.IsNullOrWhiteSpace(DbPortText.Text) && int.TryParse(DbPortText.Text.Trim(), out var parsedPort))
                {
                    port = parsedPort;
                }

                var mysqlConfig = new MySqlConfiguration
                {
                    Server = DbServerText.Text?.Trim() ?? "localhost",
                    Port = port,
                    Database = DbNameText.Text?.Trim() ?? "canteen_procurement",
                    User = DbUserText.Text?.Trim() ?? "root",
                    Password = DbPasswordBox.Password ?? string.Empty
                };

                var sqlitePath = SqlitePathText.Text?.Trim() ?? "data/canteen.db";

                // 切换提供者（立即生效）
                DatabaseConfig.SwitchProvider(providerType, mysqlConfig, sqlitePath);
                
                // 保存配置到文件
                DatabaseConfig.SaveConfiguration();

                var displayInfo = providerType == "Sqlite" 
                    ? $"SQLite: {sqlitePath}" 
                    : $"MySQL: {mysqlConfig.Server}:{mysqlConfig.Port} / {mysqlConfig.Database}";

                ShellFeedbackService.ShowSuccess($"数据库配置已保存并立即生效！\n{displayInfo}", "配置保存成功");
            }
            catch (Exception ex)
            {
                AppDialogService.ShowError(Window.GetWindow(this), "保存失败", $"保存失败：{ex.Message}");
            }
        }

        private string BuildMySqlConnectionString()
        {
            var server = DbServerText.Text?.Trim() ?? "localhost";
            var db = DbNameText.Text?.Trim() ?? "canteen_procurement";
            var user = DbUserText.Text?.Trim() ?? "root";
            var pwd = DbPasswordBox.Password ?? string.Empty;
            
            // 解析端口
            int port = 3306;
            if (!string.IsNullOrWhiteSpace(DbPortText.Text) && int.TryParse(DbPortText.Text.Trim(), out var parsedPort))
            {
                port = parsedPort;
            }
            
            return $"server={server};port={port};database={db};user={user};password={pwd};" +
                   "AllowUserVariables=True;TreatTinyAsBoolean=true;ConvertZeroDateTime=True;SslMode=Disabled;CharSet=utf8mb4;";
        }
    }
}
