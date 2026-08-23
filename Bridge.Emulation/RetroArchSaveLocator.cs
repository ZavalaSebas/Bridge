using System.Text.RegularExpressions;

namespace Bridge.Emulation;

public enum RomSaveRole
{
    Saves,
    States,
    Content
}

public readonly record struct RomSaveFile(string Path, RomSaveRole Role, string RelativePath);

/// <summary>
/// Resolves Bridge-managed RetroArch save folders from <c>retroarch.cfg</c>:
/// SRAM (<c>saves</c>), savestates (<c>states</c>), and files next to the ROM
/// when <c>savefiles_in_content_dir</c> is on.
/// </summary>
public static class RetroArchSaveLocator
{
    private static readonly string[] SaveExtensions =
        [".srm", ".sav", ".eep", ".fla", ".rtc", ".mcr", ".mem"];

    public static string? TryFind(string? retroArchInstallPath, string? romPath)
    {
        var baseName = string.IsNullOrWhiteSpace(romPath)
            ? null
            : RomArchivePath.GetCheatBaseName(romPath);
        var contentDir = GetContentDirectory(romPath);

        if (!string.IsNullOrWhiteSpace(baseName) &&
            contentDir is not null &&
            FindSaveOrStateFile(contentDir, baseName) is not null)
        {
            return contentDir;
        }

        if (string.IsNullOrWhiteSpace(retroArchInstallPath) || !Directory.Exists(retroArchInstallPath))
            return null;

        var saves = ResolveConfiguredDirectory(retroArchInstallPath, "savefile_directory", "saves");
        var states = ResolveConfiguredDirectory(retroArchInstallPath, "savestate_directory", "states");

        if (!string.IsNullOrWhiteSpace(baseName))
        {
            if (TryFindContainingDirectory(saves, baseName) is { } saveMatch)
                return saveMatch;
            if (TryFindContainingDirectory(states, baseName) is { } stateMatch)
                return stateMatch;
        }

        return saves;
    }

    /// <summary>
    /// Lists SRAM and savestate files for a ROM: next to the ROM, then under
    /// RetroArch <c>saves/</c> and <c>states/</c> (including core subfolders).
    /// </summary>
    public static IReadOnlyList<RomSaveFile> EnumerateSaveFiles(string? retroArchInstallPath, string? romPath)
    {
        var results = new List<RomSaveFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var baseName = string.IsNullOrWhiteSpace(romPath)
            ? null
            : RomArchivePath.GetCheatBaseName(romPath);
        if (string.IsNullOrWhiteSpace(baseName))
            return results;

        void Add(string path, RomSaveRole role, string relativeRoot)
        {
            if (!seen.Add(path))
                return;

            var relative = Path.GetRelativePath(relativeRoot, path);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                relative = Path.GetFileName(path);

            results.Add(new RomSaveFile(path, role, relative.Replace('\\', '/')));
        }

        var contentDir = TryGetContentDirectory(romPath);
        if (contentDir is not null)
        {
            foreach (var file in EnumerateMatchingFiles(contentDir, baseName, SearchOption.TopDirectoryOnly))
                Add(file, RomSaveRole.Content, contentDir);
        }

        if (string.IsNullOrWhiteSpace(retroArchInstallPath) || !Directory.Exists(retroArchInstallPath))
            return results;

        var saves = ResolveSaveDirectory(retroArchInstallPath);
        if (Directory.Exists(saves))
        {
            foreach (var file in EnumerateMatchingFiles(saves, baseName, SearchOption.AllDirectories))
                Add(file, RomSaveRole.Saves, saves);
        }

        var states = ResolveStateDirectory(retroArchInstallPath);
        if (Directory.Exists(states))
        {
            foreach (var file in EnumerateMatchingFiles(states, baseName, SearchOption.AllDirectories))
                Add(file, RomSaveRole.States, states);
        }

        return results;
    }

    public static string ResolveSaveDirectory(string retroArchInstallPath) =>
        ResolveConfiguredDirectory(retroArchInstallPath, "savefile_directory", "saves");

    public static string ResolveStateDirectory(string retroArchInstallPath) =>
        ResolveConfiguredDirectory(retroArchInstallPath, "savestate_directory", "states");

    public static string? TryGetContentDirectory(string? romPath)
    {
        if (string.IsNullOrWhiteSpace(romPath))
            return null;

        var filePath = RomArchivePath.TrySplit(romPath, out var archivePath, out _)
            ? archivePath
            : romPath;
        var directory = Path.GetDirectoryName(filePath);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : null;
    }

    private static string ResolveConfiguredDirectory(string retroArchInstallPath, string key, string defaultFolder)
    {
        var fallback = Path.Combine(retroArchInstallPath, defaultFolder);
        var configPath = Path.Combine(retroArchInstallPath, "retroarch.cfg");
        if (!File.Exists(configPath))
            return fallback;

        var configured = ReadConfigValue(File.ReadAllText(configPath), key);
        if (string.IsNullOrWhiteSpace(configured) ||
            configured.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        if (configured[0] == ':')
            return Path.Combine(retroArchInstallPath, configured[1..].TrimStart('\\', '/'));

        return configured;
    }

    private static string? TryFindContainingDirectory(string root, string baseName)
    {
        if (!Directory.Exists(root))
            return null;

        if (FindSaveOrStateFile(root, baseName) is { } direct)
            return Path.GetDirectoryName(direct);

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, baseName + ".*", SearchOption.AllDirectories))
            {
                if (IsSaveOrStateFile(file, baseName))
                    return Path.GetDirectoryName(file);
            }
        }
        catch (IOException)
        {
            // Unreadable tree — fall back to the canonical folder.
        }

        return null;
    }

    private static string? FindSaveOrStateFile(string directory, string baseName)
    {
        if (!Directory.Exists(directory))
            return null;

        foreach (var extension in SaveExtensions)
        {
            var path = Path.Combine(directory, baseName + extension);
            if (File.Exists(path))
                return path;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, baseName + ".state*"))
            {
                if (IsSaveOrStateFile(file, baseName))
                    return file;
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static bool IsSaveOrStateFile(string path, string baseName)
    {
        var name = Path.GetFileName(path);
        if (!name.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
            return false;

        var extension = Path.GetExtension(name);
        if (SaveExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            return true;

        return extension.StartsWith(".state", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith(baseName + ".state", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateMatchingFiles(string directory, string baseName, SearchOption search)
    {
        if (!Directory.Exists(directory))
            yield break;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, baseName + ".*", search).ToList();
        }
        catch (IOException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (IsSaveOrStateFile(file, baseName))
                yield return file;
        }
    }

    private static string? GetContentDirectory(string? romPath) => TryGetContentDirectory(romPath);

    private static string ReadConfigValue(string content, string key)
    {
        var match = new Regex(
            $@"^[ \t]*{Regex.Escape(key)}[ \t]*=[ \t]*""?([^""\r\n]*)""?[ \t]*$",
            RegexOptions.Multiline).Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }
}
