using BasicRegionNavigation;
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

        public AlarmMonitorViewModel(IEventAggregator ea)
        {
            ea.GetEvent<MyDataUpdatedEvent>().Subscribe(OnMyDataUpdated, ThreadOption.UIThread);
            StartWarningSimulation();
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