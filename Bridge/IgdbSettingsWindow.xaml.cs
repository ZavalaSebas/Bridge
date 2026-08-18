using System.Windows;
using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class IgdbSettingsWindow : FluentWindow
{
    private readonly IgdbSettingsViewModel _viewModel;

    public IgdbSettingsWindow(IgdbSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        SecretBox.Password = viewModel.ClientSecret;
        viewModel.Saved += () => DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClientSecret = SecretBox.Password;
        _viewModel.SaveCommand.Execute(null);
    }
}
