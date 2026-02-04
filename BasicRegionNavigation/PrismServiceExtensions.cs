using BasicRegionNavigation.Services;
using Microsoft.Extensions.DependencyInjection;
using MyDatabase;
using MyLog;
using MyModbus;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BasicRegionNavigation.Services.UpDropHourlyService;

namespace BasicRegionNavigation.Helper
{
    public static class PrismServiceExtensions
    {
        /// <summary>
        /// 集中注册所有业务服务
        /// </summary>
        public static void AddBusinessServices(this IServiceCollection services)
        {
            // 1. 注册 Modbus 核心 (包含克隆逻辑)
            RegisterModbus(services);

            // 2. 注册 SqlSugar 数据库
            RegisterDatabase(services);

            // 3. 注册其他服务
            services.AddSingleton<IConfigService>(new ConfigService(new string[] { "Configs/config.json", "Configs/product_setting.json" }));

            // 4. 【关键】注册后台任务 (BackgroundService)

            // 5. 配置 Log
            // 将 IMyLogConfig 映射到已经注册的 IProductionService 实例
            // 这样 MyLog 就会使用 IProductionService 中定义的配置
            services.AddSingleton<IMyLogConfig>(sp => sp.GetRequiredService<IProductionService>());

            // 注册 MyLog 服务
            services.AddMyLogService();


            services.AddSingleton<IModbusService, ModbusService>();
            services.AddSingleton<IFlipperHourlyService, FlipperHourlyService>();
            services.AddSingleton<IUpDropHourlyService, UpDropHourlyService>();
            services.AddSingleton<IProductionService, ProductionService>();
            services.AddSingleton<IMiddleFrameBusinessServices, MiddleFrameBusinessServices>();
            services.AddSingleton<IQueryService, QueryService>();


        }

        private static void RegisterModbus(IServiceCollection services)
        {
            string modbusConfigPath = "Configs/config.csv";

            // 调用 AddMyModbusCore，在回调中处理克隆与参数设置
            services.AddMyModbusCore(modbusConfigPath, devices =>
            {
                //    var cloneList = new[]
                //    {
                //(Template: SystemConfig.Dev_Peripheral,     ModuleId: "1", Ip: "10.120.93.82"),

                //(Template: SystemConfig.Dev_Robot,          ModuleId: "1", Ip: "10.120.93.82"),

                //(Template: SystemConfig.Dev_DownFeeder_A,   ModuleId: "1", Ip: "10.120.93.80"),

                //(Template: SystemConfig.Dev_DownFeeder_B,   ModuleId: "1", Ip: "10.120.93.81"),

                //(Template: SystemConfig.Dev_Flipper,        ModuleId: "1", Ip: "10.120.93.82"),


                //(Template: SystemConfig.Dev_Peripheral, ModuleId: "2", Ip: "10.120.93.89"),

                //(Template: SystemConfig.Dev_Robot,      ModuleId: "2", Ip: "10.120.93.89"),

                //(Template: SystemConfig.Dev_DownFeeder_A,   ModuleId: "2", Ip: "10.120.93.87"),

                //(Template: SystemConfig.Dev_DownFeeder_B,   ModuleId: "2", Ip: "10.120.93.88"),

                //(Template: SystemConfig.Dev_Flipper,    ModuleId: "2", Ip: "10.120.93.89"),
                //};

                var cloneList = new[]
                {
                    (Template: SystemConfig.Dev_Peripheral, ModuleId: "1", Ip: "127.0.0.1"),

                    (Template: SystemConfig.Dev_Robot,      ModuleId: "1", Ip: "127.0.0.1"),

                    (Template: SystemConfig.Dev_DownFeeder_A,   ModuleId: "1", Ip: "127.0.0.2"),

                    (Template:  SystemConfig.Dev_DownFeeder_B,   ModuleId: "1", Ip: "127.0.0.3"),

                    (Template: SystemConfig.Dev_Flipper,    ModuleId: "1", Ip: "127.0.0.1"),

                    (Template: SystemConfig.Dev_Peripheral, ModuleId: "2", Ip: "127.0.0.4"),

                    (Template: SystemConfig.Dev_Robot,      ModuleId: "2", Ip: "127.0.0.4"),

                    (Template: SystemConfig.Dev_DownFeeder_A,   ModuleId: "2", Ip: "127.0.0.5"),

                    (Template:  SystemConfig.Dev_DownFeeder_B,   ModuleId: "2", Ip: "127.0.0.6"),

                    (Template: SystemConfig.Dev_Flipper,    ModuleId: "2", Ip: "127.0.0.4"),

                };

                var templatesToRemove = new HashSet<Device>();

                // 2. 执行设备克隆逻辑
                foreach (var item in cloneList)
                {
                    var template = devices.FirstOrDefault(d => d.DeviceId == item.Template);
                    if (template != null)
                    {
                        templatesToRemove.Add(template);

                        // 使用 CloneToModule 生成带前缀的设备，例如 "1_PLC_Peripheral"
                        var newDevice = template.CloneToModule(item.ModuleId, item.Ip);
                        devices.Add(newDevice);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"警告：找不到模板设备 {item.Template}");
                    }
                }

                // 3. 移除原始模板设备，避免占用资源
                foreach (var t in templatesToRemove)
                {
                    devices.Remove(t);
                }

                // 4. 【核心需求】统一设置所有新设备的字节序和字符串反转
                foreach (var device in devices)
                {
                    // 设置为 CDAB 模式 (双字反转)
                    device.ByteOrder = MyModbus.DataFormat.CDAB;

                    if (false)
                    {
                        device.IsStringReverse = false;
                    }
                    else
                    {
                        // 开启字符串字内字节反转 (例如 "BA" -> "AB")
                        device.IsStringReverse = true;
                    }
                }
            });
        }

        private static void RegisterDatabase(IServiceCollection services)
        {
            var dbConfig = new ConnectionConfig
            {
                ConnectionString = "DataSource=IndustrialData.db",
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
                MoreSettings = new ConnMoreSettings { IsAutoRemoveDataCache = true }
            };

            // 注册 Store 和相关 Service
            services.AddMySqlSugarStore(dbConfig
                , typeof(FlipperHourlyRecord)
                , typeof(ProductionRecord)
                , typeof(UpDropHourlyRecord)
                , typeof(DeviceLog)
            );

            //上下料机小时产能入库
            //services.AddTransient<IUpDropHourlyCapacityService, UpDropHourlyCapacityService>();
        }
    }
}
