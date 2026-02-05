using BasicRegionNavigation;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Models;
using BasicRegionNavigation.Services;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel; // 核心引用
using CommunityToolkit.Mvvm.Input;          // 命令引用
using Core;
using HandyControl.Controls;
using Microsoft.Win32;
using Prism.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using static BasicRegionNavigation.Models.CurrentStatus;

namespace BasicRegionNavigation.ViewModels
{
    // 1. 必须是 partial 类
    // 2. 必须继承 ObservableObject
    internal partial class AlarmMonitorViewModel : ObservableObject, INavigationAware
    {
        [ObservableProperty]
        private CurrentWarningInfo _currentWarningInfo = new CurrentWarningInfo();
        private readonly IModbusService _modbusService;
        private readonly Dictionary<string, List<AlarmInfo>> _moduleAlarmsCache = new Dictionary<string, List<AlarmInfo>>();

        // 报警描述字典 (Key: UI标识, Value: 中文描述)
        private readonly Dictionary<string, string> _alarmDescriptions = new Dictionary<string, string>
        {
            // --- 供料机 A ---
            { "FeederASensorFault",       "供料机A-传感器故障" },
            { "FeederAComponentFault",    "供料机A-气缸/元件故障" },
            { "FeederATraceCommFault",    "供料机A-轨道通讯故障" },
            { "FeederAMasterCommFault",   "供料机A-主控通讯故障" },
            
            // --- 供料机 B ---
            { "FeederBSensorFault",       "供料机B-传感器故障" },
            { "FeederBComponentFault",    "供料机B-气缸/元件故障" },
            { "FeederBTraceCommFault",    "供料机B-轨道通讯故障" },
            { "FeederBMasterCommFault",   "供料机B-主控通讯故障" },
            
            // --- 翻转台 ---
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
                        _currentWarningInfo.UpdateValue(ModuleDataCategory.WarningInfo, warningData);

                    });
                }
            });
        }





        // -----------------------------------------------------------------------
        // 属性定义 (Property Definitions)
        // -----------------------------------------------------------------------

        [ObservableProperty]
        private List<string> _deviceSelectGroup = new List<string>
        {
            "上料机A", "上料机B", "下料机A", "下料机B", "上翻转台", "下翻转台"
        };

        [ObservableProperty]
        private string _selectDevice = "上料机A";

        [ObservableProperty]
        private DateTime _start = default;

        [ObservableProperty]
        private DateTime _end = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<AlarmTableRowViewModel> _realTimeAlarmRowItems = new ObservableCollection<AlarmTableRowViewModel>
        {
            new AlarmTableRowViewModel
            {
                ID = "2",
                AlarmTime = "2024-01-15 10:32:08",
                DeviceName = "下翻转台",
                AlarmType = "警告",
                AlarmDescription = "与Trace交互异常(模拟消息)",
                Status = "未处理"
            },
        };

        [ObservableProperty]
        private ObservableCollection<AlarmTableRowViewModel> _historyRowItems = new ObservableCollection<AlarmTableRowViewModel>
        {
            new AlarmTableRowViewModel
            {
                ID = "1",
                AlarmTime = "2024-01-15 11:06:50",
                DeviceName = "上料机A",
                AlarmType = "警告",
                AlarmDescription = "与Trace通信异常(模拟消息)",
                Status = "未处理"
            }
        };

        [ObservableProperty]
        private IEnumerable<AlarmInfo> _receivedValue;

        // -----------------------------------------------------------------------
        // 构造函数与初始化 (Constructor & Init)
        // -----------------------------------------------------------------------

        public AlarmMonitorViewModel(IEventAggregator ea, IModbusService modbusService)
        {
            ea.GetEvent<MyDataUpdatedEvent>().Subscribe(OnMyDataUpdated, ThreadOption.UIThread);
            _modbusService = modbusService;

            // 1. 注册数据监听
            _modbusService.OnModuleDataChanged += HandleDataChanged;

            // 2. 初始化订阅
            InitializeSubscriptions();
            //StartWarningSimulation();
        }
        private void InitializeSubscriptions()
        {
            // 定义需要监控的所有模组 ID
            var allModuleIds = SystemConfig.Modules;

            // 定义点位映射 (Key: UI标识, Value: PLC点位后缀)
            // 这里的 Value 必须与 CSV 中的 TagName 后缀一致
            var warningMapping = new Dictionary<string, string>
            {
                { "FeederASensorFault",       "PLC_Feeder_A_SensorFault" },
                { "FeederAComponentFault",    "PLC_Feeder_A_ComponentFault" },
                { "FeederATraceCommFault",    "PLC_Feeder_A_TraceCommFault" },
                { "FeederAMasterCommFault",   "PLC_Feeder_A_MasterCommFault" },

                { "FeederBSensorFault",       "PLC_Feeder_B_SensorFault" },
                { "FeederBComponentFault",    "PLC_Feeder_B_ComponentFault" },
                { "FeederBTraceCommFault",    "PLC_Feeder_B_TraceCommFault" },
                { "FeederBMasterCommFault",   "PLC_Feeder_B_MasterCommFault" },

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

            // 循环订阅所有模组
            foreach (var id in allModuleIds)
            {
                _modbusService.SubscribeDynamicGroup(
                    moduleId: id,
                    category: ModuleDataCategory.WarningInfo,
                    fieldMapping: warningMapping
                );
            }
        }

        private void HandleDataChanged(string moduleId, ModuleDataCategory category, object data)
        {
            // 只处理报警信息
            if (category != ModuleDataCategory.WarningInfo) return;

            if (data is IDictionary dict)
            {
                var currentModuleAlarms = new List<AlarmInfo>();

                // 1. 解析当前模组的报警数据
                foreach (DictionaryEntry entry in dict)
                {
                    string key = entry.Key?.ToString();
                    if (string.IsNullOrEmpty(key)) continue;

                    bool isTriggered = false;
                    // 兼容不同类型的数据源 (bool, int, string)
                    if (entry.Value is bool bVal) isTriggered = bVal;
                    else if (entry.Value is int iVal) isTriggered = iVal != 0;
                    else if (entry.Value is string sVal) isTriggered = (sVal == "1" || sVal.Equals("True", StringComparison.OrdinalIgnoreCase));

                    if (isTriggered)
                    {
                        // 获取中文描述
                        string fullMsg = _alarmDescriptions.ContainsKey(key) ? _alarmDescriptions[key] : $"未知报警: {key}";

                        // 解析设备名和描述 (例如 "供料机A-传感器故障")
                        string deviceName = $"模组{moduleId}"; // 默认显示模组号
                        string descText = fullMsg;

                        var parts = fullMsg.Split(new[] { '-', ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            deviceName = $"{moduleId}#{parts[0]}"; // 例如: 1#供料机A
                            descText = parts[1];
                        }

                        currentModuleAlarms.Add(new AlarmInfo
                        {
                            // Index 在合并时重新生成
                            PropertyKey = key,
                            Time = DateTime.Now,
                            Device = deviceName,
                            Description = descText
                        });
                    }
                }

                // 2. 更新缓存并刷新 UI
                lock (_moduleAlarmsCache)
                {
                    // 更新当前模组的缓存
                    _moduleAlarmsCache[moduleId] = currentModuleAlarms;

                    // 汇总所有模组的报警
                    var allAlarms = _moduleAlarmsCache.Values.SelectMany(x => x).OrderBy(x => x.Device).ToList();

                    // 重新分配序号
                    int index = 1;
                    foreach (var alarm in allAlarms)
                    {
                        alarm.Index = index++;
                    }

                    // 3. 线程安全更新 ObservableCollection
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var collection = _currentWarningInfo.AlarmList;
                        collection.Clear();
                        foreach (var alarm in allAlarms)
                        {
                            collection.Add(alarm);
                        }
                    });
                }
            }
        }
        private void OnMyDataUpdated(IEnumerable<AlarmInfo> value)
        {
            ReceivedValue = value;
            UpdateRows(RealTimeAlarmRowItems, ReceivedValue);
        }

        public void UpdateRows(ObservableCollection<AlarmTableRowViewModel> alarmTableRowViewModels, IEnumerable<AlarmInfo> alarms)
        {
            // 清空原有数据
            alarmTableRowViewModels.Clear();

            if (alarms == null) return;

            foreach (var alarm in alarms)
            {
                var row = new AlarmTableRowViewModel
                {
                    ID = alarm.Index.ToString(),
                    AlarmTime = alarm.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    DeviceName = alarm.Device ?? "-",
                    AlarmType = "-",
                    AlarmDescription = alarm.Description ?? "-",
                    Status = "-"
                };

                alarmTableRowViewModels.Add(row);
            }
        }

        // -----------------------------------------------------------------------
        // 命令 (Commands) - 使用 [RelayCommand]
        // -----------------------------------------------------------------------

        // 自动生成 QueryCommand
        [RelayCommand]
        private async Task QueryAsync()
        {
        }

        // 自动生成 ExportCommand
        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                Global.LoadingManager.StartLoading();
                // 这是一个 CPU 密集型或 IO 操作，如果在 UI 线程跑可能会卡顿，
                // 但因为 SaveFileDialog 需要 UI 线程，且 Excel 操作通常较快，这里直接调用即可。
                // 如果数据量巨大，建议用 Task.Run 包裹 Export 逻辑。
                ExportAlarmTableWithDialog(HistoryRowItems);
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
            }
        }

        // -----------------------------------------------------------------------
        // 导航接口实现 (INavigationAware)
        // -----------------------------------------------------------------------

        public void OnNavigatedTo(NavigationContext context)
        {
            Start = Global.GetCurrentClassTime().Start;

            // 手动执行命令的方式
            if (QueryCommand.CanExecute(null))
            {
                QueryCommand.Execute(null);
            }
        }

        public void OnNavigatedFrom(NavigationContext context) { }

        public bool IsNavigationTarget(NavigationContext context) => true;

        // -----------------------------------------------------------------------
        // 辅助方法 (Helpers)
        // -----------------------------------------------------------------------

        [RequireRole(Role.Admin)]
        public static void ExportAlarmTableWithDialog(ObservableCollection<AlarmTableRowViewModel> alarmData)
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = "报警信息表.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("报警信息表");

                    // 表头
                    sheet.Cell(1, 1).Value = "序号";
                    sheet.Cell(1, 2).Value = "时间";
                    sheet.Cell(1, 3).Value = "设备";
                    sheet.Cell(1, 4).Value = "报警类别";
                    sheet.Cell(1, 5).Value = "报警描述";
                    sheet.Cell(1, 6).Value = "状态";

                    // 数据
                    int row = 2;
                    foreach (var item in alarmData)
                    {
                        sheet.Cell(row, 1).Value = item.ID;
                        sheet.Cell(row, 2).Value = item.AlarmTime;
                        sheet.Cell(row, 3).Value = item.DeviceName;
                        sheet.Cell(row, 4).Value = item.AlarmType;
                        sheet.Cell(row, 5).Value = item.AlarmDescription;
                        sheet.Cell(row, 6).Value = item.Status;
                        row++;
                    }

                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(dialog.FileName);
                }
            }
        }
    }

    // 子项也建议使用 ObservableObject，虽然如果是只读显示也可以不用，
    // 但为了后续如果需要修改某一行状态能即时刷新，推荐加上。
    public partial class AlarmTableRowViewModel : ObservableObject
    {
        [ObservableProperty] private string _iD;
        [ObservableProperty] private string _alarmTime;
        [ObservableProperty] private string _deviceName;
        [ObservableProperty] private string _alarmType;
        [ObservableProperty] private string _alarmDescription;
        [ObservableProperty] private string _status;
    }
}