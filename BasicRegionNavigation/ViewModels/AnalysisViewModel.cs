using CommunityToolkit.Mvvm.ComponentModel; // 需要引用此命名空间
using Prism.Commands; // 假设你使用的是 Prism
using System.Windows;

namespace BasicRegionNavigation.ViewModels
{
    // 修改点 1 & 2: 添加 partial 关键字，并继承 ObservableObject
    internal partial class AnalysisViewModel : ObservableObject, INavigationAware
    {
        // 修改点 3: 将属性改为私有字段，并添加特性
        // CommunityToolkit 会自动生成 public string CurrentView { get; set; } 以及通知逻辑
        [ObservableProperty]
        private string currentView;

        private readonly IRegionManager _regionManager;

        public DelegateCommand<string> NavigateCommand { get; private set; }

        public AnalysisViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;
            NavigateCommand = new DelegateCommand<string>(Navigate);
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Navigate("Productivity");
            });
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public void Navigate(string navigatePath)
        {
            // 注意：这里 CurrentView 是生成器生成的属性（大写开头）
            if (navigatePath == CurrentView)
                return; // 阻止重复导航

            CurrentView = navigatePath;

            if (navigatePath != null)
                _regionManager.RequestNavigate("AnalysisContentRegion", navigatePath);
        }
    }
}