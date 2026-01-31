using System;
using System.Collections;
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

namespace BasicRegionNavigation.Controls
{
    /// <summary>
    /// TwoDataTableWithHeader.xaml 的交互逻辑
    /// </summary>
    public partial class TwoDataTableWithHeader : UserControl
    {
        public TwoDataTableWithHeader()
        {
            InitializeComponent();
        }
        // 总标题
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(TwoDataTableWithHeader));

        // 上/下数据源
        public IEnumerable ItemsSourceTop
        {
            get => (IEnumerable)GetValue(ItemsSourceTopProperty);
            set => SetValue(ItemsSourceTopProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceTopProperty =
            DependencyProperty.Register(nameof(ItemsSourceTop), typeof(IEnumerable), typeof(TwoDataTableWithHeader));

        public IEnumerable ItemsSourceBottom
        {
            get => (IEnumerable)GetValue(ItemsSourceBottomProperty);
            set => SetValue(ItemsSourceBottomProperty, value);
        }
        public static readonly DependencyProperty ItemsSourceBottomProperty =
            DependencyProperty.Register(nameof(ItemsSourceBottom), typeof(IEnumerable), typeof(TwoDataTableWithHeader));

        // 上/下动态列（注意：DataGrid.Columns 不能直接绑定，只能在回调中应用）
        public IEnumerable<DataGridColumn> DynamicColumnsTop
        {
            get => (IEnumerable<DataGridColumn>)GetValue(DynamicColumnsTopProperty);
            set => SetValue(DynamicColumnsTopProperty, value);
        }
        public static readonly DependencyProperty DynamicColumnsTopProperty =
            DependencyProperty.Register(nameof(DynamicColumnsTop), typeof(IEnumerable<DataGridColumn>), typeof(TwoDataTableWithHeader),
                new PropertyMetadata(null, OnDynamicColumnsTopChanged));

        public IEnumerable<DataGridColumn> DynamicColumnsBottom
        {
            get => (IEnumerable<DataGridColumn>)GetValue(DynamicColumnsBottomProperty);
            set => SetValue(DynamicColumnsBottomProperty, value);
        }
        public static readonly DependencyProperty DynamicColumnsBottomProperty =
            DependencyProperty.Register(nameof(DynamicColumnsBottom), typeof(IEnumerable<DataGridColumn>), typeof(TwoDataTableWithHeader),
                new PropertyMetadata(null, OnDynamicColumnsBottomChanged));

        private static void OnDynamicColumnsTopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (TwoDataTableWithHeader)d;
            v.ApplyColumns(v.gridTop, e.NewValue as IEnumerable<DataGridColumn>);
        }

        private static void OnDynamicColumnsBottomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (TwoDataTableWithHeader)d;
            v.ApplyColumns(v.gridBottom, e.NewValue as IEnumerable<DataGridColumn>);
        }

        private void ApplyColumns(DataGrid grid, IEnumerable<DataGridColumn>? cols)
        {
            grid.Columns.Clear();
            if (cols == null) return;
            foreach (var c in cols) grid.Columns.Add(c);
        }
    }
}
