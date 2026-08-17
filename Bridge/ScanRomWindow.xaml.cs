using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class ScanRomWindow : FluentWindow
{
    public string RomFolder => FolderBox.Text.Trim();

    public ScanRomWindow(string? backgroundImage = null)
    {
        InitializeComponent();
        BackgroundArt.SourceUrl = backgroundImage;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select ROM folder" };
        if (dialog.ShowDialog(this) == true)
        {
            FolderBox.Text = dialog.FolderName;
        }
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FolderBox.Text))
        {
            System.Windows.MessageBox.Show(this, "Select a folder to scan.", "Scan ROMs", System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
