using MyDatabase;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

            // 1. 提取 TotalCapacity 用于计算增量
            int currTotalCap = GetInt(data, "TotalCapacity");

            // 2. 获取状态并计算增量
            var state = _deviceStates.GetOrAdd(deviceName, new DeviceState
            {
                LastTotalCapacity = currTotalCap,
                IsFirstRun = true
            });

            int deltaCap = CalculateDelta(currTotalCap, state.LastTotalCapacity);

            if (state.IsFirstRun)
            {
                deltaCap = 0;
                state.IsFirstRun = false;
            }
            state.LastTotalCapacity = currTotalCap;

            // 3. 构造记录
            var record = new FlipperHourlyRecord
            {
                DeviceName = deviceName,

                // 字符串字段
                ProjectNumber = GetString(data, "Hourly_ProjectNo"),
                ProductType = GetString(data, "Hourly_ProductType"),
                BatchNo = GetString(data, "Hourly_BatchNo"),
                AnodeType = GetString(data, "Hourly_AnodeType"),
                MaterialCategory = GetString(data, "Hourly_MaterialCategory"),

                // 产能数据
                HourlyCapacity = deltaCap,
                RawTotalCapacity = currTotalCap,

                // 统计数据
                HourlyStandbyTimeMin = GetInt(data, "Hourly_StandbyTimeMin"),
                HourlyFaultTimeMin = GetInt(data, "Hourly_FaultTimeMin"),
                HourlyFaultCount = GetInt(data, "Hourly_FaultCount"),
                HourlyMixingQty = GetInt(data, "Hourly_MixingQty"),
                HourlyScanNGQty = GetInt(data, "Hourly_ScanNGQty"),
                HourlySysFeedbackQty = GetInt(data, "Hourly_SysFeedbackQty"),

                CreateTime = DateTime.Now
            };

            // 4. 入库
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

        private class DeviceState
        {
            public int LastTotalCapacity { get; set; }
            public bool IsFirstRun { get; set; }
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
