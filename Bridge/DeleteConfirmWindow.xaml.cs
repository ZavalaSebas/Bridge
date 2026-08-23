using System.Windows;

namespace Bridge;

public partial class DeleteConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    public DeleteConfirmWindow()
    {
        InitializeComponent();
        InputBox.Focus();
    }

    private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateState();
    private void ConfirmCheck_Changed(object sender, RoutedEventArgs e) => UpdateState();

    private void UpdateState()
    {
        var textOk = string.Equals(InputBox.Text?.Trim(), "BORRAR", StringComparison.Ordinal);
        var checkOk = ConfirmCheck.IsChecked == true;
        ConfirmButton.IsEnabled = textOk && checkOk;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
