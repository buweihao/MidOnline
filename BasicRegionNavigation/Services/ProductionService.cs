using BasicRegionNavigation.ViewModels;
using Dm;
using Microsoft.Extensions.DependencyInjection;
using MyDatabase;
using MyLog;
using MyModbus;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BasicRegionNavigation.Services.UpDropHourlyService;

namespace BasicRegionNavigation.Services
{

    public class ProductionService : IProductionService
    {

        private readonly IServiceProvider _serviceProvider;

        // 1. 明确注入：你需要操作哪两张表，就注入哪两个仓储
        private readonly IRepository<DeviceLog> _logRepo;
        private readonly IRepository<ProductionRecord> _prodRepo;
        private readonly ISqlSugarClientFactory _clientFactory;

        private readonly IRepository<UpDropHourlyRecord> _hourlyRepo;

        public ProductionService(
            ISqlSugarClientFactory clientFactory,
            IRepository<DeviceLog> logRepo,
            IRepository<ProductionRecord> prodRepo,
            IRepository<UpDropHourlyRecord> hourlyRepo)
        {
            _clientFactory = clientFactory;
            _logRepo = logRepo;
            _prodRepo = prodRepo;
            _hourlyRepo = hourlyRepo;
        }

        public MyLogOptions Configure()
        {
            return new MyLogOptions
            {
                MinimumLevel = Serilog.Events.LogEventLevel.Verbose, // 演示：针对此服务的配置
                EnableConsole = true,
                EnableFile = true,
                FilePath = "logs/ProductionService.log",
                OutputTemplate = "{Timestamp:HH:mm:ss} [Service] {Message:lj}{NewLine}{Exception}"
            };
        }

