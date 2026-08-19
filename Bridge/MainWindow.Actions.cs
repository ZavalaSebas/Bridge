using System.Windows;
using Bridge.Services;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge;

public partial class MainWindow
{
    private void OpenSupportLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { Tag: string url })
            SafeLauncher.TryOpenUrl(url);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var background = (DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
        var window = new AboutWindow(background) { Owner = this };
        window.ShowDialog();
    }

    // Edit game in the dedicated editor window; details panel stays read-only.
    internal void HandleEditGameClick(object sender, RoutedEventArgs e) => EditGame_Click(sender, e);

    internal void HandleScanInstalledClick(object sender, RoutedEventArgs e) => ScanInstalled_Click(sender, e);

    internal void HandleScanRomClick(object sender, RoutedEventArgs e) => ScanRom_Click(sender, e);

    private void EditGame_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainVm || mainVm.SelectedGame is not { } game)
        {
            return;
        }

        var editViewModel = App.Services.GetRequiredService<GameEditViewModelFactory>().Create(game);

        var window = new GameEditWindow(editViewModel, game.BackgroundImage) { Owner = this };
        if (window.ShowDialog() == true)
        {
            mainVm.RefreshGameDisplay(game);
        }
    }

    // Window construction stays in code-behind, matching DEVELOPMENT.md's
    // own "Credits / About Dialog" pattern — not every dialog needs a
    // MainViewModel command just to open it.
    private void ConfigureEmulator_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = App.Services.GetRequiredService<EmulationSettingsViewModel>();
        var window = new EmulationSettingsWindow(viewModel) { Owner = this };
        window.ShowDialog();
    }

    private void IgdbSettings_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = App.Services.GetRequiredService<IgdbSettingsViewModel>();
        var window = new IgdbSettingsWindow(viewModel) { Owner = this };
        window.ShowDialog();
    }

    private void AddGame_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel mainVm)
        {
            return;
        }

        var game = new Bridge.Core.Entities.Game();
        var editViewModel = App.Services.GetRequiredService<GameEditViewModelFactory>().Create(game, isNew: true);

        var window = new GameEditWindow(editViewModel, game.BackgroundImage ?? mainVm.SelectedGame?.BackgroundImage) { Owner = this };
        if (window.ShowDialog() == true)
        {
            mainVm.AddGameToLibrary(game);
        }
    }

    private async void ScanInstalled_Click(object sender, RoutedEventArgs e)
    {
        var background = (DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
        var window = new ScanInstalledWindow(background) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            foreach (var game in window.CreatedGames)
            {
                viewModel.AddGameToLibrary(game);
            }

            if (!string.IsNullOrWhiteSpace(window.LastScannedFolder))
            {
                InstalledScanFolderSettingsStore.Save(window.LastScannedFolder);
                viewModel.RestartWatchedScanFolders();
            }

            if (window.CreatedGames.Count > 0)
            {
                await viewModel.DownloadMetadataForAddedGamesAsync(window.CreatedGames);
            }
        }
    }

    private async void ScanRom_Click(object sender, RoutedEventArgs e)
    {
        var background = (DataContext as MainViewModel)?.SelectedGame?.BackgroundImage;
        var window = new ScanRomWindow(background) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.ScanRomFolderAsync(window.RomFolder);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        RequestExit();
    }
}
