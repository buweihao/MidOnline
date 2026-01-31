using System;
using System.Collections;
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

namespace BasicRegionNavigation
{
    /// <summary>
    /// DataTableWithHeader.xaml 的交互逻辑
    /// </summary>
    public partial class DataTableWithHeader : UserControl
    {
        public DataTableWithHeader()
        {
                InitializeComponent();
        }

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(DataTableWithHeader), new PropertyMetadata("标题"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(DataTableWithHeader), new PropertyMetadata(null));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }


        // 定义依赖属性，用于传入列定义
        public ObservableCollection<DataGridColumn> DynamicColumns
        {
            get { return (ObservableCollection<DataGridColumn>)GetValue(DynamicColumnsProperty); }
            set { SetValue(DynamicColumnsProperty, value); }
        }

        public static readonly DependencyProperty DynamicColumnsProperty =
            DependencyProperty.Register("DynamicColumns", typeof(ObservableCollection<DataGridColumn>),
                typeof(DataTableWithHeader), new PropertyMetadata(null, OnColumnsChanged));

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as DataTableWithHeader;
            if (control?.dataGrid != null && e.NewValue is ObservableCollection<DataGridColumn> newColumns)
            {
                control.dataGrid.Columns.Clear();
                foreach (var column in newColumns)
                {
                    control.dataGrid.Columns.Add(column);
                }
            }
        }

    }
}
