using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// LoadingOverlay.xaml 的交互逻辑
    /// </summary>
    public partial class LoadingOverlay : UserControl
    {
        public LoadingOverlay()
        {
            InitializeComponent();
        }
        // 注册依赖属性
        public static readonly DependencyProperty LoadingTextProperty =
            DependencyProperty.Register(
                nameof(LoadingText),              // 属性名
                typeof(string),                   // 属性类型
                typeof(LoadingOverlay),           // 所属类型
                new PropertyMetadata("正在加载...") // 默认值
            );

        // CLR 包装器
        public string LoadingText
        {
            get => (string)GetValue(LoadingTextProperty);
            set => SetValue(LoadingTextProperty, value);
        }
    }
}
