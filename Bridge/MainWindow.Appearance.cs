using System.Windows;
using System.Windows.Threading;
using Bridge.Services;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class MainWindow
{
    internal void ApplyTranslucentBackgroundSettings(bool translucent)
    {
        GameBackgroundHost.Visibility = translucent ? Visibility.Visible : Visibility.Collapsed;
        ApplyWindowBackdropDeferred(translucent);
    }

    private void ApplyWindowBackdropDeferred(bool translucent)
    {
        var target = translucent ? WindowBackdropType.Mica : WindowBackdropType.None;
        if (WindowBackdropType == target)
            return;

        // FluentWindow reconfigures WindowChrome when backdrop changes; doing that
        // synchronously during Loaded throws Freezable context errors in Wpf.Ui.
        void Apply()
        {
            if (!IsLoaded || WindowBackdropType == target)
                return;

            WindowBackdropType = target;
        }

        if (IsLoaded)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Apply);
            return;
        }

        Loaded += OnFirstLoadedApplyBackdrop;

        void OnFirstLoadedApplyBackdrop(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnFirstLoadedApplyBackdrop;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, Apply);
        }
    }
}
