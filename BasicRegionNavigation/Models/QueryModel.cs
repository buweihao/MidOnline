using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasicRegionNavigation.Models
{
    public partial class ProductInfo : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private string _productCode = "-";
        [ObservableProperty] private string _productProjectNo = "-";
        [ObservableProperty] private string _materialType = "-";
        [ObservableProperty] private string _batchNo = "-";
        [ObservableProperty] private string _anodeType = "-";
        // [已移除] 喷砂线体
        [ObservableProperty] private string _hangerCode = "-";
        [ObservableProperty] private string _productPosition = "-";
        [ObservableProperty] private string _loader = "-"; // 上料机
        [ObservableProperty] private string _feedTime = "-"; // 上供料时间
        [ObservableProperty] private string _hangerPosition = "-";
        [ObservableProperty] private string _color = "-";
        // [已移除] 下挂翻转台
        [ObservableProperty] private string _hangTime = "-"; // 上挂时间
        // [已移除] 下挂时间
    }
    public class ProductQueryFilter
    {
        public string ProductCode { get; set; }
        public string ProjectNo { get; set; }
        public string HangerCode { get; set; }
        public string Color { get; set; }
        public string MaterialCategory { get; set; }
        public string UpHangerModule { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
