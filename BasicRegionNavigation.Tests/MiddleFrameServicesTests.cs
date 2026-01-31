using Xunit;
using Moq;
using BasicRegionNavigation.Services;
using My.Services;
using BasicRegionNavigation.Helper; // 引用 TagData
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using MyModbus;

namespace BasicRegionNavigation.Tests
{
    // =============================================================
    // 1. 创建 DataBus 的“替身”类
    //    原因：真正的 DataBus 会连接硬件，测试时我们需要一个能手动控制的假对象
    // =============================================================
    public class TestableDataBus : DataBus
    {
        // 用字典来模拟 PLC 的内存：Key=点位名, Value=当前值
        public Dictionary<string, object> MemoryMap { get; } = new Dictionary<string, object>();

        // 用字典来记录被测代码订阅了哪些点位：Key=点位名, Value=回调函数
        public Dictionary<string, Action<TagData>> RegisteredSubscriptions { get; } = new Dictionary<string, Action<TagData>>();

        // 覆盖基类的 Subscribe 方法 (使用 new 关键字隐藏原方法)
        public override void Subscribe(string tagName, Action<TagData> handler)
        {
            // 当业务代码调用 Subscribe 时，我们不连硬件，而是把它的请求记录下来
            RegisteredSubscriptions[tagName] = handler;
        }

        // 覆盖基类的 GetValue 方法
        public override object GetValue(string tagName)
        {
            // 当业务代码想读数据时，我们从“模拟内存”里查给它
            if (MemoryMap.ContainsKey(tagName))
            {
                return MemoryMap[tagName];
            }
            return null; // 模拟读不到数据
        }

        // 辅助工具：模拟 PLC 触发信号
        public void SimulateTrigger(string tagName, short value)
        {
            if (RegisteredSubscriptions.ContainsKey(tagName))
            {
                var tagData = new TagData
                {
                    TagName = tagName,
                    Value = value,
                    IsQualityGood = true
                };
                // 执行业务代码注册的回调函数
                RegisteredSubscriptions[tagName].Invoke(tagData);
            }
        }
    }

    // =============================================================
    // 2. 编写测试类
    // =============================================================
    public class MiddleFrameServicesTests
    {
        // 定义测试需要的对象
        private TestableDataBus _fakeBus;
        private Mock<IProductionService> _mockProductionService;
        private Mock<IFlipperHourlyCapacityService> _mockFlipperHourlyService;
        private Mock<IUpDropHourlyCapacityService> _mockUpDropHourlyService;
        private MiddleFrameBusinessServices _service;

        // 构造函数：相当于初始化的 Setup
        public MiddleFrameServicesTests()
        {
            // A. 初始化模拟的接口 (Mock)
            _mockProductionService = new Mock<IProductionService>();
            _mockFlipperHourlyService = new Mock<IFlipperHourlyCapacityService>();
            _mockUpDropHourlyService = new Mock<IUpDropHourlyCapacityService>();

            // B. 初始化模拟的总线 (Stub)
            _fakeBus = new TestableDataBus();

            // C. 创建我们要测试的服务，把上面假的依赖注入进去
            _service = new MiddleFrameBusinessServices(
                _fakeBus,
                _mockProductionService.Object,
                _mockFlipperHourlyService.Object,
                _mockUpDropHourlyService.Object
            );
        }

        // 测试场景 1: 上料机 (Feeder) 触发逻辑
        [Fact]
        public void Test_FeederSubscription_And_DataCollection()
        {
            // --- 1. 准备 (Arrange) ---
            string triggerTag = "1_PLC_Feeder_A_ReadTrigger";    // 预期的触发点位名
            string productCodeTag = "1_PLC_Feeder_A_ProductCode"; // 预期的读取点位名
            string expectedSN = "SN123456";

            // 在模拟内存中预先放入产品码（模拟 PLC 里已经有这个数据了）
            _fakeBus.MemoryMap[productCodeTag] = expectedSN;

            // --- 2. 执行 (Act) ---
            // 启动业务，这会触发 InitializeSubscriptions
            _service.ProductCollectionMissionStart();

            // --- 3. 验证 (Assert) ---

            // 验证 A: 检查是否订阅了正确的触发点位名
            // 如果这里失败，说明 ModbusKeyHelper 解析出的点位名不对
            Assert.True(_fakeBus.RegisteredSubscriptions.ContainsKey(triggerTag),
                $"失败：代码没有订阅预期点位 '{triggerTag}'");

            // 模拟动作: 触发信号来了 (值为 11)
            _fakeBus.SimulateTrigger(triggerTag, 11);

            // 验证 B: 检查 IProductionService 是否被正确调用
            // 这一步能验证：触发 -> 代理类计算点位名 -> 读取DataBus -> 获取SN -> 调用服务 的全过程
            _mockProductionService.Verify(s => s.ProcessProductDataAsync(
                It.Is<StationProcessContext>(ctx =>
                    ctx.IdentityValue == expectedSN &&                    // SN 是否读取正确
                    ctx.DeviceId == "PLC_Feeder_A" &&                     // 设备名是否解析正确
                    ctx.ProcessType == StationProcessType.Entry_Upload    // 工序类型是否正确
                )),
                Times.Once, "失败：未收到预期的上料数据处理请求");
        }

        // 测试场景 2: 翻转台 (Flipper) 复杂数据逻辑
        [Fact]
        public void Test_FlipperSubscription_And_ComplexDataParsing()
        {
            // --- 1. 准备 ---
            string triggerTag = "1_PLC_Flipper_A_ReadTrigger";

            // 预埋翻转台需要的所有数据
            _fakeBus.MemoryMap["1_PLC_Flipper_A_ProductCode"] = "SN_A_01"; // 产品码
            _fakeBus.MemoryMap["1_PLC_Flipper_A_FixtureCode"] = "FIX_001"; // 挂具号
            _fakeBus.MemoryMap["1_PLC_Flipper_A_ProjectNo"] = "PROJ_2024"; // 项目号
            _fakeBus.MemoryMap["1_PLC_Flipper_A_ProductType"] = "Type_Metal"; // 类别

            // --- 2. 执行 ---
            _service.ProductCollectionMissionStart();

            // --- 3. 验证 ---

            // 验证订阅
            Assert.True(_fakeBus.RegisteredSubscriptions.ContainsKey(triggerTag),
                $"失败：代码没有订阅翻转台点位 '{triggerTag}'");

            // 模拟触发
            _fakeBus.SimulateTrigger(triggerTag, 11);

            // 验证数据是否完整传给了 Service
            _mockProductionService.Verify(s => s.ProcessProductDataAsync(
                It.Is<StationProcessContext>(ctx =>
                    ctx.IdentityValue == "SN_A_01" &&
                    ctx.DeviceId == "PLC_Flipper_A" &&
                    // 检查字典里的扩展数据是否读到了
                    ctx.PlcData.ContainsKey("FixtureCode") &&
                    ctx.PlcData["FixtureCode"].ToString() == "FIX_001" &&
                    ctx.PlcData["Side"].ToString() == "A"
                )), Times.Once, "失败：翻转台数据处理参数不正确");
        }
    }
}