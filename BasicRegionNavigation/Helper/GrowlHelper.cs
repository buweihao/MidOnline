using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace BasicRegionNavigation.Helper
{
    public static class GrowlHelper
    {
        private const string Token = "GlobalGrowl";

        public static void Success(string msg, int time = 0) =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                Growl.Success(new HandyControl.Data.GrowlInfo
                {
                    Message = msg,
                    Token = Token,
                    WaitTime = time,                // 默认 0 秒后自动关闭
                                                    //ShowDateTime = true,
                                                    //ShowCloseButton = true
                });
            });

        public static void Error(string msg, int time = 0) =>
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Growl.Error(new HandyControl.Data.GrowlInfo
                            {
                                Message = msg,
                                Token = Token,
                                WaitTime = time,
                                //ShowDateTime = true,
                                //ShowCloseButton = true
                            });
                        });
        public static void Info(string msg, int time = 0) =>
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Growl.Info(new HandyControl.Data.GrowlInfo
                            {
                                Message = msg,
                                Token = Token,
                                WaitTime = time,
                                //ShowDateTime = true,
                                //ShowCloseButton = true
                            });
                        });
        public static void Warning(string msg, int time = 0) =>
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Growl.Warning(new HandyControl.Data.GrowlInfo
                            {
                                Message = msg,
                                Token = Token,
                                WaitTime = time,
                                //ShowDateTime = true,
                                //ShowCloseButton = true
                            });
                        });
    }


}
