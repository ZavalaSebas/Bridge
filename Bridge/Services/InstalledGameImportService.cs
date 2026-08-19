using System.IO;
using Bridge.Core.Contracts;
using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Core.Utilities;
using Bridge.Resources;

namespace Bridge.Services;

/// <summary>
/// Shared import logic for Scan Automatically and the watched installed-games folder.
/// </summary>
public sealed class InstalledGameImportService
{
    private readonly InstalledGameDetector _detector;
    private readonly IGameRepository _gameRepository;

    public InstalledGameImportService(InstalledGameDetector detector, IGameRepository gameRepository)
    {
        _detector = detector;
        _gameRepository = gameRepository;
    }

    public IReadOnlyList<InstalledGameCandidate> ScanFolder(string folder) =>
        _detector.ScanFolder(folder);

    public IReadOnlyList<InstalledGameCandidate> DedupeCandidates(IReadOnlyList<InstalledGameCandidate> candidates) =>
        candidates
            .GroupBy(c => (
                Name: InstalledNameNormalizer.Normalize(c.Name),
                Folder: c.WorkingDirectory?.TrimEnd('\\', '/').ToLowerInvariant() ?? string.Empty))
            .Select(g => g.OrderByDescending(ExeSize).First())
            .ToList();

    public (bool IsImported, string Reason) IsAlreadyImported(
        IReadOnlyList<Game> existing, string name, string executablePath)
    {
        var pathMatch = existing
            .SelectMany(g => g.GameActions.Where(a => a.Type == GameActionType.File))
            .Any(a => a.Path.Equals(executablePath, StringComparison.OrdinalIgnoreCase));
        if (pathMatch)
        {
            return (true, "file action path match");
        }

        var candidateDir = Path.GetDirectoryName(executablePath) ?? string.Empty;
        var dirMatch = existing
            .Where(g => !string.IsNullOrWhiteSpace(g.InstallDirectory))
            .Any(g => PathContainment.IsPathUnderDirectory(
                candidateDir,
                g.InstallDirectory.TrimEnd('\\', '/')));
        if (dirMatch)
        {
            return (true, "install directory match");
        }

        var normalized = InstalledNameNormalizer.Normalize(name);
        var nameMatch = existing.Any(g => InstalledNameNormalizer.Normalize(g.Name) == normalized);
        if (nameMatch)
        {
            return (true, $"normalized name '{normalized}'");
        }

        return (false, string.Empty);
    }

    public InstalledGameImportResult ImportCandidates(IEnumerable<InstalledGameCandidate> candidates)
    {
        var existing = _gameRepository.GetAll();
        var skipped = new List<string>();
        var added = new List<Game>();

        foreach (var candidate in candidates)
        {
            if (IsAlreadyImported(existing, candidate.Name, candidate.ExecutablePath).IsImported)
            {
                continue;
            }

            var duplicate = existing.Any(g =>
                g.GameActions.Any(a => a.Type == GameActionType.File &&
                    a.Path.Equals(candidate.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                || g.Name.Equals(candidate.Name, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                skipped.Add(candidate.Name);
                continue;
            }

            var game = CreateGame(candidate);
            _gameRepository.Add(game);
            added.Add(game);
            existing = _gameRepository.GetAll();
        }

        return new InstalledGameImportResult(added, skipped);
    }

    public InstalledGameImportResult ImportNewFromFolder(string folder)
    {
        var candidates = DedupeCandidates(ScanFolder(folder));
        var existing = _gameRepository.GetAll();
        var toImport = candidates
            .Where(c => !IsAlreadyImported(existing, c.Name, c.ExecutablePath).IsImported)
            .ToList();
        return ImportCandidates(toImport);
    }

    private static Game CreateGame(InstalledGameCandidate candidate)
    {
        var game = new Game
        {
            Name = candidate.Name,
            IsInstalled = true,
            InstallDirectory = candidate.WorkingDirectory ?? string.Empty,
            Icon = candidate.ExecutablePath
        };
        game.GameActions.Add(new GameAction
        {
            Name = Strings.Play,
            Type = GameActionType.File,
            IsPlayAction = true,
            Path = candidate.ExecutablePath,
            Arguments = candidate.Arguments ?? string.Empty,
            WorkingDirectory = candidate.WorkingDirectory ?? string.Empty
        });
        return game;
    }

    private static long ExeSize(InstalledGameCandidate candidate)
    {
        try
        {
            return File.Exists(candidate.ExecutablePath)
                ? new FileInfo(candidate.ExecutablePath).Length
                : 0;
        }
        catch
        {
            return 0;
        }
    }
}

public sealed record InstalledGameImportResult(
    IReadOnlyList<Game> Added,
    IReadOnlyList<string> Skipped);
