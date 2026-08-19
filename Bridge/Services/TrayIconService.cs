using System.Drawing;
using System.Windows;
using Bridge.Resources;
using WinFormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using WinFormsContextMenu = System.Windows.Forms.ContextMenuStrip;
using WinFormsToolStripSeparator = System.Windows.Forms.ToolStripSeparator;

namespace Bridge.Services;

/// <summary>
/// Notification-area icon with show/exit actions. Active only when
/// <see cref="TrayIconSettingsStore"/> is enabled.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private WinFormsNotifyIcon? _notifyIcon;
    private MainWindow? _mainWindow;
    private bool _disposed;

    public void Attach(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        Refresh();
    }

    public void Refresh()
    {
        if (TrayIconSettingsStore.Load())
            EnsureCreated();
        else
            HideIcon();
    }

    public bool TryMinimizeToTray()
    {
        if (!TrayIconSettingsStore.Load() || _mainWindow is null)
            return false;

        EnsureCreated();
        _mainWindow.Hide();
        return true;
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
            return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    public void ExitApplication()
    {
        HideIcon();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        HideIcon();
    }

    private void EnsureCreated()
    {
        if (_notifyIcon is not null)
            return;

        _notifyIcon = new WinFormsNotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = Strings.BridgeAppName,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        var menu = new WinFormsContextMenu();
        menu.Items.Add(Strings.TrayIconShowBridge, null, (_, _) => ShowMainWindow());
        menu.Items.Add(new WinFormsToolStripSeparator());
        menu.Items.Add(Strings.Exit, null, (_, _) => ExitApplication());
        _notifyIcon.ContextMenuStrip = menu;
    }

    private void HideIcon()
    {
        if (_notifyIcon is null)
            return;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    private static Icon LoadAppIcon()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            var associated = Icon.ExtractAssociatedIcon(exePath);
            if (associated is not null)
                return associated;
        }

        var stream = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/Bridge.ico", UriKind.Absolute))?.Stream;

        return stream is not null ? new Icon(stream) : SystemIcons.Application;
    }
}
