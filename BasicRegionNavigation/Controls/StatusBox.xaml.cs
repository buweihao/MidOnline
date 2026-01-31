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
    /// StatusBox.xaml 的交互逻辑
    /// </summary>
    public partial class StatusBox : UserControl
    {
        public StatusBox()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatusBox), new PropertyMetadata("模块标题"));

        public int Output
        {
            get => (int)GetValue(OutputProperty);
            set => SetValue(OutputProperty, value);
        }

        public static readonly DependencyProperty OutputProperty =
            DependencyProperty.Register(nameof(Output), typeof(int), typeof(StatusBox), new PropertyMetadata(0));

        public Brush StatusColor
        {
            get => (Brush)GetValue(StatusColorProperty);
            set => SetValue(StatusColorProperty, value);
        }

        public static readonly DependencyProperty StatusColorProperty =
            DependencyProperty.Register(nameof(StatusColor), typeof(Brush), typeof(StatusBox), new PropertyMetadata(Brushes.LimeGreen));
    }
}
