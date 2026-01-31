using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Controls
{
    // 1. 必须声明为 partial 类，以便源生成器生成代码
    // 2. 继承 ObservableObject 以获得 INotifyPropertyChanged 实现
    public partial class ProductInfoItem : ObservableObject
    {
        [ObservableProperty]
        private string _label;

        [ObservableProperty]
        private string _value;

        // 源生成器会自动生成首字母大写的 public 属性：
        // public string Label { get => _label; set => SetProperty(ref _label, value); }
        // public string Value { get => _value; set => SetProperty(ref _value, value); }
    }
}