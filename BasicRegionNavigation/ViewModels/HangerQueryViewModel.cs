using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using HandyControl.Controls;
using Microsoft.Win32;
using BasicRegionNavigation.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BasicRegionNavigation.ViewModels
{
    // 1. partial 关键字
    // 2. 继承 ObservableObject
    internal partial class HangerQueryViewModel : ObservableObject, INavigationAware
    {
        // -----------------------------------------------------------------------
        // 属性定义 (Property Definitions)
        // -----------------------------------------------------------------------

        [ObservableProperty]
        private string _hangerCode;

        [ObservableProperty]
        private DateTime _start;

        [ObservableProperty]
        private DateTime _end = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<HangerQueryRowItemsModel> _hangerQueryRowItems = new ObservableCollection<HangerQueryRowItemsModel>
        {
            new HangerQueryRowItemsModel { VehicleId = "H0001", UpHangerTime = "2025-06-07 10:23:45", UpHangerModoule = "上料模组1", DnHangerTime = "2025-06-07 10:23:45", DnHangerModoule = "下料模组1"},
            new HangerQueryRowItemsModel { VehicleId = "H0002", UpHangerTime = "2025-06-07 10:23:45", UpHangerModoule = "上料模组1", DnHangerTime = "2025-06-07 10:23:45", DnHangerModoule = "下料模组1"},
            new HangerQueryRowItemsModel { VehicleId = "H0003", UpHangerTime = "2025-06-07 10:23:45", UpHangerModoule = "上料模组1", DnHangerTime = "2025-06-07 10:23:45", DnHangerModoule = "下料模组1"},
            new HangerQueryRowItemsModel { VehicleId = "H0004", UpHangerTime = "2025-06-07 10:23:45", UpHangerModoule = "上料模组1", DnHangerTime = "2025-06-07 10:23:45", DnHangerModoule = "下料模组1"}
        };

        // -----------------------------------------------------------------------
        // 构造函数
        // -----------------------------------------------------------------------
        public HangerQueryViewModel()
        {
        }

        // -----------------------------------------------------------------------
        // 导航事件
        // -----------------------------------------------------------------------
        public void OnNavigatedTo(NavigationContext context)
        {
            Start = Global.GetCurrentClassTime().Start;
            // 执行命令
            if (QueryCommand.CanExecute(null))
            {
                QueryCommand.Execute(null);
            }
        }

        public bool IsNavigationTarget(NavigationContext c) => true;
        public void OnNavigatedFrom(NavigationContext c) { }

        // -----------------------------------------------------------------------
        // 命令 (Commands)
        // -----------------------------------------------------------------------

        [RelayCommand]
        private async Task QueryAsync()
        {
            // CommunityToolkit.Mvvm 会自动处理跨线程 UI 更新，但因为这里可能有第三方库调用（Growl），保留 Dispatcher
            Application.Current.Dispatcher.Invoke(() =>
            {
                Growl.Success("查询成功", "GlobalGrowl");
            });
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                Global.LoadingManager.StartLoading();
                // 导出操作
                ExportHangerQueryWithDialog(HangerQueryRowItems);
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
            }
        }

        // -----------------------------------------------------------------------
        // 静态辅助方法
        // -----------------------------------------------------------------------

        [RequireRole(Role.Admin)]
        public static void ExportHangerQueryWithDialog(ObservableCollection<HangerQueryRowItemsModel> hangerData)
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = "挂具查询表.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("挂具查询表");

                    // 表头
                    sheet.Cell(1, 1).Value = "挂具编号";
                    sheet.Cell(1, 2).Value = "上挂模组";
                    sheet.Cell(1, 3).Value = "上挂时间";
                    sheet.Cell(1, 4).Value = "下挂模组";
                    sheet.Cell(1, 5).Value = "下挂时间";

                    // 数据
                    int row = 2;
                    foreach (var item in hangerData)
                    {
                        sheet.Cell(row, 1).Value = item.VehicleId;
                        sheet.Cell(row, 2).Value = item.UpHangerModoule;
                        sheet.Cell(row, 3).Value = item.UpHangerTime;
                        sheet.Cell(row, 4).Value = item.DnHangerModoule;
                        sheet.Cell(row, 5).Value = item.DnHangerTime;
                        row++;
                    }

                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(dialog.FileName);
                }
            }
        }
    }

    // 建议也对 Model 进行 ObservableObject 改造，虽然只读列表不一定必须
    public partial class HangerQueryRowItemsModel : ObservableObject
    {
        [ObservableProperty] private string _vehicleId;
        [ObservableProperty] private string _upHangerTime;
        [ObservableProperty] private string _upHangerModoule;
        [ObservableProperty] private string _dnHangerTime;
        [ObservableProperty] private string _dnHangerModoule;
    }
}