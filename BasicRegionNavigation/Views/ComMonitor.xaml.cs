using Core;
using LiveChartsCore.VisualElements;
using BasicRegionNavigation.Controls;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// ComMonitor.xaml 的交互逻辑
    /// </summary>
    public partial class ComMonitor : UserControl
    {   
        public static bool firstNavigateStatus = true;
        private int Modules = Global.Modules;
        public ComMonitor()
        {
            InitializeComponent();

            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000);

                    if (Global.Modules != Modules)
                    {
                        Modules = Global.Modules;
                        ApplyModelVisibility(Global.Modules);
                    }
                }
            });
        }
        private void ApplyModelVisibility(int visibleCount)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                int index = 1;
                foreach (var child in AllModel.Children.OfType<ConnectUnit>())
                {
                    child.Visibility = index <= visibleCount ? Visibility.Visible : Visibility.Collapsed;
                    child.LineVisibility = index < visibleCount ? Visibility.Visible : Visibility.Collapsed;
                    index++;
                }
            });
        }
    }
}
