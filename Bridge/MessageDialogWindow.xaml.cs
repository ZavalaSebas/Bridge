using System.Windows;
using Wpf.Ui.Controls;

namespace Bridge;

/// <summary>
/// The app's own styled message dialog — a <see cref="FluentWindow"/> with Mica,
/// a custom title bar and themed buttons, matching windows like
/// <see cref="AboutWindow"/> instead of a floating card or stock MessageBox.
/// </summary>
public partial class MessageDialogWindow : FluentWindow
{
    public MessageDialogWindow(string message, string title, SymbolRegular? icon = null)
    {
        InitializeComponent();

        Title = title;
        MessageTitle.Text = title;
        MessageText.Text = message;

        if (icon is { } symbol)
        {
            MessageIcon.Symbol = symbol;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public static void Show(string message, string title, SymbolRegular? icon = null, Window? owner = null)
    {
        var dialog = new MessageDialogWindow(message, title, icon)
        {
            Owner = owner ?? Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    public static void ShowWarning(string message, string title, Window? owner = null) =>
        Show(message, title, SymbolRegular.Warning24, owner);

    public static bool ShowConfirm(
        string message,
        string title,
        SymbolRegular? icon = null,
        string? confirmText = null,
        string? cancelText = null)
    {
        var dialog = new MessageDialogWindow(message, title, icon) { Owner = Application.Current.MainWindow };
        dialog.CancelButton.Visibility = Visibility.Visible;
        dialog.OkButton.Content = confirmText ?? "OK";
        dialog.CancelButton.Content = cancelText ?? "Cancel";
        return dialog.ShowDialog() == true;
    }

    public static Task ShowAsync(string message, string title, SymbolRegular? icon = null)
    {
        Show(message, title, icon);
        return Task.CompletedTask;
    }
}
