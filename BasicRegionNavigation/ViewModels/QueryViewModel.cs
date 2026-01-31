using BasicRegionNavigation;
using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Services;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using DocumentFormat.OpenXml.Wordprocessing;
using HandyControl.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BasicRegionNavigation.ViewModels
{
    // 1. Partial 类
    // 2. 继承 ObservableObject
    // 3. 移除 INotifyPropertyChanged 接口声明（基类已实现）
    internal partial class QueryViewModel : ObservableObject, INavigationAware
    {
        private readonly IConfigService _configService;
        // -----------------------------------------------------------------------
        // 筛选条件 (Filter Properties)
        // -----------------------------------------------------------------------
        #region Filters

        [ObservableProperty] private string _productCode;
        [ObservableProperty] private string _productProjectNo;
        [ObservableProperty] private string _hangerCode;
        [ObservableProperty] private string _color;
        [ObservableProperty] private string _rawMaterialCategory;
        [ObservableProperty] private string _upHangerModoule;
        [ObservableProperty] private DateTime _startCreatedAt = default;
        [ObservableProperty] private DateTime _endCreatedAt = DateTime.Now;
        [ObservableProperty] private DateTime? _startDownHangTime = default;
        [ObservableProperty] private DateTime? _endDownHangTime = default;

        #endregion

        // -----------------------------------------------------------------------
        // 列表与分页 (List & Pagination)
        // -----------------------------------------------------------------------
        #region Pagination

        // 注意：AllProducts 变化时需要触发分页更新
        [ObservableProperty]
        private ObservableCollection<ProductInfo> _allProducts;

        // 生成代码会提供 OnAllProductsChanged 钩子
        partial void OnAllProductsChanged(ObservableCollection<ProductInfo> value)
        {
            UpdatePage(); // 数据源变了，刷新当前页
            OnPropertyChanged(nameof(TotalPages)); // 通知总页数变化
        }

        [ObservableProperty]
        private ObservableCollection<ProductInfo> _pagedProducts;

        [ObservableProperty]
        private int _currentPage = 1;

        // 钩子方法：当 CurrentPage 发生变化时自动调用
        partial void OnCurrentPageChanged(int value)
        {
            UpdatePage();
        }

        [ObservableProperty]
        private int _pageSize = 10;

        // 钩子方法：当 PageSize 发生变化时自动调用
        partial void OnPageSizeChanged(int value)
        {
            UpdatePage();
            OnPropertyChanged(nameof(TotalPages));
        }

        // 计算属性
        public int TotalPages
        {
            get
            {
                if (AllProducts == null || AllProducts.Count == 0) return 1;
                return (int)Math.Ceiling((double)AllProducts.Count / PageSize);
            }
        }

        #endregion

        // -----------------------------------------------------------------------
        // 构造函数
        // -----------------------------------------------------------------------
        public QueryViewModel(IConfigService configService)
        {
            _configService = configService;
            // 初始化假数据
            var temp = new ObservableCollection<ProductInfo>();
            for (int i = 1; i <= 50; i++)
            {
                temp.Add(new ProductInfo
                {
                    Index = i,
                    ProductCode = $"P000{i}",
                    ProductProjectNo = $"PJ2025-{i}",
                    MaterialType = "铝",
                    BatchNo = $"BATCH{i}",
                    AnodeType = "标准",
                    LineNum = $"MCH-{i}",
                    HangerCode = $"HGR-{i}",
                    ProductPosition = $"Pos-{i}",
                    Loader = $"Loader-{i}",
                    FeedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    HangerPosition = $"挂具位-{i}",
                    Color = "银色",
                    FlipTable = $"翻转-{i}",
                    HangTime = DateTime.Now.ToString("HH:mm"),
                    DropTime = DateTime.Now.ToString("HH:mm")
                });
            }
            // 直接赋值给属性，触发 OnAllProductsChanged -> UpdatePage
            AllProducts = temp;
        }

        // -----------------------------------------------------------------------
        // 导航事件
        // -----------------------------------------------------------------------
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            StartCreatedAt = Global.GetCurrentClassTime().Start;
            // 如果需要自动查询
            if (QueryCommand.CanExecute(null))
            {
                QueryCommand.Execute(null);
            }
        }

        public bool IsNavigationTarget(NavigationContext c) => true;
        public void OnNavigatedFrom(NavigationContext c) { }

        // -----------------------------------------------------------------------
        // 方法 (Methods)
        // -----------------------------------------------------------------------

        public void UpdatePage()
        {
            if (AllProducts == null) return;

            // 防止 CurrentPage 超出范围
            if (TotalPages > 0 && CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var items = AllProducts
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            PagedProducts = new ObservableCollection<ProductInfo>(items);
        }

        // -----------------------------------------------------------------------
        // 命令 (Commands)
        // -----------------------------------------------------------------------

        [RelayCommand]
        private void PrevPage()
        {
            if (CurrentPage > 1)
                CurrentPage--;
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
                CurrentPage++;
        }

        [RelayCommand]
        private void Reset()
        {
            ProductCode = null;
            ProductProjectNo = null;
            HangerCode = null;
            Color = null;
            RawMaterialCategory = null;
            UpHangerModoule = null;

            StartCreatedAt = Global.GetCurrentClassTime().Start;
            EndCreatedAt = DateTime.Now;
            StartDownHangTime = default;
            EndDownHangTime = default;
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            try
            {
                Global.LoadingManager.StartLoading();
                ExportProductInfoWithDialog(AllProducts);
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
            }
        }

        [RelayCommand]
        private async Task QueryAsync()
        {
            await Task.Delay(200);

        }

        // -----------------------------------------------------------------------
        // 静态辅助方法
        // -----------------------------------------------------------------------

        [RequireRole(Role.Admin)]
        public static void ExportProductInfoWithDialog(ObservableCollection<ProductInfo> productData)
        {
            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = "产品明细信息表.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                using (var workbook = new XLWorkbook())
                {
                    var sheet = workbook.Worksheets.Add("产品明细信息表");

                    // 表头
                    sheet.Cell(1, 1).Value = "序号";
                    sheet.Cell(1, 2).Value = "产品码";
                    sheet.Cell(1, 3).Value = "项目号";
                    sheet.Cell(1, 4).Value = "原材料别";
                    sheet.Cell(1, 5).Value = "批次";
                    sheet.Cell(1, 6).Value = "阳极类型";
                    sheet.Cell(1, 7).Value = "喷砂线体";
                    sheet.Cell(1, 8).Value = "挂具码";
                    sheet.Cell(1, 9).Value = "产品位置";
                    sheet.Cell(1, 10).Value = "上料机";
                    sheet.Cell(1, 11).Value = "上供料时间";
                    sheet.Cell(1, 12).Value = "挂具位置";
                    sheet.Cell(1, 13).Value = "颜色";
                    sheet.Cell(1, 14).Value = "下挂翻转台";
                    sheet.Cell(1, 15).Value = "上挂时间";
                    sheet.Cell(1, 16).Value = "下挂时间";

                    // 数据
                    int row = 2;
                    foreach (var item in productData)
                    {
                        sheet.Cell(row, 1).Value = item.Index;
                        sheet.Cell(row, 2).Value = item.ProductCode;
                        sheet.Cell(row, 3).Value = item.ProductProjectNo;
                        sheet.Cell(row, 4).Value = item.MaterialType;
                        sheet.Cell(row, 5).Value = item.BatchNo;
                        sheet.Cell(row, 6).Value = item.AnodeType;
                        sheet.Cell(row, 7).Value = item.LineNum;
                        sheet.Cell(row, 8).Value = item.HangerCode;
                        sheet.Cell(row, 9).Value = item.ProductPosition;
                        sheet.Cell(row, 10).Value = item.Loader;
                        sheet.Cell(row, 11).Value = item.FeedTime;
                        sheet.Cell(row, 12).Value = item.HangerPosition;
                        sheet.Cell(row, 13).Value = item.Color;
                        sheet.Cell(row, 14).Value = item.FlipTable;
                        sheet.Cell(row, 15).Value = item.HangTime;
                        sheet.Cell(row, 16).Value = item.DropTime;
                        row++;
                    }

                    sheet.Columns().AdjustToContents();
                    workbook.SaveAs(dialog.FileName);
                }
            }
        }
    }

    // 同样建议简单改造 Model
    public partial class ProductInfo : ObservableObject
    {
        [ObservableProperty] private int? _index;
        [ObservableProperty] private string _productCode = "-";
        [ObservableProperty] private string _productProjectNo = "-";
        [ObservableProperty] private string _materialType = "-";
        [ObservableProperty] private string _batchNo = "-";
        [ObservableProperty] private string _anodeType = "-";
        [ObservableProperty] private string _lineNum = "-";
        [ObservableProperty] private string _hangerCode = "-";
        [ObservableProperty] private string _productPosition = "-";
        [ObservableProperty] private string _loader = "-";
        [ObservableProperty] private string _feedTime = "-";
        [ObservableProperty] private string _hangerPosition = "-";
        [ObservableProperty] private string _color = "-";
        [ObservableProperty] private string _flipTable = "-";
        [ObservableProperty] private string _hangTime = "-";
        [ObservableProperty] private string _dropTime = "-";
    }
}