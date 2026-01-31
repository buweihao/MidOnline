using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace BasicRegionNavigation.Controls
{
    /// <summary>
    /// LineWithImage.xaml 的交互逻辑
    /// </summary>
    public partial class LineWithImage : UserControl
    {
        public LineWithImage()
        {
            InitializeComponent();
        }

        // 坐标和样式
        public double X1 { get => (double)GetValue(X1Property); set => SetValue(X1Property, value); }
        public static readonly DependencyProperty X1Property =
            DependencyProperty.Register(nameof(X1), typeof(double), typeof(LineWithImage));

        public double Y1 { get => (double)GetValue(Y1Property); set => SetValue(Y1Property, value); }
        public static readonly DependencyProperty Y1Property =
            DependencyProperty.Register(nameof(Y1), typeof(double), typeof(LineWithImage));

        public double X2 { get => (double)GetValue(X2Property); set => SetValue(X2Property, value); }
        public static readonly DependencyProperty X2Property =
            DependencyProperty.Register(nameof(X2), typeof(double), typeof(LineWithImage));

        public double Y2 { get => (double)GetValue(Y2Property); set => SetValue(Y2Property, value); }
        public static readonly DependencyProperty Y2Property =
            DependencyProperty.Register(nameof(Y2), typeof(double), typeof(LineWithImage));

        public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(LineWithImage));

        public double StrokeThickness { get => (double)GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(LineWithImage));

        // 图片参数
        public ImageSource ImageSource { get => (ImageSource)GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(LineWithImage));

        public double ImageWidth { get => (double)GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }
        public static readonly DependencyProperty ImageWidthProperty =
            DependencyProperty.Register(nameof(ImageWidth), typeof(double), typeof(LineWithImage), new PropertyMetadata(16.0));

        public double ImageHeight { get => (double)GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }
        public static readonly DependencyProperty ImageHeightProperty =
            DependencyProperty.Register(nameof(ImageHeight), typeof(double), typeof(LineWithImage), new PropertyMetadata(16.0));

        // 新增：图片在内部 Canvas 的位置
        public double ImageLeft
        {
            get => (double)GetValue(ImageLeftProperty);
            set => SetValue(ImageLeftProperty, value);
        }
        public static readonly DependencyProperty ImageLeftProperty =
            DependencyProperty.Register(nameof(ImageLeft), typeof(double), typeof(LineWithImage), new PropertyMetadata(0.0));

        public double ImageTop
        {
            get => (double)GetValue(ImageTopProperty);
            set => SetValue(ImageTopProperty, value);
        }
        public static readonly DependencyProperty ImageTopProperty =
            DependencyProperty.Register(nameof(ImageTop), typeof(double), typeof(LineWithImage), new PropertyMetadata(0.0));

        // 计算中点
        public double MidX => (X1 + X2) / 2 - ImageWidth / 2;
        public double MidY => (Y1 + Y2) / 2 - ImageHeight / 2;
    }

    public class BrushToConnectionIconConverter : IValueConverter
    {
        private static readonly Uri ConnectedUri =
            new Uri("/Resources/connection.png", UriKind.Relative);
        private static readonly Uri DisconnectedUri =
            new Uri("/Resources/connectionNo.png", UriKind.Relative);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var brush = value as Brush;

            // 仅处理 SolidColorBrush，其他类型按“未连接”图标处理
            if (brush is SolidColorBrush scb)
            {
                var c = scb.Color;
                // 你也可以按需要扩展：如 >= 某亮度判定为“绿”等
                if (c == Colors.Green)
                    return new BitmapImage(ConnectedUri);
                if (c == Colors.Red)
                    return new BitmapImage(DisconnectedUri);
            }

            // 默认：断开图标
            return new BitmapImage(DisconnectedUri);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
