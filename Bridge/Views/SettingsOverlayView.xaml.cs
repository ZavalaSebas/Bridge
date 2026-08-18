using System.Windows;
using System.Windows.Controls;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge.Views;

public partial class SettingsOverlayView : UserControl
{
    public SettingsOverlayView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
            BetaChannelToggle.IsChecked = UpdateChannelSettingsStore.Load() == UpdateChannel.Beta;
    }

    private void ConfigureEmulator_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<EmulationSettingsViewModel>();
        new EmulationSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    private void IgdbSettings_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var viewModel = App.Services.GetRequiredService<IgdbSettingsViewModel>();
        new IgdbSettingsWindow(viewModel) { Owner = owner }.ShowDialog();
    }

    private void CustomThemeColor_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        new ThemeColorWindow { Owner = owner }.ShowDialog();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var background = (owner?.DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
        new AboutWindow(background) { Owner = owner }.ShowDialog();
    }

    private void BetaChannelToggle_Changed(object sender, RoutedEventArgs e)
    {
        UpdateChannelSettingsStore.Save(
            BetaChannelToggle.IsChecked == true ? UpdateChannel.Beta : UpdateChannel.Stable);
    }
}
