using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace BasicRegionNavigation.Controls
{
    public partial class CapacityChartCard : UserControl
    {
        public CapacityChartCard()
        {
            InitializeComponent();
        }

        #region 公共属性

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(CapacityChartCard), new PropertyMetadata("图表标题"));

        // 修改 1: 新增 Series 属性，直接接收 ViewModel 的图表序列
        public IEnumerable<ISeries> Series
        {
            get => (IEnumerable<ISeries>)GetValue(SeriesProperty);
            set => SetValue(SeriesProperty, value);
        }
        public static readonly DependencyProperty SeriesProperty =
            DependencyProperty.Register(nameof(Series), typeof(IEnumerable<ISeries>), typeof(CapacityChartCard), new PropertyMetadata(null));

        // 修改 2: 新增 XAxes 属性，直接接收 ViewModel 的 X 轴配置
        public IEnumerable<Axis> XAxes
        {
            get => (IEnumerable<Axis>)GetValue(XAxesProperty);
            set => SetValue(XAxesProperty, value);
        }
        public static readonly DependencyProperty XAxesProperty =
            DependencyProperty.Register(nameof(XAxes), typeof(IEnumerable<Axis>), typeof(CapacityChartCard), new PropertyMetadata(null));

        // 修改 3: 新增 YAxes 属性 (可选)
        public IEnumerable<Axis> YAxes
        {
            get => (IEnumerable<Axis>)GetValue(YAxesProperty);
            set => SetValue(YAxesProperty, value);
        }
        public static readonly DependencyProperty YAxesProperty =
            DependencyProperty.Register(nameof(YAxes), typeof(IEnumerable<Axis>), typeof(CapacityChartCard),
                new PropertyMetadata(new Axis[] {
                    new Axis {
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(50))
                    }
                }));

        #endregion
    }
}