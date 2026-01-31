using Core;
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
    /// Query.xaml 的交互逻辑
    /// </summary>
    public partial class Query : UserControl
    {
        public Query()
        {
            InitializeComponent();
        }
        //private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    var vm = (QueryViewModel)this.DataContext;
        //    vm.StartCreatedAt = Global.GetCurrentClassTime().Start;
        //    //vm.StartDownHangTime = Global.GetCurrentClassTime().Start;
        //    vm.QueryCommand.Execute(null);
            

        //}
    }
}
