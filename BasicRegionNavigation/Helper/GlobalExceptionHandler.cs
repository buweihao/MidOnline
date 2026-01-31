using System.Windows.Threading;

namespace BasicRegionNavigation.Helper
{
    public class GlobalExceptionHandler
    {
        public static void Register()
        {
            // UI 线程
            System.Windows.Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
            // 非 UI 线程
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            // Task 异常
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowException(e.Exception, "UI 线程发生未捕获的异常");
            e.Handled = true;
        }

        private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            ShowException(ex, "应用程序域发生未捕获的异常");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            ShowException(e.Exception, "任务调度器发生未观察到的异常");
            e.SetObserved();
        }

        private static void ShowException(Exception? ex, string title)
        {
            if (ex == null) return;

            var msg = $"{title}：\n\n消息：{ex.Message}\n\n堆栈：{ex.StackTrace}";
            if (ex.InnerException != null)
            {
                msg += $"\n\n内部异常：\n{ex.InnerException.Message}\n\n{ex.InnerException.StackTrace}";
            }

            // 使用刚刚分离出去的 GrowlHelper
            GrowlHelper.Warning(ex.Message);

            // 也可以记录日志 (Log.Error(msg))
            Console.WriteLine(msg);
        }
    }
}