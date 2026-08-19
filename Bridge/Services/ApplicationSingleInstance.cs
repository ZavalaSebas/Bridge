namespace Bridge.Services;

/// <summary>
/// Ensures only one Bridge process owns the library database. A second launch
/// signals the running instance to show its main window instead of starting
/// again (important when Bridge stays in the system tray after close).
/// </summary>
public static class ApplicationSingleInstance
{
    private const string MutexName = @"Global\ZavalaSebas.Bridge.SingleInstance";
    private const string ShowWindowEventName = @"Global\ZavalaSebas.Bridge.ShowWindow";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showWindowEvent;
    private static CancellationTokenSource? _listenerCancellation;

    public static bool TryBecomeOwner()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
            return true;
        }

        SignalExistingInstance();
        return false;
    }

    public static void ListenForShowWindowRequests(Action showMainWindow)
    {
        if (_showWindowEvent is null)
            return;

        _listenerCancellation?.Cancel();
        _listenerCancellation = new CancellationTokenSource();
        var token = _listenerCancellation.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!_showWindowEvent.WaitOne(TimeSpan.FromSeconds(1)))
                        continue;
                }
                catch (AbandonedMutexException)
                {
                    continue;
                }

                if (token.IsCancellationRequested)
                    break;

                System.Windows.Application.Current.Dispatcher.Invoke(showMainWindow);
            }
        }, token);
    }

    public static void Dispose()
    {
        _listenerCancellation?.Cancel();
        _listenerCancellation = null;
        _showWindowEvent?.Dispose();
        _showWindowEvent = null;

        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // Already released or never acquired.
        }

        _mutex.Dispose();
        _mutex = null;
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
            showEvent.Set();
        }
        catch
        {
            // The owner may still be starting — nothing to activate.
        }
    }
}
