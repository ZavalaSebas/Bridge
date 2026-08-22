using System.Windows;
using Bridge.Services;
using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class SteamGridDbSettingsWindow : FluentWindow
{
    private readonly SteamGridDbSettingsViewModel _viewModel;
    private SteamGridDbBrowserWindow? _browserWindow;

    public SteamGridDbSettingsWindow(SteamGridDbSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ApiKeyBox.Password = viewModel.ApiKey;
        viewModel.Saved += () => DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ApiKey = ApiKeyBox.Password;
        _viewModel.SaveCommand.Execute(null);
    }

    private void OpenInBrowser_Click(object sender, RoutedEventArgs e) =>
        SafeLauncher.TryOpenUrl(SteamGridDbUrls.ApiPreferences);

    private void OpenInApp_Click(object sender, RoutedEventArgs e)
    {
        if (_browserWindow is { IsLoaded: true })
        {
            _browserWindow.Activate();
            return;
        }

        _browserWindow = new SteamGridDbBrowserWindow { Owner = this };
        _browserWindow.Closed += (_, _) => _browserWindow = null;
        _browserWindow.Show();
    }
}
