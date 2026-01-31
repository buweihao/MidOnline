using Core;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Windows;
using System.Windows.Controls;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// Interaction logic for ViewB
    /// </summary>
    public partial class ExceptionData : UserControl
    {
        public ExceptionData()
        {
            InitializeComponent();
            init();

        }
        void init()
        {
            myChart.LegendTextPaint = new SolidColorPaint(SKColors.White)
            {
                FontFamily = "Microsoft YaHei",
                SKFontStyle = SKFontStyle.Bold,
                StrokeThickness = 1
            };

        }
        //private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        //{
        //    var vm = (ExceptionDataViewModel)this.DataContext;
        //    vm.Start = Global.GetCurrentClassTime().Start;
        //    vm.QueryCommand.Execute(null);
        //}
    }
}
