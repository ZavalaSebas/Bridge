using System.Windows;

namespace Bridge;

public partial class ScanRomWindow : Window
{
    public string RomFolder => FolderBox.Text.Trim();

    public ScanRomWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => FolderBox.Focus();
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
