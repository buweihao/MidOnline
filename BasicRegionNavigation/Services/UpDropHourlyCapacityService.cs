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

            // 1. 提取当前数据
            // 核心：读取累计产能 TotalCapacity
            int currTotalCap = GetInt(data, "TotalCapacity");
            string projectNo = data.ContainsKey("ProjectNo") ? data["ProjectNo"]?.ToString() ?? "-" : "-";

            // 2. 获取或初始化上一次的状态
            var state = _deviceStates.GetOrAdd(deviceName, new DeviceState
            {
                LastTotalCapacity = currTotalCap,
                IsFirstRun = true
            });

            // 3. 计算产能增量 (核心算法)
            int deltaCap = CalculateDelta(currTotalCap, state.LastTotalCapacity);

            // 4. 首次运行特殊处理
            // 如果程序刚启动，我们无法知道过去一小时产了多少，
            // 为了避免数据突变（例如算出几万的产量），通常第一笔记录记为 0，或者跳过不记。
            // 这里选择记为 0，并同步基准。
            if (state.IsFirstRun)
            {
                deltaCap = 0;
                state.IsFirstRun = false;
            }

            // 5. 更新内存状态 (为下个小时做准备)
            state.LastTotalCapacity = currTotalCap;

            // 6. 构造记录
            var record = new UpDropHourlyRecord
            {
                DeviceName = deviceName,
                ProjectNumber = projectNo,

                // 产能是计算出来的增量
                HourlyCapacity = deltaCap,
                // 留底
                RawTotalCapacity = currTotalCap,

                // 其他字段直接取值 (PLC如果这些字段也是累计值，也需要做差值；如果是只存当前小时的快照值，则直接存)
                // 假设您的CSV配置 implying 这些是 "Hourly_..." 即 PLC 已经算好了当前小时的值
                HourlyStandbyTimeMin = GetInt(data, "Hourly_StandbyTimeMin"),
                HourlyFaultTimeMin = GetInt(data, "Hourly_FaultTimeMin"),
                HourlyFaultCount = GetInt(data, "Hourly_FaultCount"),
                HourlySystemNG = GetInt(data, "Hourly_SystemNG"),
                HourlyMaterialLost = GetInt(data, "Hourly_MaterialLost"),

                CreateTime = DateTime.Now
            };

            // 7. 入库
            await _repo.InsertAsync(record);
        }

        // 增量计算逻辑
        private int CalculateDelta(int current, int last)
        {
            int delta = current - last;
            // 如果 delta < 0，说明PLC寄存器被清零/回滚了
            // 此时认为 current 就是本小时新增量
            if (delta < 0) return current;
            return delta;
        }

        // 安全获取整型
        private int GetInt(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var val) && val != null)
            {
                try { return Convert.ToInt32(val); } catch { return 0; }
            }
            return 0;
        }

        // 内部状态类
        private class DeviceState
        {
            public int LastTotalCapacity { get; set; }
            public bool IsFirstRun { get; set; }
        }
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
