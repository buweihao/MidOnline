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
    public interface IUpDropHourlyService
    {
        /// <summary>
        /// 处理小时数据：计算产能增量并入库
        /// </summary>
        Task ProcessHourlyDataAsync(string deviceName, Dictionary<string, object> data);
    }

    public class UpDropHourlyService : IUpDropHourlyService
    {
        private readonly IRepository<UpDropHourlyRecord> _repo;

        // --- 内存状态缓存 (用于计算增量) ---
        // Key: DeviceName (如 "1_PLC_Feeder_A"), Value: 上一次的 DeviceState
        private static readonly ConcurrentDictionary<string, DeviceState> _deviceStates
            = new ConcurrentDictionary<string, DeviceState>();

        public UpDropHourlyService(IRepository<UpDropHourlyRecord> repo)
        {
            _repo = repo;
        }

        public async Task ProcessHourlyDataAsync(string deviceName, Dictionary<string, object> data)
        {
            if (data == null || data.Count == 0) return;

            // ---------------------------------------------------------
            // 1. 提取当前数据 (全部视为累计值)
            // ---------------------------------------------------------
            string projectNo = data.ContainsKey("ProjectNo") ? data["ProjectNo"]?.ToString() ?? "-" : "-";

            // 读取当前的“电表读数”
            int currTotalCap = GetInt(data, "TotalCapacity");
            int currStandby = GetInt(data, "Hourly_StandbyTimeMin"); // 虽叫Hourly，实际是Total
            int currFaultTime = GetInt(data, "Hourly_FaultTimeMin");
            int currFaultCount = GetInt(data, "Hourly_FaultCount");
            int currSystemNG = GetInt(data, "Hourly_SystemNG");
            int currMatLost = GetInt(data, "Hourly_MaterialLost");

            // ---------------------------------------------------------
            // 2. 获取或初始化状态 (从内存中拿出上一次的读数)
            // ---------------------------------------------------------
            var state = _deviceStates.GetOrAdd(deviceName, new DeviceState
            {
                // 如果是新设备，初始化时先把当前值赋进去，避免第一次计算出错
                LastTotalCapacity = currTotalCap,
                LastStandbyTime = currStandby,
                LastFaultTime = currFaultTime,
                LastFaultCount = currFaultCount,
                LastSystemNG = currSystemNG,
                LastMaterialLost = currMatLost,
                IsFirstRun = true
            });

            // ---------------------------------------------------------
            // 3. 计算增量 (核心逻辑: 当前累计值 - 上次累计值)
            // ---------------------------------------------------------
            // 假设 CalculateDelta 内部处理了负数情况(如PLC清零重置)
            int deltaCap = CalculateDelta(currTotalCap, state.LastTotalCapacity);
            int deltaStandby = CalculateDelta(currStandby, state.LastStandbyTime);
            int deltaFaultTime = CalculateDelta(currFaultTime, state.LastFaultTime);
            int deltaFaultCount = CalculateDelta(currFaultCount, state.LastFaultCount);
            int deltaSystemNG = CalculateDelta(currSystemNG, state.LastSystemNG);
            int deltaMatLost = CalculateDelta(currMatLost, state.LastMaterialLost);

            // ---------------------------------------------------------
            // 4. 首次运行特殊处理 (截断逻辑)
            // ---------------------------------------------------------
            // 如果程序刚启动，或者设备第一次上线，我们不知道过去一小时发生了什么。
            // 为了防止把设备运行了3年的累计值全部算在当前这一小时里，强制归零。
            if (state.IsFirstRun)
            {
                deltaCap = 0;
                deltaStandby = 0;
                deltaFaultTime = 0;
                deltaFaultCount = 0;
                deltaSystemNG = 0;
                deltaMatLost = 0;

                state.IsFirstRun = false; // 标记已运行过
            }

            // ---------------------------------------------------------
            // 5. 更新内存状态 (保存当前值，供下个小时做减数)
            // ---------------------------------------------------------
            state.LastTotalCapacity = currTotalCap;
            state.LastStandbyTime = currStandby;
            state.LastFaultTime = currFaultTime;
            state.LastFaultCount = currFaultCount;
            state.LastSystemNG = currSystemNG;
            state.LastMaterialLost = currMatLost;

            // ---------------------------------------------------------
            // 6. 构造记录 (存入数据库的是增量)
            // ---------------------------------------------------------
            var record = new UpDropHourlyRecord
            {
                DeviceName = deviceName,
                ProjectNumber = projectNo,
                CreateTime = DateTime.Now,

                // 存入增量值
                HourlyCapacity = deltaCap,
                HourlyStandbyTimeMin = deltaStandby,
                HourlyFaultTimeMin = deltaFaultTime,
                HourlyFaultCount = deltaFaultCount,
                HourlySystemNG = deltaSystemNG,
                HourlyMaterialLost = deltaMatLost,

                // 建议：保留一个原始读数 TotalCapacity 方便日后核对数据的连续性
                RawTotalCapacity = currTotalCap
            };

            // 7. 入库
            await _repo.InsertAsync(record);
        }

        // 附带：增量计算辅助方法 (防止你忘了处理PLC归零的情况)
        private int CalculateDelta(int current, int last)
        {
            // 正常情况：当前 100 - 上次 80 = 20
            if (current >= last)
            {
                return current - last;
            }
            else
            {
                // 异常情况：PLC复位了，当前 5 - 上次 10000
                // 策略1：认为这5个都是新增的
                return current;
                // 策略2：如果有最大值(如65535)，可以做回环计算 (current + Max - last)
            }
        }        // 安全获取整型
        private int GetInt(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var val) && val != null)
            {
                try { return Convert.ToInt32(val); } catch { return 0; }
            }
            return 0;
        }

        // 内部状态类
        public class DeviceState
        {
            public bool IsFirstRun { get; set; } = true;

            // --- 通用字段 ---
            public int LastTotalCapacity { get; set; }
            public int LastStandbyTime { get; set; }
            public int LastFaultTime { get; set; }
            public int LastFaultCount { get; set; }

            // --- 供料机 (Feeder) 特有 ---
            public int LastSystemNG { get; set; }
            public int LastMaterialLost { get; set; }

            // --- [新增] 翻转台 (Flipper) 特有 ---
            public int LastMixingQty { get; set; }      // 混料数量
            public int LastScanNGQty { get; set; }      // 扫码NG数量
            public int LastSysFeedbackQty { get; set; } // 系统回传数量
        }

        [SugarTable("UpDrop_Hourly_Record")]
        public class UpDropHourlyRecord
        {
            [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            // 设备名 (带模组前缀，如 "1_PLC_Feeder_A")
            public string? DeviceName { get; set; }

            // 项目号
            public string? ProjectNumber { get; set; } = "-";

            // --- 核心数据 ---

            // 小时产能 (计算得出的增量：本小时新增了多少)
            public int HourlyCapacity { get; set; } = 0;

            // 原始累计产能 (PLC上的 TotalCapacity读数，留底备查)
            public int RawTotalCapacity { get; set; } = 0;

            // --- 其他统计字段 (直接读取PLC) ---

            public int HourlyStandbyTimeMin { get; set; } = 0;
            public int HourlyFaultTimeMin { get; set; } = 0;
            public int HourlyFaultCount { get; set; } = 0;
            public int HourlySystemNG { get; set; } = 0;
            public int HourlyMaterialLost { get; set; } = 0;

            // 记录时间
            public DateTime CreateTime { get; set; } = DateTime.Now;
        }


    }
}
