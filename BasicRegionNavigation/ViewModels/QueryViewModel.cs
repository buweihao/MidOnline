using BasicRegionNavigation.Helper;
using BasicRegionNavigation.Models; // 确保引用了 ProductionRecord
using BasicRegionNavigation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BasicRegionNavigation.ViewModels
{
    internal partial class QueryViewModel : ObservableObject, INavigationAware
    {
        private readonly IQueryService _productService;

        // -----------------------------------------------------------------------
        // 筛选属性
        // -----------------------------------------------------------------------
        [ObservableProperty] private string _productCode;
        [ObservableProperty] private string _productProjectNo;
        [ObservableProperty] private string _hangerCode;
        [ObservableProperty] private string _color;
        [ObservableProperty] private string _rawMaterialCategory;
        [ObservableProperty] private string _upHangerModoule;
        [ObservableProperty] private DateTime _startCreatedAt = DateTime.Now.AddDays(-1);
        [ObservableProperty] private DateTime _endCreatedAt = DateTime.Now;

        // -----------------------------------------------------------------------
        // 列表与分页
        // -----------------------------------------------------------------------

        // [修改 1] 泛型类型改为 ProductionRecord
        [ObservableProperty]
        private ObservableCollection<ProductionRecord> _allProducts;

        // [修改 2] 泛型类型改为 ProductionRecord
        [ObservableProperty]
        private ObservableCollection<ProductionRecord> _pagedProducts;

        [ObservableProperty] private int _currentPage = 1;
        [ObservableProperty] private int _pageSize = 15;
        [ObservableProperty] private int _totalPages = 1;

        public QueryViewModel(IQueryService productService)
        {
            _productService = productService;
            // 初始化集合
            _allProducts = new ObservableCollection<ProductionRecord>();
            _pagedProducts = new ObservableCollection<ProductionRecord>();
        }

        // -----------------------------------------------------------------------
        // 导航事件
        // -----------------------------------------------------------------------
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 进入页面自动查询
            QueryCommand.Execute(null);
        }

        public bool IsNavigationTarget(NavigationContext c) => true;
        public void OnNavigatedFrom(NavigationContext c) { }

        // -----------------------------------------------------------------------
        // 业务逻辑
        // -----------------------------------------------------------------------

        [RelayCommand]
        private async Task QueryAsync()
        {
            try
            {
                // Global.LoadingManager.StartLoading(); 

                // 1. 构建 Filter
                var filter = new ProductQueryFilter
                {
                    ProductCode = ProductCode,
                    ProjectNo = ProductProjectNo,
                    HangerCode = HangerCode,
                    Color = Color,
                    MaterialCategory = RawMaterialCategory,
                    UpHangerModule = UpHangerModoule,
                    StartTime = StartCreatedAt,
                    EndTime = EndCreatedAt
                };

                // 2. 调用 Service 获取数据
                // [修改 3] Service 返回的已经是 List<ProductionRecord> 了，不需要转换
                var data = await _productService.GetProductsAsync(filter);

                // 直接包装成 ObservableCollection
                AllProducts = new ObservableCollection<ProductionRecord>(data);

                // 3. 重置分页
                CurrentPage = 1;
                RefreshPagination();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询失败: {ex.Message}");
            }
            finally
            {
                // Global.LoadingManager.StopLoading();
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (AllProducts == null || AllProducts.Count == 0)
            {
                MessageBox.Show("当前没有数据可导出");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "选择导出路径",
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"产品明细_{DateTime.Now:yyyyMMddHHmm}.xlsx"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Service 的 ExportToExcelAsync 签名也已经改为了 IEnumerable<ProductionRecord>
                    // 所以这里直接传 AllProducts 即可
                    await _productService.ExportToExcelAsync(AllProducts, dialog.FileName);
                    MessageBox.Show("导出成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}");
                }
            }
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
            StartCreatedAt = DateTime.Today;
            EndCreatedAt = DateTime.Now;
        }

        // -----------------------------------------------------------------------
        // 分页逻辑
        // -----------------------------------------------------------------------

        partial void OnPageSizeChanged(int value) => RefreshPagination();
        partial void OnCurrentPageChanged(int value) => RefreshPagination();

        private void RefreshPagination()
        {
            if (AllProducts == null) return;

            TotalPages = (int)Math.Ceiling((double)AllProducts.Count / PageSize);
            if (TotalPages == 0) TotalPages = 1;

            if (CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            // [修改 4] Linq 结果直接就是 ProductionRecord
            var items = AllProducts
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            PagedProducts = new ObservableCollection<ProductionRecord>(items);
        }

        [RelayCommand]
        private void PrevPage()
        {
            if (CurrentPage > 1) CurrentPage--;
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages) CurrentPage++;
        }
    }
}