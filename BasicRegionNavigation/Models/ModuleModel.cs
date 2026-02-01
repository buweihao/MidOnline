using BasicRegionNavigation.Controls;
using BasicRegionNavigation.Services;
using BasicRegionNavigation.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Core;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MyModbus;
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

namespace BasicRegionNavigation.Models
{
    public partial class ModuleModel : ObservableObject
    {
        public ModuleModel(string id)
        {
            _moduleId = id;
        }
        [ObservableProperty]
        private string _moduleId = "0";

        [ObservableProperty]
        private CurrentStatus _currentStatus = new CurrentStatus();

        [ObservableProperty]
        private CurrentProductInfo _currentProductInfo = new CurrentProductInfo();

        [ObservableProperty]
        private CurrentPieInfo _currentPieInfo = new CurrentPieInfo();

        [ObservableProperty]
        private CurrentColumnInfo _currentColumnInfo = new CurrentColumnInfo();

        [ObservableProperty]
        private CurrentWarningInfo _currentWarningInfo = new CurrentWarningInfo();

        /// <summary>
        /// 被动接收数据的方法，不需要 index 参数，因为调这个方法时已经确定是给我这个模组的了
        /// </summary>
        public void DispatchData(ModuleDataCategory category, object data)
        {
            switch (category)
            {
                // 状态类数据
                case ModuleDataCategory.Status:
                case ModuleDataCategory.Capacity:
                    _currentStatus.UpdateValue(category, data);
                    break;

                // 产品信息类数据
                case ModuleDataCategory.UpProductInfo:
                case ModuleDataCategory.DnProductInfo:
                    _currentProductInfo.UpdateValue(category, data);
                    break;

                //产品饼图
                case ModuleDataCategory.UpPieInfo:
                case ModuleDataCategory.DnPieInfo:
                    _currentPieInfo.UpdateValue(category, data);
                    break;
                //柱状图
                case ModuleDataCategory.UpColumnSeries:
                case ModuleDataCategory.DnColumnSeries:
                    _currentColumnInfo.UpdateValue(category, data);
                    break;
                case ModuleDataCategory.WarningInfo:
                    _currentWarningInfo.UpdateValue(category, data);
                    break;
                    
                default:
                    // 可以在这里记录未处理的类别日志
                    Console.WriteLine($"未识别的数据类别: {category}");
                    break;
            }
        }

    }
    public partial class CurrentStatus : ObservableObject
    {
        // --- 1. 静态画刷缓存 (性能优化关键) ---
        // 必须使用静态实例，否则每次 new SolidColorBrush 即使颜色一样，引用也不一样，无法触发界面更新过滤
        // 使用辅助方法创建并冻结画刷
        private static readonly Brush ColorDefault = CreateFrozenBrush(Color.FromArgb(255, 0, 235, 246));
        private static readonly Brush ColorOk = CreateFrozenBrush(Colors.LimeGreen);
        private static readonly Brush ColorNg = CreateFrozenBrush(Colors.Red);
        private static readonly Brush ColorOffline = CreateFrozenBrush(Colors.Gray);

