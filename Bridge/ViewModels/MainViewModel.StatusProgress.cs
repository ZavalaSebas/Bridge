using CommunityToolkit.Mvvm.ComponentModel;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    // Shared status-bar progress for long-running work: metadata sync/download,
    // artwork preload, app updates, and RetroArch install.
    [ObservableProperty]
    private bool _showStatusProgress;

    [ObservableProperty]
    private double _statusProgress;

    [ObservableProperty]
    private bool _isStatusProgressIndeterminate;

    private int _statusProgressDepth;

    private void BeginStatusProgress(bool indeterminate = true)
    {
        if (_statusProgressDepth++ == 0)
        {
            ShowStatusProgress = true;
            IsStatusProgressIndeterminate = indeterminate;
            StatusProgress = 0;
        }
    }

    private void ReportStatusProgress(double percent, bool? indeterminate = null)
    {
        if (indeterminate.HasValue)
            IsStatusProgressIndeterminate = indeterminate.Value;

        StatusProgress = Math.Clamp(percent, 0, 100);
        if (!ShowStatusProgress)
            ShowStatusProgress = true;
    }

    private void ReportBatchProgress(int completed, int total)
    {
        ReportStatusProgress(
            total > 0 ? completed * 100.0 / total : 0,
            indeterminate: total <= 0);
    }

    private void EndStatusProgress()
    {
        if (_statusProgressDepth <= 0)
            return;

        if (--_statusProgressDepth == 0)
        {
            ShowStatusProgress = false;
            IsStatusProgressIndeterminate = false;
            StatusProgress = 0;
        }
    }

    private void RunOnUiThread(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher.CheckAccess() == true)
            action();
        else
            app?.Dispatcher.InvokeAsync(action);
    }
}
