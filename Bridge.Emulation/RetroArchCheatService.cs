using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Bridge.Core.Entities;

namespace Bridge.Emulation;

/// Fetches and manages per-game RetroArch .cht files and writes per-game override configs.
public sealed class RetroArchCheatService
{
    private const string SourceSidecarFileName = "source.txt";

    private static readonly Regex ApplyCheatsAfterLoadLinePattern =
        new(@"^[ \t]*apply_cheats_after_load[ \t]*=.*\r?\n?", RegexOptions.Multiline);

    private static readonly Regex CheatDatabasePathLinePattern =
        new(@"^[ \t]*cheat_database_path[ \t]*=.*\r?\n?", RegexOptions.Multiline);

    private readonly HttpClient _httpClient;
    private readonly string _cheatsDirectory;

    public RetroArchCheatService(HttpClient httpClient, string cheatsDirectory)
    {
        _httpClient = httpClient;
        _cheatsDirectory = cheatsDirectory;
    }

    public async Task<CheatsResult> LoadCheatsAsync(Game game, RomPlatformDefinition platform, CancellationToken ct = default)
    {
        if (!platform.SupportsCheats)
        {
            return new CheatsResult { Outcome = CheatFetchOutcome.PlatformNotSupported };
        }

        var localPath = GetCheatFilePath(game, platform);
        if (localPath is not null && File.Exists(localPath))
        {
            return await LoadLocalAsync(localPath, ct);
        }

        var cheatBaseName = RomCheatNameResolver.GetCheatBaseName(game);
        var encodedFolder = Uri.EscapeDataString(platform.LibretroCheatFolder!);
        var encodedName = Uri.EscapeDataString(cheatBaseName);
        var rawUrl = $"{CheatDatabaseUrls.RawBaseUrl}/{encodedFolder}/{encodedName}.cht";
        var blobUrl = $"{CheatDatabaseUrls.BlobBaseUrl}/{encodedFolder}/{encodedName}.cht";

        string content;
        try
        {
            using var response = await _httpClient.GetAsync(rawUrl, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new CheatsResult { Outcome = CheatFetchOutcome.NotFound };
            }

            response.EnsureSuccessStatusCode();
            content = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new CheatsResult
            {
                Outcome = CheatFetchOutcome.FetchFailed,
                ErrorMessage = "Couldn't reach the cheat database. Check your connection and try again."
            };
        }

        var parseResult = CheatFileParser.Parse(content);
        if (!parseResult.IsValid)
        {
            return new CheatsResult
            {
                Outcome = CheatFetchOutcome.Corrupted,
                ErrorMessage = "The cheat file for this game couldn't be read — its format wasn't recognized."
            };
        }

        if (localPath is null)
        {
            return new CheatsResult { Outcome = CheatFetchOutcome.PlatformNotSupported };
        }

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllTextAsync(localPath, content, ct);
        await File.WriteAllTextAsync(GetSourceSidecarPath(game, platform)!, blobUrl, ct);

        return new CheatsResult { Outcome = CheatFetchOutcome.Success, Cheats = parseResult.Cheats, SourceFileUrl = blobUrl };
    }

    public async Task SetCheatEnabledAsync(Game game, RomPlatformDefinition platform, int cheatIndex, bool enabled, CancellationToken ct = default)
    {
        var localPath = GetCheatFilePath(game, platform)
            ?? throw new InvalidOperationException($"No known RetroArch core name for platform '{platform.PlatformName}'.");
        var content = await File.ReadAllTextAsync(localPath, ct);
        var updated = CheatFileParser.SetEnabled(content, cheatIndex, enabled);
        await File.WriteAllTextAsync(localPath, updated, ct);
    }

    public string? GetCheatDirectoryIfExists(Game game, RomPlatformDefinition platform)
    {
        var path = GetCheatFilePath(game, platform);
        return path is not null && File.Exists(path) ? GetGameRootDirectory(game) : null;
    }

    public async Task ApplyCheatLaunchOverridesAsync(
        Game game,
        RomPlatformDefinition platform,
        string retroArchExecutablePath,
        string cheatDirectory,
        bool autoApplyCheatsEnabled,
        CancellationToken ct = default)
    {
        if (!platform.SupportsCheats)
        {
            return;
        }

        var coreName = platform.RetroArchCoreName!;
        var retroArchConfigDirectory = RetroArchConfigPaths.ResolveConfigDirectory(retroArchExecutablePath);
        var cheatBaseName = RomCheatNameResolver.GetCheatBaseName(game);
        var overridePath = Path.Combine(retroArchConfigDirectory, coreName, $"{cheatBaseName}.cfg");
        var existing = File.Exists(overridePath) ? await File.ReadAllTextAsync(overridePath, ct) : string.Empty;

        var cheatDatabasePathLine = $"cheat_database_path = \"{cheatDirectory}\"\n";
        var withCheatDatabasePath = CheatDatabasePathLinePattern.IsMatch(existing)
            ? CheatDatabasePathLinePattern.Replace(existing, cheatDatabasePathLine)
            : existing + (existing.Length > 0 && !existing.EndsWith('\n') ? "\n" : "") + cheatDatabasePathLine;

        string updated;
        if (autoApplyCheatsEnabled)
        {
            const string applyLine = "apply_cheats_after_load = true\n";
            updated = ApplyCheatsAfterLoadLinePattern.IsMatch(withCheatDatabasePath)
                ? ApplyCheatsAfterLoadLinePattern.Replace(withCheatDatabasePath, applyLine)
                : withCheatDatabasePath + applyLine;
        }
        else
        {
            updated = ApplyCheatsAfterLoadLinePattern.Replace(withCheatDatabasePath, "");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(overridePath)!);
        await File.WriteAllTextAsync(overridePath, updated, ct);
    }

    private async Task<CheatsResult> LoadLocalAsync(string localPath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(localPath, ct);
        var parseResult = CheatFileParser.Parse(content);
        if (!parseResult.IsValid)
        {
            return new CheatsResult
            {
                Outcome = CheatFetchOutcome.Corrupted,
                ErrorMessage = "This game's saved cheat file couldn't be read — it may be corrupted."
            };
        }

        var sourcePath = Path.Combine(Path.GetDirectoryName(localPath)!, SourceSidecarFileName);
        var sourceUrl = File.Exists(sourcePath) ? await File.ReadAllTextAsync(sourcePath, ct) : null;

        return new CheatsResult { Outcome = CheatFetchOutcome.Success, Cheats = parseResult.Cheats, SourceFileUrl = sourceUrl };
    }

    private string GetGameRootDirectory(Game game) => Path.Combine(_cheatsDirectory, game.Id.ToString());

    private string? GetCheatFilePath(Game game, RomPlatformDefinition platform) =>
        platform.SupportsCheats
            ? Path.Combine(GetGameRootDirectory(game), platform.RetroArchCoreName!, $"{RomCheatNameResolver.GetCheatBaseName(game)}.cht")
            : null;

    private string? GetSourceSidecarPath(Game game, RomPlatformDefinition platform) =>
        platform.SupportsCheats
            ? Path.Combine(GetGameRootDirectory(game), platform.RetroArchCoreName!, SourceSidecarFileName)
            : null;
}

public static class CheatDatabaseUrls
{
    public const string RawBaseUrl = "https://raw.githubusercontent.com/libretro/libretro-database/master/cht";
    public const string BlobBaseUrl = "https://github.com/libretro/libretro-database/blob/master/cht";
}
