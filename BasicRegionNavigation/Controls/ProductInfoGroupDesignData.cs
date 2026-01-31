using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Controls
{
    public class ProductInfoGroupDesignData
    {
        public string Title => "上挂产品";

        public ObservableCollection<ProductInfoItem> InfoItems => new ObservableCollection<ProductInfoItem>
        {
            new ProductInfoItem { Label = "项目编号", Value = "CY21468" },
            new ProductInfoItem { Label = "原料", Value = "金桥" },
            new ProductInfoItem { Label = "阳极类型", Value = "一阳" },
            new ProductInfoItem { Label = "颜色", Value = "蓝色" }
        };
    }
}
