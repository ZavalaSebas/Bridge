using System.Windows;
using Wpf.Ui.Controls;

namespace Bridge.Services;

public sealed class DialogService : IDialogService
{
    public void Show(string message, string title, SymbolRegular? icon = null, Window? owner = null) =>
        MessageDialogWindow.Show(message, title, icon, owner);

    public void ShowWarning(string message, string title, Window? owner = null) =>
        MessageDialogWindow.ShowWarning(message, title, owner);

    public bool ShowConfirm(
        string message,
        string title,
        SymbolRegular? icon = null,
        string? confirmText = null,
        string? cancelText = null,
        Window? owner = null) =>
        MessageDialogWindow.ShowConfirm(message, title, icon, confirmText, cancelText, owner);

    public Task ShowAsync(string message, string title, SymbolRegular? icon = null, Window? owner = null) =>
        MessageDialogWindow.ShowAsync(message, title, icon, owner);
}
