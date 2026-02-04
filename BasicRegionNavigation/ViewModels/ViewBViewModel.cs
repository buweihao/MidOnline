using BasicRegionNavigation;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Models;
using BasicRegionNavigation.Services;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using HandyControl.Controls;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Axis = LiveChartsCore.SkiaSharpView.Axis;

namespace BasicRegionNavigation.ViewModels
{
    // 1. Partial 类
    // 2. 继承 ObservableObject
    public partial class ViewBViewModel : ObservableObject, INavigationAware
    {
        // 模组缓存
        private readonly ConcurrentDictionary<string, ModuleModelB> _modulesCache = new ConcurrentDictionary<string, ModuleModelB>();
        [ObservableProperty]
        private ModuleModelB _currentModule;

        // 1. 注入两个服务
        private readonly IFlipperHourlyService _flipperService;
        private readonly IProductionService _productionService;
        private readonly IUpDropHourlyService _upDropService;     // [必须] 用于供料机效能


        public ViewBViewModel(
                    IConfigService configService,
                    IProductionService productionService,   // 注入
                    IFlipperHourlyService flipperService,
                    IUpDropHourlyService upDropService) // 注入
        {
            _configService = configService;
            _upDropService = upDropService; // 赋值
            _productionService = productionService;
            _flipperService = flipperService;
            InitializeModules(SystemConfig.Modules);
            OnModulesChanged(2);

            //StartPieInfoSimulation();
            //StartProductInfoBSimulation();
            //StartEfficiencyAndFaultSimulation();
            // [修改] 移除旧的模拟启动，改为定时真实查询
            //StartRealDataPolling();
            _upDropService = upDropService;
        }

        // [核心逻辑] 真实数据轮询
        private void StartRealDataPolling()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        var currentModuleId = _modelNum;
                        var start = DateTime.Today; // 统计当天的
                        var end = DateTime.Now;

                        // ---------------------------------------------------
                        // A. 获取表格 & 柱状图数据 (列表/效能)
                        // ---------------------------------------------------
                        var productStatsTask = _productionService.GetViewBProductionStatsAsync(start, end, currentModuleId);
                        var flipperStatsTask = _flipperService.GetFlipperStatsAsync(start, end, currentModuleId);
                        var feederStatsTask = _upDropService.GetFeederStatsAsync(start, end, currentModuleId);

                        // ---------------------------------------------------
                        // B. [新增] 获取饼图数据
                        // ---------------------------------------------------
                        var pieStatsTask = _productionService.GetViewBPieStatsAsync(start, end, currentModuleId);

                        // 并行等待所有任务
                        await Task.WhenAll(productStatsTask, flipperStatsTask, feederStatsTask, pieStatsTask);

                        // 获取结果
                        var productStats = productStatsTask.Result;
                        var flipperEff = flipperStatsTask.Result.Efficiencies;
                        var feederEff = feederStatsTask.Result.Efficiencies;
                        var pieStats = pieStatsTask.Result; // 饼图结果

                        // 合并效能列表 (供表格和柱状图使用)
                        var allEfficiencyStats = new List<ProductionEfficiencyTable>();
                        allEfficiencyStats.AddRange(feederEff);
                        allEfficiencyStats.AddRange(flipperEff);
                        allEfficiencyStats = allEfficiencyStats.OrderBy(x => x.DeviceName).ToList();

                        // ---------------------------------------------------
                        // C. 更新 UI
                        // ---------------------------------------------------
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 1. 更新表格
                            HandleDataChanged(currentModuleId, ModuleDataCategory.ProductInfoTop, productStats);
                            HandleDataChanged(currentModuleId, ModuleDataCategory.ProductInfoBottom, productStats);
                            HandleDataChanged(currentModuleId, ModuleDataCategory.EfficiencyData, allEfficiencyStats);

                            // 2. 更新柱状图
                            UpdateFaultChart(currentModuleId, allEfficiencyStats);

