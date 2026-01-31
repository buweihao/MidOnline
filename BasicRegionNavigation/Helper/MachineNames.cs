using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Helper
{
    public static class MachineNames
    {
        // 1. 定义模组 ID 生成器
        //public static string GetUpLoadModuleAId(int index) => $"{index:D2}_UpLoadModuleA";
        //public static string GetUpLoadModuleBId(int index) => $"{index:D2}_UpLoadModuleB";
        //public static string GetPierModuleId(int index) => $"{index:D2}_PierModule";
        //public static string GetDropModuleAId(int index) => $"{index:D2}_DropModuleA";
        //public static string GetDropModuleBId(int index) => $"{index:D2}_DropModuleB";
        //public static string GetUpperFlipperId(int index) => $"{index:D2}_UpperFlipper";
        //public static string GetLowerFlipperId(int index) => $"{index:D2}_LowerFlipper";
        public static readonly string[] CapacityModules = new[]
            {
        "UpperFlipper",
        "LowerFlipper"
    };

        /// <summary>
        /// 判断这个 DeviceId 是否属于需要记录产能的模组
        /// </summary>
        public static bool IsCapacityModule(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return false;

            // 检查 ID 是否包含定义的后缀 (例如 "01_UpLoadModuleA" 包含 "UpLoadModuleA")
            return CapacityModules.Any(type => deviceId.EndsWith(type));
        }

        // 定义一个静态映射表，方便后续维护和扩展
        private static readonly Dictionary<string, string> NameMapping = new Dictionary<string, string>
    {
        { "UpLoadModuleA", "上料A" },
        { "UpLoadModuleB", "上料B" },
        { "PierModule",    "周边挂具" },
        { "DropModuleA",   "下料A" },
        { "DropModuleB",   "下料B" },
        { "UpperFlipper",  "上翻转台" },
        { "LowerFlipper",  "下翻转台" }
    };

        /// <summary>
        /// 根据 DeviceId 包含的内容解析出中文名称
        /// </summary>
        /// <param name="deviceId">例如 "01_UpLoadModuleA" 或 "UpperFlipper"</param>
        /// <returns>解析出的中文，如果未匹配则返回原字符串</returns>
        public static string GetChineseName(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId)) return "未知模组";

            // 遍历映射表，检查 deviceId 是否包含特定的后缀/关键字
            // 这种方式可以兼容带有编号前缀的 ID，如 "01_UpLoadModuleA"
            var match = NameMapping.FirstOrDefault(kv => deviceId.Contains(kv.Key));

            // 如果找到了匹配项，返回对应的中文值；否则返回原始 ID
            return !string.IsNullOrEmpty(match.Value) ? match.Value : deviceId;
        }

    }
}
