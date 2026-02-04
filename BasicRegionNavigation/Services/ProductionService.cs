using BasicRegionNavigation.ViewModels;
using Dm;
using Microsoft.Extensions.DependencyInjection;
using MyDatabase;
using MyLog;
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
                // 1. 定义目标设备名称
                string upFeederA = $"{modulePrefix}_PLC_Feeder_A";
                string upFeederB = $"{modulePrefix}_PLC_Feeder_B";
                string dnFeederA = $"{modulePrefix}_PLC_UnFeeder_A";
                string dnFeederB = $"{modulePrefix}_PLC_UnFeeder_B";

                // 2. 查出该时间段内所有相关的生产记录
                // 只要是该模组的上料或下料记录，都查出来
                var rawData = await _prodRepo.GetListAsync(x =>
                    x.CreateTime >= start &&
                    x.CreateTime <= end &&
                    (
                        x.UpLoadDeivceName == upFeederA ||
                        x.UpLoadDeivceName == upFeederB ||
                        x.LowerHangFlipDeivceName == dnFeederA ||
                        x.LowerHangFlipDeivceName == dnFeederB
                    ));

                // -------------------------------------------------------------
                // 3. 上挂饼图数据：上料机产量分布
                // -------------------------------------------------------------
                var upGroups = rawData
                    .Where(x => x.UpLoadDeivceName == upFeederA || x.UpLoadDeivceName == upFeederB)
                    .GroupBy(x => x.ProjectNumber ?? "未知项目") // <--- 修改此处
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToList();

                foreach (var item in upGroups)
                {
                    result.UpPieData.Add(item.Name, item.Count);
                }
                if (result.UpPieData.Count == 0) result.UpPieData.Add("无上料数据", 1); // 占位

                // -------------------------------------------------------------
                // 4. 下挂饼图数据：下料机产量分布
                // -------------------------------------------------------------
                var dnGroups = rawData
                    .Where(x => x.LowerHangFlipDeivceName == dnFeederA || x.LowerHangFlipDeivceName == dnFeederB)
                    .GroupBy(x => x.ProductCategory ?? "未知型号")
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToList();

                foreach (var item in dnGroups)
                {
                    result.DnPieData.Add(item.Name, item.Count);
                }
                if (result.DnPieData.Count == 0) result.DnPieData.Add("无下料数据", 1); // 占位

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PieStats] 统计异常: {ex.Message}");
                // 异常时返回空对象，防止 UI 崩溃
            }

            return result;
        }

        /// <summary>
        /// 获取 ViewB 生产统计数据
        /// </summary>
        /// <param name="modulePrefix">模组前缀，如 "1" 或 "2"</param>
        public async Task<List<ProductInfoTable>> GetViewBProductionStatsAsync(DateTime start, DateTime end, string modulePrefix)
        {
            try
            {
                // 1. 定义标准设备名称字符串，避免在循环中重复拼接
                // 上料机标准名
                string targetUpFeederA = $"{modulePrefix}_PLC_Feeder_A";
                string targetUpFeederB = $"{modulePrefix}_PLC_Feeder_B";

                // 下料机标准名 (注意：根据你的要求，这里映射到 UnFeeder)
                string targetDnFeederA = $"{modulePrefix}_PLC_UnFeeder_A";
                string targetDnFeederB = $"{modulePrefix}_PLC_UnFeeder_B";

                // 2. 数据库查询：获取时间范围内，该模组相关的所有记录
                // 只要该记录的 上料设备、上翻转设备、或下翻转设备 名字以模组号开头，都算该模组的数据
                var rawList = await _prodRepo.GetListAsync(x =>
                    x.CreateTime >= start &&
                    x.CreateTime <= end &&
                    (
                        (x.UpLoadDeivceName != null && x.UpLoadDeivceName.StartsWith(modulePrefix)) ||
                        (x.UpperHangFlipDeivceName != null && x.UpperHangFlipDeivceName.StartsWith(modulePrefix)) ||
                        (x.LowerHangFlipDeivceName != null && x.LowerHangFlipDeivceName.StartsWith(modulePrefix))
                    )
                );

                // 3. 内存分组统计
                var result = rawList
                    .GroupBy(x => x.ProjectNumber ?? "未知项目")
                    .Select(g => new ProductInfoTable
                    {
                        ProjectId = g.Key,

                        // 基础信息 (取第一条非空值)
                        MaterialType = g.Select(x => x.ProductCategory).FirstOrDefault(s => !string.IsNullOrEmpty(s)) ?? "-",
                        AnodeType = "-", // 数据库若无此字段则给默认值

                        // --- 上料统计 (根据 DeviceName 精确匹配) ---

                        // 上料机A: UpLoadDeivceName == "1_PLC_Feeder_A"
                        UpFeeder1 = g.Count(x => x.UpLoadDeivceName == targetUpFeederA),

                        // 上料机B: UpLoadDeivceName == "1_PLC_Feeder_B"
                        UpFeeder2 = g.Count(x => x.UpLoadDeivceName == targetUpFeederB),

                        // --- 上翻转台统计 (根据 Time 是否存在) ---
                        // 逻辑：只要有 UpperHangFlip_Time，且设备名属于当前模组，就算过站
                        UpTurnTable = g.Count(x => x.UpperHangFlip_Time != null &&
                                                   (x.UpperHangFlipDeivceName != null && x.UpperHangFlipDeivceName.StartsWith(modulePrefix))),

                        // --- 下料统计 (字段映射: LowerHangFlipDeivceName -> UnFeeder) ---

                        // 下料机A: LowerHangFlipDeivceName == "1_PLC_UnFeeder_A"
                        DnFeeder1 = g.Count(x => x.LowerHangFlipDeivceName == targetDnFeederA),

                        // 下料机B: LowerHangFlipDeivceName == "1_PLC_UnFeeder_B"
                        DnFeeder2 = g.Count(x => x.LowerHangFlipDeivceName == targetDnFeederB),

                        // --- 下翻转台统计 (根据 Time 是否存在) ---
                        // 逻辑：只要有 LowerHangFlip_Time，就算完成了下翻转动作
                        DnTurnTable = g.Count(x => x.LowerHangFlip_Time != null &&
                                                   (x.LowerHangFlipDeivceName != null && x.LowerHangFlipDeivceName.StartsWith(modulePrefix)))
                    })
                    .ToList();

                // 4. 计算合计
                foreach (var item in result)
                {
                    item.UpTotalFeederOutput = item.UpFeeder1 + item.UpFeeder2;
                    item.DnTotalFeederOutput = item.DnFeeder1 + item.DnFeeder2;
                }

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
