using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Utilities;
using Bridge.Resources;
using Bridge.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace Bridge;

/// <summary>One row in the installed-games picker.</summary>
public class InstalledGameEntry
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public BitmapSource? Icon { get; init; }
    public bool Import { get; set; } = true;
}

public partial class ScanInstalledWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly InstalledGameDetector _detector;
    private readonly IGameRepository _gameRepository;
    private readonly ObservableCollection<InstalledGameEntry> _allCandidates = [];

    public ScanInstalledWindow(string? backgroundImage = null)
    {
        // Resolve services BEFORE InitializeComponent: the XAML's HideImported
        // checkbox is IsChecked="True", so its Checked event fires during
        // InitializeComponent and hits RefreshFilter, which needs the repo.
        var services = App.Services;
        _detector = services.GetRequiredService<InstalledGameDetector>();
        _gameRepository = services.GetRequiredService<IGameRepository>();
        InitializeComponent();
        BackgroundArt.SourceUrl = backgroundImage;
    }

    private async void DetectInstalled_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ScanAsync(() => _detector.ScanStartMenu());
        }
        catch (Exception ex)
        {
            // Start-menu enumeration can hit permission-denied subfolders on
            // corporate machines — show a friendly message instead of a raw
            // .NET exception to the global handler.
            await ShowMessageAsync(
                Strings.Format(nameof(Strings.ScanStartMenuFailedFormat), ex.Message),
                Strings.ScanTitle);
        }
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Strings.SelectFolderToScanGames };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                await ScanAsync(() => _detector.ScanFolder(dialog.FolderName));
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(ex.Message, Strings.ScanTitle);
            }
        }
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.SelectExecutable,
            Filter = Strings.ExecutablesFilter
        };
        if (dialog.ShowDialog(this) == true)
        {
            var candidate = await Task.Run(() => _detector.FromFile(dialog.FileName));
            if (candidate is null)
            {
                await ShowMessageAsync(Strings.InvalidExecutable, Strings.AddGame);
                return;
            }

            LoadCandidates([candidate]);
        }
    }

    // Runs the scan off the UI thread with a visible loading indicator and the
    // scan buttons disabled, so a slow folder walk doesn't freeze the window or
    // let the user start two scans at once.
    private async Task ScanAsync(Func<IReadOnlyList<InstalledGameCandidate>> scan)
    {
        SetScanning(true);
        try
        {
            var candidates = await Task.Run(scan);
            LoadCandidates(candidates);
        }
        finally
        {
            SetScanning(false);
        }
    }

    private void SetScanning(bool scanning)
    {
        ScanProgress.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = scanning ? Strings.Scanning : StatusText.Text;
        DetectInstalledButton.IsEnabled = !scanning;
        ScanFolderButton.IsEnabled = !scanning;
        BrowseButton.IsEnabled = !scanning;
    }

    private Task ShowMessageAsync(string message, string title) =>
        MessageDialogWindow.ShowAsync(message, title, owner: this);

    private void LoadCandidates(IReadOnlyList<InstalledGameCandidate> candidates)
    {
        _allCandidates.Clear();
        var existing = _gameRepository.GetAll();

        // A game often ships several executables that all report the same name
        // (MGS V: mgsvmgo.exe + mgsvtpp.exe, The Last of Us II: tlou-ii.exe +
        // tlou-ii-l.exe). Collapse them to the one that's most likely the real
        // game — the largest binary — so the picker shows a single entry.
        // Grouping is by install folder + name, never name alone: two different
        // games can share a generic product name ("Everything" is reported by
        // both Marathon and Where Winds Meet) and must not be merged.
        var deduped = candidates
            .GroupBy(c => (
                Name: InstalledNameNormalizer.Normalize(c.Name),
                Folder: c.WorkingDirectory?.TrimEnd('\\', '/').ToLowerInvariant() ?? string.Empty))
            .Select(g => g.OrderByDescending(ExeSize).First());

        foreach (var candidate in deduped)
        {
            var (alreadyImported, _) = IsAlreadyImported(existing, candidate.Name, candidate.ExecutablePath);
            _allCandidates.Add(new InstalledGameEntry
            {
                Name = candidate.Name,
                Path = candidate.ExecutablePath,
                Arguments = candidate.Arguments,
                WorkingDirectory = candidate.WorkingDirectory,
                Icon = ExeIconLoader.GetIcon(candidate.IconPath ?? candidate.ExecutablePath),
                Import = !alreadyImported
            });
        }

        RefreshFilter();
        StatusText.Text = Strings.Format(nameof(Strings.CandidatesFoundFormat), _allCandidates.Count);
    }

    private static long ExeSize(InstalledGameCandidate c)
    {
        try
        {
            return File.Exists(c.ExecutablePath) ? new FileInfo(c.ExecutablePath).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    // Already-imported detection covers both a file play action matching the
    // exact executable AND a normalized game name — the name match catches
    // Steam/Epic games (their play action is a URL, not a File), so a start-menu
    // shortcut for a game Bridge already imported from a store doesn't show as
    // new. Names are normalized so "AlanWake.exe" / "Genshin Impact game" match
    // "Alan Wake" / "Genshin Impact" in the library.
    private (bool IsImported, string Reason) IsAlreadyImported(
        IReadOnlyList<Game> existing, string name, string executablePath)
    {
        var pathMatch = existing
            .SelectMany(g => g.GameActions.Where(a => a.Type == GameActionType.File))
            .Any(a => a.Path.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
        if (pathMatch)
            return (true, "file action path match");

        // The candidate exe lives inside the install folder of an already
        // imported game (e.g. Steam's steamapps/common/<game>). Catches games
        // whose product name differs from the library name — "Murdered" vs
        // "MURDERED: SOUL SUSPECT", the UE "-Shipping" executables, etc.
        var candidateDir = Path.GetDirectoryName(executablePath) ?? string.Empty;
        var dirMatch = existing
            .Where(g => !string.IsNullOrWhiteSpace(g.InstallDirectory))
            .Any(g => PathContainment.IsPathUnderDirectory(
                candidateDir,
                g.InstallDirectory.TrimEnd('\\', '/')));
        if (dirMatch)
            return (true, "install directory match");

        var normalized = InstalledNameNormalizer.Normalize(name);
        var nameMatch = existing.Any(g => InstalledNameNormalizer.Normalize(g.Name) == normalized);
        if (nameMatch)
            return (true, $"normalized name '{normalized}'");

        return (false, string.Empty);
    }

    private void RefreshFilter_Click(object sender, RoutedEventArgs e) => RefreshFilter();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
        => SetAllSelected(selected: true);

    private void SelectNone_Click(object sender, RoutedEventArgs e)
        => SetAllSelected(selected: false);

    private void SetAllSelected(bool selected)
    {
        foreach (var candidate in _allCandidates)
        {
            candidate.Import = selected;
        }

        RefreshFilter();
    }

    private void RefreshFilter()
    {
        var hideImported = HideImportedCheck is { IsChecked: true };

        var existing = hideImported ? _gameRepository.GetAll() : null;
        var filtered = _allCandidates
            .Where(c => !hideImported || !IsAlreadyImported(existing!, c.Name, c.Path).IsImported)
            .ToList();

        // The Checked event fires during InitializeComponent, before the ListBox
        // and status text exist — guard against that early call.
        if (CandidatesList is not null)
            CandidatesList.ItemsSource = filtered;
        if (StatusText is not null)
            StatusText.Text = Strings.Format(nameof(Strings.CandidatesShownFormat), filtered.Count, _allCandidates.Count);
    }

    // Games persisted during AddGames_Click, so the owner can insert them into
    // the in-memory library without re-querying.
    public IReadOnlyList<Game> CreatedGames { get; private set; } = [];

    private async void AddGames_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allCandidates.Where(c => c.Import).ToList();
        if (selected.Count == 0)
        {
            await ShowMessageAsync(Strings.NoGamesSelected, Strings.AddGamesTitle);
            return;
        }

        var existing = _gameRepository.GetAll();
        var skipped = new List<string>();
        var added = new List<Game>();
        foreach (var entry in selected)
        {
            // Duplicate check covers both the exact executable (manual games, a
            // File action) and the game name (Steam/Epic games, a URL action) —
            // so re-adding something Bridge already has is caught either way.
            bool isDuplicate = existing.Any(g =>
                g.GameActions.Any(a => a.Type == GameActionType.File && a.Path.Equals(entry.Path, StringComparison.OrdinalIgnoreCase))
                || g.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));
            if (isDuplicate)
            {
                skipped.Add(entry.Name);
                continue;
            }

            var game = new Game
            {
                Name = entry.Name,
                IsInstalled = true,
                InstallDirectory = entry.WorkingDirectory ?? string.Empty,
                Icon = entry.Path // local .exe — rendered via ExeIconLoader
            };
            game.GameActions.Add(new GameAction
            {
                Name = Strings.Play,
                Type = GameActionType.File,
                IsPlayAction = true,
                Path = entry.Path,
                Arguments = entry.Arguments ?? string.Empty,
                WorkingDirectory = entry.WorkingDirectory ?? string.Empty
            });

            _gameRepository.Add(game);
            added.Add(game);
        }

        CreatedGames = added;

        // Surface skipped duplicates while this window is still open — setting
        // DialogResult closes it, and a closed window can't be a dialog owner.
        if (skipped.Count > 0)
        {
            var preview = string.Join(", ", skipped.Take(3));
            var more = skipped.Count > 3
                ? Strings.Format(nameof(Strings.MoreSkippedFormat), skipped.Count - 3)
                : string.Empty;
            await ShowMessageAsync(
                Strings.Format(nameof(Strings.AlreadyInLibrarySkippedFormat), skipped.Count, preview, more),
                Strings.AddGamesTitle);
        }

        DialogResult = true;
    }
}
