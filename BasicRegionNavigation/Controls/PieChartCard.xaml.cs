using LiveChartsCore;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
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
    /// PieChartCard.xaml 的交互逻辑
    /// </summary>
    public partial class PieChartCard : UserControl
    {
        public PieChartCard()
        {
            InitializeComponent();
        }

        // Series 属性
        public IEnumerable<ISeries> Series
        {
            get => (IEnumerable<ISeries>)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }

        public SolidColorPaint White 
        {
            get => new SolidColorPaint(SKColors.White);
        }

        public static readonly DependencyProperty SeriesProperty =
            DependencyProperty.Register(nameof(Series), typeof(IEnumerable<ISeries>), typeof(PieChartCard), new PropertyMetadata(null));

        // Title 属性
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(PieChartCard), new PropertyMetadata("图表标题"));
    }
}
