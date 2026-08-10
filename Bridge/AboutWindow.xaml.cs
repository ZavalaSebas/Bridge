using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Bridge
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
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
    }
}
