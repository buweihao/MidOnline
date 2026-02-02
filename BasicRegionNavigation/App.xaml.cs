using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using BasicRegionNavigation.ViewModels;
using BasicRegionNavigation.Views;
using DryIoc; // 引用 DryIoc 原生命名空间
using DryIoc.Microsoft.DependencyInjection; // 必须！提供 Populate 扩展方法
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prism.DryIoc; // 提供 GetContainer 扩展
using Prism.Ioc;
using Prism.Modularity;
using SkiaSharp;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BasicRegionNavigation
{
    public partial class App : PrismApplication
    {
        public App()
        {
            InitializeComponent();
            // 1. 注册全局异常捕获 
            GlobalExceptionHandler.Register();
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ================================================================
            // 核心部分：连接 Microsoft DI 和 Prism DryIoc
            // ================================================================

            // A. 创建 Microsoft 的 ServiceCollection
            var services = new ServiceCollection();

            // B. 调用你封装好的扩展方法，注册 Modbus、DB、Config 等业务服务
            services.AddBusinessServices();
            // C. 获取 DryIoc 的原生容器实例
            // 注意：需要引用 using Prism.DryIoc; 才能使用 .GetContainer()
            // 如果报错，可以使用强转：var container = ((IContainerExtension<IContainer>)containerRegistry).Instance;
            DryIoc.IContainer container = containerRegistry.GetContainer();

            // D. 【关键】使用 Populate 将 ServiceCollection 里的服务“搬运”到 DryIoc 中
            // 这会自动处理生命周期（Singleton/Transient/Scoped）的转换
            container.Populate(services);

            // ================================================================
            // Prism 视图注册
            // ================================================================
            RegisterViews(containerRegistry);
        }

        private void RegisterViews(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<AlarmMonitor>();
            containerRegistry.RegisterForNavigation<Analysis>();
            containerRegistry.RegisterForNavigation<ComMonitor>();
            containerRegistry.RegisterForNavigation<Query>();
            containerRegistry.RegisterForNavigation<Setting>();
            containerRegistry.RegisterForNavigation<UserManage>();
            containerRegistry.RegisterForNavigation<ViewA, ViewAViewModel>(); // 显式关联 ViewModel 更安全
            containerRegistry.RegisterForNavigation<ViewB>();
            containerRegistry.RegisterForNavigation<Productivity>();
            containerRegistry.RegisterForNavigation<ExceptionData>();
            containerRegistry.RegisterForNavigation<HangerQuery>();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // 配置图表字体
            LiveCharts.Configure(config =>
                config.HasGlobalSKTypeface(SKTypeface.FromFamilyName("Microsoft YaHei")));
        }

        // =========================================================
        // 【核心】在这里手动启动 BackgroundService
        // =========================================================
        protected override async void OnInitialized()
        {
            // 必须先调用 base，否则 MainWindow 不会显示
            base.OnInitialized();
            // 从容器中解析出所有注册的 IHostedService
            // 这会包含 EngineLifecycleManager, DbInitializationService 等
            var hostedServices = Container.Resolve<IEnumerable<IHostedService>>();

            if (hostedServices != null)
            {
                foreach (var service in hostedServices)
                {
                    // 手动触发启动，Modbus 引擎将在这里开始轮询
                    await service.StartAsync(CancellationToken.None);
                }
            }

        }


        protected override async void OnExit(ExitEventArgs e)
        {
            // 优雅退出：调用 StopAsync 停止引擎，断开连接，保存数据等
            var hostedServices = Container.Resolve<IEnumerable<IHostedService>>();
            if (hostedServices != null)
            {
                foreach (var service in hostedServices)
                {
                    await service.StopAsync(CancellationToken.None);
                }
            }

            base.OnExit(e);
        }
    }
}