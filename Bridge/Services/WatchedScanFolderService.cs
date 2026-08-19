using System.IO;
using System.Windows.Threading;
using Bridge.ViewModels;

namespace Bridge.Services;

/// <summary>
/// Watches persisted ROM and installed-game folders and triggers silent rescans
/// when new files appear.
/// </summary>
public sealed class WatchedScanFolderService : IDisposable
{
    private const int DebounceMilliseconds = 2000;

    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounceTimer;
    private FileSystemWatcher? _romWatcher;
    private FileSystemWatcher? _installedWatcher;
    private MainViewModel? _viewModel;
    private bool _scanRomPending;
    private bool _scanInstalledPending;
    private bool _disposed;

    public WatchedScanFolderService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds) };
        _debounceTimer.Tick += (_, _) => RunPendingScans();
    }

    public void Start(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        RestartWatchers();
    }

    public void RestartWatchers()
    {
        StopWatchers();

        var romFolder = RomScanFolderSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(romFolder) && Directory.Exists(romFolder))
        {
            _romWatcher = CreateWatcher(romFolder, ScanRomPending);
        }

        var installedFolder = InstalledScanFolderSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(installedFolder) && Directory.Exists(installedFolder))
        {
            _installedWatcher = CreateWatcher(installedFolder, ScanInstalledPending);
        }
    }

    private FileSystemWatcher CreateWatcher(string folder, Action markPending)
    {
        var watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        watcher.Created += (_, _) => markPending();
        watcher.Renamed += (_, _) => markPending();
        watcher.Changed += (_, _) => markPending();
        return watcher;
    }

    private void ScanRomPending()
    {
        _scanRomPending = true;
        ScheduleDebouncedScan();
    }

    private void ScanInstalledPending()
    {
        _scanInstalledPending = true;
        ScheduleDebouncedScan();
    }

    private void ScheduleDebouncedScan()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void RunPendingScans()
    {
        _debounceTimer.Stop();
        if (_viewModel is null)
        {
            _scanRomPending = false;
            _scanInstalledPending = false;
            return;
        }

        var scanRom = _scanRomPending;
        var scanInstalled = _scanInstalledPending;
        _scanRomPending = false;
        _scanInstalledPending = false;

        if (scanRom)
        {
            var folder = RomScanFolderSettingsStore.Load();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                _viewModel.ScanRomFolderAsync(folder, silent: true).FireAndForget("WatchedScanFolderService.ROM");
            }
        }

        if (scanInstalled)
        {
            var folder = InstalledScanFolderSettingsStore.Load();
            if (!string.IsNullOrWhiteSpace(folder))
            {
                _viewModel.ScanInstalledFolderAsync(folder, silent: true).FireAndForget("WatchedScanFolderService.Installed");
            }
        }
    }

    private void StopWatchers()
    {
        _romWatcher?.Dispose();
        _installedWatcher?.Dispose();
        _romWatcher = null;
        _installedWatcher = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer.Stop();
        StopWatchers();
    }
}
