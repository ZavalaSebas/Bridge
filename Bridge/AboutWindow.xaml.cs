using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Navigation;
using Bridge.Resources;
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
            VersionText.Text = Strings.Format(nameof(Strings.VersionFormat), version?.ToString(3) ?? "?");

            RuntimeText.Text = Strings.Format(nameof(Strings.RuntimeFormat), Environment.Version);
            OsText.Text = RuntimeInformation.OSDescription;

            ProjectLinks.ItemsSource = new[]
            {
                new AboutLink(Strings.SourceCode, "https://github.com/ZavalaSebas/Bridge",
                    Strings.BridgeRepositoryPath, SymbolRegular.Code24),
                new AboutLink(Strings.License, "https://github.com/ZavalaSebas/Bridge/blob/main/LICENSE",
                    Strings.GnuGplLicense, SymbolRegular.Document24)
            };

            SupportLinks.ItemsSource = new[]
            {
                new AboutLink(Strings.KoFi, Config.KoFiUrl,
                    Strings.BuyMeACoffee, SymbolRegular.DrinkCoffee24),
                new AboutLink(Strings.GitHubSponsors, Config.GitHubSponsorsUrl,
                    Strings.SupportDevelopmentOnGitHub, SymbolRegular.Heart24)
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
    }

    /// <summary>A single row in the About dialog's link lists.</summary>
    public sealed record AboutLink(string Label, string? Url, string Detail, SymbolRegular Icon);
}
