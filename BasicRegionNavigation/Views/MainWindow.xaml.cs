using BasicRegionNavigation;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using BasicRegionNavigation.ViewModels;
using BasicRegionNavigation.Views;
using Core;
using HandyControl.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Wpf;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WebView2 = Microsoft.Web.WebView2.Wpf.WebView2;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private readonly IConfigService _configService;
        MainWindowViewModel vm;
        private WindowStyle _oldWindowStyle;
        private ResizeMode _oldResizeMode;
        private Brush _oldBackground;
        public MainWindow(IConfigService configService)
        {
            _configService = configService;
            InitializeComponent();

            SCADAInit();

            //InitializeWebView(View1, "my.chart1", "view1");
            //InitializeWebView(View2, "my.chart2", "view2");
            vm = (MainWindowViewModel)this.DataContext;
        }

        private void SCADAInit()
        {
        }


        // Close button click event handler
        private void close_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // 关闭窗口
        }

        // Minimize button click event handler
        private void min_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized; // 最小化窗口
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            //vm.LoginedSign = Visibility.Collapsed;
            //vm.LoginingSign = Visibility.Collapsed;

            //// 缓存原始样式和尺寸
            //_oldWindowStyle = this.WindowStyle;
            //_oldResizeMode = this.ResizeMode;
            //_oldBackground = this.Background;

            //// 切到无边框、固定 450×800 的“纯应用视觉”
            //this.WindowStyle = WindowStyle.None;
            //this.ResizeMode = ResizeMode.NoResize;
            //this.Width = 680;
            //this.Height = 420;
            //var imageUri = new Uri("pack://application:,,,/Resources/OnImage1.png", UriKind.Absolute);
            //this.Background = new ImageBrush(new BitmapImage(imageUri))
            //{
            //    Stretch = Stretch.UniformToFill
            //};

            //var timer = new DispatcherTimer
            //{
            //    Interval = TimeSpan.FromSeconds(2)
            //};
            //timer.Tick += (s, args) =>
            //{
            //    timer.Stop();
            //    vm.LoginingSign = Visibility.Visible;
            //    //WelcomeImage.Visibility = Visibility.Collapsed;
            //    // 恢复原始样式和尺寸
            //    this.WindowStyle = _oldWindowStyle;
            //    this.ResizeMode = _oldResizeMode;
            //    this.Width = 1920;
            //    this.Height = 1080;
            //    this.Background = _oldBackground;
            //    this.WindowStartupLocation = WindowStartupLocation.Manual;
            //    this.Left = 0;
            //    this.Top = 0;
            //};
            //timer.Start();

        }
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 左键按下拖动窗口
            if (e.ButtonState == MouseButtonState.Pressed)
                this.DragMove();
        }
        private void OnRoleAreaClicked(object sender, MouseButtonEventArgs e)
        {
            // 这里写你想要执行的逻辑
            vm.LoginedSign = Visibility.Collapsed;
            vm.LoginingSign = Visibility.Visible;
        }
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Global.LoadingManager.StartLoading("Ctrl + - 新增/减少模组，右键模组名编辑IP");
            try
            {
                if (UserName.Text == "Admin")
                {
                    if (UserPassWrod.Password != "Admin")
                    {
                        HandyControl.Controls.MessageBox.Show("密码错误，请重新输入！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    //切换身份
                    CurrentUserContext.role = Role.Admin;
                    ShowRole.Text = "Administrator";
                    GrowlHelper.Success("当前身份：Administrator");

                }
                else if (UserName.Text == "User")
                {
                    if (UserPassWrod.Password != "User")
                    {
                        HandyControl.Controls.MessageBox.Show("密码错误，请重新输入！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    //切换身份
                    CurrentUserContext.role = Role.User;
                    ShowRole.Text = "User";
                    GrowlHelper.Success("当前身份：User");
                }
                else
                {
                }


                // 切换界面可见性
                vm.LoginedSign = Visibility.Visible;
                vm.LoginingSign = Visibility.Collapsed;

                // 设置登录成功后的背景图（可选）
                this.Background = new ImageBrush(new BitmapImage(new Uri("pack://application:,,,/Resources/backPic.png")));

                vm.NavigateProxy("ViewA");
                await Task.Delay(2000);

                //重新加载需要重载的内容
                //var loadingManager = new LoadingManager(vm);
                //Global.GlobalMain(loadingManager);
            }
            finally
            {
                Global.LoadingManager.StopLoading();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                HideAll(); // 先全部隐藏

                switch (e.SystemKey)
                {
                    case Key.A:  break;
                    case Key.D: View1.Visibility = Visibility.Visible; break;
                    case Key.F: View2.Visibility = Visibility.Visible; break;
                }
            }
        }

        private void HideAll()
        {
            View1.Visibility = Visibility.Collapsed;
            View2.Visibility = Visibility.Collapsed;
        }
        private async void InitializeWebView(WebView2 view, string hostName, string folder)
        {
            await view.EnsureCoreWebView2Async();

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string wwwrootPath = System.IO.Path.Combine(baseDir, "wwwroot", folder);

            if (!Directory.Exists(wwwrootPath))
            {
                System.Windows.MessageBox.Show($"找不到路径：{wwwrootPath}");
                return;
            }

            // 映射虚拟域名
            view.CoreWebView2.SetVirtualHostNameToFolderMapping(
                hostName,
                wwwrootPath,
                CoreWebView2HostResourceAccessKind.Allow
            );

            // 可选
            view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

            // 加载 index.html
            view.Source = new Uri($"http://{hostName}/index.html");
        }
    }
}
