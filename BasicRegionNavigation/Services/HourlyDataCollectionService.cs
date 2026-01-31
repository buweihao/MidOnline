using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyModbus;
using System;
using System.Collections.Generic;
using System.Text;

namespace My.Services
{
    public class HourlyDataCollectionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DataBus _bus;            // 👈 1. 注入 DataBus
        private readonly List<Device> _devices;   // 👈 2. 注入设备列表(为了知道要存哪些模组)

        public HourlyDataCollectionService(
            IServiceProvider serviceProvider,
            DataBus bus,
            List<Device> devices)
        {
            _serviceProvider = serviceProvider;
            _bus = bus;
            _devices = devices;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ... (保持之前的定时逻辑不变，计算下一小时的最后一秒) ...
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var nextHour = now.Date.AddHours(now.Hour + 1);
                var targetTime = nextHour.AddSeconds(-1);

                if (targetTime < now) targetTime = targetTime.AddHours(1);
                var delay = targetTime - now;
                if (delay.TotalMilliseconds <= 0) delay = TimeSpan.FromSeconds(1);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException) { break; }

                // 执行存档
                await DoHourlySaveAsync();

                // 避开当前秒
                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task DoHourlySaveAsync()
        {
            Console.WriteLine($"[{DateTime.Now}] 开始执行整点数据存档...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbService = scope.ServiceProvider.GetRequiredService<IUpDropHourlyService>();

                // 1. 筛选产能模组
                var targetDevices = _devices
                    .Where(d => MachineNames.IsCapacityModule(d.DeviceId))
                    .ToList();

                // 2. 获取当前时间的小时索引 (0-23)
                // 假设当前是 00:59:59，Hour=0，对应 H01
                // 假设当前是 09:59:59，Hour=9，对应 H10
                int currentHour = DateTime.Now.Hour;
                string capacitySuffix = $"HourlyCapacity_H{(currentHour + 1):D2}";

                foreach (var device in targetDevices)
                {
                    // 3. 先读取两个辅助计算值 (Helper1, Helper2)
                    // 注意：ReadInt 返回的是 object (-1 或 int)，需要强转
                    int helper1 = Convert.ToInt32(ReadInt(_bus, device.DeviceId, "HourlyCapacityHelper1"));
                    int helper2 = Convert.ToInt32(ReadInt(_bus, device.DeviceId, "HourlyCapacityHelper2"));

                    // 如果读取失败返回 -1，为了不破坏计算，这里可视情况归 0，或者保留 -1 逻辑
                    // 这里假设：如果 helper 读取失败(-1)，则视作 0 处理，避免计算结果偏差太大
                    if (helper1 == -1) helper1 = 0;
                    if (helper2 == -1) helper2 = 0;

                    // 4. 定义一个局部函数来处理通用计算公式: (Value - Helper1 + Helper2)
                    // T: 返回类型 (int 或 short)
                    object Calc<T>(string tagSuffix)
                    {
                        object rawObj = ReadInt(_bus, device.DeviceId, tagSuffix); // 这里 ReadInt 通用于 short/int 读取
                        int rawVal = Convert.ToInt32(rawObj);

                        if (rawVal == -1) return -1; // 如果原始点位都没读到，直接返回 -1

                        int result = rawVal - helper1 + helper2;

                        // 确保结果不小于 0 (可选，视业务需求而定)
                        // if (result < 0) result = 0;

                        return Convert.ChangeType(result, typeof(T));
                    }

                    // 5. 构建数据字典
                    var dataToSave = new Dictionary<string, object>
            {
                // --- 动态产能点位 ---
                // Key 对应数据库实体属性名, Value 读取动态后缀 (如 HourlyCapacity_H01)
                { "HourlyCapacity",        ReadInt(_bus, device.DeviceId, capacitySuffix) },

                // --- 需要计算的数值字段 (Value - H1 + H2) ---
                { "HourlyStandbyTime",     Calc<int>("HourlyStandbyTime") },
                { "HourlyFaultTime",       Calc<int>("HourlyFaultTime") },
                
                // Int16 (short) 类型的计数
                { "HourlyFaultCount",      Calc<short>("HourlyFaultCount") },
                { "HourlyMixCount",        Calc<short>("HourlyMixCount") },
                { "HourlyScanNGCount",     Calc<short>("HourlyScanNGCount") },
                { "HourlySystemNGCount",   Calc<short>("HourlySystemFeedbackCount") }, // 映射到 SystemFeedbackCount

                // --- 字符串信息字段 (直接读取) ---
                { "HourlyProductTypeFlag", ReadString(_bus, device.DeviceId, "HourlyProductTypeFlag") },
                { "HourlyProjectNumber",   ReadString(_bus, device.DeviceId, "HourlyProjectNumber") },
                { "HourlyBatch",           ReadString(_bus, device.DeviceId, "HourlyBatch") },
                { "HourlyAnodeType",       ReadString(_bus, device.DeviceId, "HourlyAnodeType") },
                { "HourlyMaterialCategory",ReadString(_bus, device.DeviceId, "HourlyMaterialCategory") }
            };

                    // 6. 存入数据库
                    await dbService.ProcessHourlyDataAsync(device.DeviceId, dataToSave);
                }
            }
        }

        // =============================================================
        //  辅助读取方法：封装了你提供的 IsQualityGood 检查逻辑
        // =============================================================

        private object ReadInt(DataBus bus, string deviceId, string tagSuffix)
        {
            // 拼装点位名：例如 Module_01_HourlyCapacity
            // 你的点位命名规则如果是 "Module_01_Up_HourlyCapacity"，请在这里调整
            string tagName = $"{deviceId}_{tagSuffix}";

            // 使用你提供的逻辑获取数据
            TagData? tagData = bus.GetTagData(tagName);

            if (tagData.HasValue && tagData.Value.IsQualityGood)
            {
                return Convert.ToInt32(tagData.Value.Value);
            }

            return -1; // 默认值：无效或未采集到
        }

        private object ReadShort(DataBus bus, string deviceId, string tagSuffix)
        {
            string tagName = $"{deviceId}_{tagSuffix}";
            TagData? tagData = bus.GetTagData(tagName);

            if (tagData.HasValue && tagData.Value.IsQualityGood)
            {
                return Convert.ToInt16(tagData.Value.Value);
            }
            return -1;
        }

        private string ReadString(DataBus bus, string deviceId, string tagSuffix)
        {
            string tagName = $"{deviceId}_{tagSuffix}";
            TagData? tagData = bus.GetTagData(tagName);

            if (tagData.HasValue && tagData.Value.IsQualityGood)
            {
                return tagData.Value.Value?.ToString() ?? "-";
            }
            return "-"; // 默认值
        }
    }
}
