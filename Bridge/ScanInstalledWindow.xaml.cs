using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

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

public partial class ScanInstalledWindow : Window
{
    private readonly InstalledGameDetector _detector;
    private readonly IGameRepository _gameRepository;
    private readonly ObservableCollection<InstalledGameEntry> _allCandidates = [];

    public ScanInstalledWindow()
    {
        // Resolve services BEFORE InitializeComponent: the XAML's HideImported
        // checkbox is IsChecked="True", so its Checked event fires during
        // InitializeComponent and hits RefreshFilter, which needs the repo.
        var services = App.Services;
        _detector = services.GetRequiredService<InstalledGameDetector>();
        _gameRepository = services.GetRequiredService<IGameRepository>();
        InitializeComponent();
    }

    private void DetectInstalled_Click(object sender, RoutedEventArgs e)
    {
        LoadCandidates(_detector.ScanStartMenu());
    }

    private void ScanFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select folder to scan for games" };
        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                LoadCandidates(_detector.ScanFolder(dialog.FolderName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Scan", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select executable",
            Filter = "Executables (*.exe;*.lnk)|*.exe;*.lnk"
        };
        if (dialog.ShowDialog(this) == true)
        {
            var candidate = _detector.FromFile(dialog.FileName);
            if (candidate is null)
            {
                MessageBox.Show(this, "Not a valid executable.", "Add game", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadCandidates([candidate]);
        }
    }

    private void LoadCandidates(IReadOnlyList<InstalledGameCandidate> candidates)
    {
        var importedPaths = _gameRepository.GetAll()
            .SelectMany(g => g.GameActions.Where(a => a.Type == GameActionType.File))
            .Select(a => a.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allCandidates.Clear();
        foreach (var candidate in candidates)
        {
            _allCandidates.Add(new InstalledGameEntry
            {
                Name = candidate.Name,
                Path = candidate.ExecutablePath,
                Arguments = candidate.Arguments,
                WorkingDirectory = candidate.WorkingDirectory,
                Icon = ExeIconLoader.GetIcon(candidate.IconPath ?? candidate.ExecutablePath),
                Import = !importedPaths.Contains(candidate.ExecutablePath)
            });
        }

        RefreshFilter();
        StatusText.Text = $"{_allCandidates.Count} found";
    }

    private void RefreshFilter_Click(object sender, RoutedEventArgs e) => RefreshFilter();

    private void RefreshFilter()
    {
        var hideImported = HideImportedCheck is { IsChecked: true };
        var importedPaths = hideImported
            ? _gameRepository.GetAll()
                .SelectMany(g => g.GameActions.Where(a => a.Type == GameActionType.File))
                .Select(a => a.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var filtered = _allCandidates
            .Where(c => importedPaths is null || !importedPaths.Contains(c.Path))
            .ToList();

        // The Checked event fires during InitializeComponent, before the ListBox
        // and status text exist — guard against that early call.
        if (CandidatesList is not null)
            CandidatesList.ItemsSource = filtered;
        if (StatusText is not null)
            StatusText.Text = $"{filtered.Count} of {_allCandidates.Count} shown";
    }

    // Games persisted during AddGames_Click, so the owner can insert them into
    // the in-memory library without re-querying.
    public IReadOnlyList<Game> CreatedGames { get; private set; } = [];

    private void AddGames_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allCandidates.Where(c => c.Import).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "No games selected.", "Add games", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var added = new List<Game>();
        foreach (var entry in selected)
        {
            var existing = _gameRepository.GetAll()
                .FirstOrDefault(g => g.GameActions.Any(a => a.Type == GameActionType.File && a.Path.Equals(entry.Path, StringComparison.OrdinalIgnoreCase)));
            if (existing is not null)
            {
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
                Name = "Play",
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
        DialogResult = true;
    }
}
