using Core;
using System.Windows;
using System.Windows.Controls;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// Interaction logic for ViewA
    /// </summary>
    public partial class Productivity : UserControl
    {
        public Productivity()
        {
            InitializeComponent();
        }
        //private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    var vm = (ProductivityViewModel)this.DataContext;
        //    vm.Start = Global.GetCurrentClassTime().Start;
        //    vm.QueryCommand.Execute(null);
        //}
    }
}
