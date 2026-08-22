using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Bridge.Assets;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
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
    public ImageSource? Icon { get; init; }
    public bool Import { get; set; } = true;
}

public partial class ScanInstalledWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly InstalledGameDetector _detector;
    private readonly InstalledGameImportService _importService;
    private readonly IGameRepository _gameRepository;
    private readonly ObservableCollection<InstalledGameEntry> _allCandidates = [];

    public string? LastScannedFolder { get; private set; }

    public ScanInstalledWindow(string? backgroundImage = null)
    {
        var services = App.Services;
        _detector = services.GetRequiredService<InstalledGameDetector>();
        _importService = services.GetRequiredService<InstalledGameImportService>();
        _gameRepository = services.GetRequiredService<IGameRepository>();
        InitializeComponent();
        BackgroundArt.SourceUrl = backgroundImage;
    }

    private async void DetectInstalled_Click(object sender, RoutedEventArgs e)
    {
        LastScannedFolder = null;
        try
        {
            await ScanAsync(() => _detector.ScanStartMenu());
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                Strings.Format(nameof(Strings.ScanStartMenuFailedFormat), ex.Message),
                Strings.ScanTitle);
        }
    }

    private async void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Strings.SelectFolderToScanGames,
            InitialDirectory = InstalledScanFolderSettingsStore.Load()
        };
        if (dialog.ShowDialog(this) == true)
        {
            LastScannedFolder = dialog.FolderName;
            try
            {
                await ScanAsync(() => _importService.ScanFolder(dialog.FolderName));
            }
            catch (Exception ex)
            {
                await ShowMessageAsync(ex.Message, Strings.ScanTitle);
            }
        }
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        LastScannedFolder = null;
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
        var deduped = _importService.DedupeCandidates(candidates);

        foreach (var candidate in deduped)
        {
            var (alreadyImported, _) = _importService.IsAlreadyImported(existing, candidate.Name, candidate.ExecutablePath);
            _allCandidates.Add(new InstalledGameEntry
            {
                Name = candidate.Name,
                Path = candidate.ExecutablePath,
                Arguments = candidate.Arguments,
                WorkingDirectory = candidate.WorkingDirectory,
                Icon = ExeIconLoader.GetIcon(candidate.IconPath ?? candidate.ExecutablePath) ?? DefaultGameIcon.Source,
                Import = !alreadyImported
            });
        }

        RefreshFilter();
        StatusText.Text = Strings.Format(nameof(Strings.CandidatesFoundFormat), _allCandidates.Count);
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
            .Where(c => !hideImported || !_importService.IsAlreadyImported(existing!, c.Name, c.Path).IsImported)
            .ToList();

        if (CandidatesList is not null)
            CandidatesList.ItemsSource = filtered;
        if (StatusText is not null)
            StatusText.Text = Strings.Format(nameof(Strings.CandidatesShownFormat), filtered.Count, _allCandidates.Count);
    }

    public IReadOnlyList<Game> CreatedGames { get; private set; } = [];

    private async void AddGames_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allCandidates.Where(c => c.Import).ToList();
        if (selected.Count == 0)
        {
            await ShowMessageAsync(Strings.NoGamesSelected, Strings.AddGamesTitle);
            return;
        }

        var candidates = selected.Select(entry => new InstalledGameCandidate(
            entry.Name,
            entry.Path,
            entry.Arguments,
            entry.WorkingDirectory,
            entry.Path));
        var result = _importService.ImportCandidates(candidates);
        CreatedGames = result.Added;

        if (result.Skipped.Count > 0)
        {
            var preview = string.Join(", ", result.Skipped.Take(3));
            var more = result.Skipped.Count > 3
                ? Strings.Format(nameof(Strings.MoreSkippedFormat), result.Skipped.Count - 3)
                : string.Empty;
            await ShowMessageAsync(
                Strings.Format(nameof(Strings.AlreadyInLibrarySkippedFormat), result.Skipped.Count, preview, more),
                Strings.AddGamesTitle);
        }

        DialogResult = true;
    }
}
