using System.Linq; // 必须引用 Linq

namespace BasicRegionNavigation.Services
{
    public static class SystemConfig
    {
        // ==========================================
        // 1. 基础常量定义
        // ==========================================
        public const int ModuleCount = 2;
        public static readonly string[] Modules = new[] { "1", "2" };
        public const string TriggerSuffix = "ReadTrigger"; // 统一后缀

        // ==========================================
        // 2. 设备名称常量池 (字典/常量库)
        // ==========================================
        public const string Dev_UpFeeder_A = "PLC_Feeder_A";
        public const string Dev_UpFeeder_B = "PLC_Feeder_B";
        public const string Dev_DownFeeder_A = "PLC_UnFeeder_A";
        public const string Dev_DownFeeder_B = "PLC_UnFeeder_B";
        public const string Dev_Flipper = "PLC_Flipper";
        public const string Dev_Peripheral = "PLC_Peripheral";
        public const string Dev_Robot = "PLC_Robot";

        // ==========================================
        // 3. 业务启用列表 (在这里控制项目模式)
        // ==========================================

        // 【场景 A：上料 + 翻转】
        //public static readonly string[] ActiveUpLoaders = new[] { Dev_UpFeeder_A, Dev_UpFeeder_B };
        //public static readonly string[] ActiveDownLoaders = new string[] { };
        //public static readonly string[] FlipperDevice = new[] { Dev_Flipper };

        // 【场景 B：纯下料 + 翻转】(切换时只需解注这里)
        public static readonly string[] ActiveUpLoaders = new string[] { };
        public static readonly string[] ActiveDownLoaders = new[] { Dev_DownFeeder_A, Dev_DownFeeder_B };
        public static readonly string[] FlipperDevice = new[] { Dev_Flipper };

        // ==========================================
        // 4. 自动计算的集合 (不要手动写，自动生成)
        // ==========================================

        /// <summary>
        /// 所有启用的供料机 (上料 + 下料)
        /// 用于：小时产能采集、转产信号监听
        /// </summary>
        public static readonly string[] AllActiveFeeders = ActiveUpLoaders
                                                           .Concat(ActiveDownLoaders)
                                                           .ToArray();

        /// <summary>
        /// 所有需要时间同步的设备 (上料 + 下料 + 翻转)
        /// 用于：时间同步任务
        /// </summary>
        public static readonly string[] AllTimeSyncDevices = ActiveUpLoaders
                                                             .Concat(ActiveDownLoaders)
                                                             .Concat(FlipperDevice)
                                                             .ToArray();
    }
}