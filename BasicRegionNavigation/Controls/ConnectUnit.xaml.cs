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

namespace BasicRegionNavigation.Controls
{
    /// <summary>
    /// ConnectUnit.xaml 的交互逻辑
    /// </summary>
    public partial class ConnectUnit : UserControl
    {
        public ConnectUnit()
        {
            InitializeComponent();
        }
        // 标题/文本
        public string ModuleTitle
        {
            get => (string)GetValue(ModuleTitleProperty);
            set => SetValue(ModuleTitleProperty, value);
        }
        public static readonly DependencyProperty ModuleTitleProperty =
            DependencyProperty.Register(nameof(ModuleTitle), typeof(string), typeof(ConnectUnit), new PropertyMetadata("模组1"));
        public Brush Model1LineColorUpLoad1
        {
            get => (Brush)GetValue(Model1LineColorUpLoad1Property);
            set => SetValue(Model1LineColorUpLoad1Property, value);
        }
        public static readonly DependencyProperty Model1LineColorUpLoad1Property =
            DependencyProperty.Register(nameof(Model1LineColorUpLoad1), typeof(Brush), typeof(ConnectUnit), new PropertyMetadata(Brushes.LimeGreen));

        public Brush Model1LineColorUpLoad2
        {
            get => (Brush)GetValue(Model1LineColorUpLoad2Property);
            set => SetValue(Model1LineColorUpLoad2Property, value);
        }
        public static readonly DependencyProperty Model1LineColorUpLoad2Property =
            DependencyProperty.Register(nameof(Model1LineColorUpLoad2), typeof(Brush), typeof(ConnectUnit), new PropertyMetadata(Brushes.LimeGreen));

        public Brush Model1LineColorDnLoad1
        {
            get => (Brush)GetValue(Model1LineColorDnLoad1Property);
            set => SetValue(Model1LineColorDnLoad1Property, value);
        }
        public static readonly DependencyProperty Model1LineColorDnLoad1Property =
            DependencyProperty.Register(nameof(Model1LineColorDnLoad1), typeof(Brush), typeof(ConnectUnit), new PropertyMetadata(Brushes.LimeGreen));

        public Brush Model1LineColorDnLoad2
        {
            get => (Brush)GetValue(Model1LineColorDnLoad2Property);
            set => SetValue(Model1LineColorDnLoad2Property, value);
        }
        public static readonly DependencyProperty Model1LineColorDnLoad2Property =
            DependencyProperty.Register(nameof(Model1LineColorDnLoad2), typeof(Brush), typeof(ConnectUnit), new PropertyMetadata(Brushes.LimeGreen));

        public Brush Model1LineColorBatch
        {
            get => (Brush)GetValue(Model1LineColorBatchProperty);
            set => SetValue(Model1LineColorBatchProperty, value);
        }
        public static readonly DependencyProperty Model1LineColorBatchProperty =
            DependencyProperty.Register(nameof(Model1LineColorBatch), typeof(Brush), typeof(ConnectUnit), new PropertyMetadata(Brushes.LimeGreen));

        public Brush Model1LineColorAround
        {
            get => (Brush)GetValue(Model1LineColorAroundProperty);
            set => SetValue(Model1LineColorAroundProperty, value);
        }
        public static readonly DependencyProperty Model1LineColorAroundProperty =
            DependencyProperty.Register(nameof(Model1LineColorAround), typeof(Brush), typeof(ConnectUnit), new PropertyMetadata(Brushes.LimeGreen));

        public Visibility LineVisibility
        {
            get => (Visibility)GetValue(LineVisibilityProperty);
            set => SetValue(LineVisibilityProperty, value);
        }

        public static readonly DependencyProperty LineVisibilityProperty =
            DependencyProperty.Register(
                nameof(LineVisibility),
                typeof(Visibility),
                typeof(ConnectUnit),
                new PropertyMetadata(Visibility.Visible));
    }
}
