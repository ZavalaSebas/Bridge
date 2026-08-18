using Bridge.Resources;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Controls;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private Task CheckForUpdates() => CheckForUpdatesCoreAsync(promptWhenUpToDate: true);

    [RelayCommand]
    private void OpenSponsor() => SafeLauncher.TryOpenUrl(Config.PrimarySponsorUrl);

    private async Task CheckForUpdatesCoreAsync(bool promptWhenUpToDate)
    {
        if (!IsNetworkAvailable())
        {
            if (promptWhenUpToDate)
            {
                _dialogService.Show(
                    Strings.NoInternetCheckUpdates,
                    Strings.CheckForUpdates,
                    SymbolRegular.CloudOff24);
            }

            return;
        }

        try
        {
            var result = await _appUpdateService.CheckForUpdateAsync();
            switch (result.Status)
            {
                case AppUpdateStatus.UpToDate:
                    SetPendingUpdate(null);
                    if (promptWhenUpToDate)
                    {
                        _dialogService.Show(
                            result.Message ?? Strings.Format(nameof(Strings.LatestVersionMessageFormat), Config.AssemblyVersion.ToString(3)),
                            Strings.CheckForUpdates,
                            SymbolRegular.Checkmark24);
                    }

                    break;

                case AppUpdateStatus.NotApplicable:
                    if (promptWhenUpToDate)
                    {
                        _dialogService.Show(
                            result.Message ?? Strings.UpdatesNotApplicableMessage,
                            Strings.CheckForUpdates,
                            SymbolRegular.Info24);
                    }

                    break;

                case AppUpdateStatus.Failed:
                    if (promptWhenUpToDate)
                    {
                        _dialogService.Show(
                            result.Message ?? Strings.CouldNotCheckUpdatesMessage,
                            Strings.CheckForUpdates,
                            SymbolRegular.Warning24);
                    }

                    break;

                case AppUpdateStatus.UpdateAvailable:
                    var update = result.Update!;
                    await PromptAndApplyUpdateAsync(update, skippedSetsPending: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            if (promptWhenUpToDate)
            {
                _dialogService.Show(
                    Strings.Format(nameof(Strings.CouldNotCheckUpdatesFormat), ex.Message),
                    Strings.CheckForUpdates,
                    SymbolRegular.Warning24);
            }
        }
    }

    [RelayCommand]
    private Task ApplyPendingUpdate()
    {
        if (_pendingUpdate is null)
        {
            return Task.CompletedTask;
        }

        return PromptAndApplyUpdateAsync(_pendingUpdate, skippedSetsPending: true);
    }

    private void SetPendingUpdate(AppUpdateInfo? update)
    {
        _pendingUpdate = update;
        HasPendingUpdate = update is not null;
        PendingUpdateToolTip = update is null
            ? string.Empty
            : Strings.Format(nameof(Strings.PendingUpdateTooltipFormat), update.Version.ToString(3));
    }

    private async Task PromptAndApplyUpdateAsync(AppUpdateInfo update, bool skippedSetsPending)
    {
        var message = Strings.Format(
            nameof(Strings.UpdateAvailableMessageFormat),
            update.Version.ToString(3),
            Config.AssemblyVersion.ToString(3));
        if (!_dialogService.ShowConfirm(
                message,
                Strings.UpdateAvailableTitle,
                SymbolRegular.ArrowDownload24,
                confirmText: Strings.UpdateConfirm,
                cancelText: Strings.NotNow))
        {
            if (skippedSetsPending)
            {
                SetPendingUpdate(update);
            }

            return;
        }

        await ApplyUpdateAsync(update);
    }

    private async Task ApplyUpdateAsync(AppUpdateInfo update)
    {
        try
        {
            SetPendingUpdate(null);
            StatusMessage = Strings.DownloadingUpdate;
            BeginStatusProgress(indeterminate: true);
            await _appUpdateService.ApplyUpdateAsync(
                update,
                new Progress<AppUpdateProgress>(p =>
                {
                    StatusMessage = p.Message;
                    if (p.Percent is { } percent)
                        ReportStatusProgress(percent, indeterminate: false);
                    else
                        ReportStatusProgress(StatusProgress, indeterminate: true);
                }));
        }
        catch (Exception ex)
        {
            SetPendingUpdate(update);
            StatusMessage = Strings.Format(nameof(Strings.UpdateFailedFormat), ex.Message);
            _dialogService.Show(
                Strings.Format(nameof(Strings.UpdateFailedMessageFormat), ex.Message),
                Strings.UpdateFailedTitle,
                SymbolRegular.Warning24);
        }
        finally
        {
            EndStatusProgress();
        }
    }
}