        // 辅助方法：创建并冻结
        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            // 【关键一步】冻结画刷，使其跨线程安全
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }
            return brush;
        }
        // --- 1. 周边墩子 (Peripheral) ---
        // 上挂工位 1, 2, 3
        [ObservableProperty] private Brush _feedStation1Status = ColorDefault;
        [ObservableProperty] private Brush _feedStation2Status = ColorDefault;
        [ObservableProperty] private Brush _feedStation3Status = ColorDefault;

        // OK/NG 挂具工位
        // 修正：不再叫 Sensor，改叫 Station，区分 1 和 2
        [ObservableProperty] private Brush _hangerOkStation1Status = ColorDefault;
        [ObservableProperty] private Brush _hangerOkStation2Status = ColorDefault;
        [ObservableProperty] private Brush _hangerNgStationStatus = ColorDefault;


        // --- 2. 机械手 (Robot) ---
        // 上产品小机械手
        [ObservableProperty] private Brush _productRobotStatus = ColorDefault;
        // 上挂具大机械手
        [ObservableProperty] private Brush _hangerRobotStatus = ColorDefault;


        // --- 3. 供料机 (Feeder A/B) ---
        // A 设备 (原 UnLoadModule1)
        [ObservableProperty] private Brush _feederAStatus = ColorDefault;
        [ObservableProperty] private int _feederACapacity = -1;

        // B 设备 (原 UnLoadModule2)
        [ObservableProperty] private Brush _feederBStatus = ColorDefault;
        [ObservableProperty] private int _feederBCapacity = -1;


        // --- 4. 翻转台 (Flipper) ---
        // 翻转台 (原 UpFlipper)
        [ObservableProperty] private Brush _flipperStatus = ColorDefault;
        [ObservableProperty] private int _flipperCapacity = -1;
        // --- 1. 颜色转换规则 (集中管理) ---
        private Brush ConvertIntToBrush(object value)
        {
            return value switch
            {
                // 1. 优先适配 bool 类型
                true => ColorOk,       // 对应 Colors.LimeGreen
                false => ColorNg,      // 对应 Colors.Red

                // 2. 适配 int 类型
                1 => ColorOk,          // 运行
                2 => ColorNg,          // 故障
                0 => ColorOffline,     // 停止/离线
                3 => ColorOffline,     // 未知/其他 (这里建议用 Gray，视需求而定)

                // 3. 适配 string 类型
                string s when bool.TryParse(s, out bool b) => b ? ColorOk : ColorNg,
                "1" => ColorOk,
                "2" => ColorNg,
                "0" => ColorOffline,

                // 4. 默认情况 (使用了你定义的默认色)
                _ => ColorDefault
            };
        }
        /// <summary>
        /// 根据业务分类更新属性
        /// </summary>
        /// <param name="category">数据分类 (Status vs Capacity)</param>
        /// <param name="data">数据字典 (Key=字段名, Value=值)</param>
        public void UpdateValue(ModuleDataCategory category, object data)
        {
            // 1. 类型安全检查
            if (data is not Dictionary<string, int> statusDict) return;

            var type = this.GetType();

            // 2. 确定属性后缀策略 (用于过滤，防止误赋值)
            string expectedSuffix = category switch
            {
                ModuleDataCategory.Status => "Status",
                ModuleDataCategory.Capacity => "Capacity",
                _ => ""
            };

            if (string.IsNullOrEmpty(expectedSuffix)) return;

            // 3. 遍历数据并赋值
            foreach (var kvp in statusDict)
            {
                string fullTagName = kvp.Key;   // 例如 "PLC_Peripheral_FeedStation1Status"
                int rawValue = kvp.Value;

                // --- 核心修复：从完整 TagName 解析出属性名 ---
                // 逻辑：截取最后一个 "_" 之后的部分
                // "PLC_Peripheral_FeedStation1Status" -> "FeedStation1Status"
                // "PLC_Feeder_A_TotalCapacity" -> "TotalCapacity"
                string targetPropName = GetPropertyNameFromTag(fullTagName);

                // 4. 双重校验：确保找到的属性名确实符合当前的分类 (以 Status 或 Capacity 结尾)
                // 这样可以避免把 Capacity 的值赋给 Status 属性，反之亦然
                if (!targetPropName.EndsWith(expectedSuffix))
                {
                    continue;
                }

                PropertyInfo targetProp = type.GetProperty(targetPropName);

                if (targetProp != null)
                {
                    // 5. 根据目标属性类型赋值
                    if (targetProp.PropertyType == typeof(Brush))
                    {
                        // 假设 Status 属性绑定的是颜色
                        var newBrush = ConvertIntToBrush(rawValue);
                        // 性能优化：引用比较
                        if (!ReferenceEquals(targetProp.GetValue(this), newBrush))
                        {
                            targetProp.SetValue(this, newBrush);
                        }
                    }
                    else if (targetProp.PropertyType == typeof(int))
                    {
                        // 假设 Capacity 属性绑定的是数值
                        targetProp.SetValue(this, rawValue);
                    }
                    // 补充：如果你的属性是 bool 类型 (比如某些 Status 是 bool)
                    else if (targetProp.PropertyType == typeof(bool))
                    {
                        targetProp.SetValue(this, rawValue == 1);
                    }
                }
            }
        }

        // 辅助方法：解析属性名 (建议放入 ModbusKeyHelper 或作为私有方法)
        private string GetPropertyNameFromTag(string fullTagName)
        {
            if (string.IsNullOrEmpty(fullTagName)) return fullTagName;

            // 使用库里定义的通用分隔符 (通常是 "_")
            int lastIdx = fullTagName.LastIndexOf(ModbusKeyHelper.Separator);

            // 确保找到了分隔符，并且不是在字符串末尾
            if (lastIdx >= 0 && lastIdx < fullTagName.Length - 1)
            {
                return fullTagName.Substring(lastIdx + 1);
            }

            // 如果没有下划线，就直接返回原名 (容错)
            return fullTagName;
        }
        public partial class CurrentPieInfo : ObservableObject
        {

            [ObservableProperty]
            private ObservableCollection<ISeries> _upMyPieSeries = new();

            [ObservableProperty]
            private ObservableCollection<ISeries> _dnMyPieSeries = new();

            // --- 核心更新方法 ---

            /// <summary>
            /// 更新饼图数据
            /// </summary>
            /// <param name="location">位置 (Up/Down)</param>
            /// <param name="data">数据字典 (Key=分类名称, Value=数值)</param>
            public void UpdateValue(ModuleDataCategory location, object data)
            {
                // 1. 类型安全检查
                // 假设后端传回的是 Dictionary<string, int>，如果数值是 double 请改为 double
                if (data is not Dictionary<string, int> pieDict) return;

                // 2. 确定目标 Series 集合
                ObservableCollection<ISeries> targetSeries = location switch
                {
                    ModuleDataCategory.UpPieInfo => UpMyPieSeries,
                    ModuleDataCategory.DnPieInfo => DnMyPieSeries,
                    _ => null
                };

                if (targetSeries == null) return;

                // 3. 拆解字典为数组
                // 注意：字典是无序的，ToArray() 后的顺序 Key 和 Value 是一一对应的，这很重要
                var names = pieDict.Keys.ToArray();
                var values = pieDict.Values; // 直接传 IEnumerable<int> 即可

                // 4. 调用静态绘图逻辑
                UpdatePieData(targetSeries, values, names);
            }

            // --- 静态辅助绘图方法 (集成到类内部) ---

            public static void UpdatePieData(
                ObservableCollection<ISeries> MyPieSeries,
                IEnumerable<int> newValues,
                string[] _names,
                string[]? hexColors = null)
            {
                var values = newValues.ToArray();
                if (values.Length != _names.Length)
                    return; // 建议记录日志

                // 清空旧数据
                MyPieSeries.Clear();

                // 默认颜色配置
                hexColors ??= new[]
                {
        "#42A5F5", "#66BB6A", "#FFB74D",
        "#9575CD", "#E57373", "#4DD0E1"
    };

                int i = 0;
                foreach (var value in values)
                {
                    // 只添加大于0的数据
                    if (value > 0)
                    {
                        var color = SKColor.Parse(hexColors[i % hexColors.Length]);

                        MyPieSeries.Add(new PieSeries<int>
                        {
                            Values = new[] { value },
                            Name = _names[i],
                            DataLabelsPosition = PolarLabelsPosition.Middle,
                            DataLabelsSize = 15,
                            DataLabelsPaint = new SolidColorPaint(SKColors.White),

                            // 饼图中间显示的文字（根据需要截取）
                            DataLabelsFormatter = point =>
                            {
                                var name = point.Context.Series.Name;
                                var dash = name.IndexOf('-');
                                return dash >= 0 ? name.Substring(0, dash) : name;
                            },

                            // 【关键修改】Tooltip 格式化：显示 名称: 数量 (百分比)
                            // point.PrimaryValue 就是具体的数量
                            // point.StackedValue.Share 是百分比
                            ToolTipLabelFormatter = point => $"{point.Context.Series.Name}: {point.Model} ({point.StackedValue!.Share:P2})",
                            Fill = new SolidColorPaint(color),
                            AnimationsSpeed = TimeSpan.Zero
                        });
                    }
                    i++;
                }
            }
        }

        public partial class CurrentProductInfo : ObservableObject
        {
            [ObservableProperty]
            private ObservableCollection<ProductInfoItem> _upProductInfo = new ObservableCollection<ProductInfoItem>
        {
            new ProductInfoItem { Label = "项目编号", Value = "----" }, // Index 0
            new ProductInfoItem { Label = "原料", Value = "--" },     // Index 1
            new ProductInfoItem { Label = "阳极类型", Value = "--" }, // Index 2
            new ProductInfoItem { Label = "颜色", Value = "--" }      // Index 3
        };

            [ObservableProperty]
            private ObservableCollection<ProductInfoItem> _dnProductInfo = new ObservableCollection<ProductInfoItem>
        {
            new ProductInfoItem { Label = "项目编号", Value = "----" },
            new ProductInfoItem { Label = "原料", Value = "--" },
            new ProductInfoItem { Label = "阳极类型", Value = "--" },
            new ProductInfoItem { Label = "颜色", Value = "--" }
        };

            // --- 1. 键名与列表索引的映射 (核心配置) ---
            // Key: 后端传来的字段名 (例如 "ProjectCode")
            // Value: 对应 ObservableCollection 中的索引位置 (例如 0 代表 "项目编号")
            private static readonly Dictionary<string, int> FieldMapping = new Dictionary<string, int>
        {
            { "ProjectCode", 0 }, // 对应 "项目编号"
            { "Material",    1 }, // 对应 "原料"
            { "AnodeType",   2 }, // 对应 "阳极类型"
            { "Color",       3 }  // 对应 "颜色"
        };

            // --- 2. 核心更新方法 ---
            /// <summary>
            /// 更新产品信息
            /// </summary>
            /// <param name="location">位置分类 (Up/Down)</param>
            /// <param name="data">数据字典 (Key=字段名, Value=文本值)</param>
            public void UpdateValue(ModuleDataCategory location, object data)
            {
                // 1. 类型安全检查
                // 注意：这里假设产品信息是字符串字典。如果是其他类型，请相应调整
                if (data is not Dictionary<string, string> infoDict) return;

                // 2. 确定目标集合
                ObservableCollection<ProductInfoItem> targetCollection = location switch
                {
                    ModuleDataCategory.UpProductInfo => UpProductInfo,
                    ModuleDataCategory.DnProductInfo => DnProductInfo,
                    _ => null
                };

                if (targetCollection == null) return;

                // 3. 遍历数据并更新指定行
                foreach (var kvp in infoDict)
                {
                    string key = kvp.Key;
                    string newValue = kvp.Value;

                    // 查找该 Key 对应的行索引
                    if (FieldMapping.TryGetValue(key, out int index))
                    {
                        // 安全检查：防止索引越界
                        if (index >= 0 && index < targetCollection.Count)
                        {
                            var item = targetCollection[index];

                            // 性能优化：只有值真正改变时才触发 PropertyChanged
                            if (item.Value != newValue)
                            {
                                item.Value = newValue;
                            }
                        }
                    }
                }
            }
        }
        public partial class CurrentColumnInfo : ObservableObject
        {
            // ... (属性定义保持不变: _xAxes, _yAxes, _cSeriesUp, _cSeriesDn) ...
            [ObservableProperty]
            private Axis[] _xAxes =
            [
                new Axis
        {
            Labels = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" },
            LabelsPaint = new SolidColorPaint(SKColors.White),
            TextSize = 15,
            IsVisible = true,
            MinStep = 1,
            ForceStepToMin = true
        }
            ];

            [ObservableProperty]
            private Axis[] _yAxes =
            [
                new Axis
        {
            MinLimit = 0,
            LabelsPaint = new SolidColorPaint(SKColors.White),
            TextSize = 15,
            SeparatorsPaint = new SolidColorPaint(SKColors.White.WithAlpha(50))
        }
            ];

            [ObservableProperty]
            private ISeries[] _cSeriesUp =
            [
                new ColumnSeries<double>
        {
            Values = [ 5, 15, 25, 35, 45, 55, 65, 75 ,85, 95 ],
            Padding = 0,
            MaxBarWidth = 20,
            DataLabelsFormatter = p => (double)p.Model == 0 ? string.Empty : ((double)p.Model).ToString("0"),
            Fill = new SolidColorPaint(new SKColor(0, 235, 246)),
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            Stroke = null
        }
            ];

            [ObservableProperty]
            private ISeries[] _cSeriesDn =
            [
                new ColumnSeries<double>
        {
            Values = [ 5, 15, 25, 35, 45, 55, 65, 75 ,85, 95 ],
            Padding = 0,
            MaxBarWidth = 20,
            DataLabelsFormatter = p => (double)p.Model == 0 ? string.Empty : ((double)p.Model).ToString("0"),
            Fill = new SolidColorPaint(new SKColor(0, 235, 246)),
            DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
            DataLabelsPaint = new SolidColorPaint(SKColors.White),
            Stroke = null
        }
            ];


            /// <summary>
            /// 通用更新方法
            /// </summary>
            /// <param name="location">更新的目标区域（枚举需要包含图表相关的定义）</param>
            /// <param name="data">数据源：double[] 用于柱状图，TimeAxisData 用于时间轴</param>
            public void UpdateValue(ModuleDataCategory location, object data)
            {
                switch (location)
                {
                    // 更新柱状图数据
                    case ModuleDataCategory.UpColumnSeries: // 假设枚举名
                    case ModuleDataCategory.DnColumnSeries: // 假设枚举名
                        UpdateSeriesData(location, data);
                        break;

                    // 更新时间轴标签
                    case ModuleDataCategory.ChartAxis:      // 假设枚举名
                        UpdateAxisLabels(data);
                        break;
                }
            }

            // 私有辅助方法：更新柱状图
            private void UpdateSeriesData(ModuleDataCategory location, object data)
            {
                // 1. 类型安全检查
                if (data is not double[] newValues) return;

                // 2. 确定目标集合
                ISeries[] targetSeriesCollection = location switch
                {
                    ModuleDataCategory.UpColumnSeries => CSeriesUp,
                    ModuleDataCategory.DnColumnSeries => CSeriesDn,
                    _ => null
                };

                // 3. 执行更新
                if (targetSeriesCollection != null &&
                    targetSeriesCollection.Length > 0 &&
                    targetSeriesCollection[0] is ColumnSeries<double> columnSeries)
                {
                    // 只有当引用不同或内容改变时更新（Values本身通常是引用类型，这里直接赋值即可触发LiveCharts更新）
                    columnSeries.Values = newValues;
                }
            }
            public record TimeAxisData(DateTime Start, DateTime End, Services.Unit Unit);
            // 私有辅助方法：更新时间轴
            private void UpdateAxisLabels(object data)
            {
                // 1. 类型安全检查 (使用定义好的 Record 或类)
                if (data is not TimeAxisData axisInfo) return;

                var start = axisInfo.Start;
                var end = axisInfo.End;
                var unit = axisInfo.Unit;

                // 2. 生成标签逻辑
                var labels = new List<string>();
                switch (unit)
                {
                    case Services.Unit.年:
                        for (int year = start.Year; year <= end.Year; year++)
                            labels.Add(year.ToString());
                        break;
                    case Services.Unit.月:
                        DateTime monthCursor = new DateTime(start.Year, start.Month, 1);
                        while (monthCursor <= end)
                        {
                            labels.Add($"{monthCursor:yyyy-MM}");
                            monthCursor = monthCursor.AddMonths(1);
                        }
                        break;
                    case Services.Unit.日:
                        DateTime dayCursor = start.Date;
                        while (dayCursor <= end.Date)
                        {
                            labels.Add($"{dayCursor:MM-dd}");
                            dayCursor = dayCursor.AddDays(1);
                        }
                        break;
                    case Services.Unit.时:
                        DateTime hourCursor = start;
                        DateTime endTime = end < start ? end.AddDays(1) : end;
                        while (hourCursor <= endTime)
                        {
                            labels.Add($"{hourCursor:MM-dd HH}:00");
                            hourCursor = hourCursor.AddHours(1);
                        }
                        break;
                }

                // 3. 更新 XAxes
                if (XAxes != null && XAxes.Length > 0)
                {
                    XAxes[0].Labels = labels;
                }
                else
                {
                    // 防御性初始化
                    XAxes = new Axis[]
                    {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(SKColors.White),
                    TextSize = 15,
                    IsVisible = true,
                    MinStep = 1
                }
                    };
                }
            }
        }

        public partial class CurrentWarningInfo : ObservableObject
        {
            private readonly Dictionary<string, (string DeviceName, string Description)> _alarmConfig;

            public CurrentWarningInfo()
            {
                // 初始化报警配置
                // Key: 必须与 CSV 中的英文 Name 完全一致
                // Value: (设备名称, 报警详细描述)
                _alarmConfig = new Dictionary<string, (string DeviceName, string Description)>
            {
                // --- 供料机 A ---
                { "FeederASensorFault",      ("供料机A", "传感器故障 (15292)") },
                { "FeederAComponentFault",   ("供料机A", "元器件故障 (15293)") },
                { "FeederATraceCommFault",   ("供料机A", "与 Trace 通讯异常") },
                { "FeederAMasterCommFault",  ("供料机A", "与总控通讯异常") },

                // --- 供料机 B ---
                { "FeederBSensorFault",      ("供料机B", "传感器故障 (15292)") },
                { "FeederBComponentFault",   ("供料机B", "元器件故障 (15293)") },
                { "FeederBTraceCommFault",   ("供料机B", "与 Trace 通讯异常") },
                { "FeederBMasterCommFault",  ("供料机B", "与总控通讯异常") },

                // --- 翻转台 ---
                { "FlipperSensorFault",            ("翻转台", "感应器故障") },
                { "FlipperComponentFault",         ("翻转台", "元器件故障") },
                { "FlipperTraceCommFault",         ("翻转台", "Trace 通讯故障") },
                { "FlipperHostCommFault",          ("翻转台", "上位机数据交互故障") },
                { "FlipperRobotCommFault",         ("翻转台", "机械手数据交互故障") },
                { "FlipperDoorTriggered",          ("翻转台", "门禁被触发") },
                { "FlipperSafetyCurtainTriggered", ("翻转台", "安全光栅被触发") },
                { "FlipperEmergencyStop",          ("翻转台", "急停被按下") },
                { "FlipperScannerCommFault",       ("翻转台", "扫码枪通讯故障") }
            };
            }

            // 前端绑定的集合
            [ObservableProperty]
            private ObservableCollection<AlarmInfo> _alarmList = new();


            /// <summary>
            /// 统一更新入口
            /// </summary>
            public void UpdateValue(ModuleDataCategory location, object data)
            {
                // 1. 过滤与类型检查
                if (location != ModuleDataCategory.WarningInfo) return;
                if (data == null) return;

                Type dataType = data.GetType();

                // 2. 确保在 UI 线程更新集合 (对应你之前代码中的 Dispatcher)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    bool collectionChanged = false;

                    // 3. 遍历配置，反射检查状态
                    foreach (var kvp in _alarmConfig)
                    {
                        string propertyName = kvp.Key;
                        var (deviceName, description) = kvp.Value;

                        try
                        {
                            // 反射获取 bool 值
                            PropertyInfo? propInfo = dataType.GetProperty(propertyName);
                            if (propInfo != null && propInfo.GetValue(data) is bool isAlarmActive)
                            {
                                // 尝试更新单个报警状态，如果状态有变则返回 true
                                if (UpdateSingleAlarm(propertyName, deviceName, description, isAlarmActive))
                                {
                                    collectionChanged = true;
                                }
                            }
                        }
                        catch
                        {
                            // 忽略单个属性的反射错误
                        }
                    }

                    // 4. 如果集合有变动，重新生成序号 (Index)
                    if (collectionChanged)
                    {
                        ReIndexList();
                    }
                });
            }

            /// <summary>
            /// 处理单个报警的增删逻辑
            /// </summary>
            /// <returns>集合是否发生了变化</returns>
            private bool UpdateSingleAlarm(string key, string device, string desc, bool isActive)
            {
                // 根据 PropertyKey 查找当前列表中是否存在该报警
                var existingItem = AlarmList.FirstOrDefault(x => x.PropertyKey == key);

                if (isActive)
                {
                    // 情况A：报警触发，但列表中没有 -> 新增
                    if (existingItem == null)
                    {
                        var newAlarm = new AlarmInfo
                        {
                            PropertyKey = key, // 关键：记录 Key
                            Device = device,
                            Description = desc,
                            Time = DateTime.Now
                        };
                        // 插入到第一条，符合最新报警在最上面的习惯
                        AlarmList.Insert(0, newAlarm);
                        return true;
                    }
                }
                else
                {
                    // 情况B：报警消除 (False)，但列表中有 -> 移除
                    if (existingItem != null)
                    {
                        AlarmList.Remove(existingItem);
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// 重新生成索引序号 (1, 2, 3...)
            /// </summary>
            private void ReIndexList()
            {
                for (int i = 0; i < AlarmList.Count; i++)
                {
                    AlarmList[i].Index = i + 1;
                }
            }
        }

    }
}
