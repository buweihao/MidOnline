using BasicRegionNavigation.Controls;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Models;
using BasicRegionNavigation.Services;
using CommunityToolkit.Mvvm.ComponentModel; // 核心引用
using CommunityToolkit.Mvvm.Input;        // 核心引用
using Core;
using HandyControl.Controls;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MyConfig.Controls;
using MyDatabase;
using Prism.Events; // 假设 IEventAggregator 来自 Prism
using SkiaSharp;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Expression = System.Linq.Expressions.Expression;
using Timer = System.Timers.Timer;

namespace BasicRegionNavigation.ViewModels
{
    // 修改 1: partial + ObservableObject
    public partial class ViewAViewModel : ObservableObject
    {
        private readonly IModbusService _modbusService;
        /// <summary>
        /// 本页面专用，表示用户选中需要查看的模组
        /// </summary>
        [ObservableProperty]
        private string _moduleNum = "0";

        /// <summary>
        /// 用于给前端绑定的各个控件的数据源，会跟随模组index变化而更新
        /// </summary>
        [ObservableProperty]
        private ModuleModel _currentModule;

        // 模组缓存
        private readonly ConcurrentDictionary<string, ModuleModel> _modulesCache = new ConcurrentDictionary<string, ModuleModel>();

        // 报警信息翻译字典 (Key: UI标识, Value: 中文描述)
        private readonly Dictionary<string, string> _alarmDescriptions = new Dictionary<string, string>
        {
            // 供料机 A
            { "FeederASensorFault",       "供料机A-传感器故障" },
            { "FeederAComponentFault",    "供料机A-气缸/元件故障" },
            { "FeederATraceCommFault",    "供料机A-轨道通讯故障" },
            { "FeederAMasterCommFault",   "供料机A-主控通讯故障" },
            
            // 供料机 B
            { "FeederBSensorFault",       "供料机B-传感器故障" },
            { "FeederBComponentFault",    "供料机B-气缸/元件故障" },
            { "FeederBTraceCommFault",    "供料机B-轨道通讯故障" },
            { "FeederBMasterCommFault",   "供料机B-主控通讯故障" },
            
            // 翻转台
            { "FlipperSensorFault",       "翻转台-传感器故障" },
            { "FlipperComponentFault",    "翻转台-气缸/元件故障" },
            { "FlipperTraceCommFault",    "翻转台-轨道通讯故障" },
            { "FlipperHostCommFault",     "翻转台-上位机通讯故障" },
            { "FlipperRobotCommFault",    "翻转台-机器人通讯故障" },
            { "FlipperDoorTriggered",     "翻转台-安全门触发" },
            { "FlipperSafetyCurtain",     "翻转台-光幕触发" },
            { "FlipperEmergencyStop",     "翻转台-急停按下" },
            { "FlipperScannerCommFault",  "翻转台-扫码枪通讯故障" }
        };
        public ViewAViewModel(IModbusService modbusService)
        {
            _modbusService = modbusService;

            // 1. 初始化所有模组 (假设有2个)
            InitializeModules(new[] { "1", "2" });

            // 2. 【核心】单一入口监听
            _modbusService.OnModuleDataChanged += HandleDataChanged;

            InitializeSubscriptions(_modbusService);

            //StartStatusAndCapacitySimulation();
            //StartProductInfoSimulation();
            //StartPieInfoSimulation();
            //StartColumnInfoSimulation();
            //StartWarningSimulation();
        }
        // 在 MainViewModel 或初始化逻辑中
        public void InitializeSubscriptions(IModbusService modbusService)
        {
            string moduleId = "1";

            // --- A. 订阅状态 (Status) ---
            var statusMapping = new Dictionary<string, string>
            {
                { "FeedLift1",      "PLC_Peripheral_FeedStation1Status" },
                { "FeedLift2",      "PLC_Peripheral_FeedStation2Status" },
                { "HangOk",         "PLC_Peripheral_HangerOkStation1Status" },
                { "DropNgSensor",   "PLC_Peripheral_HangerNgStationStatus" },
                { "UnLoadModule1",  "PLC_Feeder_A_Status" },
                { "DropModule1",    "PLC_Flipper_Status" }
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.Status,
                fieldMapping: statusMapping
            );

            // --- B. 订阅产能 (Capacity) ---
            var capacityMapping = new Dictionary<string, string>
            {
                { "UnLoadModule1", "PLC_Feeder_A_TotalCapacity" },
                { "UnLoadModule2", "PLC_Feeder_B_TotalCapacity" },
                { "DropModule1",   "PLC_Flipper_TotalCapacity" }
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.Capacity,
                fieldMapping: capacityMapping
            );

            // --- C. 订阅 24小时产能点位 (柱状图) ---
            var hourlyCapacityMapping = new Dictionary<string, string>();
            for (int i = 0; i < 12; i++) hourlyCapacityMapping.Add($"Day_{i}", $"PLC_Flipper_Hourly_CapacityDay{i}");
            for (int i = 0; i < 12; i++) hourlyCapacityMapping.Add($"Night_{i}", $"PLC_Flipper_Hourly_CapacityNight{i}");

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.UpColumnSeries,
                fieldMapping: hourlyCapacityMapping
            );

