using MyDatabase;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BasicRegionNavigation.Services.UpDropHourlyService;

namespace BasicRegionNavigation.Services
{
    public interface IFlipperHourlyService
    {
        Task ProcessFlipperHourlyDataAsync(string deviceName, Dictionary<string, object> data);
    }

    public class FlipperHourlyService : IFlipperHourlyService
    {
        private readonly IRepository<FlipperHourlyRecord> _repo;

        // 内存状态缓存 (用于计算增量)
        private static readonly ConcurrentDictionary<string, DeviceState> _deviceStates
            = new ConcurrentDictionary<string, DeviceState>();

        public FlipperHourlyService(IRepository<FlipperHourlyRecord> repo)
        {
            _repo = repo;
        }

        public async Task ProcessFlipperHourlyDataAsync(string deviceName, Dictionary<string, object> data)
        {
            if (data == null || data.Count == 0) return;

            // ---------------------------------------------------------
            // 1. 提取当前读数 (全部视为累计值)
            // ---------------------------------------------------------
            // 字符串字段 (非累计，直接取)
            string projectNo = GetString(data, "Hourly_ProjectNo");
            string productType = GetString(data, "Hourly_ProductType");
            string batchNo = GetString(data, "Hourly_BatchNo");
            string anodeType = GetString(data, "Hourly_AnodeType");
            string materialCat = GetString(data, "Hourly_MaterialCategory");

            // 数值字段 (累计值，需要计算增量)
            int currTotalCap = GetInt(data, "TotalCapacity");
            int currStandby = GetInt(data, "Hourly_StandbyTimeMin");
            int currFaultTime = GetInt(data, "Hourly_FaultTimeMin");
            int currFaultCount = GetInt(data, "Hourly_FaultCount");

            // [翻转台特有字段]
            int currMixing = GetInt(data, "Hourly_MixingQty");
            int currScanNG = GetInt(data, "Hourly_ScanNGQty");
            int currSysFeedback = GetInt(data, "Hourly_SysFeedbackQty");

            // ---------------------------------------------------------
            // 2. 获取或初始化状态
            // ---------------------------------------------------------
            var state = _deviceStates.GetOrAdd(deviceName, new DeviceState
            {
                // 初始化时，将当前值直接作为"上次值"，避免首次运行产生巨大增量
                LastTotalCapacity = currTotalCap,
                LastStandbyTime = currStandby,
                LastFaultTime = currFaultTime,
                LastFaultCount = currFaultCount,
                LastMixingQty = currMixing,
                LastScanNGQty = currScanNG,
                LastSysFeedbackQty = currSysFeedback,
                IsFirstRun = true
            });

            // ---------------------------------------------------------
            // 3. 计算增量 (当前 - 上次)
            // ---------------------------------------------------------
            int deltaCap = CalculateDelta(currTotalCap, state.LastTotalCapacity);
            int deltaStandby = CalculateDelta(currStandby, state.LastStandbyTime);
            int deltaFaultTime = CalculateDelta(currFaultTime, state.LastFaultTime);
            int deltaFaultCount = CalculateDelta(currFaultCount, state.LastFaultCount);
            int deltaMixing = CalculateDelta(currMixing, state.LastMixingQty);
            int deltaScanNG = CalculateDelta(currScanNG, state.LastScanNGQty);
            int deltaSysFeedback = CalculateDelta(currSysFeedback, state.LastSysFeedbackQty);

            // ---------------------------------------------------------
            // 4. 首次运行截断逻辑
            // ---------------------------------------------------------
            if (state.IsFirstRun)
            {
                // 第一次运行不知道过去一小时发生了什么，强制归零，防止数据污染
                deltaCap = 0;
                deltaStandby = 0;
                deltaFaultTime = 0;
                deltaFaultCount = 0;
                deltaMixing = 0;
                deltaScanNG = 0;
                deltaSysFeedback = 0;

                state.IsFirstRun = false;
            }

            // ---------------------------------------------------------
            // 5. 更新状态 (为下一次计算做准备)
            // ---------------------------------------------------------
            state.LastTotalCapacity = currTotalCap;
            state.LastStandbyTime = currStandby;
            state.LastFaultTime = currFaultTime;
            state.LastFaultCount = currFaultCount;
            state.LastMixingQty = currMixing;
            state.LastScanNGQty = currScanNG;
            state.LastSysFeedbackQty = currSysFeedback;

            // ---------------------------------------------------------
            // 6. 构造入库记录
            // ---------------------------------------------------------
            var record = new FlipperHourlyRecord
            {
                DeviceName = deviceName,
                CreateTime = DateTime.Now,

                // 业务信息
                ProjectNumber = projectNo,
                ProductType = productType,
                BatchNo = batchNo,
                AnodeType = anodeType,
                MaterialCategory = materialCat,

                // 产能 (增量)
                HourlyCapacity = deltaCap,
                RawTotalCapacity = currTotalCap, // 留底

                // 统计数据 (全部使用计算出的 Delta 值)
                HourlyStandbyTimeMin = deltaStandby,
                HourlyFaultTimeMin = deltaFaultTime,
                HourlyFaultCount = deltaFaultCount,
                HourlyMixingQty = deltaMixing,
                HourlyScanNGQty = deltaScanNG,
                HourlySysFeedbackQty = deltaSysFeedback
            };

            // 7. 入库
            await _repo.InsertAsync(record);
        }
        private int CalculateDelta(int current, int last)
        {
            int delta = current - last;
            if (delta < 0) return current; // 处理清零
            return delta;
        }

