using BasicRegionNavigation.Controls; // 假设 AddUserDialog 在这里
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Core;
using DocumentFormat.OpenXml.Wordprocessing;
using HandyControl.Controls; // For Dialog
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dialog = HandyControl.Controls.Dialog;
using MessageBox = System.Windows.MessageBox; // 明确引用

namespace BasicRegionNavigation.ViewModels
{
    internal partial class UserManageViewModel : ObservableObject, INavigationAware
    {
        // -----------------------------------------------------------------------
        // 字段与属性 (Fields & Properties)
        // -----------------------------------------------------------------------

        // 对应原本的 AllUsers
        [ObservableProperty]
        private ObservableCollection<UserInfo> _allUsers = new ObservableCollection<UserInfo>();

        // 钩子：当 AllUsers 列表整体被替换时，刷新分页
        partial void OnAllUsersChanged(ObservableCollection<UserInfo> value)
        {
            UpdatePage();
            OnPropertyChanged(nameof(TotalPages));
        }

        // 对应原本的 PagedUsers
        [ObservableProperty]
        private ObservableCollection<UserInfo> _pagedUsers;

        // 对应 CurrentPage
        [ObservableProperty]
        private int _currentPage = 1;

        // 钩子：页码变化时刷新
        partial void OnCurrentPageChanged(int value)
        {
            UpdatePage();
        }

        // 对应 PageSize
        [ObservableProperty]
        private int _pageSize = 10;

        // 钩子：页大小变化时刷新
        partial void OnPageSizeChanged(int value)
        {
            UpdatePage();
            OnPropertyChanged(nameof(TotalPages));
        }

        public int TotalPages
        {
            get
            {
                if (AllUsers == null || AllUsers.Count == 0) return 1;
                return (int)Math.Ceiling((double)AllUsers.Count / PageSize);
            }
        }

        // 弹窗相关
        private UserModel _newUser;
        private Dialog _dialogInstance; // 保存 Dialog 实例以便关闭

        // -----------------------------------------------------------------------
        // 构造函数
        // -----------------------------------------------------------------------
        public UserManageViewModel()
        {
            // 空构造，不再需要手动初始化 Command
        }

        // -----------------------------------------------------------------------
        // 导航 (Navigation)
        // -----------------------------------------------------------------------
        public void OnNavigatedTo(NavigationContext context)
        {
            // 进入页面自动查询
            if (QueryCommand.CanExecute(null))
            {
                QueryCommand.Execute(null);
            }
        }

        public void OnNavigatedFrom(NavigationContext context) { }
        public bool IsNavigationTarget(NavigationContext context) => true;

        // -----------------------------------------------------------------------
        // 辅助方法 (Methods)
        // -----------------------------------------------------------------------

        public void UpdatePage()
        {
            if (AllUsers == null) return;

            // 范围校验
            if (TotalPages > 0 && CurrentPage > TotalPages) CurrentPage = TotalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var items = AllUsers
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            PagedUsers = new ObservableCollection<UserInfo>(items);
        }

        // -----------------------------------------------------------------------
        // 命令 (Commands)
        // -----------------------------------------------------------------------

        [RelayCommand]
        private void ShowAddUserDialog(string element)
        {
            _newUser = new UserModel(); // 新建模式

            // 显示弹窗
            _dialogInstance = Dialog.Show(new AddUserDialog
            {
                DataContext = _newUser,
                // 这里需要绑定 ViewModel 中的 Command
                // 注意：在 View 中绑定 Command 时，Command 参数需要手动指向
                ConfirmCommand = ConfirmCommand
            });
        }

        [RelayCommand]
        private async Task ConfirmAsync(object param)
        {
        }

        [RelayCommand]
        private void Edit(UserInfo item)
        {
            if (item == null) return;

            // 映射到编辑模型
            _newUser = new UserModel()
            {
                Name = item.Name,
                Department = item.Department,
                Email = item.Email,
                Role = item.Role,
                Phone = item.Phone,
                Password = "KeepUnchanged" // 这里的逻辑视业务而定，通常不回显密码
            };

            _dialogInstance = Dialog.Show(new AddUserDialog
            {
                DataContext = _newUser,
                ConfirmCommand = UpdateCommand,
                ConfirmParameter = item // 把原始 UserInfo 传进去以便获取 ID
            });
        }

        [RelayCommand]
        private async Task UpdateAsync(object param)
        {
        }

        [RelayCommand]
        private async Task QueryAsync()
        {
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

        [RelayCommand]
        private void Delete(UserInfo item)
        {
            // 保持权限校验逻辑
            Global.SecureRoleExecute<UserInfo>(async (u) => await DeleteFunc(u), item);
        }

        // 删除的具体逻辑
        [RequireRole(Role.Admin)]
        public async Task DeleteFunc(UserInfo item)
        {
            var result = MessageBox.Show(
                $"确定要删除用户【{item.Name}】吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                Global.LoadingManager.StartLoading();

            }
            catch (Exception e)
            {
                // 处理异常
                MessageBox.Show($"删除失败: {e.Message}");
            }
            finally
            {
                await Task.Delay(200);
                Global.LoadingManager.StopLoading();
                await QueryAsync();
            }
        }
    }

    // 建议也将 UserInfo 和 UserModel 改造为 ObservableObject
    public partial class UserInfo : ObservableObject
    {
        [ObservableProperty] private int _index;
        [ObservableProperty] private long _id;
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _department;
        [ObservableProperty] private string _role;
        [ObservableProperty] private string _phone;
        [ObservableProperty] private string _email;
    }

    // 用于 Dialog 的 Model
    public partial class UserModel : ObservableObject
    {
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _password;
        [ObservableProperty] private string _department;
        [ObservableProperty] private string _role;
        [ObservableProperty] private string _phone;
        [ObservableProperty] private string _email;
    }
}