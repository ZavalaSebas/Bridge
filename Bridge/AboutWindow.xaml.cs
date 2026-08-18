using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Navigation;
using Bridge.Services;
using Wpf.Ui.Controls;

namespace Bridge
{
    public partial class AboutWindow : FluentWindow
    {
        public AboutWindow(string? backgroundImage = null)
        {
            InitializeComponent();

            BackgroundArt.SourceUrl = backgroundImage;

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version?.ToString(3) ?? "?"}";

            RuntimeText.Text = $".NET {Environment.Version}";
            OsText.Text = RuntimeInformation.OSDescription;

            BetaChannelToggle.IsChecked = UpdateChannelSettingsStore.Load() == UpdateChannel.Beta;

            ProjectLinks.ItemsSource = new[]
            {
                new AboutLink("Source code", "https://github.com/ZavalaSebas/Bridge",
                    "github.com/ZavalaSebas/Bridge", SymbolRegular.Code24),
                new AboutLink("License", "https://github.com/ZavalaSebas/Bridge/blob/main/LICENSE",
                    "GNU General Public License v3.0", SymbolRegular.Document24)
            };

            SupportLinks.ItemsSource = new[]
            {
                new AboutLink("GitHub Sponsors", "https://github.com/sponsors/ZavalaSebas",
                    "Support development on GitHub", SymbolRegular.Heart24)
            };
        }

        private void Link_Click(object sender, RequestNavigateEventArgs e)
        {
            SafeLauncher.TryOpenUrl(e.Uri.AbsoluteUri);
            e.Handled = true;
        }

        // Opens a link row (AboutLink.Tag = URL) in the default browser.
        private void LinkRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { Tag: string url })
                return;

            SafeLauncher.TryOpenUrl(url);
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // Persists the update channel choice as the toggle flips; the update
        // check reads it via UpdateChannelSettingsStore on every run.
        private void BetaChannelToggle_Changed(object sender, RoutedEventArgs e)
        {
            UpdateChannelSettingsStore.Save(
                BetaChannelToggle.IsChecked == true ? UpdateChannel.Beta : UpdateChannel.Stable);
        }
    }

    /// <summary>A single row in the About dialog's link lists.</summary>
    public sealed record AboutLink(string Label, string? Url, string Detail, SymbolRegular Icon);
}