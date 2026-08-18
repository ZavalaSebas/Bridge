using System.Windows;
using Wpf.Ui.Controls;

namespace Bridge.Services;

/// <summary>UI prompts from ViewModels without referencing <see cref="MessageDialogWindow"/> directly.</summary>
public interface IDialogService
{
    void Show(string message, string title, SymbolRegular? icon = null, Window? owner = null);

    void ShowWarning(string message, string title, Window? owner = null);

    bool ShowConfirm(
        string message,
        string title,
        SymbolRegular? icon = null,
        string? confirmText = null,
        string? cancelText = null,
        Window? owner = null);

    Task ShowAsync(string message, string title, SymbolRegular? icon = null, Window? owner = null);
}