        public async Task<PieChartDto> GetViewBPieStatsAsync(DateTime start, DateTime end, string modulePrefix)
        {
            var result = new PieChartDto();

            try
            {
                // =========================================================================
                // 1. 动态生成目标设备ID列表
                // =========================================================================
                // 不再手动定义 string upFeederA = ..., 而是根据 Config 动态生成
                // 逻辑：遍历配置中启用的设备名，加上当前的模组前缀 (例如 "1")

                // 生成所有启用的 [上料] 设备ID列表 (如: ["1_PLC_Feeder_A", "1_PLC_Feeder_B"])
                var targetUpDevices = SystemConfig.ActiveUpLoaders
                    .Select(template => ModbusKeyHelper.BuildDeviceId(modulePrefix, template))
                    .ToList();

                // 生成所有启用的 [下料] 设备ID列表 (如: ["1_PLC_DnFeeder_A"])
                var targetDnDevices = SystemConfig.ActiveDownLoaders
                    .Select(template => ModbusKeyHelper.BuildDeviceId(modulePrefix, template))
                    .ToList();

                // =========================================================================
                // 2. 数据库查询
                // =========================================================================
                // 使用 .Contains() 替代原来的 == A || == B
                // SqlSugar/EF Core 会将其自动翻译为 SQL 的 IN ('...', '...') 语法

                var rawData = await _prodRepo.GetListAsync(x =>
                    x.CreateTime >= start &&
                    x.CreateTime <= end &&
                    (
                        // 查询条件：上料设备在列表中 OR 下料设备在列表中
                        // 注意：如果列表为空 (例如无下料项目)，Contains 会自动返回 false，逻辑依然成立
                        targetUpDevices.Contains(x.UpLoadDeivceName) ||
                        targetDnDevices.Contains(x.LowerHangFlipDeivceName)
                    ));

                // =========================================================================
                // 3. 上挂饼图数据：上料机产量分布
                // =========================================================================
                // 过滤条件同样使用 List.Contains
                var upGroups = rawData
                    .Where(x => targetUpDevices.Contains(x.UpLoadDeivceName))
                    .GroupBy(x => x.ProjectNumber ?? "未知项目")
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToList();

                foreach (var item in upGroups)
                {
                    result.UpPieData.Add(item.Name, item.Count);
                }

                // 只有当实际上开启了上料业务，且没查到数据时，才显示无数据占位
                // 如果 ActiveUpLoaders 为空(说明是纯下料项目)，则不需要显示"无上料数据"的占位(或者根据UI需求决定)
                if (result.UpPieData.Count == 0 && targetUpDevices.Any())
                {
                    result.UpPieData.Add("无上料数据", 1);
                }

                // =========================================================================
                // 4. 下挂饼图数据：下料机产量分布
                // =========================================================================
                var dnGroups = rawData
                    .Where(x => targetDnDevices.Contains(x.LowerHangFlipDeivceName))
                    .GroupBy(x => x.ProductCategory ?? "未知型号") // 如果你想统计颜色，这里可以改成 x.ProductColor
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToList();

                foreach (var item in dnGroups)
                {
                    result.DnPieData.Add(item.Name, item.Count);
                }

                if (result.DnPieData.Count == 0 && targetDnDevices.Any())
                {
                    result.DnPieData.Add("无下料数据", 1);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PieStats] 统计异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取 ViewB 生产统计数据 (已适配 SystemConfig)
        /// </summary>
        /// <param name="modulePrefix">模组前缀，如 "1" 或 "2"</param>
        public async Task<List<ProductInfoTable>> GetViewBProductionStatsAsync(DateTime start, DateTime end, string modulePrefix)
        {
            try
            {
                // ========================================================================
                // 1. 动态生成设备 ID 列表
                // ========================================================================

                // 生成上料设备 ID 列表 (例如: ["1_PLC_Feeder_A", "1_PLC_Feeder_B"])
                var activeUpDevices = SystemConfig.ActiveUpLoaders
                    .Select(t => ModbusKeyHelper.BuildDeviceId(modulePrefix, t))
                    .ToList();

                // 生成下料设备 ID 列表
                var activeDnDevices = SystemConfig.ActiveDownLoaders
                    .Select(t => ModbusKeyHelper.BuildDeviceId(modulePrefix, t))
                    .ToList();

                // 生成翻转台设备 ID 列表 (通常只有一个)
                var activeFlipperDevices = SystemConfig.FlipperDevice
                    .Select(t => ModbusKeyHelper.BuildDeviceId(modulePrefix, t))
                    .ToList();

                // ========================================================================
                // 2. 准备 UI 映射变量
                // ========================================================================
                // 为了填充 DTO 中的 Feeder1/Feeder2 字段，我们需要知道列表中谁是老大，谁是老二
                // ElementAtOrDefault 防止列表为空或只有一个元素时报错

                string? upDev1 = activeUpDevices.ElementAtOrDefault(0); // 映射到 UpFeeder1
                string? upDev2 = activeUpDevices.ElementAtOrDefault(1); // 映射到 UpFeeder2

                string? dnDev1 = activeDnDevices.ElementAtOrDefault(0); // 映射到 DnFeeder1
                string? dnDev2 = activeDnDevices.ElementAtOrDefault(1); // 映射到 DnFeeder2

                // ========================================================================
                // 3. 数据库查询
                // ========================================================================
                var rawList = await _prodRepo.GetListAsync(x =>
                    x.CreateTime >= start &&
                    x.CreateTime <= end &&
                    (
                        // 只要记录中的设备名 存在于 我们计算出的列表中，就查出来
                        // SqlSugar/EF 会自动转为 IN (...) 语法
                        activeUpDevices.Contains(x.UpLoadDeivceName) ||
                        activeFlipperDevices.Contains(x.UpperHangFlipDeivceName) ||
                        activeDnDevices.Contains(x.LowerHangFlipDeivceName)
                    // 注意：如果只是单纯统计下料，用 LowerHangFlipDeivceName 判定即可
                    )
                );

                // ========================================================================
                // 4. 内存分组统计
                // ========================================================================
                var result = rawList
                    .GroupBy(x => x.ProjectNumber ?? "未知项目")
                    .Select(g => new ProductInfoTable
                    {
                        ProjectId = g.Key,
                        MaterialType = g.Select(x => x.ProductCategory).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "-",
                        AnodeType = "-",

                        // --- 上料统计 ---
                        // 统计属于 "第一个配置设备" 的数量
                        UpFeeder1 = upDev1 != null ? g.Count(x => x.UpLoadDeivceName == upDev1) : 0,
                        // 统计属于 "第二个配置设备" 的数量
                        UpFeeder2 = upDev2 != null ? g.Count(x => x.UpLoadDeivceName == upDev2) : 0,

                        // 计算总数：直接统计属于列表中的所有记录 (即使未来有 Feeder3 也会被算进 Total)
                        UpTotalFeederOutput = g.Count(x => activeUpDevices.Contains(x.UpLoadDeivceName)),


                        // --- 上翻转台统计 ---
                        // 逻辑：有时间 且 设备在翻转台列表中
                        UpTurnTable = g.Count(x => x.UpperHangFlip_Time != null &&
                                                   activeFlipperDevices.Contains(x.UpperHangFlipDeivceName)),


                        // --- 下料统计 ---
                        DnFeeder1 = dnDev1 != null ? g.Count(x => x.LowerHangFlipDeivceName == dnDev1) : 0,
                        DnFeeder2 = dnDev2 != null ? g.Count(x => x.LowerHangFlipDeivceName == dnDev2) : 0,

                        // 下料总数
                        DnTotalFeederOutput = g.Count(x => activeDnDevices.Contains(x.LowerHangFlipDeivceName)),


                        // --- 下翻转台统计 ---
                        DnTurnTable = g.Count(x => x.LowerHangFlip_Time != null &&
                                                   activeFlipperDevices.Contains(x.LowerHangFlipDeivceName))
                    })
                    .ToList();

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProductionService] 统计异常: {ex.Message}");
                return new List<ProductInfoTable>();
            }
        }

        public async Task ProcessProductDataAsync(StationProcessContext context)
        {
            // ---------------------------------------------------------
            // 1. 调试日志 (Logger)：记录原始数据，方便排查 PLC 通讯问题
            // ---------------------------------------------------------
            string jsonPayload = "{}";
            try
            {
                jsonPayload = System.Text.Json.JsonSerializer.Serialize(context.PlcData);
                // 使用 Logger 记录详细报文
                Console.WriteLine($"[{context.DeviceId}] 收到请求 | Type: {context.ProcessType} | Key: {context.IdentityValue} | Payload: {jsonPayload}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{context.DeviceId}] 数据序列化异常", ex);
            }

            try
            {
                // ---------------------------------------------------------
                // 2. 业务逻辑处理
                // ---------------------------------------------------------
                switch (context.ProcessType)
                {
                    // === 场景 A：第一道工序（上料） ===
                    case StationProcessType.Entry_Upload:
                        {
                            // 1. 解析产品码：按 ",," 分割，并去除空白项
                            // 假设传入的是 "SN001,,SN002,,SN003"
                            string rawInput = context.IdentityValue ?? string.Empty;
                            string[] productCodes = rawInput.Split(new string[] { ";;" }, StringSplitOptions.RemoveEmptyEntries);

                            Console.WriteLine($"[上料-批量] 收到请求，原始值: {rawInput}，解析数量: {productCodes.Length}");

                            if (productCodes.Length == 0)
                            {
                                Console.WriteLine($"[上料-警告] 解析到的产品码列表为空，跳过处理。Raw: {rawInput}");
                                break;
                            }

                            // 2. 遍历处理每一个产品码
                            foreach (var code in productCodes)
                            {
                                string currentSn = code.Trim(); // 去除可能的首尾空格
                                if (string.IsNullOrWhiteSpace(currentSn)) continue;

                                var existingItem = await _prodRepo.GetAsync(x => x.ProductCode == currentSn);

                                if (existingItem == null)
                                {
                                    // [新增]
                                    var newItem = new ProductionRecord
                                    {
                                        ProductCode = currentSn,
                                        UpLoadDeivceName = context.DeviceId,
                                        UpLoad_Time = DateTime.Now,
                                        CreateTime = DateTime.Now,
                                        IsCompleted = false
                                    };

                                    await _prodRepo.InsertAsync(newItem);

                                    // Logger: 调试用
                                    Console.WriteLine($"[上料-新增] SN: {currentSn} 入库成功 (Batch Item)");

                                    // LogRepo: 数据库留痕 (仅关键节点)
                                    await _logRepo.InsertAsync(new DeviceLog
                                    {
                                        Module = context.DeviceId,
                                        Message = $"产品上线: {currentSn}",
                                        CreateTime = DateTime.Now
                                    });
                                }
                                else
                                {
                                    // [更新]
                                    existingItem.UpLoadDeivceName = context.DeviceId;
                                    existingItem.UpLoad_Time = DateTime.Now;

                                    await _prodRepo.UpdateAsync(existingItem);

                                    // Logger: 调试用
                                    Console.WriteLine($"[上料-更新] SN: {currentSn} 更新位置信息 (Batch Item)");
                                }
                            }
                        }
                        break;

                    // === 场景 B：中间工序（上翻转台） ===
                    case StationProcessType.Process_Flip:
                        {
                            // 1. 解析产品码：按分号 ";" 分割
                            string rawInput = context.IdentityValue ?? string.Empty;
                            string[] productCodes = rawInput.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                            Console.WriteLine($"[翻转-批量] 收到请求，原始值: {rawInput}，解析数量: {productCodes.Length}");

                            if (productCodes.Length == 0)
                            {
                                Console.WriteLine($"[翻转-警告] 解析到的产品码列表为空，跳过处理。");
                                break;
                            }

                            // 2. 预先提取公共数据 (同一批翻转通常共享挂具号和项目信息)
                            string? fixture = context.PlcData.TryGetValue("FixtureCode", out var fVal) ? fVal?.ToString() : null;
                            string? projNum = context.PlcData.TryGetValue("ProjectNumber", out var pVal) ? pVal?.ToString() : null;
                            string? category = context.PlcData.TryGetValue("ProductCategory", out var cVal) ? cVal?.ToString() : null;

                            // [新增] 提取颜色信息
                            string? color = context.PlcData.TryGetValue("ProductColor", out var colorVal) ? colorVal?.ToString() : null;

                            // 3. 遍历每一个产品码进行更新
                            foreach (var code in productCodes)
                            {
                                string currentSn = code.Trim();
                                if (string.IsNullOrWhiteSpace(currentSn)) continue;

                                var item = await _prodRepo.GetAsync(x => x.ProductCode == currentSn);

                                if (item != null)
                                {
                                    // 更新属性
                                    item.UpperHangFlipDeivceName = context.DeviceId;
                                    item.UpperHangFlip_Time = DateTime.Now;

                                    if (fixture != null) item.FixtureCode = fixture;
                                    if (projNum != null) item.ProjectNumber = projNum;
                                    if (category != null) item.ProductCategory = category;

                                    // [新增] 更新数据库实体的颜色字段
                                    // 请确保你的数据库实体类 (item) 中已经包含了 ProductColor 属性
                                    if (color != null) item.ProductColor = color;

                                    await _prodRepo.UpdateAsync(item);

                                    // Logger: 记录详细变更 (增加颜色日志)
                                    Console.WriteLine($"[翻转-绑定] SN: {currentSn} | 挂具: {fixture} | 项目: {projNum} | 颜色: {color}");

                                    // LogRepo: 数据库留痕 (业务流转节点)
                                    await _logRepo.InsertAsync(new DeviceLog
                                    {
                                        Module = context.DeviceId,
                                        Message = $"翻转台流转: {currentSn}, 颜色: {color}", // [可选] 在Log表中也记录一下颜色
                                        CreateTime = DateTime.Now
                                    });
                                }
                                else
                                {
                                    // 异常流程：有物理产品但无数据
                                    string errorMsg = $"[逻辑异常] 翻转台收到 SN {currentSn}，但数据库未找到上料记录 (Batch Item)";

                                    // Logger: 记录为 Error
                                    Console.WriteLine(errorMsg);

                                    // LogRepo: 记录异常
                                    await _logRepo.InsertAsync(new DeviceLog
                                    {
                                        Module = context.DeviceId,
                                        Message = $"异常: 未知产品 {currentSn}",
                                        CreateTime = DateTime.Now
                                    });
                                }
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // ---------------------------------------------------------
                // 3. 异常处理
                // ---------------------------------------------------------
                // Logger: 记录完整堆栈，这是调试最关键的
                Console.WriteLine($"[{context.DeviceId}] 业务处理崩溃", ex);

                // LogRepo: 数据库记录简短错误，防止数据库爆炸
                await _logRepo.InsertAsync(new DeviceLog
                {
                    Module = context.DeviceId,
                    Message = $"系统错误: {ex.Message}",
                    CreateTime = DateTime.Now
                });
            }
        }
        public async Task<ObservableCollection<ProductionRecord>> GetProductionRecordsAsync(
            DateTime? startTime = null,
            DateTime? endTime = null,
            Dictionary<string, object>? filters = null)
        {
            // 查询还是用仓储的 GetListAsync
            // 由于 GetListAsync 只能传简单的 Expression，如果需要复杂动态查询，
            // 这种情况下，我们需要稍微妥协一下：
            // 方案 1：把数据全查出来（如果数据量不大），在内存里过滤 (Where + Reflection)。
            // 方案 2：在 IRepository 接口里增加一个暴露 Client 的方法（稍微破坏封装，但实用）。

            // 这里演示方案 1 (适合数据量 < 10000 条的场景)

            // 1. 先查出所有 (或者按时间查出大部分)
            IEnumerable<ProductionRecord> list;
            if (startTime.HasValue && endTime.HasValue)
            {
                list = await _prodRepo.GetListAsync(x => x.CreateTime >= startTime && x.CreateTime <= endTime);
            }
            else
            {
                list = await _prodRepo.GetAllAsync();
            }

            // 2. 内存动态过滤
            if (filters != null && filters.Count > 0)
            {
                foreach (var filter in filters)
                {
                    var prop = typeof(ProductionRecord).GetProperty(filter.Key);
                    if (prop != null && filter.Value != null)
                    {
                        string targetVal = filter.Value.ToString();
                        list = list.Where(x =>
                        {
                            var val = prop.GetValue(x)?.ToString();
                            return val == targetVal;
                        });
                    }
                }
            }

            // 3. 排序并返回
            var sortedList = list.OrderByDescending(x => x.CreateTime).ToList();
            return new ObservableCollection<ProductionRecord>(sortedList);
        }
        public async Task<Dictionary<string, Dictionary<string, int>>> GetProductStatsByModuleAndProjectAsync(DateTime startTime, DateTime endTime)
        {
            return new Dictionary<string, Dictionary<string, int>>(); // 占位实现
        }
    }
    [SugarTable("Production_Records")]
    public class ProductionRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        // --- 工序 1 (上料机A/B) ---
        public string? ProductCode { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? UpLoadDeivceName { get; set; }

        [SugarColumn(IsNullable = true)]
        public DateTime? UpLoad_Time { get; set; }

        // --- 工序 2 (上翻转台) ---
        [SugarColumn(IsNullable = true)]
        public string? FixtureCode { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? ProjectNumber { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? ProductCategory { get; set; }

        // [新增] 产品颜色字段
        // 必须允许为空，因为上料时还没读取到颜色
        [SugarColumn(IsNullable = true)]
        public string? ProductColor { get; set; }

        [SugarColumn(IsNullable = true)]
        public string? UpperHangFlipDeivceName { get; set; }

        [SugarColumn(IsNullable = true)]
        public DateTime? UpperHangFlip_Time { get; set; }

        // --- 工序 3 (下翻转台) ---
        [SugarColumn(IsNullable = true)]
        public string? LowerHangFlipDeivceName { get; set; }

        [SugarColumn(IsNullable = true)]
        public DateTime? LowerHangFlip_Time { get; set; }

        // 最终状态
        public DateTime CreateTime { get; set; }

        [SugarColumn(IsNullable = true)]
        public DateTime? FinishTime { get; set; }

        public bool IsCompleted { get; set; }
    }


    // 设备日志表
    [SugarTable("Device_Logs")]
    public class DeviceLog
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        public string Module { get; set; } // 来源模块 (例如: PLC, Vision)
        public string Message { get; set; }
        public DateTime CreateTime { get; set; }
    }
    public interface IProductionService : IMyLogConfig
    {
        Task ProcessProductDataAsync(StationProcessContext context);
        Task<ObservableCollection<ProductionRecord>> GetProductionRecordsAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        Dictionary<string, object>? filters = null);
        Task<List<ProductInfoTable>> GetViewBProductionStatsAsync(DateTime start, DateTime end, string modulePrefix);
        Task<Dictionary<string, Dictionary<string, int>>> GetProductStatsByModuleAndProjectAsync(DateTime startTime, DateTime endTime);

        Task<PieChartDto> GetViewBPieStatsAsync(DateTime start, DateTime end, string modulePrefix);
    }
    // 1. 定义工序类型（明确告诉程序当前是哪一步）
    public enum StationProcessType
    {
        /// <summary>
        /// 进站/上料 (Identity = SN)
        /// 行为：新建或更新记录
        /// </summary>
        Entry_Upload,

        /// <summary>
        /// 中间工序 (Identity = SN)
        /// 行为：只更新数据
        /// </summary>
        Process_Flip,

        /// <summary>
        /// 出站/下料 (Identity = FixtureCode)
        /// 行为：反查挂具 -> 完结记录
        /// </summary>
        Exit_Unload
    }

    // 2. 定义统一的参数对象 (Context)
    public class StationProcessContext
    {
        /// <summary>
        /// 触发的设备全名 (如 "1_PLC_UpLoad")
        /// 用于写入数据库的 DeviceName 字段
        /// </summary>
        public string DeviceId { get; set; }

        /// <summary>
        /// 关键标识值 (可能是 SN，也可能是 挂具号)
        /// </summary>
        public string IdentityValue { get; set; }

        /// <summary>
        /// 当前工序类型 (核心逻辑开关)
        /// </summary>
        public StationProcessType ProcessType { get; set; }

        /// <summary>
        /// PLC 原始数据包
        /// </summary>
        public Dictionary<string, object> PlcData { get; set; }

        /// <summary>
        /// 辅助方法：快速创建
        /// </summary>
        public static StationProcessContext Create(string deviceId, string identity, StationProcessType type, Dictionary<string, object> data)
        {
            return new StationProcessContext
            {
                DeviceId = deviceId,
                IdentityValue = identity,
                ProcessType = type,
                PlcData = data
            };
        }
    }
    // 定义传输对象
    public class PieChartDto
    {
        public Dictionary<string, int> UpPieData { get; set; } = new(); // 设备状态分布 (时间/分钟)
        public Dictionary<string, int> DnPieData { get; set; } = new(); // 产品产量分布 (数量/个)
    }
}
