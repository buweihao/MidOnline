using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using MyModbus; // 引用你的 MyModbus 库
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading; // 引入 DispatcherTimer

namespace BasicRegionNavigation.ViewModels
{
    internal partial class ComMonitorViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly DataCollectionEngine _engine;
        private readonly DispatcherTimer _monitorTimer;

        // 定义颜色常量，方便统一修改
        private static readonly Brush ColorConnected = Brushes.Green; // 或 Brushes.Green
        private static readonly Brush ColorDisconnected = Brushes.Red;   // 或 Brushes.Red

        public ComMonitorViewModel(IConfigService configService, DataCollectionEngine engine)
        {
            _configService = configService;
            _engine = engine;

            // 1. 初始化标题
            InitializeTitles();

            // 2. 初始化定时器 (替代原本复杂的 Task 循环)
            _monitorTimer = new DispatcherTimer
            {
                // 建议 1秒刷新一次即可，UI 不需要像数据采集那样毫秒级刷新
                Interval = TimeSpan.FromSeconds(1)
            };
            _monitorTimer.Tick += OnMonitorTick;
            _monitorTimer.Start();
        }

        private void InitializeTitles()
        {
            Model1RawType = $"模组1 {_configService.GetConfigValue("1_备注")}";
            Model2RawType = $"模组2 {_configService.GetConfigValue("2_备注")}";
            Model3RawType = $"模组3 {_configService.GetConfigValue("3_备注")}";
            Model4RawType = $"模组4 {_configService.GetConfigValue("4_备注")}";
            Model5RawType = $"模组5 {_configService.GetConfigValue("5_备注")}";
            Model6RawType = $"模组6 {_configService.GetConfigValue("6_备注")}";
        }

        // 定时器回调：在 UI 线程执行，直接赋值属性即可
        private void OnMonitorTick(object? sender, EventArgs e)
        {
            // 遍历 SystemConfig 中定义的每一个模组前缀 (如 "1", "2")
            foreach (var module in SystemConfig.Modules)
            {
                // 将字符串 "1" 转换为整数 1，用于定位 UI 属性
                if (int.TryParse(module, out int index))
                {
                    // 调用统一的更新方法
                    UpdateModuleStatus(module, index);
                }
            }
        }
        // =======================================================================
        // 状态更新逻辑
        // =======================================================================

        // 辅助方法：根据设备ID获取颜色
        private Brush GetStatusBrush(string deviceId)
        {
            // 调用引擎的新方法查询在线状态
            bool isOnline = _engine.IsDeviceConnected(deviceId);
            return isOnline ? ColorConnected : ColorDisconnected;
        }

        /// <summary>
        /// 统一的模组状态更新逻辑
        /// </summary>
        private void UpdateModuleStatus(string modulePrefix, int uiIndex)
        {
            // --- 1. 获取设备 ID 列表 ---
            // 上料机列表
            var upDevs = SystemConfig.ActiveUpLoaders
                .Select(t => ModbusKeyHelper.BuildDeviceId(modulePrefix, t)).ToList();

            // 下料机列表
            var dnDevs = SystemConfig.ActiveDownLoaders
                .Select(t => ModbusKeyHelper.BuildDeviceId(modulePrefix, t)).ToList();

            // 翻转台 ID
            string flipperId = ModbusKeyHelper.BuildDeviceId(modulePrefix, SystemConfig.Dev_Flipper);
            var flipperBrush = GetStatusBrush(flipperId);

            // --- 2. 设置上挂线条 (UpLoad) ---
            // 如果 ActiveUpLoaders 为空，Count > 0 就会为 false，线条自动变灰
            SetPropertyBrush($"Model{uiIndex}LineColorUpLoad1", upDevs.Count > 0 ? GetStatusBrush(upDevs[0]) : Brushes.Gray);
            SetPropertyBrush($"Model{uiIndex}LineColorUpLoad2", upDevs.Count > 1 ? GetStatusBrush(upDevs[1]) : Brushes.Gray);

            // --- 3. 设置下挂线条 (DnLoad / DownLoad) ---
            // 请确保你的 ViewModel 中有 Model1LineColorDnLoad1 这样的属性
            SetPropertyBrush($"Model{uiIndex}LineColorDnLoad1", dnDevs.Count > 0 ? GetStatusBrush(dnDevs[0]) : Brushes.Gray);
            SetPropertyBrush($"Model{uiIndex}LineColorDnLoad2", dnDevs.Count > 1 ? GetStatusBrush(dnDevs[1]) : Brushes.Gray);

            // --- 4. 设置翻转台相关线条 ---
            SetPropertyBrush($"Model{uiIndex}LineColorAround", flipperBrush);
            SetPropertyBrush($"Model{uiIndex}LineColorBatch", flipperBrush);

            // 如果有中间过渡线的逻辑，也可以统一在这里赋值
        }

        /// <summary>
        /// 反射辅助方法：根据字符串名称给属性赋值
        /// </summary>
        private void SetPropertyBrush(string propertyName, Brush brush)
        {
            // 在当前 ViewModel/View 中查找名为 propertyName 的公共属性
            var prop = this.GetType().GetProperty(propertyName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(this, brush);
            }
        }
        private void UpdateModel3Status() { /* ... */ }
        private void UpdateModel4Status() { /* ... */ }
        private void UpdateModel5Status() { /* ... */ }
        private void UpdateModel6Status() { /* ... */ }


        // =======================================================================
        // 属性定义 (保持原样以兼容 XAML)
        // =======================================================================

        #region Model 1
        [ObservableProperty] private string _model1RawType;
        [ObservableProperty] private Brush _model1LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorBatch = Brushes.Gray;
        [ObservableProperty] private Brush _model1LineColorAround = Brushes.Gray;
        #endregion

        #region Model 2
        [ObservableProperty] private string _model2RawType;
        [ObservableProperty] private Brush _model2LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorBatch = Brushes.Gray;
        [ObservableProperty] private Brush _model2LineColorAround = Brushes.Gray;
        #endregion

        #region Model 3
        [ObservableProperty] private string _model3RawType;
        [ObservableProperty] private Brush _model3LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model3LineColorBatch = Brushes.Gray;
        #endregion

        #region Model 4
        [ObservableProperty] private string _model4RawType;
        [ObservableProperty] private Brush _model4LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model4LineColorBatch = Brushes.Gray;
        #endregion

        #region Model 5
        [ObservableProperty] private string _model5RawType;
        [ObservableProperty] private Brush _model5LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model5LineColorBatch = Brushes.Gray;
        #endregion

        #region Model 6
        [ObservableProperty] private string _model6RawType;
        [ObservableProperty] private Brush _model6LineColorUpLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorUpLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorDnLoad1 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorDnLoad2 = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorAround = Brushes.Gray;
        [ObservableProperty] private Brush _model6LineColorBatch = Brushes.Gray;
        #endregion

        // 命令
        [RelayCommand]
        private void Insert()
        {
            // 测试命令逻辑
        }
    }
}