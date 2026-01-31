using System;
using System.Collections;
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

namespace BasicRegionNavigation.Controls
{
    /// <summary>
    /// AlarmListControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlarmListControl : UserControl
    {
        public AlarmListControl()
        {
            InitializeComponent();
        }

        // 定义依赖属性
        public static readonly DependencyProperty AlarmItemsSourceProperty =
            DependencyProperty.Register(
                nameof(AlarmItemsSource),      // 属性名
                typeof(IEnumerable),           // 属性类型 (DataGrid源通常是IEnumerable)
                typeof(AlarmListControl),      // 拥有者类型
                new PropertyMetadata(null));   // 默认值

        // CLR 包装属性
        public IEnumerable AlarmItemsSource
        {
            get { return (IEnumerable)GetValue(AlarmItemsSourceProperty); }
            set { SetValue(AlarmItemsSourceProperty, value); }
        }
    }
}