        private int GetInt(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var val) && val != null)
                try { return Convert.ToInt32(val); } catch { return 0; }
            return 0;
        }

        private string GetString(Dictionary<string, object> d, string key)
        {
            return d.TryGetValue(key, out var val) ? val?.ToString() ?? "-" : "-";
        }

    }

    [SugarTable("Flipper_Hourly_Record")]
    public class FlipperHourlyRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        // 设备名 (如 "1_PLC_Flipper")
        public string? DeviceName { get; set; }

        // --- 业务字段 ---
        public string? ProjectNumber { get; set; } = "-";     // Hourly_ProjectNo
        public string? ProductType { get; set; } = "-";       // Hourly_ProductType
        public string? BatchNo { get; set; } = "-";           // Hourly_BatchNo
        public string? AnodeType { get; set; } = "-";         // Hourly_AnodeType
        public string? MaterialCategory { get; set; } = "-";  // Hourly_MaterialCategory

        // --- 核心统计数据 ---

        // 小时产能 (计算得出的增量)
        public int HourlyCapacity { get; set; } = 0;

        // 原始累计产能 (留底)
        public int RawTotalCapacity { get; set; } = 0;

        // --- 其他统计字段 (PLC直接读值) ---
        public int HourlyStandbyTimeMin { get; set; } = 0;
        public int HourlyFaultTimeMin { get; set; } = 0;
        public int HourlyFaultCount { get; set; } = 0;

        // 翻转台特有字段
        public int HourlyMixingQty { get; set; } = 0;        // 混料数量
        public int HourlyScanNGQty { get; set; } = 0;        // 扫码NG数量
        public int HourlySysFeedbackQty { get; set; } = 0;   // 系统反馈数量

        // 记录时间
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }

    public class ColumnChartDto
    {
        public bool IsUp { get; set; }          // true=上翻转, false=下翻转
        public double[] Values { get; set; }    // Y轴数值 (产能)
        public DateTime StartTime { get; set; } // 起始时间 (用于生成 X轴)
        public DateTime EndTime { get; set; }   // 结束时间
        public Unit TimeUnit { get; set; }      // 时间粒度 (时/日/月)
    }

    public enum Unit { 年, 月, 日, 时 }
}
