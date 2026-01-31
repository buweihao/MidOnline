using BasicRegionNavigation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core; // 假设 Global 在这里
using MyModbus;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = HandyControl.Controls.MessageBox;

namespace BasicRegionNavigation.ViewModels
{
    public partial class SettingViewModel : ObservableObject
    {
        private readonly IEventAggregator _ea;
        private readonly IConfigService _configService;
        private readonly DataCollectionEngine _engine;
        private readonly IMiddleFrameBusinessServices  _middleFrameBusinessServices;


        // 缓存所有模组行对象 (最大支持 6 个模组，共 12 行)，避免重复创建
        private readonly List<TableRowViewModel> _allRowsCache = new();

        // 记录当前显示的模组数量，用于检测变化
        private int _currentModulesCount = -1;

        // 配置项数据源 (所有行共享)
        private List<string> _projectCodes;
        private List<string> _productTypes;
        private List<string> _materialTypes;

        [ObservableProperty]
        private ObservableCollection<TableRowViewModel> _rowItems = new();

        public SettingViewModel(IMiddleFrameBusinessServices middleFrameBusinessServices,DataCollectionEngine engine, IEventAggregator ea, IConfigService configService)
        {
            _ea = ea;
            _configService = configService;
            _engine = engine;
            _middleFrameBusinessServices = middleFrameBusinessServices;

            LoadConfigData();
            InitializeRows();
            StartModuleMonitor();
        }

        /// <summary>
        /// 一次性加载配置数据
        /// </summary>
        private void LoadConfigData()
        {
            List<string> GetList(string key) =>
                _configService.GetConfigValue(key)?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                ?? new List<string> { "-" };

            _projectCodes = GetList("ComboItems_ProjectCode");
            _productTypes = GetList("ComboItems_Procedure");
            _materialTypes = GetList("ComboItems_ProdctType");
        }

        /// <summary>
        /// 初始化所有可能的行对象 (1-6号模组)
        /// </summary>
        private void InitializeRows()
        {
            // 假设最大支持 6 个模组
            for (int i = 1; i <= 6; i++)
            {
                _allRowsCache.Add(CreateRow(i, SettingModoulNum.上));
                _allRowsCache.Add(CreateRow(i, SettingModoulNum.下));
            }

            // 根据当前 Global.Modules 刷新显示
            RefreshVisibleRows();
        }

        private TableRowViewModel CreateRow(int moduleNum, SettingModoulNum position)
        {
            var vm = new TableRowViewModel
            {
                ModuleNum = moduleNum,
                Position = position,
                ModuleName = $"模组{moduleNum}{position}挂",

                // 共享数据源引用
                ProjectCodes = _projectCodes,
                ProductTypes = _productTypes,
                MaterialTypes = _materialTypes,

                // 默认值
                SelectedProject = _projectCodes.FirstOrDefault() ?? "-",
                SelectedProductType = "DH",
                SelectedAnodeType = "一阳",
                SelectedProductColor = "银色",
                SelectedMaterialType = "UACJ",
                SelectedTimes = "-",
                SelectBatchNumber = "1"
            };

            // 直接绑定命令，无需外部再次 Bind
            vm.ConfirmCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(async () => await ExecuteConfirmAsync(vm));
            vm.SettingCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => ResetRowToDefault(vm));

            return vm;
        }

