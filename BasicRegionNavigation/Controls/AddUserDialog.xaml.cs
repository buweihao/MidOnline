using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// AddUserDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AddUserDialog : UserControl
    {
        public AddUserDialog()
        {
            InitializeComponent();
        }
        public ICommand ConfirmCommand
        {
            get { return (ICommand)GetValue(ConfirmCommandProperty); }
            set { SetValue(ConfirmCommandProperty, value); }
        }

        public static readonly DependencyProperty ConfirmCommandProperty =
            DependencyProperty.Register(nameof(ConfirmCommand), typeof(ICommand), typeof(AddUserDialog));
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is UserModel user)
            {
                user.Password = ((PasswordBox)sender).Password;
            }
        }
        public object ConfirmParameter
        {
            get => GetValue(ConfirmParameterProperty);
            set => SetValue(ConfirmParameterProperty, value);
        }

        public static readonly DependencyProperty ConfirmParameterProperty =
            DependencyProperty.Register(nameof(ConfirmParameter), typeof(object), typeof(AddUserDialog));
    }
    public class UserModel : INotifyPropertyChanged
    {
        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        private string _department;
        public string Department { get => _department; set { _department = value; OnPropertyChanged(); } }

        private string _role;
        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public List<string> Roles { get; } = new() { "Admin", "User" };

        private string _phone;
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _email;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _password;
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
