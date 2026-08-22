using System.Windows;
using Bridge.Services;
using Bridge.ViewModels;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class RetroAchievementsSettingsWindow : FluentWindow
{
    private readonly RetroAchievementsSettingsViewModel _viewModel;

    public RetroAchievementsSettingsWindow(RetroAchievementsSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ApiKeyBox.Password = viewModel.WebApiKey;
        PasswordBox.Password = viewModel.Password;
        viewModel.Saved += () => DialogResult = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.WebApiKey = ApiKeyBox.Password;
        _viewModel.Password = PasswordBox.Password;
        _viewModel.SaveCommand.Execute(null);
    }

    private void OpenControlPanel_Click(object sender, RoutedEventArgs e) =>
        SafeLauncher.TryOpenUrl("https://retroachievements.org/controlpanel.php");
}
