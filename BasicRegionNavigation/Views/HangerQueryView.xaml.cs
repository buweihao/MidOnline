using Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// HangerQueryView.xaml 的交互逻辑
    /// </summary>
    public partial class HangerQuery : UserControl
    {

        public HangerQuery()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            
        }
        //private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    var vm = (HangerQueryViewModel)this.DataContext;
        //    vm.Start = Global.GetCurrentClassTime().Start;
        //    vm.QueryCommand.Execute(null);
        //}
    }
    public class HangerQueryRowItemsModel
    {
        public string VehicleId { get; set; }
        public string UpHangerTime { get; set; }
        public string UpHangerModoule { get; set; }
        public string DnHangerTime { get; set; }
        public string DnHangerModoule { get; set; }
    }

}