            // --- D. 订阅产品信息 ---
            var productInfoMapping = new Dictionary<string, string>
            {
                { "ProjectCode", "PLC_Flipper_ProjectNo" },
                { "Material",    "PLC_Flipper_ProductType" },
                { "AnodeType",   "PLC_Flipper_AnodeType" },
                { "Color",       "PLC_Flipper_ProductColor" }
            };
            modbusService.SubscribeDynamicGroup(moduleId: moduleId, category: ModuleDataCategory.UpProductInfo, fieldMapping: productInfoMapping);
            modbusService.SubscribeDynamicGroup(moduleId: moduleId, category: ModuleDataCategory.DnProductInfo, fieldMapping: productInfoMapping);

            // --- [修改 2] E. 新增：报警信息订阅 ---
            // Key 是 UI/逻辑中使用的标识，Value 是 CSV 中的点位名后缀
            var warningMapping = new Dictionary<string, string>
            {
                // 供料机 A (7100-7103)
                { "FeederASensorFault",       "PLC_Feeder_A_SensorFault" },
                { "FeederAComponentFault",    "PLC_Feeder_A_ComponentFault" },
                { "FeederATraceCommFault",    "PLC_Feeder_A_TraceCommFault" },
                { "FeederAMasterCommFault",   "PLC_Feeder_A_MasterCommFault" },

                // 供料机 B (7100-7103)
                { "FeederBSensorFault",       "PLC_Feeder_B_SensorFault" },
                { "FeederBComponentFault",    "PLC_Feeder_B_ComponentFault" },
                { "FeederBTraceCommFault",    "PLC_Feeder_B_TraceCommFault" },
                { "FeederBMasterCommFault",   "PLC_Feeder_B_MasterCommFault" },

                // 翻转台 (1500-1508)
                { "FlipperSensorFault",       "PLC_Flipper_SensorFault" },
                { "FlipperComponentFault",    "PLC_Flipper_ComponentFault" },
                { "FlipperTraceCommFault",    "PLC_Flipper_TraceCommFault" },
                { "FlipperHostCommFault",     "PLC_Flipper_HostCommFault" },
                { "FlipperRobotCommFault",    "PLC_Flipper_RobotCommFault" },
                { "FlipperDoorTriggered",     "PLC_Flipper_DoorTriggered" },
                { "FlipperSafetyCurtain",     "PLC_Flipper_SafetyCurtainTriggered" },
                { "FlipperEmergencyStop",     "PLC_Flipper_EmergencyStop" },
                { "FlipperScannerCommFault",  "PLC_Flipper_ScannerCommFault" }
            };

            modbusService.SubscribeDynamicGroup(
                moduleId: moduleId,
                category: ModuleDataCategory.WarningInfo,
                fieldMapping: warningMapping
            );
        }

        private void InitializeModules(string[] ids)
        {
            foreach (var id in ids)
            {
                var model = new ModuleModel(id);
                _modulesCache.TryAdd(id, model);
            }

            // 默认显示第一个
            if (ids.Length > 0) CurrentModule = _modulesCache[ids[0]];
        }