        /// <summary>
        /// 监控模组数量变化的任务
        /// </summary>
        private void StartModuleMonitor()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    if (Global.Modules != _currentModulesCount)
                    {
                        _currentModulesCount = Global.Modules;
                        Application.Current.Dispatcher.Invoke(RefreshVisibleRows);
                    }
                    await Task.Delay(1000);
                }
            });
        }

        private void RefreshVisibleRows()
        {
            RowItems.Clear();
            // 计算需要显示的行数：模组数 * 2 (上下)
            // 确保不超出缓存总数
            int rowsToShow = Math.Min(_currentModulesCount * 2, _allRowsCache.Count);

            for (int i = 0; i < rowsToShow; i++)
            {
                RowItems.Add(_allRowsCache[i]);
            }
        }

        [RequireRole(Role.Admin)]
        private async Task ExecuteConfirmAsync(TableRowViewModel vm)
        {
            if (!ConfirmRowSettings(vm)) return;

            //下发
            //_middleFrameBusinessServices.SendSetting(vm.ModuleNum, vm);

            ////主动通知转产
            //_middleFrameBusinessServices.ChangeoverTrigger(vm.ModuleNum);

            //// 通知变更
            //NotifyChanges(vm);
        }

        private void ResetRowToDefault(TableRowViewModel vm)
        {
            vm.SelectedProject = "CY50132";
            vm.SelectedProductType = "DH";
            vm.SelectedAnodeType = "一阳";
            vm.SelectedProductColor = "银色";
            vm.SelectedMaterialType = "UACJ";
            vm.SelectBatchNumber = "1";
            vm.SelectedTimes = "-";
        }

        public void NotifyChanges(TableRowViewModel vm)
        {
            var coreModel = new Core.TableRowViewModel
            {
                ModuleNum = vm.ModuleNum,
                UporDn = vm.Position,
                ProjectCodes = vm.SelectedProject,
                AnodeTypes = vm.SelectedAnodeType,
                ProductColors = vm.SelectedProductColor,
                MaterialTypes = vm.SelectedMaterialType,
            };
            _ea.GetEvent<MyDataUpdatedSettingEvent>().Publish(coreModel);
        }

        // 保持原有的 ConfirmRowSettings 和 SendSetting2PLC 逻辑不变
        // ... (省略部分未变动的业务逻辑代码以节省篇幅) ...

        public static bool ConfirmRowSettings(TableRowViewModel vm)
        {
            // 1. 数据校验 (可选)：如果有必填项为空，可以直接提示并返回 false
            if (string.IsNullOrEmpty(vm.SelectedProject))
            {
                HandyControl.Controls.MessageBox.Show("请先选择项目代号！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // 2. 拼接要展示的信息 (使用 StringBuilder 提高性能和可读性)
            StringBuilder sb = new StringBuilder();
            //sb.AppendLine($"当前模块: {vm.ModuleName ?? "未命名"} (#{vm.ModuleNum})");
            //sb.AppendLine("--------------------------------------------");
            //sb.AppendLine($"项目代号:  {vm.SelectedProject ?? "未选择"}");
            //sb.AppendLine($"产品型号:  {vm.SelectedProductType ?? "未选择"}");
            //sb.AppendLine($"阳极类型:  {vm.SelectedAnodeType ?? "未选择"}");
            //sb.AppendLine($"产品颜色:  {vm.SelectedProductColor ?? "未选择"}");
            //sb.AppendLine($"材料类型:  {vm.SelectedMaterialType ?? "未选择"}");
            //sb.AppendLine($"批次号:    {vm.SelectBatchNumber ?? "未选择"}");
            //sb.AppendLine($"次数:      {vm.SelectedTimes ?? "未选择"}");
            //sb.AppendLine("--------------------------------------------");
            //sb.AppendLine("确认应用以上配置吗？");
            sb.AppendLine("请到触摸屏修改");

            // 3. 调用 HandyControl 的 MessageBox
            // Show 方法是阻塞的，用户点击按钮前代码会停在这里
            MessageBoxResult result = HandyControl.Controls.MessageBox.Show(
                sb.ToString(),               // 消息内容
                "确认设置",                   // 标题
                MessageBoxButton.OKCancel,   // 按钮类型：确认和取消
                MessageBoxImage.Question     // 图标：问号
            );

            // 4. 根据用户点击的按钮返回结果
            // 只有点击 "OK" (确认) 才返回 true
            return result == MessageBoxResult.OK;
        }

    }

    // 重构后的 TableRowViewModel
    public partial class TableRowViewModel : ObservableObject
    {
        // 标识属性：让行对象知道自己是谁
        public int ModuleNum { get; init; }
        public SettingModoulNum Position { get; init; }

        [ObservableProperty] private string _moduleName;

        // 下拉框数据源
        [ObservableProperty] private List<string> _projectCodes;
        [ObservableProperty] private List<string> _productTypes;
        [ObservableProperty] private List<string> _anodeTypes = new() { "一阳", "-" }; // 假设这是固定的，也可从 config 加载
        [ObservableProperty] private List<string> _productColors = new() { "银色", "-" };
        [ObservableProperty] private List<string> _materialTypes;
        [ObservableProperty] private List<string> _batchNumber; // 如果需要动态生成批次号，可在 CreateRow 中处理
        [ObservableProperty] private List<string> _times;

        // 选中项
        [ObservableProperty] private string _selectedProject;
        [ObservableProperty] private string _selectedProductType;
        [ObservableProperty] private string _selectedAnodeType;
        [ObservableProperty] private string _selectedProductColor;
        [ObservableProperty] private string _selectedMaterialType;
        [ObservableProperty] private string _selectBatchNumber;
        [ObservableProperty] private string _selectedTimes;

        // 命令
        [ObservableProperty] private ICommand _confirmCommand;
        [ObservableProperty] private ICommand _settingCommand;
    }
}