using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Core;

namespace BasicRegionNavigation.Views
{
    /// <summary>
    /// Interaction logic for ViewB
    /// </summary>
    public partial class ViewB : UserControl
    {


        public ViewB()
        {
            InitializeComponent();
            cartesianChart1.LegendTextPaint= new SolidColorPaint(SKColors.White);
            
        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
