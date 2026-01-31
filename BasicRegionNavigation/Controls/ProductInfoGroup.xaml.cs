using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace BasicRegionNavigation.Controls
{
    public partial class ProductInfoGroup : UserControl
    {
        public ProductInfoGroup()
        {
            InitializeComponent();
        }

        // --- 标题 ---
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(ProductInfoGroup), new PropertyMetadata("标题"));
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // --- 数据源集合 ---
        // 使用 IEnumerable 类型兼容性最强（支持 Array, List, ObservableCollection 等）
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(IEnumerable), typeof(ProductInfoGroup), new PropertyMetadata(null));

        public IEnumerable Items
        {
            get => (IEnumerable)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }
    }
}