using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Bridge.Services;

/// <summary>A detected game candidate: the executable to launch and its icon.</summary>
public sealed record InstalledGameCandidate(string Name, string ExecutablePath, string? Arguments, string? WorkingDirectory, string? IconPath);

/// <summary>
/// Detects games installed on the PC that aren't covered by a library plugin —
/// Playnite's "Scan Automatically / Add Game Installed" (Programs2.cs). Three
/// sources, all returning the same candidate shape:
///  - Start menu shortcuts (.lnk) under "All Users" and the current user
///  - Every .exe/.lnk/.bat in a user-chosen folder (recursive)
///  - A single executable the user browses to
/// Uninstallers, installers, redists and engine helpers are filtered out with
/// the same exclusion masks Playnite uses.
/// </summary>
public sealed class InstalledGameDetector
{
    private static readonly string[] FileExclusionMasks =
    [
        "uninst", "setup", @"unins\d+", "Config", "DXSETUP",
        @"vc_redist\.x64", @"vc_redist\.x86",
        @"^UnityCrashHandler32\.exe$", @"^UnityCrashHandler64\.exe$",
        @"^notification_helper\.exe$", @"^python\.exe$", @"^pythonw\.exe$",
        @"^zsync\.exe$", @"^zsyncmake\.exe$"
    ];

    private static readonly string[] FolderExceptions =
    [
        @"\Accessibility\", @"\Accessories\", @"\Administrative Tools\",
        @"\Maintenance\", @"\StartUp\", @"\Windows ", @"\Microsoft ",
        @"\system32\", @"\windows\"
    ];

    private static bool IsExcludedFile(string name) =>
        FileExclusionMasks.Any(mask => Regex.IsMatch(name, mask, RegexOptions.IgnoreCase));

    /// <summary>Shortcuts from both start-menu folders, deduped by target.</summary>
    public IReadOnlyList<InstalledGameCandidate> ScanStartMenu()
    {
        var allUsers = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
        var currentUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

        var candidates = new List<InstalledGameCandidate>();
        foreach (var folder in new[] { allUsers, currentUser }.Where(Directory.Exists))
        {
            candidates.AddRange(ScanShortcutFolder(folder));
        }

        return candidates
            .GroupBy(c => c.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>Every runnable file under a folder, recursive.</summary>
    public IReadOnlyList<InstalledGameCandidate> ScanFolder(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Folder not found: {directory}");

        var candidates = new List<InstalledGameCandidate>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
        {
            if (IsExcludedFile(Path.GetFileName(file)))
                continue;

            var ext = Path.GetExtension(file);
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(BuildFromExecutable(file));
            }
            else if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var resolved = ResolveShortcut(file);
                if (resolved is not null)
                    candidates.Add(resolved);
            }
        }

        return candidates;
    }

    /// <summary>A single executable (Browse).</summary>
    public InstalledGameCandidate? FromFile(string executablePath) =>
        Path.GetExtension(executablePath).Equals(".exe", StringComparison.OrdinalIgnoreCase)
            ? BuildFromExecutable(executablePath)
            : ResolveShortcut(executablePath);

    private IReadOnlyList<InstalledGameCandidate> ScanShortcutFolder(string folder)
    {
        var candidates = new List<InstalledGameCandidate>();
        foreach (var shortcutPath in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
        {
            if (FolderExceptions.Any(f => shortcutPath.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                continue;

            var resolved = ResolveShortcut(shortcutPath);
            if (resolved is not null)
                candidates.Add(resolved);
        }

        return candidates;
    }

    private InstalledGameCandidate? ResolveShortcut(string lnkPath)
    {
        var target = ShellLink.ResolveTarget(lnkPath);
        if (string.IsNullOrWhiteSpace(target))
            return null;

        // Ignore uninstallers/helpers and non-application links (Playnite parity).
        if (IsExcludedFile(Path.GetFileName(target)))
            return null;

        if (!Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return null;

        return new InstalledGameCandidate(
            Path.GetFileNameWithoutExtension(lnkPath),
            target,
            null,
            Path.GetDirectoryName(target),
            target);
    }

    private InstalledGameCandidate BuildFromExecutable(string exePath)
    {
        var name = GetProductName(exePath);
        return new InstalledGameCandidate(
            name,
            exePath,
            null,
            Path.GetDirectoryName(exePath),
            exePath);
    }

    private static string GetProductName(string exePath)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(exePath);
            if (!string.IsNullOrWhiteSpace(info.ProductName))
                return info.ProductName.Trim();
        }
        catch
        {
            // Unreadable version info — fall back to folder name below.
        }

        return Path.GetFileNameWithoutExtension(exePath);
    }
}
