using BasicRegionNavigation.Helper;
using BasicRegionNavigation.ViewModels;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using My.Services;
using MyLog;
using MyModbus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{




    public interface IMiddleFrameBusinessServices
    {
        //中框阳极上下挂的业务内容
        //中框每个模组拥有3个PLC:上料机A、上料机B、翻转台

        //一、上料信息采集,这个采集是根据某个触发点从而触发的一个任务，然后将数据存入数据库
        void ProductCollectionMissionStart();

        //二、两个上料机的小时数据采集，需要在每个整点的最后时刻将某个寄存器的数据作为小时产能数据存入数据库,并且伴随部分其他的小时数据
        void FeedersHourlyDataCollectionMissionStart();

        //三、翻转台的小时数据采集，需要在每个整点的最后时刻将某个寄存器的数据作为小时产能数据存入数据库,并且伴随部分其他的小时数据
        void FlipperHourlyDataCollectionMissionStart();

        //四、转产
         void ChangeoverMissionStart();

    }
    public class MiddleFrameBusinessServices : IMiddleFrameBusinessServices
    {
        private readonly IFlipperHourlyService _flipperHourlyService;
        private readonly IUpDropHourlyService _upDropHourlyCapacityService;
        private readonly DataBus _bus;
        private readonly IProductionService _productionService;
        private readonly DataCollectionEngine _engine;
        private ILoggerService _logger => _serviceProvider.GetRequiredService<ILoggerService>();
        private readonly IServiceProvider _serviceProvider;

        public MiddleFrameBusinessServices(IServiceProvider serviceProvider,DataCollectionEngine engine, DataBus bus, IProductionService productionService, IFlipperHourlyService flipperHourlyService, IUpDropHourlyService upDropHourlyCapacityService)
        {
            //构造函数
            _serviceProvider = serviceProvider;
            _flipperHourlyService = flipperHourlyService;
            _upDropHourlyCapacityService = upDropHourlyCapacityService;
            _bus = bus;
            _productionService = productionService;
            _engine = engine;
        }
        /// <summary>
        /// [入口] 启动自动采集任务 (更新版)
        /// 确保此方法只被调用一次
        /// </summary>
        public void StartHourlyCollectionTask()
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 1. 计算等待时间 (对齐到 xx:59:59)
                        var now = DateTime.Now;
                        var nextTarget = new DateTime(now.Year, now.Month, now.Day, now.Hour, 59, 59);
                        if (now >= nextTarget) nextTarget = nextTarget.AddHours(1);
                        var delay = nextTarget - now;

                        if (delay.TotalMilliseconds > 0) await Task.Delay(delay);

                        // 2. 执行所有设备的采集任务
                        // 供料机采集
                        FeedersHourlyDataCollectionMissionStart();

                        // [新增] 翻转台采集
                        FlipperHourlyDataCollectionMissionStart();

                        // 3. 防止重复触发
                        await Task.Delay(2000);
                    }
                    catch (Exception ex)
                    {
                        await Task.Delay(60000);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        #region  业务一、产品信息采集
        private const string ModuleId = "1"; // 建议放入配置或作为类属性
        private const string TriggerSuffix = "ReadTrigger"; // 统一的后缀，防止手写错误
        public void ProductCollectionMissionStart()
        {
            // --- 供料机 (Feeders) ---
            // 对应 CSV: PLC_Feeder_A_ReadTrigger -> 运行时: 1_PLC_Feeder_A_ReadTrigger
            SubscribeToDevice("PLC_Feeder_A", TriggerSuffix, HandleUpLoad_Trigger);
            SubscribeToDevice("PLC_Feeder_B", TriggerSuffix, HandleUpLoad_Trigger);

            // --- 翻转台 (Flipper) ---
            // 翻转台比较特殊，CSV 中设备ID是 PLC_Flipper，但点位区分了 A/B 面
            // 对应 CSV: PLC_Flipper_A_ReadTrigger -> 运行时: 1_PLC_Flipper_A_ReadTrigger
            // 因此这里的后缀需要补上 "A_" 或 "B_"
            SubscribeToDevice("PLC_Flipper", $"A_{TriggerSuffix}", HandleFlipper_Trigger);
            SubscribeToDevice("PLC_Flipper", $"B_{TriggerSuffix}", HandleFlipper_Trigger);

        }
        /// <summary>
        /// 通用订阅辅助方法
        /// </summary>
        /// <param name="templateDeviceId">CSV中的原始设备ID (如 PLC_Feeder_A)</param>
        /// <param name="pointSuffix">点位后缀 (如 ReadTrigger)</param>
        /// <param name="handler">回调函数</param>
        private void SubscribeToDevice(string templateDeviceId, string pointSuffix, Action<TagData> handler)
        {
            // 1. 构造运行时的设备 ID (自动加上模组前缀)
            // 结果: "1_PLC_Feeder_A"
            string realDeviceId = ModbusKeyHelper.BuildDeviceId(ModuleId, templateDeviceId);

            // 2. 构造完整的点位名 (自动加上分隔符)
            // 结果: "1_PLC_Feeder_A_ReadTrigger"
            string finalTagName = ModbusKeyHelper.Build(realDeviceId, null, pointSuffix);

            // 3. 注册订阅
            _bus.Subscribe(finalTagName, handler);
        }


        private void HandleUpLoad_Trigger(TagData data)
        {
            //上料机这边是触发点为1时表示触发，读完回写0即可
            if (data.IsQualityGood && data.Value is System.Int16 speed && speed == 1)
            {
                //触发成功
                //去缓冲区读产品码,先要知道那个点位名
                //使用代理，传入触发点TagData，可以直接代理获取对应数据
                var flipper = new UpLoadProxy(_bus, data);

                //通过代理获取产品码
                var ProductCode = flipper.ProductCode;
                //通过代理获取所属机器名
                var BelongMechine = flipper.DeviceName;

                if (ProductCode is string)
                {
                    var contextA = StationProcessContext.Create(
                        deviceId: BelongMechine,
                        identity: (string)ProductCode,        // 传 SN
                        type: StationProcessType.Entry_Upload, // 明确指明是上料
                        data: null
                    );
                    _productionService.ProcessProductDataAsync(contextA);
                }

                //回写
                _engine.WriteTag(data.TagName, 0);
            }



        }


        private void HandleFlipper_Trigger(TagData data)
        {
            // 1. 校验触发信号：必须是 Good 且值为 11
            if (data.IsQualityGood && data.Value is short speed && speed == 11)
            {
                // 2. 创建智能代理 (自动识别是 A 面还是 B 面触发)
                var flipper = new FlipperProductProxy(_bus, data.TagName);

                // 3. 通过代理一次性获取所有上下文信息
                var fixture = flipper.FixtureCode;
                var belongMachine = flipper.DeviceName; // 例如 PLC_Flipper_A
                var projectNo = flipper.ProductProjectNo;
                var category = flipper.ProductCategory;
                var productCodes = flipper.CurrentProductCodes; // 获取列表

                // 4. 遍历所有产品码，逐个生成生产数据
                // (翻转台一次可能翻转多个产品，CSV中长度168也暗示了这一点)
                foreach (var sn in productCodes)
                {
                    if (string.IsNullOrWhiteSpace(sn)) continue;

                    // 4.1 组装扩展数据 (这里可以放想要存入数据库Json列的任何额外信息)
                    var plcData = new Dictionary<string, object>
                    {
                        { "FixtureCode", fixture },
                        { "ProjectNo", projectNo },
                        { "Category", category },
                        { "Side", flipper.IsSideA ? "A" : "B" } // 记录是哪一面
                    };

                    // 4.2 构造调用上下文
                    var context = StationProcessContext.Create(
                        deviceId: belongMachine,              // 哪个逻辑设备 (PLC_Flipper_A)
                        identity: sn,                         // 产品的 SN 码 (循环变量)
                        type: StationProcessType.Process_Flip,// 工序类型
                        data: plcData                         // 原始数据包
                    );

                    // 4.3 执行异步调用 (Fire and Forget 或 await 取决于上层调用)
                    // 注意：如果在 void 方法中调用 async，建议使用 Task.Run 或确保内部处理了异常
                    _productionService.ProcessProductDataAsync(context);
                }

                // 可选：打印日志
                //Console.WriteLine($"翻转台触发处理完成: 设备={belongMachine}, 数量={productCodes.Count}");
            }
        }

        #endregion

        #region 业务二、供料机小时数据采集
        /// <summary>
        /// [业务逻辑] 执行一次数据采集
        /// </summary>
        public void FeedersHourlyDataCollectionMissionStart()
        {
            // 1. 定义模组列表
            string[] modules = new[] { "1", "2" };

            // 2. 定义设备模板
            string[] feeders = new[] { "PLC_Feeder_A", "PLC_Feeder_B" };

            // 3. 遍历采集
            foreach (var module in modules)
            {
                foreach (var templateDeviceName in feeders)
                {
                    try
                    {
                        // A. 构造真实设备ID (如 "1_PLC_Feeder_A")
                        string realDeviceId = ModbusKeyHelper.BuildDeviceId(module, templateDeviceName);

                        // B. 构造点位名称
                        // [重点] 产能点位：使用 "_TotalCapacity" (累计值)
                        string tagTotalCap = ModbusKeyHelper.Build(realDeviceId, null, "TotalCapacity");

                        // 项目号
                        string tagProject = ModbusKeyHelper.Build(realDeviceId, null, "ProjectNo");

                        // [重点] 其他统计点位：使用 "_Hourly_xxx" (PLC提供的统计值)
                        string tagStandby = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_StandbyTimeMin");
                        string tagFaultTime = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_FaultTimeMin");
                        string tagFaultCount = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_FaultCount");
                        string tagSystemNG = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_SystemNG");
                        string tagMaterialLost = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_MaterialLost");

                        // C. 从 DataBus 获取数据
                        var dataPayload = new Dictionary<string, object>();

                        // Key 必须与 Service 中的 GetInt 字符串一致
                        dataPayload["TotalCapacity"] = _bus.GetValue(tagTotalCap);
                        dataPayload["ProjectNo"] = _bus.GetValue(tagProject);

                        dataPayload["Hourly_StandbyTimeMin"] = _bus.GetValue(tagStandby);
                        dataPayload["Hourly_FaultTimeMin"] = _bus.GetValue(tagFaultTime);
                        dataPayload["Hourly_FaultCount"] = _bus.GetValue(tagFaultCount);
                        dataPayload["Hourly_SystemNG"] = _bus.GetValue(tagSystemNG);
                        dataPayload["Hourly_MaterialLost"] = _bus.GetValue(tagMaterialLost);

                        // D. 调用服务处理
                        // 使用 Task.Run 确保不阻塞主线程，因为涉及到数据库IO
                        Task.Run(() => _upDropHourlyCapacityService.ProcessHourlyDataAsync(realDeviceId, dataPayload));
                    }
                    catch (Exception ex)
                    {
                        // 这里可以记录单个设备的采集失败，互不影响
                        // Console.WriteLine($"采集设备 {module}_{templateDeviceName} 失败", ex);
                    }
                }
            }
        }
        #endregion

        #region 业务三、翻转台小时数据采集
        /// <summary>
        /// [业务逻辑] 执行翻转台小时数据采集
        /// </summary>
        public void FlipperHourlyDataCollectionMissionStart()
        {
            // 1. 定义模组列表
            string[] modules = new[] { "1", "2" };

            // 2. 翻转台设备模板名
            string templateDeviceName = "PLC_Flipper";

            foreach (var module in modules)
            {
                try
                {
                    // A. 构造真实设备ID (如 "1_PLC_Flipper")
                    string realDeviceId = ModbusKeyHelper.BuildDeviceId(module, templateDeviceName);

                    // B. 构造点位名称
                    // 产能增量源
                    string tagTotalCap = ModbusKeyHelper.Build(realDeviceId, null, "TotalCapacity");

                    // 业务字段
                    string tagProject = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_ProjectNo");
                    string tagProductType = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_ProductType");
                    string tagBatchNo = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_BatchNo");
                    string tagAnodeType = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_AnodeType");
                    string tagMaterialCat = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_MaterialCategory");

                    // 统计字段
                    string tagStandby = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_StandbyTimeMin");
                    string tagFaultTime = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_FaultTimeMin");
                    string tagFaultCount = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_FaultCount");
                    string tagMixing = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_MixingQty");
                    string tagScanNG = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_ScanNGQty");
                    string tagSysFeedback = ModbusKeyHelper.Build(realDeviceId, null, "Hourly_SysFeedbackQty");

                    // C. 获取数据
                    var dataPayload = new Dictionary<string, object>();

                    dataPayload["TotalCapacity"] = _bus.GetValue(tagTotalCap);
                    dataPayload["Hourly_ProjectNo"] = _bus.GetValue(tagProject);
                    dataPayload["Hourly_ProductType"] = _bus.GetValue(tagProductType);
                    dataPayload["Hourly_BatchNo"] = _bus.GetValue(tagBatchNo);
                    dataPayload["Hourly_AnodeType"] = _bus.GetValue(tagAnodeType);
                    dataPayload["Hourly_MaterialCategory"] = _bus.GetValue(tagMaterialCat);

                    dataPayload["Hourly_StandbyTimeMin"] = _bus.GetValue(tagStandby);
                    dataPayload["Hourly_FaultTimeMin"] = _bus.GetValue(tagFaultTime);
                    dataPayload["Hourly_FaultCount"] = _bus.GetValue(tagFaultCount);
                    dataPayload["Hourly_MixingQty"] = _bus.GetValue(tagMixing);
                    dataPayload["Hourly_ScanNGQty"] = _bus.GetValue(tagScanNG);
                    dataPayload["Hourly_SysFeedbackQty"] = _bus.GetValue(tagSysFeedback);

                    // D. 调用服务
                    Task.Run(() => _flipperHourlyService.ProcessFlipperHourlyDataAsync(realDeviceId, dataPayload));
                }
                catch (Exception ex)
                {
                    // Console.WriteLine($"翻转台采集失败 {module}", ex);
                }
            }
        }


        #endregion

        #region 业务四、向供料机、翻转台下发数据

        /// <summary>
        /// 启动转产信号监听与转发任务
        /// [修改] 改为订阅模式：只需在程序启动时调用一次即可，无需循环调用
        /// </summary>
        public void ChangeoverMissionStart()
        {
            // 1. 定义模组列表 (1号和2号)
            string[] modules = new[] { "1", "2" };

            // 2. 遍历模组进行订阅
            foreach (var module in modules)
            {
                // 分别订阅 Feeder A 和 Feeder B 的转产信号
                SubscribeToChangeover(module, "PLC_Feeder_A");
                SubscribeToChangeover(module, "PLC_Feeder_B");
            }
        }

        /// <summary>
        /// 注册单个设备的转产信号订阅
        /// </summary>
        private void SubscribeToChangeover(string moduleId, string feederTemplateName)
        {
            // 1. 构造触发点位名
            // 结果示例: "1_PLC_Feeder_A_Hourly_ChangeoverTrigger"
            string feederId = ModbusKeyHelper.BuildDeviceId(moduleId, feederTemplateName);
            string triggerTag = ModbusKeyHelper.Build(feederId, null, "ChangeoverTrigger");

            // 2. 向 DataBus 注册订阅
            _bus.Subscribe(triggerTag, (data) =>
            {
                // 3. 过滤条件：只有通讯质量好 且 值为1 时才触发
                if (data.IsQualityGood && IsTriggered(data.Value))
                {
                    // 4. 异步执行转发逻辑，避免阻塞通讯线程
                    // 这里传入 triggerTag 是为了后续复位它
                    Task.Run(() => ExecuteForwardingLogic(moduleId, feederTemplateName, triggerTag));
                }
            });
        }

        /// <summary>
        /// [核心逻辑] 执行转发：读取Feeder配方 -> 写入Flipper -> 复位触发
        /// </summary>
        private void ExecuteForwardingLogic(string moduleId, string feederTemplateName, string triggerTag)
        {
            try
            {
                // 1. 构造设备ID
                string feederId = ModbusKeyHelper.BuildDeviceId(moduleId, feederTemplateName);
                string flipperId = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Flipper");

                // =========================================================
                // 2. 读取源头(Feeder)的 6 个配方字段 (直接从缓存读)
                // =========================================================
                string srcProjectNo = ModbusKeyHelper.Build(feederId, null, "ProjectNo");
                string srcProductType = ModbusKeyHelper.Build(feederId, null, "ProductType");
                string srcAnodeType = ModbusKeyHelper.Build(feederId, null, "AnodeType");
                string srcColor = ModbusKeyHelper.Build(feederId, null, "ProductColor");
                string srcCategory = ModbusKeyHelper.Build(feederId, null, "MaterialCategory");
                string srcReserved = ModbusKeyHelper.Build(feederId, null, "Reserved");

                var valProjectNo = _bus.GetValue(srcProjectNo);
                var valProductType = _bus.GetValue(srcProductType);
                var valAnodeType = _bus.GetValue(srcAnodeType);
                var valColor = _bus.GetValue(srcColor);
                var valCategory = _bus.GetValue(srcCategory);
                var valReserved = _bus.GetValue(srcReserved);

                // =========================================================
                // 3. 写入目标(Flipper)
                // =========================================================
                string destProjectNo = ModbusKeyHelper.Build(flipperId, null, "ProjectNo");
                string destProductType = ModbusKeyHelper.Build(flipperId, null, "ProductType");
                string destAnodeType = ModbusKeyHelper.Build(flipperId, null, "AnodeType");
                string destColor = ModbusKeyHelper.Build(flipperId, null, "ProductColor");
                string destCategory = ModbusKeyHelper.Build(flipperId, null, "MaterialCategory");
                string destReserved = ModbusKeyHelper.Build(flipperId, null, "Reserved");

                _engine.WriteTag(destProjectNo, valProjectNo);
                _engine.WriteTag(destProductType, valProductType);
                _engine.WriteTag(destAnodeType, valAnodeType);
                _engine.WriteTag(destColor, valColor);
                _engine.WriteTag(destCategory, valCategory);
                _engine.WriteTag(destReserved, valReserved);

                // =========================================================
                // 4. 复位信号：将触发点写回 0
                // =========================================================
               //写入0表示转产完成，防止重复触发
                _engine.WriteTag(triggerTag, (short)0);
                //写入11通知转产
                ChangeoverFlipperTrigger(int.Parse(moduleId));
                // 记录日志
                Console.WriteLine($"[{moduleId}] 转产自动同步: {feederTemplateName} -> Flipper. 项目号: {valProjectNo}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{moduleId}] 转产转发异常: {feederTemplateName}", ex);
            }
        }

        // 辅助判断方法
        private bool IsTriggered(object val)
        {
            if (val == null) return false;
            try
            {
                return Convert.ToInt32(val) == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 上位机主动触发转产 (保持不变)
        /// </summary>
        public void ChangeoverFlipperTrigger(int num)
        {
            string moduleId = num.ToString();
            string suffix = "ChangeoverTrigger"; // 注意：这里可能需要确认是否也带 Hourly_ 前缀
            short triggerVal_ = 11; // 写入 11 触发

            string deviceFlipper = ModbusKeyHelper.BuildDeviceId(moduleId, "PLC_Flipper");

            // 如果翻转台的触发点也是 Hourly_ChangeoverTrigger，请同步修改 suffix
            // 根据之前的上下文，翻转台触发可能是 1_PLC_Flipper_Hourly_ChangeoverTrigger
            _engine.WriteTag(ModbusKeyHelper.Build(deviceFlipper, null, suffix), triggerVal_);
        }

        #endregion
    }
    public class UpLoadProxy
    {
        private readonly DataBus _bus;
        private readonly string _triggerTagName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bus">全局数据总线，用于读取兄弟点位的值</param>
        /// <param name="triggerData">触发信号的数据包</param>
        public UpLoadProxy(DataBus bus, TagData triggerData)
        {
            _bus = bus;
            _triggerTagName = triggerData.TagName;
        }

        /// <summary>
        /// 动态获取所属机器名
        /// 逻辑：从 "PLC_Feeder_A_ReadTrigger" 解析出 "PLC_Feeder_A"
        /// </summary>
        public string DeviceName
        {
            get
            {
                // 使用 ModbusKeyHelper.GetDeviceNameFromTag (截取最后一个 '_' 之前的内容)
                // 结果示例: "PLC_Feeder_A" 或 "PLC_Flipper_A"
                return ModbusKeyHelper.GetDeviceNameFromTag(_triggerTagName);
            }
        }

        /// <summary>
        /// 动态获取同组的产品码
        /// 逻辑：将 "ReadTrigger" 替换为 "ProductCode"
        /// </summary>
        public object ProductCode
        {
            get
            {
                // 1. 计算目标点位名
                // 你的 ModbusKeyHelper.GetSibling 完美适用于此场景
                // 它会将 "PLC_Feeder_A_ReadTrigger" 变成 "PLC_Feeder_A_ProductCode"
                string targetTagName = ModbusKeyHelper.GetSibling(_triggerTagName, "ProductCode");

                // 2. 从 DataBus 缓存中直接读取该点位的最新值
                return _bus.GetValue(targetTagName);
            }
        }

    }


    public class FlipperProductProxy
    {
        private readonly DataBus _bus;
        private readonly string _prefix; // 例如: "PLC_Flipper_A" 或 "PLC_Flipper_B"

        public FlipperProductProxy(DataBus bus, string triggerTagName)
        {
            _bus = bus;
            // 核心逻辑：利用 Helper 截取触发信号的前缀
            // 输入: "PLC_Flipper_A_ReadTrigger" -> 得到: "PLC_Flipper_A"
            // 输入: "PLC_Flipper_B_ReadTrigger" -> 得到: "PLC_Flipper_B"
            _prefix = ModbusKeyHelper.GetDeviceNameFromTag(triggerTagName);
        }

        /// <summary>
        /// 获取当前逻辑设备名 (如 "PLC_Flipper_A")
        /// </summary>
        public string DeviceName => _prefix;

        /// <summary>
        /// 动态获取当前触发面的所有产品码
        /// 自动适配 A 面或 B 面
        /// </summary>
        public List<string> CurrentProductCodes
        {
            get
            {
                // 1. 拼接目标点位名: PLC_Flipper_A_ProductCode
                string tagName = $"{_prefix}_ProductCode";

                // 2. 获取数据
                object val = _bus.GetValue(tagName);

                // 3. 解析字符串列表
                // 假设 PLC 传上来的是 "SN001,SN002" 这种格式，或者就是一个长字符串
                if (val is string s && !string.IsNullOrWhiteSpace(s))
                {
                    // 兼容逗号、分号分隔，去除空白项
                    return s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                return new List<string>();
            }
        }

        // --- 以下属性利用 Expression Body 动态获取对应点位值 ---

        // 挂具码: PLC_Flipper_A_FixtureCode
        public string FixtureCode => _bus.GetValue($"{_prefix}_FixtureCode")?.ToString() ?? string.Empty;

        // 项目号: PLC_Flipper_A_ProjectNo
        public string ProductProjectNo => _bus.GetValue($"{_prefix}_ProjectNo")?.ToString() ?? string.Empty;

        // 产品类型/原料类别: PLC_Flipper_A_ProductType (或 MaterialCategory，视你具体需求而定)
        // 这里映射到 ProductType，如需 MaterialCategory 请修改后缀
        public string ProductCategory => _bus.GetValue($"{_prefix}_MaterialCategory")?.ToString() ?? string.Empty;

        // 辅助属性：获取当前是 A 面还是 B 面
        public bool IsSideA => _prefix.EndsWith("A");
    }
}