                            // 3. [新增] 更新饼图
                            // 上挂饼图 (设备状态)
                            HandleDataChanged(currentModuleId, ModuleDataCategory.UpPieInfo, pieStats.UpPieData);

                            // 下挂饼图 (产品分布)
                            HandleDataChanged(currentModuleId, ModuleDataCategory.DnPieInfo, pieStats.DnPieData);
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ViewB 数据刷新失败: {ex.Message}");
                    }

                    // 5秒刷新一次
                    await Task.Delay(5000);
                }
            });
        }        /// <summary>
                 /// 合并逻辑：将供料机数据和翻转台数据按【项目号】合并到一行
                 /// </summary>
        private List<ProductInfoTable> MergeProductInfos(List<ProductInfoTable> feeders, List<ProductInfoTable> flippers)
        {
            // 1. 找出所有唯一的项目号
            var allProjectIds = feeders.Select(x => x.ProjectId)
                .Union(flippers.Select(x => x.ProjectId))
                .Distinct()
                .Where(id => id != "-") // 过滤无效项目
                .ToList();

            var result = new List<ProductInfoTable>();

            foreach (var pid in allProjectIds)
            {
                // 尝试找两边的数据
                var f = feeders.FirstOrDefault(x => x.ProjectId == pid);
                var t = flippers.FirstOrDefault(x => x.ProjectId == pid);

                result.Add(new ProductInfoTable
                {
                    ProjectId = pid,
                    // 优先从翻转台获取类型信息 (因为翻转台数据里有 MaterialCategory)
                    MaterialType = t?.MaterialType ?? "-",
                    AnodeType = t?.AnodeType ?? "-",

                    // 供料机数据
                    UpFeeder1 = f?.UpFeeder1 ?? 0,
                    UpFeeder2 = f?.UpFeeder2 ?? 0,
                    UpTotalFeederOutput = (f?.UpFeeder1 ?? 0) + (f?.UpFeeder2 ?? 0),

                    // 翻转台数据
                    UpTurnTable = t?.UpTurnTable ?? 0,

                    // 下料数据
                    DnFeeder1 = f?.DnFeeder1 ?? 0,
                    DnFeeder2 = f?.DnFeeder2 ?? 0,
                    DnTotalFeederOutput = (f?.DnFeeder1 ?? 0) + (f?.DnFeeder2 ?? 0),
                    DnTurnTable = t?.DnTurnTable ?? 0
                });
            }

            return result;
        }

        /// <summary>
        /// 合并逻辑：直接拼接两个列表
        /// </summary>
        private List<ProductionEfficiencyTable> MergeEfficiencies(List<ProductionEfficiencyTable> list1, List<ProductionEfficiencyTable> list2)
        {
            var list = new List<ProductionEfficiencyTable>();
            list.AddRange(list1);
            list.AddRange(list2);
            // 可以在这里按 DeviceName 排序
            return list.OrderBy(x => x.DeviceName).ToList();
        }


        private void UpdateFaultChart(string moduleId, List<ProductionEfficiencyTable> data)
        {
            if (data == null || data.Count == 0) return;

            // 1. 提取 X轴标签 (设备名称)
            var deviceNames = data.Select(x => x.DeviceName ?? "-").ToArray();

            // 2. 提取 Y轴数据 (故障时间 & 故障次数)
            var times = data.Select(x => (double)x.FailureTime).ToArray();
            var counts = data.Select(x => (double)x.FailureCount).ToArray();

            // 3. 打包数据 (Labels, Values1, Values2)
            // 使用 Tuple<string[], double[], double[]> 传递
            var chartData = new Tuple<string[], double[], double[]>(deviceNames, times, counts);

            // 4. 发送通知
            HandleDataChanged(moduleId, ModuleDataCategory.FaultStatsSeries, chartData);
        }




        private void InitializeModules(string[] ids)
        {
            foreach (var id in ids)
            {
                var model = new ModuleModelB(id);
                _modulesCache.TryAdd(id, model);
            }

            // 默认显示第一个
            if (ids.Length > 0) CurrentModule = _modulesCache[ids[0]];
        }

        // 3. 交通指挥：收到数据 -> 查找字典 -> 定点更新
        private void HandleDataChanged(string moduleId, ModuleDataCategory category, object data)
        {
            if (_modulesCache.TryGetValue(moduleId, out var targetModule))
            {
                targetModule.DispatchData(category, data);
            }
            else
            {
                // 收到了一个不存在的模组ID的数据，忽略或记录日志
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

        partial void OnModelNumChanged(string value)
        {
            if (string.IsNullOrEmpty(value)) return;

            // 解析 ID：如果下拉框内容是 "模组1"，我们需要提取 "1"
            // 假设您的 Key 是纯数字字符串 "1", "2"
            string id = value.Replace("模组", "").Trim();

            SwitchModule(id);
        }

        private readonly IConfigService _configService;

        // -----------------------------------------------------------------------
        // 筛选条件属性 (Filter Properties)
        // -----------------------------------------------------------------------
        #region Filters

        [ObservableProperty] private string _modelNum = "1";
        [ObservableProperty] private DateTime _start = DateTime.Now.AddDays(-1);
        [ObservableProperty] private DateTime _end = DateTime.Now;
        [ObservableProperty] private List<string> _modeSelectGroup = new List<string> { "1", "2", "3" };

        // 模组数量（逻辑属性）
        [ObservableProperty] private int _modules = 3;

        // 钩子：当 Modules 改变时重新生成下拉列表
        partial void OnModulesChanged(int value)
        {
            ModeSelectGroup = Enumerable
                .Range(1, value)
                .Select(i => $"{i}")
                .ToList();
        }

        #endregion

        // -----------------------------------------------------------------------
        // 表格数据属性 (Table Data Properties)
        // -----------------------------------------------------------------------
        // -----------------------------------------------------------------------
        // 构造函数
        // -----------------------------------------------------------------------

        // -----------------------------------------------------------------------
        // 导航 (Navigation)
        // -----------------------------------------------------------------------
        public void OnNavigatedTo(NavigationContext context)
        {
            Start = Global.GetCurrentClassTime().Start;
            if (QueryCommand.CanExecute(null))
            {
                QueryCommand.Execute(null);
            }
        }

        public void OnNavigatedFrom(NavigationContext context) { }
        public bool IsNavigationTarget(NavigationContext context) => true;

        // -----------------------------------------------------------------------
        // 命令 (Commands)
        // -----------------------------------------------------------------------

        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                Global.LoadingManager.StartLoading();
                //ExportToExcelWithDialog(ProductInfoData, ProductInfoData_Down, ProductEfficiencyData);
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
            }
        }

        [RelayCommand]
        private async Task QueryAsync()
        {
            try
            {
                // 0.以此来显示加载动画 (如果有)
                 Global.LoadingManager.StartLoading(); 

                // 1. 获取界面上选择的参数
                // 注意：这里不再是写死的 DateTime.Today，而是绑定到界面日期选择器的 Start/End
                var currentModuleId = _modelNum;
                var start = Start;
                var end = End;

                // ---------------------------------------------------
                // A. 并行获取所有数据
                // ---------------------------------------------------

                // 1. 产品生产信息 (ProductionService - 统计产量)
                var productStatsTask = _productionService.GetViewBProductionStatsAsync(start, end, currentModuleId);

                // 2. 翻转台效能 (FlipperHourlyService)
                var flipperStatsTask = _flipperService.GetFlipperStatsAsync(start, end, currentModuleId);

                // 3. 供料机效能 (UpDropHourlyService)
                var feederStatsTask = _upDropService.GetFeederStatsAsync(start, end, currentModuleId);

                // 4. 饼图数据 (ProductionService - 统计分布)
                var pieStatsTask = _productionService.GetViewBPieStatsAsync(start, end, currentModuleId);

                // 等待所有任务完成
                await Task.WhenAll(productStatsTask, flipperStatsTask, feederStatsTask, pieStatsTask);

                // ---------------------------------------------------
                // B. 数据处理与合并
                // ---------------------------------------------------
                var productStats = productStatsTask.Result;
                var flipperEff = flipperStatsTask.Result.Efficiencies;
                var feederEff = feederStatsTask.Result.Efficiencies;
                var pieStats = pieStatsTask.Result;

                // 合并效能列表 (供料机 + 翻转台)
                var allEfficiencyStats = new List<ProductionEfficiencyTable>();
                allEfficiencyStats.AddRange(feederEff);
                allEfficiencyStats.AddRange(flipperEff);
                // 按设备名排序，保证图表美观
                allEfficiencyStats = allEfficiencyStats.OrderBy(x => x.DeviceName).ToList();

                // ---------------------------------------------------
                // C. 更新 UI (必须在 UI 线程)
                // ---------------------------------------------------
                // 注意：CommunityToolkit.Mvvm 的 RelayCommand 默认是在 UI 线程触发的，
                // 但 Task.WhenAll 之后的上下文可能会变，为了保险起见，或者如果是在非 UI 线程回调中，
                // 建议使用 Application.Current.Dispatcher。
                // 如果你的 QueryAsync 直接绑定在按钮上，通常可以直接操作 ObservableCollection，
                // 但为了安全，保留 Dispatcher 也没错。

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 1. 更新表格: 产品生产信息 (上表 & 下表)
                    HandleDataChanged(currentModuleId, ModuleDataCategory.ProductInfoTop, productStats);
                    HandleDataChanged(currentModuleId, ModuleDataCategory.ProductInfoBottom, productStats);

                    // 2. 更新表格: 设备效能
                    HandleDataChanged(currentModuleId, ModuleDataCategory.EfficiencyData, allEfficiencyStats);

                    // 3. 更新图表: 故障统计
                    UpdateFaultChart(currentModuleId, allEfficiencyStats);

                    // 4. 更新饼图
                    HandleDataChanged(currentModuleId, ModuleDataCategory.UpPieInfo, pieStats.UpPieData);
                    HandleDataChanged(currentModuleId, ModuleDataCategory.DnPieInfo, pieStats.DnPieData);
                });
            }
            catch (Exception ex)
            {
                // 错误处理
                // MessageBox.Show($"查询失败: {ex.Message}");
                Console.WriteLine($"[ViewB] Query Error: {ex.Message}");
            }
            finally
            {
                 Global.LoadingManager.StopLoading();
            }
        }


        [ObservableProperty]
        private string _twoDataTableWithHeaderTitle = "产品生产信息表";

        // 产品生产信息表 (Columns 这种通常只读，也可以不做 ObservableProperty，保留原样即可)
        public ObservableCollection<DataGridColumn> ProductInfoColumns { get; } = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn { Header = "项目号", Binding = new Binding("ProjectId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "原料类别", Binding = new Binding("MaterialType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "产品类别", Binding = new Binding("AnodeType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上料机A", Binding = new Binding("UpFeeder1"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上料机B", Binding = new Binding("UpFeeder2"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上料合计", Binding = new Binding("UpTotalFeederOutput"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上翻转台", Binding = new Binding("UpTurnTable"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
        };

        // 设备效能表
        public ObservableCollection<DataGridColumn> ProductEfficiencyColumns { get; } = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn { Header = "设备名称", Binding = new Binding("DeviceName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "扫码NG", Binding = new Binding("ScanNG"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "系统反馈NG", Binding = new Binding("SystemNG"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "故障次数", Binding = new Binding("FailureCount"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "故障时间(min)", Binding = new Binding("FailureTime"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "待机时间(min)", Binding = new Binding("IdleTime"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "上挂率", Binding = new Binding("MountRate"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "稼动率", Binding = new Binding("UtilizationRate"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }
        };
        public ObservableCollection<DataGridColumn> ProductInfoColumns_Down { get; } = new ObservableCollection<DataGridColumn>
        {
            new DataGridTextColumn { Header = "项目号", Binding = new Binding("ProjectId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "原料类别", Binding = new Binding("MaterialType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "阳极类型", Binding = new Binding("AnodeType"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下料机A", Binding = new Binding("DnFeeder1"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下料机B", Binding = new Binding("DnFeeder2"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下料合计", Binding = new Binding("DnTotalFeederOutput"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
            new DataGridTextColumn { Header = "下翻转台", Binding = new Binding("DnTurnTable"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) },
        };
        #region 测试数据

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
                        HandleDataChanged("2", ModuleDataCategory.DnPieInfo, dnPieData);
                    });
                }
            });
        }
        private void StartProductInfoBSimulation()
        {
            // 开启后台任务：模拟 ViewB 的产品生产信息 (上表 & 下表)
            Task.Run(async () =>
            {
                var random = new Random();

                while (true)
                {
                    await Task.Delay(3000); // 每 3 秒刷新一次

                    // --- 1. 构造模拟数据列表 ---
                    // 每次随机生成 1~5 行数据
                    var newDataList = new List<ProductInfoTable>();
                    int rowCount = random.Next(1, 6);

                    for (int i = 0; i < rowCount; i++)
                    {
                        // 随机生成一行数据（包含上料和下料的所有字段）
                        var item = new ProductInfoTable
                        {
                            // 基础信息
                            ProjectId = $"PROJ-{random.Next(10000, 99999)}",
                            MaterialType = random.Next(0, 2) == 0 ? "铝合金" : "不锈钢",
                            AnodeType = random.Next(0, 2) == 0 ? "一阳" : "二阳",

                            // 上料数据 (Up) -> 对应上表
                            UpFeeder1 = random.Next(100, 200),
                            UpFeeder2 = random.Next(100, 200),
                            UpTotalFeederOutput = random.Next(200, 400), // 简单求和模拟
                            UpTurnTable = random.Next(0, 50),

                            // 下料数据 (Dn) -> 对应下表
                            DnFeeder1 = random.Next(100, 200),
                            DnFeeder2 = random.Next(100, 200),
                            DnTotalFeederOutput = random.Next(200, 400),
                            DnTurnTable = random.Next(0, 50)
                        };
                        newDataList.Add(item);
                    }

                    // --- 2. 推送数据到 UI ---
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 确保 CurrentModule 不为空
                        if (CurrentModule != null)
                        {
                            // 更新上表 (ProductInfoTop)
                            // 注意：这里使用的是 ModuleDataCategory 枚举
                            //CurrentModule.DispatchData(ModuleDataCategory.ProductInfoTop, newDataList);
                            HandleDataChanged("1", ModuleDataCategory.ProductInfoTop, newDataList);

                            // 更新下表 (ProductInfoBottom)
                            // 这里为了演示，传了相同的数据源，实际业务中可以是不同的 List
                            //CurrentModule.DispatchData(ModuleDataCategory.ProductInfoBottom, newDataList);
                            HandleDataChanged("2", ModuleDataCategory.ProductInfoBottom, newDataList);

                        }
                    });
                }
            });
        }

        private void StartEfficiencyAndFaultSimulation()
        {
            // 开启后台任务：模拟 效能表格 (Efficiency) 和 故障统计图表 (FaultStats)
            Task.Run(async () =>
            {
                var random = new Random();
                // 定义设备名称列表 (与 X 轴标签一致)
                var deviceNames = new[] { "上料机1", "上料机2", "上翻转台", "下料机1", "下料机2", "下翻转台" };

                while (true)
                {
                    await Task.Delay(4000); // 每 4 秒刷新一次

                    // --- 1. 构造效能表格数据 (Efficiency Table) ---
                    var efficiencyList = new List<ProductionEfficiencyTable>();

                    // 用于收集图表数据
                    var failureTimes = new double[deviceNames.Length];
                    var failureCounts = new double[deviceNames.Length];

                    for (int i = 0; i < deviceNames.Length; i++)
                    {
                        // 随机生成各项指标
                        int failTime = random.Next(0, 60);  // 故障时间 0-60分钟
                        int failCount = random.Next(0, 10); // 故障次数 0-10次

                        var item = new ProductionEfficiencyTable
                        {
                            DeviceName = deviceNames[i],
                            ScanNG = random.Next(0, 5),
                            SystemNG = random.Next(0, 5),
                            FailureCount = failCount,
                            FailureTime = failTime,
                            IdleTime = random.Next(0, 30),
                            MountRate = $"{random.Next(95, 100)}.{random.Next(0, 99)}%", // 模拟 95.00% - 99.99%
                            UtilizationRate = $"{random.Next(80, 100)}.{random.Next(0, 99)}%"
                        };
                        efficiencyList.Add(item);

                        // 同步填充图表数据
                        failureTimes[i] = failTime;
                        failureCounts[i] = failCount;
                    }

                    // --- 2. 推送数据到 UI ---
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (CurrentModule != null)
                        {
                            // A. 更新效能表格
                            HandleDataChanged("1", ModuleDataCategory.EfficiencyData, efficiencyList);

                            // B. 更新故障统计图表
                            // 构造 Tuple<double[], double[]> 传递给 ModuleModelB
                            var chartData = new Tuple<double[], double[]>(failureTimes, failureCounts);
                            HandleDataChanged("2", ModuleDataCategory.FaultStatsSeries, chartData);

                        }
                    });
                }
            });
        }



        #endregion


        // -----------------------------------------------------------------------
        // 业务逻辑与辅助方法 (Logic & Helpers)
        // -----------------------------------------------------------------------

        [RequireRole(Role.Admin)]
        public static void ExportToExcelWithDialog(
            ObservableCollection<ProductInfoTable> productInfoDataUp,
            ObservableCollection<ProductInfoTable> productInfoDataDn,
            ObservableCollection<ProductionEfficiencyTable> efficiencyData)
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = "生产数据导出.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    // (导出逻辑保持不变，略)
                    var sheetUp = workbook.Worksheets.Add("上料生产信息表");
                    // ... Header & Loop
                    sheetUp.Columns().AdjustToContents();

                    var sheetDn = workbook.Worksheets.Add("下料生产信息表");
                    // ... Header & Loop
                    sheetDn.Columns().AdjustToContents();

                    var sheetEff = workbook.Worksheets.Add("设备效能数据表");
                    // ... Header & Loop
                    sheetEff.Columns().AdjustToContents();

                    workbook.SaveAs(dialog.FileName);
                }
            }
        }
    }

    // Model 也建议改造，虽非强制
    public partial class ProductInfoTable : ObservableObject
    {
        [ObservableProperty] private string _projectId;
        [ObservableProperty] private string _materialType;
        [ObservableProperty] private string _anodeType;
        [ObservableProperty] private int _upFeeder1;
        [ObservableProperty] private int _upFeeder2;
        [ObservableProperty] private int _upTotalFeederOutput;
        [ObservableProperty] private int _upTurnTable;
        [ObservableProperty] private int _dnFeeder1;
        [ObservableProperty] private int _dnFeeder2;
        [ObservableProperty] private int _dnTotalFeederOutput;
        [ObservableProperty] private int _dnTurnTable;
    }

    public partial class ProductionEfficiencyTable : ObservableObject
    {
        [ObservableProperty] private string _deviceName;
        [ObservableProperty] private int _scanNG;
        [ObservableProperty] private int _systemNG;
        [ObservableProperty] private int _failureCount;
        [ObservableProperty] private int _failureTime;
        [ObservableProperty] private int _idleTime;
        [ObservableProperty] private string _mountRate;
        [ObservableProperty] private string _utilizationRate;
    }
}