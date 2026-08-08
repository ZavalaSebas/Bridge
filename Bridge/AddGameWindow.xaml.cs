using System.Windows;

namespace Bridge;

public partial class AddGameWindow : Window
{
    public string GameName => NameBox.Text.Trim();

    public AddGameWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
