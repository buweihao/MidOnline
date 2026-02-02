using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using BasicRegionNavigation.Views;
using CommunityToolkit.Mvvm.ComponentModel; // 核心：替换 BindableBase
using CommunityToolkit.Mvvm.Input;        // 核心：替换手动 Command 定义
using Core;
using HandyControl.Controls;
using MyModbus;
using Prism.Commands; // 保留 Prism 命令，用于导航
using Prism.Mvvm;     // 如果不再使用 BindableBase，可以移除此引用，但 Prism.Regions 可能需要
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static BasicRegionNavigation.Services.MiddleFrameBusinessServices;
namespace BasicRegionNavigation.ViewModels
{
    // 1. 必须添加 partial 关键字
    // 2. 继承 ObservableObject
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IRegionManager _regionManager;
        private readonly IConfigService _configService;
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        private readonly Func<DashboardWindow> _dashboardFactory;

        // ========================== 属性区域 ==========================

        [ObservableProperty]
        private string _loadingText;

        [ObservableProperty]
        private string _title_ch;

        [ObservableProperty]
        private string _title_en;

        [ObservableProperty]
        private Visibility _isLoading = Visibility.Collapsed;

        [ObservableProperty]
        private string _currentView;

        [ObservableProperty]
        private string _title = "Prism Unity Application";

        [ObservableProperty]
        private Visibility _loginedSign = Visibility.Collapsed;

        [ObservableProperty]
        private Visibility _loginingSign = Visibility.Visible;

        [ObservableProperty]
        private string _currentTime;

        [ObservableProperty]
        private string _bindingRole = CurrentUserContext.role.ToString() ?? Role.Guest.ToString();

        // 这是一个标志位，虽然没用 [ObservableProperty]，但保持原样
        public bool isFirstTimeLoad = true;

        // ========================== 构造函数 ==========================
        public ICommand OpenDashboardCommand { get; }
        public MainWindowViewModel(
            IMiddleFrameBusinessServices flipperHourlyCapacityService,
            IRegionManager regionManager, Func<DashboardWindow> dashboardFactory, IConfigService configService)
        {
            _regionManager = regionManager;
            _configService = configService;
            _dashboardFactory = dashboardFactory;

            // Prism 的 DelegateCommand 依然可以使用，特别是如果你需要 Prism 特有的功能
            // 如果想纯化，也可以换成 [RelayCommand]
            NavigateCommand = new DelegateCommand<string>(NavigateProxy);

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            };
            _timer.Start();

            var loadingManager = new LoadingManager(this);
            Global.GlobalMain(loadingManager);

            // 注意：这里使用的是生成的属性（大写开头）
            Title_ch = _configService.GetConfigValue("Title_ch");
            Title_en = _configService.GetConfigValue("Title_en");
            OpenDashboardCommand = new MyModbus.RelayCommand(_ => OpenDashboard());

            _flipperHourlyCapacityService = flipperHourlyCapacityService;
            _flipperHourlyCapacityService.ProductCollectionMissionStart();
            _flipperHourlyCapacityService.ChangeoverMissionStart();
            _flipperHourlyCapacityService.TimeSyncMissionStart();
            //_flipperHourlyCapacityService.StartCollectionTask(CollectionFrequency.Minutely);
        }

        IMiddleFrameBusinessServices _flipperHourlyCapacityService;

        private void OpenDashboard()
        {
            var win = _dashboardFactory();
            win.Owner = Application.Current.MainWindow;

            // --- 新增逻辑：根据屏幕工作区动态调整 ---
            // 获取主屏幕的工作区高度（即屏幕高度减去任务栏高度）
            double screenHeight = SystemParameters.WorkArea.Height;
            double screenWidth = SystemParameters.WorkArea.Width;

            // 如果预设的 600/900 超过了屏幕的 90%，则缩小窗口
            if (win.Height > screenHeight * 0.9)
            {
                win.Height = screenHeight * 0.9;
            }
            if (win.Width > screenWidth * 0.9)
            {
                win.Width = screenWidth * 0.9;
            }
            // -------------------------------------

            win.Show();
        }        // ========================== 命令区域 ==========================

        // 导航命令属性 (保持 Prism 风格以兼容现有绑定)
        public DelegateCommand<string> NavigateCommand { get; private set; }

        /// <summary>
        /// 使用 CommunityToolkit 的 [RelayCommand] 生成 AddModuleCommand
        /// 方法名 AddModule -> 生成命令 AddModuleCommand
        /// </summary>
        [RelayCommand]
        private async Task AddModule()
        {
            // 改变config中的Modules,并且重载
            // 注意：做好异常处理，防止 int.Parse 崩溃
            if (int.TryParse(_configService.GetConfigValue("Modules"), out int count))
            {
                if (count >= 12)
                {
                    GrowlHelper.Warning($"添加失败,已达最大值,请联系工程师增加");
                    return;
                }

                if (Global.SetConfig("Modules", (count + 1).ToString()))
                    GrowlHelper.Success($"添加成功，当前模组数:{count + 1}");

                // 重载
                Global.ReInit();
            }
        }

        /// <summary>
        /// 使用 [RelayCommand] 生成 MinusModuleCommand
        /// </summary>
        [RelayCommand]
        private async Task MinusModule()
        {
            if (int.TryParse(_configService.GetConfigValue("Modules"), out int count))
            {
                if (count <= 0)
                {
                    GrowlHelper.Warning($"减少失败，已达最小值");
                    return;
                }
                if (Global.SetConfig("Modules", (count - 1).ToString()))
                    GrowlHelper.Success($"减少成功，当前模组数:{count - 1}");

                // 重载
                Global.ReInit();
            }
        }

        // ========================== 逻辑方法 ==========================

        // 原 Navigate_ 方法，改名为 Proxy 以便区分
        public void NavigateProxy(string navigatePath)
        {
            Global.SecureRoleExecute<string>(path => Navigate(path), navigatePath);
        }

        [RequireRole(Role.User)]
        public void Navigate(string navigatePath)
        {
            // 访问生成的属性 CurrentView
            if (navigatePath == CurrentView)
                return; // 阻止重复导航

            CurrentView = navigatePath;

            if (navigatePath != null)
                _regionManager.RequestNavigate("ContentRegion", navigatePath);
        }
    }

    // LoadingManager 稍微调整以适配 ObservableObject (基本不用动，因为属性名没变)
    public class LoadingManager : ILoadingManager
    {
        // 警告：静态引用 ViewModel 可能会导致内存泄漏，建议检查生命周期
        static MainWindowViewModel _vm;

        public LoadingManager(MainWindowViewModel vm)
        {
            _vm = vm;
        }

        public void StartLoading(string mes = "")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrEmpty(mes))
                    _vm.LoadingText = mes;
                else
                    _vm.LoadingText = "加载中...";

                _vm.IsLoading = Visibility.Visible;
            });
        }

        public void StopLoading()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _vm.IsLoading = Visibility.Collapsed;
            });
        }
    }
}