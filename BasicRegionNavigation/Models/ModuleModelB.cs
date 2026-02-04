using BasicRegionNavigation.Controls;
using BasicRegionNavigation.Services;
using BasicRegionNavigation.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Core;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static BasicRegionNavigation.Models.CurrentStatus;
using Axis = LiveChartsCore.SkiaSharpView.Axis;

namespace BasicRegionNavigation.Models
{
    public partial class ModuleModelB : ObservableObject
    {
        public ModuleModelB(string id)
        {
            _moduleId = id;
        }

        [ObservableProperty]
        private string _moduleId = "0";

        // --- 1. 饼图数据 (PieChartCard) ---
        [ObservableProperty]
        private CurrentPieInfo _currentPieInfo = new CurrentPieInfo();

        //// --- 2. 产品生产信息数据 (TwoDataTableWithHeader) ---
        [ObservableProperty]
        private CurrentProductInfoB _currentProductInfo = new CurrentProductInfoB();

        // --- 3. 设备效能数据 (DataTableWithHeader) ---
        [ObservableProperty]
        private CurrentEfficiencyInfo _currentEfficiencyInfo = new CurrentEfficiencyInfo();

        //// --- 4. 故障统计图表数据 (CartesianChart) ---
        [ObservableProperty]
        private CurrentFaultStatsInfo _currentFaultStatsInfo = new CurrentFaultStatsInfo();

        /// <summary>
        /// 统一数据分发入口
        /// </summary>
        public void DispatchData(ModuleDataCategory category, object data)
        {
            switch (category)
            {
                // 1. 饼图
                case ModuleDataCategory.UpPieInfo:
                case ModuleDataCategory.DnPieInfo:
                    _currentPieInfo.UpdateValue(category, data);
                    break;

                //// 2. 产品信息表 (上/下)
                case ModuleDataCategory.ProductInfoTop:
                case ModuleDataCategory.ProductInfoBottom:
                    _currentProductInfo.UpdateValue(category, data);
                    break;

                //// 3. 效能表
                case ModuleDataCategory.EfficiencyData:
                    _currentEfficiencyInfo.UpdateValue(data);
                    break;

                //// 4. 故障统计图
                case ModuleDataCategory.FaultStatsSeries:
                case ModuleDataCategory.FaultStatsAxis:
                    _currentFaultStatsInfo.UpdateValue(category, data);
                    break;

                default:
                    Console.WriteLine($"未识别的数据类别: {category}");
                    break;
            }
        }
    }
    /// <summary>
    /// 2. 产品生产信息 (对应 TwoDataTableWithHeader)
    /// </summary>
    public partial class CurrentProductInfoB : ObservableObject
    {
        // 上表数据源
        [ObservableProperty]
        private ObservableCollection<ProductInfoTable> _topData = new();

        // 下表数据源
        [ObservableProperty]
        private ObservableCollection<ProductInfoTable> _bottomData = new();

        public void UpdateValue(ModuleDataCategory category, object data)
        {
            // 假设传入的是 List 或 ObservableCollection
            if (data is not IEnumerable<ProductInfoTable> list) return;

            var target = category == ModuleDataCategory.ProductInfoTop ? TopData : BottomData;

            // 全量更新 (也可以做增量更新优化)
            target.Clear();
            foreach (var item in list)
            {
                target.Add(item);
            }
        }
    }
    /// <summary>
    /// 3. 设备效能数据 (对应 DataTableWithHeader)
    /// </summary>
    public partial class CurrentEfficiencyInfo : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ProductionEfficiencyTable> _efficiencyData = new();

        public void UpdateValue(object data)
        {
            if (data is not IEnumerable<ProductionEfficiencyTable> list) return;

            EfficiencyData.Clear();
            foreach (var item in list)
            {
                EfficiencyData.Add(item);
            }
        }
    }

    /// <summary>
    /// 4. 故障统计图表 (对应 CartesianChart)
    /// </summary>
    public partial class CurrentFaultStatsInfo : ObservableObject
    {
        [ObservableProperty]
        private ISeries[] _series = Array.Empty<ISeries>();

        [ObservableProperty]
        private Axis[] _xAxes =
        {
            new Axis { Labels = new[] { "上料机1", "上料机2", "上翻转台", "下料机1", "下料机2", "下翻转台" }, LabelsPaint = new SolidColorPaint(SKColors.White), TextSize = 14 }
        };

        [ObservableProperty]
        private Axis[] _yAxes =
        {
            new Axis { MinLimit = 0, LabelsPaint = new SolidColorPaint(SKColors.Red), Name = "时间(min)", Position = LiveChartsCore.Measure.AxisPosition.Start },
            new Axis { MinLimit = 0, LabelsPaint = new SolidColorPaint(SKColors.Aqua), Name = "次数", Position = LiveChartsCore.Measure.AxisPosition.End }
        };

        public void UpdateValue(ModuleDataCategory category, object data)
        {
            // 如果是故障统计相关的更新
            if (category == ModuleDataCategory.FaultStatsSeries)
            {
                // 1. 解析三元组数据 (Tuple<Labels, FailureTime, FailureCount>)
                // Item1: string[] (设备名称/X轴标签)
                // Item2: double[] (故障时间)
                // Item3: double[] (故障次数)
                if (data is Tuple<string[], double[], double[]> tupleData)
                {
                    var labels = tupleData.Item1;
                    var timeValues = tupleData.Item2;
                    var countValues = tupleData.Item3;

                    // 2. 更新 X 轴标签
                    // LiveCharts2 的 Axis 是响应式的，直接赋值即可
                    if (XAxes != null && XAxes.Length > 0)
                    {
                        XAxes[0].Labels = labels;
                    }

                    // 3. 更新 Series 数据
                    // 直接重新创建 Series 集合是最简单的全量刷新方式
                    Series = new ISeries[]
                    {
                new ColumnSeries<double>
                {
                    Name = "故障时间",
                    Values = timeValues,
                    Fill = new SolidColorPaint(SKColors.Red),
                    ScalesYAt = 0 // 对应左侧 Y 轴
                },
                new ColumnSeries<double>
                {
                    Name = "故障次数",
                    Values = countValues,
                    Fill = new SolidColorPaint(SKColors.Aqua),
                    ScalesYAt = 1 // 对应右侧 Y 轴
                }
                    };
                }
            }
        }
    }
}
