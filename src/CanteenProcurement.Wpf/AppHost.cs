using CanteenProcurement.Application.Interfaces;
using CanteenProcurement.Application.Services;
using CanteenProcurement.Core.Interfaces;
using CanteenProcurement.Infrastructure.Providers;
using CanteenProcurement.Infrastructure.Repositories;
using CanteenProcurement.Infrastructure.Services;
using CanteenProcurement.Wpf.Providers;
using CanteenProcurement.Wpf.Services;
using CanteenProcurement.Wpf.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenProcurement.Wpf;

/// <summary>
/// 应用服务容器入口
/// </summary>
public static class AppHost
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static void ConfigureServices()
    {
        var serviceCollection = new ServiceCollection();

        // 基础设施
        var dbFactory = DatabaseProviderFactory.Initialize();
        serviceCollection.AddSingleton(dbFactory);
        serviceCollection.AddSingleton<IDatabaseProvider>(dbFactory.Provider);

        // 数据库连接工厂
        serviceCollection.AddScoped<Func<Task<System.Data.Common.DbConnection>>>(sp =>
        {
            var provider = sp.GetRequiredService<IDatabaseProvider>();
            return () => provider.CreateAndOpenConnectionAsync();
        });

        // 服务
        serviceCollection.AddSingleton<SchemaCapabilitiesService>();
        serviceCollection.AddSingleton<SchemaCapabilitiesProvider>();
        serviceCollection.AddSingleton<LicensingService>(sp => LicensingService.Instance);

        // 仓储
        serviceCollection.AddSingleton<ICategoryRepository, CategoryRepository>();
        serviceCollection.AddSingleton<IProductRepository, ProductRepository>();
        serviceCollection.AddSingleton<ITaskRepository, TaskRepository>();

        // 应用服务
        serviceCollection.AddSingleton<ITaskPlanningService, TaskPlanningService>();

        // WPF 数据服务
        serviceCollection.AddSingleton<CategoryDataService>();
        serviceCollection.AddSingleton<ProductDataService>();
        serviceCollection.AddSingleton<TaskDataService>();

        // WPF 页面（Transient）
        serviceCollection.AddTransient<CategoryManagementView>();
        serviceCollection.AddTransient<ProductManagementView>();
        serviceCollection.AddTransient<TaskManagementView>();
        serviceCollection.AddTransient<SystemSettingsView>();

        // WPF 主窗口
        serviceCollection.AddTransient<MainWindow>();

        Services = serviceCollection.BuildServiceProvider();

        // 预初始化：确保数据库连接已建立（触发迁移）
        _ = Services.GetRequiredService<IDatabaseProvider>();
    }
}
