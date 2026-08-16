using System.Windows;
using Wpf.Ui.Controls;

namespace Bridge;

/// <summary>
/// The app's own styled message dialog. A borderless, transparent window so the
/// rounded card (with the app's accent color, card background and separator
/// border) floats on its own — no stock Windows chrome, no surrounding frame.
/// Same shape as a standard message box — title, icon, message text and an OK
/// (optionally Cancel) button.
/// </summary>
public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow(string message, string title, SymbolRegular? icon = null)
    {
        InitializeComponent();

        MessageTitle.Text = title;
        MessageText.Text = message;

        if (icon is { } symbol)
        {
            MessageIcon.Symbol = symbol;
        }
    }

    public bool WithCancel { get; private set; }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    // Shows a single-OK dialog. Fire-and-forget from an async handler — the
    // dialog is modal so it returns once dismissed. Owned by the main window so
    // it survives a launcher window closing right after (e.g. ScanInstalledWindow
    // sets DialogResult=true before showing a skip notice).
    public static void Show(string message, string title, SymbolRegular? icon = null)
    {
        var dialog = new MessageDialogWindow(message, title, icon) { Owner = Application.Current.MainWindow };
        dialog.ShowDialog();
    }

    // Shows a dialog with OK + Cancel, returning true when OK was pressed.
    public static bool ShowConfirm(string message, string title, SymbolRegular? icon = null)
    {
        var dialog = new MessageDialogWindow(message, title, icon) { Owner = Application.Current.MainWindow };
        dialog.CancelButton.Visibility = Visibility.Visible;
        dialog.OkButton.Content = "OK";
        return dialog.ShowDialog() == true;
    }

    public static Task ShowAsync(string message, string title, SymbolRegular? icon = null)
    {
        Show(message, title, icon);
        return Task.CompletedTask;
    }
}