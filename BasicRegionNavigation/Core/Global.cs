using BasicRegionNavigation.ViewModels; // 注意引用这个命名空间
using CommunityToolkit.Mvvm.ComponentModel;
using Dm.util;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MyConfig;
using MyDatabase;
using SkiaSharp;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace Core
{
    public static partial class Global
    {

        #region  配置文件
        public static ConfigHelper _config = new ConfigHelper("Core/config.json");

        public static void ConfigInit()
        {
            _config.ConfigInit();

        }
        public static string GetValue(string? key)
        {
            return _config.GetValue(key);
        }
        [RequireRole(Role.Admin)]
        public static bool SetConfig(string key, string value)
        {
            return _config.SetConfig(key, value);
        }

        #endregion
        #region 数据库
        #region 模组数量管理
        public static int Modules;

        public static void SetModules()
        {
            Modules = 2;
        }
        #endregion

        #region 加载管理器
        public static ILoadingManager LoadingManager { get; private set; }
        #endregion

        #region 权限管理

        public static void SecureRoleExecute<T>(Action<T> action, T arg)
        {
            //try
            //{
            action(arg);
            //}
            //catch (UnauthorizedAccessException)
            //{

            //    Console.WriteLine("权限不足，终止方法执行。");
            //}
        }


        public static void RoleInit()
        {
        }
        #endregion


        //初始化
        public static void GlobalMain(ILoadingManager loadingManager)
        {
            LoadingManager = loadingManager;
            ReInit();
            RoleInit();
            //SQLInit();


        }
        public static void ReInit()
        {
            ConfigInit();
            SetModules();
        }





        public static ClassTimeInfo GetCurrentClassTime()
        {
            DateTime now = DateTime.Now;
            DateTime start, end;
            ClassStatus status;

            if (now.Hour >= 8 && now.Hour < 20)
            {
                // 白班：今天 08:00 ～ 今天 20:00
                start = now.Date.AddHours(8);
                end = now.Date.AddHours(20);
                status = ClassStatus.白班;
            }
            else if (now.Hour < 8)
            {
                // 夜班：昨天 20:00 ～ 今天 08:00
                start = now.Date.AddDays(-1).AddHours(20);
                end = now.Date.AddHours(8);
                status = ClassStatus.夜班;
            }
            else
            {
                // 夜班：今天 20:00 ～ 明天 08:00
                start = now.Date.AddHours(20);
                end = now.Date.AddDays(1).AddHours(8);
                status = ClassStatus.夜班;
            }

            return new ClassTimeInfo
            {
                Status = status,
                Start = start,
                End = end
            };
        }



    }


    public static class Update
    {

        public static void UpdateXLabels(ObservableCollection<Axis> axes, DateTime start, DateTime end, Unit unit)
        {
            if (axes == null)
                return;

            var labels = new List<string>();

            switch (unit)
            {
                case Unit.年:
                    for (int year = start.Year; year <= end.Year; year++)
                        labels.Add(year.ToString());
                    break;

                case Unit.月:
                    DateTime monthCursor = new DateTime(start.Year, start.Month, 1);
                    while (monthCursor <= end)
                    {
                        labels.Add($"{monthCursor:yyyy-MM}");
                        monthCursor = monthCursor.AddMonths(1);
                    }
                    break;

                case Unit.日:
                    DateTime dayCursor = start.Date;
                    while (dayCursor <= end.Date)
                    {
                        labels.Add($"{dayCursor:MM-dd}");
                        dayCursor = dayCursor.AddDays(1);
                    }
                    break;

                case Unit.时:
                    DateTime hourCursor = start;
                    DateTime endTime = end;

                    if (endTime < start)
                        endTime = endTime.AddDays(1);

                    while (hourCursor <= endTime)
                    {
                        labels.Add($"{hourCursor:MM-dd HH}:00");
                        hourCursor = hourCursor.AddHours(1);
                    }
                    break;
            }

            axes.Clear();
            axes.Add(new Axis
            {
                Labels = labels.ToArray(),
                LabelsPaint = new SolidColorPaint
                {
                    Color = SKColors.White,
                    SKTypeface = SKTypeface.FromFamilyName("Microsoft YaHei")
                },
                TextSize = 14,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(100))
            });
        }

        //更新柱状图的数据
        public static void UpdateCSeries(ISeries[] series, double[] newValues)
        {
            if (series == null || series.Length == 0)
                return;

            if (series[0] is ColumnSeries<double> columnSeries)
            {
                columnSeries.Values = newValues;
            }
        }
        //更新表格的数据
        public static void UpdateTableInfoData<T>(ObservableCollection<T> BindingData, ObservableCollection<T> newData)
        {

            BindingData.Clear();
            foreach (var item in newData)
            {
                BindingData.Add(item);
            }
        }
        public static void UpdateSeries<T>(IEnumerable<T> values, ObservableCollection<ISeries> series, ValueUnit valueUnit)
        {
            if (series == null)
                series = new ObservableCollection<ISeries>();

            series.Clear();

            if (valueUnit == ValueUnit.RealValue)
            {
                series.Add(new LineSeries<T>
                {
                    Values = (IReadOnlyCollection<T>)values,
                    Fill = new SolidColorPaint(new SKColor(30, 80, 130, 50)), // 区域底色透明
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsSize = 14,
                    DataLabelsFormatter = point => point.Coordinate.PrimaryValue.ToString()
                });
            }

            else if (valueUnit == ValueUnit.Rate)
            {
                series.Add(new LineSeries<T>
                {
                    Values = (IReadOnlyCollection<T>)values,
                    Fill = new SolidColorPaint(new SKColor(30, 80, 130, 50)), // 区域底色透明
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsSize = 14,
                    // ✅ 格式化为百分比
                    DataLabelsFormatter = point =>
                    {
                        double value = Convert.ToDouble(point.Coordinate.PrimaryValue);
                        return (value).ToString("0.##") + "%";
                    }
                });
            }

        }


        public static void UpdatePieData(
            ObservableCollection<ISeries> MyPieSeries,
            IEnumerable<int> newValues,
            string[] _names,
            string[]? hexColors = null)  // 新增：可选纯色配色数组
        {
            var values = newValues.ToArray();
            if (values.Length != _names.Length)
                throw new ArgumentException("值与名称数量需一致");

            MyPieSeries.Clear();

            // 默认颜色列表，用于 fallback
            hexColors ??= new[]
            {
        "#42A5F5", "#66BB6A", "#FFB74D",
        "#9575CD", "#E57373", "#4DD0E1"
    };

            int i = 0;
            foreach (var value in values)
            {
                var color = SKColor.Parse(hexColors[i % hexColors.Length]);

                MyPieSeries.Add(new PieSeries<int>
                {
                    Values = new[] { value },
                    Name = _names[i],
                    DataLabelsPosition = PolarLabelsPosition.Middle,
                    DataLabelsSize = 15,
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsFormatter = point =>
                    {
                        var name = point.Context.Series.Name;
                        var dash = name.IndexOf('-');
                        return dash >= 0 ? name.Substring(0, dash) : name;
                    },
                    ToolTipLabelFormatter = point => $"{point.StackedValue!.Share:P2}",
                    Fill = new SolidColorPaint(color),  // 每片纯色填充
                    AnimationsSpeed = TimeSpan.Zero
                });

                i++;
            }
        }

    }
    public class ClassTimeInfo
    {
        public ClassStatus Status { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public static class CurrentUserContext
    {
        public static string UserName { get; set; }
        public static Role role { get; set; }

    }
    public enum Unit
    {
        年 = 0,
        月 = 1,
        日 = 2,
        时 = 3,
    }


    public enum ValueUnit
    {
        RealValue = 0,
        Rate = 1
    }
    public enum ClassStatus
    {
        白班 = 0,
        夜班 = 1
    }
    public enum TriggerType
    {
        上料机1_产品码读取,
        上料机2_产品码读取,
        上翻转台_产品码读取,
        下翻转台_挂具码读取,
        上料机1_小时产能,
        上料机2_小时产能,
        下料机1_小时产能,
        下料机2_小时产能,
        上翻转台_小时产能,
        下翻转台_小时产能,
    }
    public enum TriggerInfo
    {
        触发_读取_11,
        触发_读取_10,
        返回结果_读取_0,
        返回结果_写入_11
    }
    public interface ILoadingManager
    {
        void StartLoading(string text = "");
        void StopLoading();

    }
    public partial class AlarmInfo : ObservableObject
    {
        // [新增] 用于追踪是哪个属性触发的报警，方便移除
        public string PropertyKey { get; set; } = string.Empty;

        [ObservableProperty] private int _index;
        [ObservableProperty] private DateTime _time;
        [ObservableProperty] private string _device;
        [ObservableProperty] private string _description;

        public string TimeFormatted => _time.ToString("yyyy-MM-dd HH:mm:ss");
    }
    public class TableRowViewModel
    {
        public int ModuleNum { get; set; }
        public SettingModoulNum UporDn { get; set; }
        public string ProjectCodes { get; set; }

        public string AnodeTypes { get; set; }

        public string ProductColors { get; set; }

        public string MaterialTypes { get; set; }

    }
    public enum SettingModoulNum
    {
        上,
        下
    }
    public class MyDataUpdatedEvent : PubSubEvent<IEnumerable<AlarmInfo>> { }
    public class MyDataUpdatedSettingEvent : PubSubEvent<TableRowViewModel> { }
    public enum ProductStatus
    {
        上料 = 0,
        上翻转 = 1,
        下翻转 = 2,
    }
    public enum Batch
    {
        上翻转台 = 0,
        下翻转台 = 1,
    }
    public enum UpHangerModoule
    {
        上料机1 = 0,
        上料机2 = 1,
    }
    public enum DnHangerModoule
    {
        下料机1 = 0,
        下料机2 = 1,
    }
    public enum UpDnHangerModoule
    {
        上料机1 = 0,
        上料机2 = 1,
        下料机1 = 2,
        下料机2 = 3,
    }
    public enum WarningDevice
    {
        上料机1 = 0,
        上料机2 = 1,
        下料机1 = 2,
        下料机2 = 3,
        上翻转台 = 4,
        下翻转台 = 5,
    }


    public enum UpDnHangerWarningMessage
    {
        传感器故障 = 0,
        元器件故障 = 1,
        与Trace通讯异常 = 2,
        与总控通讯异常 = 3
    }
    public enum BatchWarningMessage
    {
        感应器故障 = 0,
        元器件故障 = 1,
        与Trace通讯故障 = 2,
        与上位机数据交互故障 = 3,
        与机械手数据交互故障 = 4,
        门禁被触发 = 5,
        安全光栅被触发 = 6
    }
    public class RelayCommand<T> : ICommand
    {
        public readonly Action<T> _execute;
        public readonly Predicate<T>? _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute((T)parameter!);
        public void Execute(object? parameter) => _execute((T)parameter!);

        public event EventHandler? CanExecuteChanged;
    }
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
        public void Execute(object? parameter) => _execute();
    }
    public static class PredicateExtensions
    {
        public static Expression<Func<T, bool>> AndAlsoSafe<T>(
            this Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            var param = expr1.Parameters[0];
            var visitor = new ReplaceVisitor(expr2.Parameters[0], param);
            var body = System.Linq.Expressions.Expression.AndAlso(expr1.Body, visitor.Visit(expr2.Body)!);
            return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, param);
        }

        private class ReplaceVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParam;
            private readonly ParameterExpression _newParam;
            public ReplaceVisitor(ParameterExpression oldParam, ParameterExpression newParam)
            {
                _oldParam = oldParam;
                _newParam = newParam;
            }
            protected override System.Linq.Expressions.Expression VisitParameter(ParameterExpression node)
                => node == _oldParam ? _newParam : base.VisitParameter(node);
        }

    }

}
#endregion
