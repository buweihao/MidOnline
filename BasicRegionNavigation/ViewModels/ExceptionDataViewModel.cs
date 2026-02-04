using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel; // 核心命名空间
using CommunityToolkit.Mvvm.Input;        // RelayCommand 需要
using Core;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using HandyControl.Controls;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Extensions;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using static HslCommunication.Profinet.Knx.KnxCode;
using Axis = LiveChartsCore.SkiaSharpView.Axis;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace BasicRegionNavigation.ViewModels
{
    // 修改 1: 添加 partial 关键字
    // 修改 2: 继承 ObservableObject (替代 BindableBase)
    // 修改 3: 移除 [AddINotifyPropertyChangedInterface]
    internal partial class ExceptionDataViewModel : ObservableObject, INavigationAware
    {
        // ==================== 属性转换区域 ====================

        [ObservableProperty]
        private ObservableCollection<string> _moduleList = new ObservableCollection<string> { "1", "2", "3" };

        [ObservableProperty]
        private string _selectModule = "";

        [ObservableProperty]
        private ObservableCollection<string> _unitList = new ObservableCollection<string> { "年", "月", "日", "时" };

        [ObservableProperty]
        private string _selectedUnit = "月";

        [ObservableProperty]
        private DateTime _start = DateTime.Now.AddYears(-1);

        [ObservableProperty]
        private DateTime _end = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<Axis> _aXAxes = new ObservableCollection<Axis>();

        [ObservableProperty]
        private ObservableCollection<ISeries> _myPieSeries = new ObservableCollection<ISeries>();

        [ObservableProperty]
        private Axis[] _aYAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                LabelsPaint = new SolidColorPaint(SKColors.White),
                TextSize = 14,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(100))
            }
        };

        [ObservableProperty]
        private ObservableCollection<AbnormalItem> _abnormalList = new()
        {
            new AbnormalItem { DeviceName="上料机A", Mix="是", ScanNg="否", SystemFeedback="通信超时", MaterialLost="是" },
        };

        [ObservableProperty]
        private ISeries[] _series;

        [ObservableProperty]
        private Axis[] _xAxes;

        [ObservableProperty]
        private Axis[] _yAxes;

        [ObservableProperty]
        private ISeries[] _aSeries = new ISeries[]
        {
            // 上料机1
            new LineSeries<int>
            {
                Name = "上料机1",
                Values = new[] { 30, 26, 33, 28, 24, 32 },
                GeometrySize = 8,
                Stroke = new SolidColorPaint(new SKColor(144, 238, 144)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.LightGreen),
                GeometryStroke = new SolidColorPaint(SKColors.White),
                Fill = null
            },
            // 上料机2
            new LineSeries<int>
            {
                Name = "上料机2",
                Values = new[] { 25, 29, 27, 31, 26, 30 },
                GeometrySize = 8,
                Stroke = new SolidColorPaint(new SKColor(70, 130, 180)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.SkyBlue),
                GeometryStroke = new SolidColorPaint(SKColors.White),
                Fill = null
            },
            // 下料机1
            new LineSeries<int>
            {
                Name = "下料机1",
                Values = new[] { 28, 24, 29, 27, 25, 26 },
                GeometrySize = 8,
                Stroke = new SolidColorPaint(new SKColor(255, 99, 71)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.OrangeRed),
                GeometryStroke = new SolidColorPaint(SKColors.White),
                Fill = null
            },
            // 下料机2
            new LineSeries<int>
            {
                Name = "下料机2",
                Values = new[] { 22, 26, 24, 28, 23, 25 },
                GeometrySize = 8,
                Stroke = new SolidColorPaint(new SKColor(255, 215, 0)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.Gold),
                GeometryStroke = new SolidColorPaint(SKColors.White),
                Fill = null
            },
            // 上翻转
            new LineSeries<int>
            {
                Name = "上翻转",
                Values = new[] { 20, 22, 21, 23, 22, 24 },
                GeometrySize = 8,
                Stroke = new SolidColorPaint(new SKColor(186, 85, 211)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.MediumPurple),
                GeometryStroke = new SolidColorPaint(SKColors.White),
                Fill = null
            },
            // 下翻转
            new LineSeries<int>
            {
                Name = "下翻转",
                Values = new[] { 18, 20, 19, 21, 20, 22 },
                GeometrySize = 8,
                Stroke = new SolidColorPaint(new SKColor(0, 191, 255)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.DeepSkyBlue),
                GeometryStroke = new SolidColorPaint(SKColors.White),
                Fill = null
            }
        };

        // ==================== 静态字段和逻辑 ====================

        private static string[] _names = new string[] { "Maria", "Susan", "Charles", "Fiona", "George" };
        private static int _index = 0;
        private static SKTypeface ChineseFont => SKTypeface.FromFamilyName("Microsoft YaHei");

        // 构造函数
        public ExceptionDataViewModel()
        {
            // 柱状图数据：异常次数
            var barValues = new int[] { 12, 15, 8, 10, 7, 11 };

            // 折线图数据：处理时长
            var lineValues = new int[] { 30, 26, 33, 28, 24, 32 };

            // 注意：AXAxes 已经被 ObservableProperty 包装，但这里我们操作的是集合内部，
            // 访问属性名 AXAxes (大写) 即可。
            AXAxes.Clear();
            AXAxes.Add(new Axis
            {
                Labels = new[] { "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" },
                LabelsPaint = new SolidColorPaint
                {
                    Color = SKColors.White,
                    SKTypeface = SKTypeface.FromFamilyName("Microsoft YaHei")
                },
                TextSize = 14,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(100))
            });

            // 初始化 Series 属性 (通过字段或属性赋值均可，推荐属性)
            Series = new ISeries[]
            {
                // 背景柱子
                new ColumnSeries<int>
                {
                    Values = new[] { 40, 40, 40, 40 },
                    MaxBarWidth = 150,
                    Stroke = null,
                    Fill = new SolidColorPaint(new SKColor(49, 60, 70, 255)),
                    IsHoverable = false,
                    IgnoresBarPosition = true
                },
                // 主数据柱子
                new ColumnSeries<int>
                {
                    Values = new[] { 35, 30, 20, 15 },
                    MaxBarWidth = 150,
                    Stroke = null,
                    Fill = new SolidColorPaint(new SKColor(96, 125, 255, 200)),
                    DataLabelsPaint = null,
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString(),
                    IgnoresBarPosition = true
                }
            };

            XAxes = new Axis[]
            {
                new Axis
                {
                    Labels = new[] { "定位异常", "夹具异常", "通讯异常", "其他异常" },
                    LabelsPaint = new SolidColorPaint { Color = SKColors.White, SKTypeface = ChineseFont },
                    TextSize = 14,
                    SeparatorsPaint = null
                }
            };

            YAxes = new Axis[]
            {
                new Axis
                {
                    MinLimit = 0, MaxLimit = 40,
                    Labeler = value => value.ToString("0"),
                    LabelsPaint = new SolidColorPaint { Color = SKColors.White, SKTypeface = ChineseFont },
                    TextSize = 14,
                    SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(80))
                }
            };
        }

        // ==================== 导航与命令 ====================

        public void OnNavigatedTo(NavigationContext context)
        {
            Start = Global.GetCurrentClassTime().Start;
            QueryCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext c) => true;
        public void OnNavigatedFrom(NavigationContext c) { }

        // CommunityToolkit.Mvvm 的 RelayCommand
        public ICommand QueryCommand => new RelayCommand(async () =>
        {
            // Global.LoadingManager.StartLoading();
            // await Task.Delay(200);
            // await Task.Run(async () =>
            // {
            //     var warning_data = await Global.repo_warning.GetListAsync(null, BuildPredicate());

            //     // 上挂异常趋势图
            //     Update.UpdateXLabels(AXAxes, Start, End, (Unit)Enum.Parse(typeof(Unit), SelectedUnit));
            //     GetWarningGroup(
            //         warning_data,
            //         x => x.CreatedAt,
            //         out var result,
            //         Start, End,
            //         (Unit)Enum.Parse(typeof(Unit), SelectedUnit));
            //     
            //     Application.Current.Dispatcher.Invoke(() => 
            //     {
            //          UpdateSeries(result); // 注意线程安全，UI更新最好在主线程
            //     });

            //     // 圆饼图
            //     GetPieGrope(warning_data, out var newValue, out var names);
            //     Application.Current.Dispatcher.Invoke(() => 
            //     {
            //         Update.UpdatePieData(MyPieSeries, newValue, names);
            //     });

            //     // 上挂异常明细表
            //     var AbnormalData = await LoadAbnormalData();
            //     Application.Current.Dispatcher.Invoke(() =>
            //     {
            //         Update.UpdateTableInfoData(AbnormalList, AbnormalData);
            //     });
            // });
            // Global.LoadingManager.StopLoading();
            // Application.Current.Dispatcher.Invoke(() =>
            // {
            //     Growl.Success("查询成功", "GlobalGrowl");
            // });
        });

        public ICommand ExportCommand => new RelayCommand(async () =>
        {
            try
            {
                ExportAbnormalItemsWithDialog(AbnormalList);
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
            }
        });

        // ==================== 辅助方法 ====================

        public void InitPieSeries(int[] values, string[] names)
        {
            _names = names;
            _index = 0;
            MyPieSeries.Clear();

            foreach (var series in values.AsPieSeries((value, series) =>
            {
                series.Name = _names[_index++ % _names.Length];
                series.DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle;
                series.DataLabelsSize = 15;
                series.DataLabelsPaint = new SolidColorPaint(SKColors.White);
                series.DataLabelsFormatter = point =>
                    $" {point.Coordinate.PrimaryValue} out of {point.StackedValue!.Total} ";
                series.ToolTipLabelFormatter = point => $"{point.StackedValue!.Share:P2}";
            }))
            {
                MyPieSeries.Add(series);
            }
        }

        [RequireRole(Role.Admin)]
        public static void ExportAbnormalItemsWithDialog(ObservableCollection<AbnormalItem> abnormalData)
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = "上挂异常明细表.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("上挂异常明细表");
                    // 表头
                    sheet.Cell(1, 1).Value = "设备名称";
                    sheet.Cell(1, 2).Value = "混料";
                    sheet.Cell(1, 3).Value = "扫码NG";
                    sheet.Cell(1, 4).Value = "系统反馈";
                    sheet.Cell(1, 5).Value = "物料丢失";

                    // 数据
                    int row = 2;
                    foreach (var item in abnormalData)
                    {
                        sheet.Cell(row, 1).Value = item.DeviceName;
                        sheet.Cell(row, 2).Value = item.Mix;
                        sheet.Cell(row, 3).Value = item.ScanNg;
                        sheet.Cell(row, 4).Value = item.SystemFeedback;
                        sheet.Cell(row, 5).Value = item.MaterialLost;
                        row++;
                    }
                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(dialog.FileName);
                }
            }
        }

        public void UpdateSeries(params int[][] allValues)
        {
            if (ASeries == null || ASeries.Length == 0) return;

            for (int i = 0; i < allValues.Length && i < ASeries.Length; i++)
            {
                if (ASeries[i] is ISeries<int> series)
                {
                    series.Values = allValues[i];
                }
            }
        }

        public void UpdateSeries(Dictionary<string, int[]> deviceData)
        {
            if (ASeries == null || ASeries.Length == 0) return;

            foreach (var s in ASeries)
            {
                if (s is ISeries<int> series) series.Values = Array.Empty<int>();
            }

            int index = 0;
            foreach (var kv in deviceData)
            {
                if (index >= ASeries.Length) break;
                if (ASeries[index] is ISeries<int> series)
                {
                    series.Values = kv.Value;
                    series.Name = kv.Key;
                }
                index++;
            }
        }

        public void GetWarningGroup<T>(
            IEnumerable<T> data,
            Func<T, DateTime> timeSelector,
            out Dictionary<string, int[]> deviceGroups,
            DateTime start,
            DateTime end,
            Unit unit)
        {
            deviceGroups = new Dictionary<string, int[]>();
            int slotCount = unit switch
            {
                Unit.年 => end.Year - start.Year + 1,
                Unit.月 => ((end.Year - start.Year) * 12) + end.Month - start.Month + 1,
                Unit.日 => (end.Date - start.Date).Days + 1,
                Unit.时 => (int)(end - start).TotalHours + 1,
                _ => 0
            };

            var devices = data.Select(d => typeof(T).GetProperty("Device")?.GetValue(d)?.ToString())
                              .Distinct()
                              .Where(x => !string.IsNullOrEmpty(x))
                              .ToList();

            foreach (var dev in devices)
            {
                deviceGroups[dev!] = new int[slotCount];
            }

            foreach (var item in data)
            {
                var dt = timeSelector(item);
                if (dt < start || dt > end) continue;

                int index = unit switch
                {
                    Unit.年 => dt.Year - start.Year,
                    Unit.月 => (dt.Year - start.Year) * 12 + (dt.Month - start.Month),
                    Unit.日 => (int)(dt.Date - start.Date).TotalDays,
                    Unit.时 => (int)(dt - start).TotalHours,
                    _ => 0
                };

                string device = typeof(T).GetProperty("Device")?.GetValue(item)?.ToString() ?? "未知设备";
                deviceGroups[device][index] += 1;
            }
        }

    }

    // AbnormalItem 不需要修改，保持原样即可
    public class AbnormalItem
    {
        public string DeviceName { get; set; }
        public string Mix { get; set; }
        public string ScanNg { get; set; }
        public string SystemFeedback { get; set; }
        public string MaterialLost { get; set; }
    }
}