        // 3. 交通指挥：收到数据 -> 查找字典 -> 定点更新
        // [修改] 数据处理中心 (兼容 Dictionary<string, object>)
        // [修改 3] 数据处理中心
        // [修改 3] 数据处理中心
        // [修改] 数据处理中心
        private void HandleDataChanged(string moduleId, ModuleDataCategory category, object data)
        {
            // =========================================================
            // 1. 处理柱状图数据 (兼容 Dictionary 和 double[])
            // =========================================================
            if (category == ModuleDataCategory.UpColumnSeries || category == ModuleDataCategory.DnColumnSeries)
            {
                if (data is System.Collections.IDictionary dict)
                {
                    var processedDict = new Dictionary<string, double>();
                    bool isDayNightData = false;

                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        string key = entry.Key?.ToString();
                        if (string.IsNullOrEmpty(key)) continue;

                        double val = 0;
                        try { val = Convert.ToDouble(entry.Value); } catch { }
                        processedDict[key] = val;

                        if (key.StartsWith("Day_") || key.StartsWith("Night_")) isDayNightData = true;
                    }

                    double[] finalArray;
                    if (isDayNightData)
                    {
                        var currentClass = Global.GetCurrentClassTime();
                        bool isDayShift = currentClass.Status == ClassStatus.白班;
                        string prefix = isDayShift ? "Day_" : "Night_";

                        finalArray = new double[12];
                        for (int i = 0; i < 12; i++)
                        {
                            string key = $"{prefix}{i}";
                            finalArray[i] = processedDict.ContainsKey(key) ? processedDict[key] : 0;
                        }
                        Application.Current.Dispatcher.Invoke(() => UpdateXLabelsByTime());
                    }
                    else
                    {
                        int maxIndex = 0;
                        foreach (var key in processedDict.Keys)
                            if (int.TryParse(key, out int idx) && idx > maxIndex) maxIndex = idx;

                        finalArray = new double[maxIndex + 1];
                        foreach (var kvp in processedDict)
                            if (int.TryParse(kvp.Key, out int idx)) finalArray[idx] = kvp.Value;
                    }
                    data = finalArray;
                }
            }
            // =========================================================
            // 2. 处理产品信息
            // =========================================================
            else if (category == ModuleDataCategory.UpProductInfo || category == ModuleDataCategory.DnProductInfo)
            {
                if (data is System.Collections.IDictionary dict)
                {
                    var stringDict = new Dictionary<string, string>();
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        string key = entry.Key?.ToString();
                        if (!string.IsNullOrEmpty(key)) stringDict[key] = entry.Value?.ToString() ?? "";
                    }
                    data = stringDict;
                }
            }
            // =========================================================
            // 3. [重点修改] 处理报警信息
            // =========================================================
            else if (category == ModuleDataCategory.WarningInfo)
            {
                if (data is System.Collections.IDictionary dict)
                {
                    var activeAlarms = new List<AlarmInfo>();
                    int indexCounter = 1;

                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        string key = entry.Key?.ToString();
                        if (string.IsNullOrEmpty(key)) continue;

                        bool isTriggered = false;
                        if (entry.Value is bool bVal) isTriggered = bVal;
                        else if (entry.Value is int iVal) isTriggered = iVal != 0;
                        else if (entry.Value is string sVal) isTriggered = (sVal == "1" || sVal.Equals("True", StringComparison.OrdinalIgnoreCase));

                        if (isTriggered)
                        {
                            string fullMsg = _alarmDescriptions.ContainsKey(key) ? _alarmDescriptions[key] : $"未知设备-未知报警: {key}";
                            string deviceName = "未知设备";
                            string descText = fullMsg;

                            var parts = fullMsg.Split(new[] { '-', ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                deviceName = parts[0];
                                descText = parts[1];
                            }

                            activeAlarms.Add(new AlarmInfo
                            {
                                Index = indexCounter++,
                                PropertyKey = key,
                                Time = DateTime.Now,
                                Device = deviceName,
                                Description = descText
                            });
                        }
                    }

                    // [关键修复]：不要只传递 data，而是直接在 UI 线程更新 ObservableCollection
                    if (_modulesCache.TryGetValue(moduleId, out var module))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 直接操作 CurrentWarningInfo.AlarmList
                            // 假设 ModuleModel 中已经初始化了 AlarmList
                            var collection = module.CurrentWarningInfo?.AlarmList;
                            if (collection != null)
                            {
                                collection.Clear();
                                foreach (var alarm in activeAlarms)
                                {
                                    collection.Add(alarm);
                                }
                            }
                        });
                        return; // 处理完毕，直接返回，跳过底部的 DispatchData
                    }
                }
            }

            // =========================================================
            // 4. 分发其他类型的数据 (Status, Capacity, PieInfo 等)
            // =========================================================
            if (_modulesCache.TryGetValue(moduleId, out var targetModule))
            {
                targetModule.DispatchData(category, data);
            }
        }
        // 切换模组的方法 (供前端 ComboBox 绑定)
        public void SwitchModule(string newId)
        {
            if (_modulesCache.TryGetValue(newId, out var model))
            {
                CurrentModule = model;
            }
        }
        // 在 ViewAViewModel 类中添加此方法
        private void StartStatusAndCapacitySimulation()
        {
            // 开启后台任务：模拟状态 (Status) 和 产能 (Capacity)
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(1000); // 1秒刷新一次

                    // 1. 构造 Status (状态) 数据
                    var statusData = new Dictionary<string, int>
            {
                // 周边墩子
                { "FeedStation1Status", random.Next(0, 4) },
                { "FeedStation2Status", random.Next(0, 4) },
                { "FeedStation3Status", random.Next(0, 4) },
                { "HangerOkStation1Status", random.Next(0, 2) },
                { "HangerOkStation2Status", random.Next(0, 2) },
                { "HangerNgStationStatus", random.Next(0, 2) },

                // 机械手
                { "ProductRobotStatus", random.Next(0, 4) },
                { "HangerRobotStatus", random.Next(0, 4) },

                // 供料机与翻转台
                { "FeederAStatus", random.Next(0, 4) },
                { "FeederBStatus", random.Next(0, 4) },
                { "FlipperStatus", random.Next(0, 4) }
            };

                    // 2. 构造 Capacity (产能) 数据
                    var capacityData = new Dictionary<string, int>
            {
                { "FeederACapacity", random.Next(100, 200) },
                { "FeederBCapacity", random.Next(100, 200) },
                { "FlipperCapacity", random.Next(50, 100) }
            };

                    // 3. 推送数据
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        HandleDataChanged("1", ModuleDataCategory.Status, statusData);
                        HandleDataChanged("1", ModuleDataCategory.Capacity, capacityData);
                    });
                }
            });
        }
        private void StartProductInfoSimulation()
        {
            // 开启后台任务：模拟产品信息 (ProductInfo)
            Task.Run(async () =>
            {
                var random = new Random();
                // 产品信息可能不需要像状态那样频繁刷新，这里设为 3 秒
                while (true)
                {
                    await Task.Delay(3000);

                    // 1. 构造产品信息字典
                    // Key 必须对应 CurrentProductInfo 类中的 FieldMapping 配置
                    var productData = new Dictionary<string, string>
            {
                { "ProjectCode", "PROJ-" + random.Next(1000, 9999) },   // 对应：项目编号
                { "Material",    random.Next(0, 2) == 0 ? "铝合金" : "不锈钢" }, // 对应：原料
                { "AnodeType",   "Type-" + (char)random.Next('A', 'F') }, // 对应：阳极类型
                { "Color",       random.Next(0, 2) == 0 ? "黑色" : "银色" }  // 对应：颜色
            };

                    // 2. 推送数据
                    // 这里假设 上挂(Up) 和 下挂(Dn) 显示相同的信息进行测试
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 推送给上挂产品信息
                        HandleDataChanged("1", ModuleDataCategory.UpProductInfo, productData);

                        // 推送给下挂产品信息
                        HandleDataChanged("1", ModuleDataCategory.DnProductInfo, productData);
                    });
                }
            });
        }

        private void StartPieInfoSimulation()
        {
            // 开启后台任务：模拟饼图数据 (PieInfo)
            Task.Run(async () =>
            {
                var random = new Random();

                while (true)
                {
                    await Task.Delay(2500); // 2.5秒刷新一次，避免闪烁过快

                    // --- 1. 构造上挂饼图数据 ---
                    // Key = 扇区名称, Value = 数值
                    var upPieData = new Dictionary<string, int>
            {
                { "正常运行", random.Next(60, 100) },
                { "设备待机", random.Next(10, 30) },
                { "故障停机", random.Next(0, 15) },
                { "换料暂停", random.Next(5, 20) }
            };

                    // --- 2. 构造下挂饼图数据 ---
                    // 演示使用不同的分类名称
                    var dnPieData = new Dictionary<string, int>
            {
                { "型号A", random.Next(100, 200) },
                { "型号B", random.Next(50, 150) },
                { "型号C", random.Next(20, 80) },
                { "返工",   random.Next(0, 10) }
            };

                    // --- 3. 推送数据 ---
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 推送给上挂饼图 (对应 UpMyPieSeries)
                        HandleDataChanged("1", ModuleDataCategory.UpPieInfo, upPieData);

                        // 推送给下挂饼图 (对应 DnMyPieSeries)
                        HandleDataChanged("1", ModuleDataCategory.DnPieInfo, dnPieData);
                    });
                }
            });
        }

        private void StartColumnInfoSimulation()
        {
            // 开启后台任务：模拟柱状图数据 (ColumnInfo) 及 班次切换
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(4000); // 4秒刷新一次，方便观察班次切换

                    // --- 1. 随机生成一个小时 (0-23) 用于模拟当前时间 ---
                    int simulatedHour = random.Next(0, 24);

                    // --- 2. 判断班次并生成 X 轴标签 (每班 12 小时) ---
                    // 白班定义：8:00 (含) ~ 20:00 (不含)
                    bool isDayShift = simulatedHour >= 8 && simulatedHour < 20;
                    string[] labels;

                    if (isDayShift)
                    {
                        // 白班: 8, 9, 10 ... 19
                        // 生成 8 到 19 的序列
                        labels = Enumerable.Range(8, 12).Select(h => h.ToString()).ToArray();
                    }
                    else
                    {
                        // 夜班: 20, 21 ... 23, 0, 1 ... 7
                        // 从 20 开始，循环 12 个小时
                        var nightLabels = new List<string>();
                        for (int i = 0; i < 12; i++)
                        {
                            int h = (20 + i) % 24; // 超过 24 取模
                            nightLabels.Add(h.ToString());
                        }
                        labels = nightLabels.ToArray();
                    }

                    // --- 3. 构造 12 个柱状图数据 (模拟产能) ---
                    var upValues = new double[12];
                    var dnValues = new double[12];

                    for (int i = 0; i < 12; i++)
                    {
                        // 模拟数据：随机生成 10~100 的产能
                        // (可选优化：可以根据模拟时间只填充当前时间之前的柱子，这里简单填满)
                        upValues[i] = random.Next(10, 100);
                        dnValues[i] = random.Next(10, 100);
                    }

                    // --- 4. 更新 UI ---
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // A. 推送柱状图数值 (通过 HandleDataChanged 标准流程)
                        HandleDataChanged("1", ModuleDataCategory.UpColumnSeries, upValues);
                        HandleDataChanged("1", ModuleDataCategory.DnColumnSeries, dnValues);

                        // B. 直接更新 X 轴标签
                        // 说明：这里直接操作 ViewModel 的属性来模拟 UpdateXLabelsByTime 的效果，
                        // 从而避开 ModuleModel 默认逻辑中生成 "MM-dd" 格式标签的问题，符合您要求的简单数字格式。
                        if (CurrentModule != null &&
                            CurrentModule.CurrentColumnInfo != null &&
                            CurrentModule.CurrentColumnInfo.XAxes != null &&
                            CurrentModule.CurrentColumnInfo.XAxes.Length > 0)
                        {
                            // LiveCharts 的 Axis.Labels 支持直接赋值更新
                            CurrentModule.CurrentColumnInfo.XAxes[0].Labels = labels;
                        }
                    });
                }
            });
        }

        private void StartWarningSimulation()
        {
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    await Task.Delay(1000); // 3秒刷新一次

                    // 构造匿名对象，属性名必须与 _alarmConfig 的 Key 一致
                    var warningData = new
                    {
                        // 随机触发一些报警 (10% 概率)
                        FeederASensorFault = random.Next(0, 10) == 0,
                        FeederATraceCommFault = random.Next(0, 10) == 0,

                        FeederBSensorFault = random.Next(0, 10) == 0,
                        FeederBMasterCommFault = random.Next(0, 10) == 0,

                        FlipperDoorTriggered = random.Next(0, 10) == 0,
                        FlipperEmergencyStop = random.Next(0, 20) == 0, // 5% 概率急停
                        FlipperScannerCommFault = random.Next(0, 10) == 0
                    };

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 推送报警数据
                        HandleDataChanged("1", ModuleDataCategory.WarningInfo, warningData);
                    });
                }
            });
        }

        [RelayCommand]
        private async Task NavigateModule(string index)
        {
            SwitchModule(index);
        }

        



















        // 跨线程事件聚合器
        private readonly IEventAggregator _ea;
        // 取消令牌源
        private CancellationTokenSource cts = new CancellationTokenSource();

        // ========================== 属性区域 ==========================






        [ObservableProperty]
        private ObservableCollection<ISeries> _revenueSeries;

        [ObservableProperty]
        private int[] _myIntDataArray = new int[] { 10, 50, 25, 60, 90 };


        // 模组名称属性组
        [ObservableProperty] private string _model1Name;
        [ObservableProperty] private string _model2Name;
        [ObservableProperty] private string _model3Name;
        [ObservableProperty] private string _model4Name;
        [ObservableProperty] private string _model5Name;
        [ObservableProperty] private string _model6Name;
        [ObservableProperty] private string _model7Name;
        [ObservableProperty] private string _model8Name;
        [ObservableProperty] private string _model9Name;
        [ObservableProperty] private string _model10Name;
        [ObservableProperty] private string _model11Name;
        [ObservableProperty] private string _model12Name;

        // ========================== 构造函数 ==========================

        public void ModelNameInit()
        {
            Model1Name = "模组1" + Global.GetValue("1_备注");
            Model2Name = "模组2" + Global.GetValue("2_备注");
            Model3Name = "模组3" + Global.GetValue("3_备注");
            Model4Name = "模组4" + Global.GetValue("4_备注");
            Model5Name = "模组5" + Global.GetValue("5_备注");
            Model6Name = "模组6" + Global.GetValue("6_备注");
            Model7Name = "模组7" + Global.GetValue("6_备注");
            Model8Name = "模组8" + Global.GetValue("6_备注");
            Model9Name = "模组9" + Global.GetValue("6_备注");
            Model10Name = "模组10" + Global.GetValue("6_备注");
            Model11Name = "模组11" + Global.GetValue("6_备注");
            Model12Name = "模组12" + Global.GetValue("6_备注");
        }

        private void OnMyDataUpdated(Core.TableRowViewModel value)
        {
            // 你的逻辑代码...
            // var model = _cache.GetOrAdd(value.ModuleNum, i => GetModule(i));
            // ...
        }

        public void NotifyChanges(IEnumerable<AlarmInfo> newValue)
        {
            _ea.GetEvent<MyDataUpdatedEvent>().Publish(newValue);
        }

        public static string[] WarningName = new string[] { /* 省略长列表，保持原样 */ "上料模组1_传感器故障", "..." };

        // ========================== 命令 ==========================


        [RelayCommand]
        private void ShowText(string param)
        {
            MyConfigCommand.configHelper = Global._config;
            MyConfigCommand.ShowText(param);
        }

        private Brush GetBrushByStatus(string value)
        {
            return value switch
            {
                "3" => Brushes.Gray,
                "2" => Brushes.Red,
                "1" => Brushes.Lime,
                "0" => Brushes.Red,
                _ => Brushes.Aqua
            };
        }

        public void UpdateXLabelsByTime()
        {
            if (CurrentModule.CurrentColumnInfo.XAxes == null || CurrentModule.CurrentColumnInfo.XAxes.Length == 0)
                return;

            string[] labels;
            var currentClassTime = Global.GetCurrentClassTime();
            if (currentClassTime.Status == ClassStatus.白班)
                labels = new[] { "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19" };
            else
                labels = new[] { "20", "21", "22", "23", "0", "1", "2", "3", "4", "5", "6", "7" };

            CurrentModule.CurrentColumnInfo.XAxes[0].Labels = labels;
        }

        public void UpdateAlarmList(IEnumerable<AlarmInfo> newAlarms)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CurrentModule.CurrentWarningInfo.AlarmList.Clear();
                foreach (var alarm in newAlarms)
                {
                    CurrentModule.CurrentWarningInfo.AlarmList.Add(alarm);
                }
            });
        }

    }

}