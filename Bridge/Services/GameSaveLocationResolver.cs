using System.IO;
using Bridge.Core.Entities;
using Bridge.Emulation;
using Bridge.Import.Epic;
using Bridge.Import.Steam;

namespace Bridge.Services;

/// <summary>
/// Best-effort save folder candidates: RetroArch for ROMs (authoritative),
/// then Steam Cloud / Epic Cloud caches, then common Windows / install names.
/// Steam, Epic, and other PC games still need the user to confirm a folder
/// before Bridge backs it up — these paths are suggestions, not guarantees.
/// </summary>
public static class GameSaveLocationResolver
{
    private static readonly string[] InstallSaveFolderNames =
        ["saves", "save", "SaveData", "savedata", "Saved Games", "SavedGames"];

    public static string? TryResolve(Game game, GameSaveLocationOptions options)
    {
        if (IsRomGame(game, options) &&
            TryRomSave(game, options.RetroArchInstallPath) is { } romSave)
        {
            return romSave;
        }

        if (GameDetailLinkResolver.TryResolveSteamAppId(game, out var appId) &&
            SteamCloudSaveLocator.TryFind(options.SteamInstallPath, appId.ToString(System.Globalization.CultureInfo.InvariantCulture)) is { } steamSave)
        {
            return steamSave;
        }

        var epicAppName = string.IsNullOrWhiteSpace(game.ExternalId) ? null : game.ExternalId;
        var localAppData = options.LocalApplicationData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (EpicCloudSaveLocator.TryFind(localAppData, epicAppName) is { } epicSave)
            return epicSave;

        return TryHeuristic(game, options);
    }

    private static bool IsRomGame(Game game, GameSaveLocationOptions options) =>
        options.IsManagedRom || game.Roms.Exists(rom => !string.IsNullOrWhiteSpace(rom.Path));

    private static string? TryRomSave(Game game, string? retroArchInstallPath)
    {
        var romPath = game.Roms.FirstOrDefault(rom => !string.IsNullOrWhiteSpace(rom.Path))?.Path;
        return RetroArchSaveLocator.TryFind(retroArchInstallPath, romPath);
    }

    private static string? TryHeuristic(Game game, GameSaveLocationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(game.InstallDirectory) && Directory.Exists(game.InstallDirectory))
        {
            foreach (var name in InstallSaveFolderNames)
            {
                var nested = Path.Combine(game.InstallDirectory, name);
                if (Directory.Exists(nested))
                    return nested;
            }
        }

        var folderName = SanitizeFolderName(game.Name);
        if (string.IsNullOrWhiteSpace(folderName))
            return null;

        foreach (var root in EnumerateHeuristicRoots(options))
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                continue;

            var candidate = Path.Combine(root, folderName);
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateHeuristicRoots(GameSaveLocationOptions options)
    {
        foreach (var extra in options.ExtraSearchRoots)
            yield return extra;

        var profile = options.UserProfile
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var documents = options.Documents
            ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var appData = options.ApplicationData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = options.LocalApplicationData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(profile))
            yield return Path.Combine(profile, "Saved Games");
        if (!string.IsNullOrWhiteSpace(documents))
        {
            yield return Path.Combine(documents, "My Games");
            yield return Path.Combine(documents, "Saved Games");
        }

        if (!string.IsNullOrWhiteSpace(appData))
            yield return appData;
        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return localAppData;
    }

    internal static string SanitizeFolderName(string name)
    {
        var trimmed = name.Trim();
        var chars = Path.GetInvalidFileNameChars();
        var cleaned = new string(trimmed.Where(ch => Array.IndexOf(chars, ch) < 0).ToArray());
        return cleaned.Trim();
    }
}

public sealed class GameSaveLocationOptions
{
    public string? SteamInstallPath { get; init; }
    public string? RetroArchInstallPath { get; init; }
    public bool IsManagedRom { get; init; }
    public string? UserProfile { get; init; }
    public string? Documents { get; init; }
    public string? ApplicationData { get; init; }
    public string? LocalApplicationData { get; init; }
    public IReadOnlyList<string> ExtraSearchRoots { get; init; } = [];
}
