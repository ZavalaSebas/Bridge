using System.Windows;
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
    }
}