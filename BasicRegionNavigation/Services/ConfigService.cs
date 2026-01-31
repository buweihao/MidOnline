using MyConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Services
{
    public interface IConfigService
    {
        string GetConfigValue(string? key);

    }

    public class ConfigService : IConfigService
    {
        public ConfigService(params string[] configPaths)
        {
            MyConfigInit(configPaths);
        }

        public string GetConfigValue(string? key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty; // 安全检查建议加上

            return MyConfigContext.Service.GetValue(key);
        }

        private void MyConfigInit(string[] path)
        {
            // 使用传入的 path，而不是写死
            MyConfigContext.Initialize(path);
            //private void MyConfigInit()
            //{
            //    MyConfigContext.Initialize("Configs/custom_config.json"); // 替换为您想要的文件路径

            Console.WriteLine(MyConfigContext.Service.GetValue("Modules"));

            //// 1. 获取管理器实例
            //var manager = MyConfigContext.DefaultManager;
            //// 注册分类 1：加载 My 开头的所有项
            //manager.RegisterCategory("系统参数", key => key.StartsWith("1_") || key.StartsWith("_1"));

            //// 注册分类 2：加载 Database 开头 或者 Connection 结尾的项 (复杂的业务逻辑)
            //manager.RegisterCategory("数据库配置", key =>
            //    key.StartsWith("Database") || key.EndsWith("Connection"));

            //// 注册分类 3：只精确加载某几个特定的键
            //manager.RegisterCategory("关键开关", key =>
            //    key == "EnableLog" || key == "IsDebugMode");
        }

    }

}
