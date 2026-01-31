using BasicRegionNavigation.ViewModels;
using Core;
using HandyControl.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// Interaction logic for ViewA
    /// </summary>
    public partial class ViewA : UserControl
    {

        private int Modules = Global.Modules;
        public ViewA()
        {
            InitializeComponent();
            SetNavigateVisbility();
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);

                    if (Global.Modules != Modules)
                    {
                        Modules = Global.Modules;
                        SetNavigateVisbility();
                    }
                }
            });

        }

        private void SetNavigateVisbility()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                模组1.Visibility = Modules >= 1 ? Visibility.Visible : Visibility.Hidden;
                模组2.Visibility = Modules >= 2 ? Visibility.Visible : Visibility.Hidden;
                模组3.Visibility = Modules >= 3 ? Visibility.Visible : Visibility.Hidden;
                模组4.Visibility = Modules >= 4 ? Visibility.Visible : Visibility.Hidden;
                模组5.Visibility = Modules >= 5 ? Visibility.Visible : Visibility.Hidden;
                模组6.Visibility = Modules >= 6 ? Visibility.Visible : Visibility.Hidden;
                模组7.Visibility = Modules >= 7 ? Visibility.Visible : Visibility.Hidden;
                模组8.Visibility = Modules >= 8 ? Visibility.Visible : Visibility.Hidden;
                模组9.Visibility = Modules >= 9 ? Visibility.Visible : Visibility.Hidden;
                模组10.Visibility = Modules >= 10 ? Visibility.Visible : Visibility.Hidden;
                模组11.Visibility = Modules >= 11 ? Visibility.Visible : Visibility.Hidden;
                模组12.Visibility = Modules >= 12 ? Visibility.Visible : Visibility.Hidden;
            });
        }


        private void EditText_Click(object sender, RoutedEventArgs e)
        {
        }
        private void ClockRadioButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            //右键执行一个vm中的命令
            var vm = (ViewAViewModel)this.DataContext;
            if (sender is ClockRadioButton radioBtn)
            {
                var parameter = radioBtn.CommandParameter;
                vm.ShowTextCommand.Execute(parameter);

            }


        }

    }

    public class IntToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 1. 处理 bool 类型 (新增逻辑)
            if (value is bool boolValue)
            {
                // true = 绿色, false = 红色
                return boolValue ? Brushes.Green : Brushes.Red;
            }

            // 2. 处理 short 类型 (原有逻辑)
            if (value is short intValue)
            {
                return intValue switch
                {
                    1 => Brushes.Green,
                    2 => Brushes.Red,
                    _ => Brushes.Gray,
                    //_ => Brushes.Transparent
                };
            }

            // 3. 兼容 int 类型 (防止绑定源是int而不是short导致的转换失败)
            if (value is int regularIntValue)
            {
                return regularIntValue switch
                {
                    1 => Brushes.Green,
                    2 => Brushes.Red,
                    3 => Brushes.Gray,
                    _ => Brushes.Transparent
                };
            }

            // 如果不是 bool 也不是数字，返回默认颜色
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



}
