using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Navigation;
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

            ProjectLinks.ItemsSource = new[]
            {
                new AboutLink("Source code", "https://github.com/ZavalaSebas/Bridge",
                    "github.com/ZavalaSebas/Bridge", SymbolRegular.Code24),
                new AboutLink("License", "https://github.com/ZavalaSebas/Bridge/blob/main/LICENSE",
                    "GNU General Public License v3.0", SymbolRegular.Document24)
            };

            SupportLinks.ItemsSource = new[]
            {
                new AboutLink("Buy me a coffee", "https://ko-fi.com/YOUR_KOFI",
                    "ko-fi.com", SymbolRegular.DrinkCoffee24),
                new AboutLink("GitHub Sponsors", "https://github.com/sponsors/ZavalaSebas",
                    "github.com/sponsors/ZavalaSebas", SymbolRegular.Heart24)
            };
        }

        // Opens any Hyperlink's NavigateUri in the default browser.
        private void Link_Click(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                // Missing browser/URL — nothing to do.
            }

            e.Handled = true;
        }

        // Opens a link row (AboutLink.Tag = URL) in the default browser.
        private void LinkRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button { Tag: string url })
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // Missing browser/URL — nothing to do.
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }

    /// <summary>A single row in the About dialog's link lists.</summary>
    public sealed record AboutLink(string Label, string? Url, string Detail, SymbolRegular Icon);
}