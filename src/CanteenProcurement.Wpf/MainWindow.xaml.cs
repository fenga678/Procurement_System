using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CanteenProcurement.Wpf.Controls;
using CanteenProcurement.Wpf.Services;
using CanteenProcurement.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenProcurement.Wpf
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private static MainWindow? _instance;
        
        /// <summary>
        /// 单例实例（用于其他页面访问）
        /// </summary>
        public static MainWindow? Instance => _instance;

        private string _licenseStatus = "试用版";
        
        /// <summary>
        /// 授权状态文本
        /// </summary>
        public string LicenseStatus
        {
            get => _licenseStatus;
            set
            {
                if (_licenseStatus != value)
                {
                    _licenseStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            _instance = this;
            InitializeComponent();
            DataContext = this;
            Loaded += MainWindow_Loaded;
            
            // 初始化授权状态
            UpdateLicenseStatus();
        }

        /// <summary>
        /// 更新授权状态显示
        /// </summary>
        public void UpdateLicenseStatus()
        {
            var license = LicensingService.Instance;
            LicenseStatus = license.GetStatusDescription();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ShellFeedbackService.Register(toastHost, loadingOverlay);
            LoadSection(ShellSection.CategoryManagement);
        }

        private void SidebarNav_SectionSelected(object? sender, ShellSectionChangedEventArgs e)
        {
            LoadSection(e.Section);
        }


        private void LoadSection(ShellSection section)
        {
            sidebarNav.SelectedSection = section;

            switch (section)
            {
                case ShellSection.CategoryManagement:
                    LoadView(() => AppHost.Services.GetRequiredService<CategoryManagementView>());
                    break;
                case ShellSection.ProductManagement:
                    LoadView(() => AppHost.Services.GetRequiredService<ProductManagementView>());
                    break;
                case ShellSection.TaskManagement:
                    LoadView(() => AppHost.Services.GetRequiredService<TaskManagementView>());
                    break;
                case ShellSection.Settings:
                    LoadView(() => AppHost.Services.GetRequiredService<SystemSettingsView>());
                    break;
            }
        }

        private void LoadView(Func<object> factory)
        {
            try
            {
                mainContent.Content = factory();
            }
            catch (Exception ex)
            {
                ShellFeedbackService.ShowError($"页面加载失败：{ex.Message}");
            }
        }
    }
}
