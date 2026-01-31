using MyModbus; // 引用包含 ModbusKeyHelper 的命名空间
using System;
using System.Collections.Generic;
using System.Linq;

namespace BasicRegionNavigation.Services
{
    public interface IModbusService
    {
        event Action<string, ModuleDataCategory, object> OnModuleDataChanged;

        void SubscribeDynamicGroup(string moduleId, ModuleDataCategory category, Dictionary<string, string> fieldMapping);

        // 产能订阅也可以保留，用于简单的连续地址读取
        void SubscribeCapacity(string moduleId, ModuleDataCategory category, string tagInfix, int startIndex, int count);
    }

    public class ModbusService : IModbusService
    {
        private readonly DataCollectionEngine _engine;
        private readonly DataBus _bus;
        private readonly List<Device> _devices;

        public event Action<bool> OnError;
        public event Action<string, ModuleDataCategory, object> OnModuleDataChanged;

        public ModbusService(DataCollectionEngine engine, DataBus bus, List<Device> devices)
        {
            _engine = engine;
            _bus = bus;
            _devices = devices;

            _bus.OnDataChanged += data =>
            {
                if (!data.IsQualityGood) OnError?.Invoke(true);
            };
        }

        /// <summary>
        /// 动态组订阅（支持字段映射）
        /// </summary>
        /// <param name="moduleId">模组编号 (如 "1")</param>
        /// <param name="category">数据分类 (Status/Capacity)</param>
        /// <param name="fieldMapping">字段映射字典：Key=UI属性名, Value=CSV中的点位后缀</param>
        public void SubscribeDynamicGroup(string moduleId, ModuleDataCategory category, Dictionary<string, string> fieldMapping)
        {
            var uiFields = fieldMapping.Keys.ToArray();
            var tagSuffixes = fieldMapping.Values.ToArray();

            // 1. 生成完整的点位名
            // 直接使用 CSV 中的名称后缀，不再强制加 IO/Data 前缀
            var fullTags = tagSuffixes.Select(suffix =>
            {
                // ModbusKeyHelper.Build 会自动处理: moduleId + "_" + suffix
                // 结果示例: "1_PLC_Peripheral_FeedStation1Status"
                return ModbusKeyHelper.Build(moduleId, null, suffix);
            }).ToArray();

            // 2. 订阅
            // 注意：如果 CSV 中是 Bool 类型，Subscribe<int> 通常会自动转换 (true->1, false->0)
            // 如果遇到类型转换错误，请改用 Subscribe<double> 或 Subscribe<bool>
            _bus.Subscribe<int>(fullTags, (values, isGood) =>
            {
                if (isGood && values != null && values.Length == uiFields.Length)
                {
                    var dataPayload = new Dictionary<string, int>();

                    for (int i = 0; i < uiFields.Length; i++)
                    {
                        // 将读取到的值 (values[i]) 赋值给 UI 对应的字段名 (uiFields[i])
                        dataPayload[uiFields[i]] = values[i];
                    }

                    OnModuleDataChanged?.Invoke(moduleId, category, dataPayload);
                }
            });
        }

        public void SubscribeCapacity(string moduleId, ModuleDataCategory category, string tagInfix, int startIndex, int count)
        {
            var tags = Enumerable.Range(startIndex, count)
                .Select(i =>
                {
                    // [修改点] 业务逻辑: 生成具体名称，如 "Counter01" 或 "Counter_01" (取决于 tagInfix 是否带下划线)
                    // 原逻辑直接拼接，这里保持一致
                    string specificName = $"{tagInfix}{i:D2}";

                    // [修改点] 库逻辑: 调用 ModbusKeyHelper 加上设备前缀
                    // 因为 specificName 已经是具体的变量名了，所以 group 传 null
                    // 结果: "1" + "_" + "Counter_01" -> "1_Counter_01"
                    return ModbusKeyHelper.Build(moduleId, null, specificName);
                })
                .ToArray();

            _bus.Subscribe<int>(tags, (values, isGood) =>
            {
                if (isGood)
                {
                    OnModuleDataChanged?.Invoke(moduleId, category, values);
                }
            });
        }
    }

    public enum ModuleDataCategory
    {
        Status,      // 基础IO/气缸状态 (Brush)
        Capacity,    // 产量数据 (int[])
        UpProductInfo,
        DnProductInfo,
        UpPieInfo,
        DnPieInfo,
        UpColumnSeries,
        DnColumnSeries,
        ChartAxis,
        WarningInfo,
        // 2. 双表头表格 (TwoDataTableWithHeader)
        ProductInfoTop,    // 上表数据
        ProductInfoBottom, // 下表数据
                           // 3. 效能表格 (DataTableWithHeader)
        EfficiencyData,

        // 4. 故障统计图表 (CartesianChart)
        FaultStatsSeries, // 图表数据
        FaultStatsAxis    // 图表X轴标签(可选)
    }
}