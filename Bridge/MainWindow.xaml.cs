using System.Windows;
using Bridge.Core.Entities;
using Bridge.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Bridge
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Window construction stays in code-behind, matching DEVELOPMENT.md's
        // own "Credits / About Dialog" pattern — not every dialog needs a
        // MainViewModel command just to open it.
        private void ConfigureEmulator_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = App.Services.GetRequiredService<EmulatorSetupViewModel>();
            var window = new EmulatorSetupWindow(viewModel) { Owner = this };
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
            var window = new AddGameWindow { Owner = this };
            if (window.ShowDialog() != true)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.AddGameCommand.Execute(window.GameName);
            }
        }

        private void ScanRom_Click(object sender, RoutedEventArgs e)
        {
            var window = new ScanRomWindow { Owner = this };
            if (window.ShowDialog() != true)
            {
                return;
            }

            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ScanRomFolderCommand.Execute(window.RomFolder);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Opens the compact info window (all details + description, no images)
        // from a cover's hover button. Uses the hovered game, not the list
        // selection — SelectedGame is set so the resolved DevelopersText/
        // PublishersText/PlatformsText refresh before the window binds.
        private void GameInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement { Tag: Game game })
            {
                var window = new GameInfoWindow { Owner = this, DataContext = DataContext };
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.SelectedGame = game;
                }
                window.ShowDialog();
            }
        }
    }
}