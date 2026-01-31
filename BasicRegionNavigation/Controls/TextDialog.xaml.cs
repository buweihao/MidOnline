using Core;
using HandyControl;
using HandyControl.Controls;
using MyConfig;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Input;

namespace MyConfig.Controls;

public partial class TextDialog
{
    ConfigHelper configHelper;
    public TextDialog(string parameter, ConfigHelper _configHelper)
    {
        InitializeComponent();
        Parameter = parameter;
        DataContext = this;
        configHelper = _configHelper;
        LoadIpNodes();
    }

    public ICommand ConfirmCommand
    {
        get => (ICommand)GetValue(ConfirmCommandProperty);
        set => SetValue(ConfirmCommandProperty, value);
    }

    public string Parameter { get; set; }

    public static readonly DependencyProperty ConfirmCommandProperty =
        DependencyProperty.Register(nameof(ConfirmCommand), typeof(ICommand), typeof(TextDialog), new PropertyMetadata(null));
    public ObservableCollection<IpNode> IpNodes { get; set; } = new();

    private void LoadIpNodes()
    {
    }
}

public static class MyConfigCommand
{
    public static ConfigHelper? configHelper;
    private static HandyControl.Controls.Dialog dialog;
    public static void ShowText(string element)
    {
        if (configHelper == null)
            throw new ArgumentNullException(nameof(configHelper) + "还没有将configHelper传入");
        dialog = HandyControl.Controls.Dialog.Show(new TextDialog(element, configHelper) { ConfirmCommand = ConfirmCommand });
    }
    public static event Action? Confirmed;

    public static ICommand ConfirmCommand => new RelayCommand<object>(OnConfirm);

    private static void OnConfirm(object param)
    {
    }


}

public class IpNode : INotifyPropertyChanged
{
    private string _value;

    public string Key { get; set; }
    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
