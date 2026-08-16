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
        @"vc_redist\.x64", @"vc_redist\.x86", @"^vcredist",
        @"^dotnetfx", @"^7z\.exe$", @"^hpatchz\.exe$",
        "crashreport", @"upload_crash", @"createdump", @"breakpad_server",
        @"APM4webCrashR", @"^BeyondEditor\.exe$", @"^ZFGameBrowser\.exe$",
        @"HYPHelper", @"HYUpdater", @"^launcher_epic\.exe$", @"^HYP\.exe$",
        @"^UnityCrashHandler32\.exe$", @"^UnityCrashHandler64\.exe$",
        @"^notification_helper\.exe$", @"^python\.exe$", @"^pythonw\.exe$",
        @"^zsync\.exe$", @"^zsyncmake\.exe$",
        @"Launcher\.exe$", @"launcher\.exe$",
        // Anti-cheat, crash reporters and engine helpers — never the game.
        "EasyAntiCheat", @"BattlEye", @"BEService", "EasyAntiCheat_Setup",
        "UnrealCEFSubProcess", "UbisoftConnect", @"ubisoftconnect",
        @"\.exe$.*installer|installer\.exe$", "InstallShield",
        "Garden of Eden", @"GECK", "Rockstar", @"Social-Club", @"FirewallInstall",
        // Crash-report / diagnostics shipped inside game folders.
        "crashsender", "crashpad", @"^crs-", "miniticket",
        // Runtime redistributables and SDK helpers.
        "msedgewebview2", @"^nw\.exe$", "ue3redist", "d3d11install",
        // Install/helper scripts that get picked up as .bat candidates.
        @"^install_pspc", @"^install-kbupdate", @"^runme", @"^testapp",
        @"^dx2\.exe$", @"^clean\.bat$", "show_third_party",
        // Benchmarks and per-game tweaker tools.
        "benchmark", "protocolselector", "nvprofilefixer"
    ];

    private static readonly string[] FolderExceptions =
    [
        @"\Accessibility\", @"\Accessories\", @"\Administrative Tools\",
        @"\Maintenance\", @"\StartUp\", @"\Windows\", @"\Microsoft\",
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
            var filename = Path.GetFileName(file);
            if (IsExcludedFile(filename))
                continue;

            var ext = Path.GetExtension(file);
            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                if (BuildFromExecutable(file) is { } candidate)
                    candidates.Add(candidate);
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

    private InstalledGameCandidate? BuildFromExecutable(string exePath)
    {
        var name = GetBestName(exePath);

        // Some helpers hide behind clean filenames but report their role in the
        // product name (EasyAntiCheat launcher, BattlEye launcher, an installer).
        // Filtering on the name-to-be-shown catches those that the filename
        // masks miss. null skips the candidate entirely.
        if (name.Length > 0 && (IsExcludedFile(name) || IsHelperName(name)))
            return null;

        return new InstalledGameCandidate(
            name,
            exePath,
            null,
            Path.GetDirectoryName(exePath),
            exePath);
    }

    /// <summary>
    /// Picks the friendliest display name for a game executable. The Windows
    /// product name is preferred when it reads like a title ("Fallout 3",
    /// "METAL GEAR SOLID V: THE PHANTOM PAIN"), but a mashed-up camelCase
    /// ("DaysGone", "AlanWake") or a technical engine marker
    /// ("NARUTO-Win64-Shipping") is cleaned up into readable words.
    /// </summary>
    private static string GetBestName(string exePath)
    {
        var product = GetProductName(exePath);

        if (product.Length > 0
            && product.Any(char.IsWhiteSpace)          // reads like a title
            && !product.Contains('\uFFFD')             // not encoding-corrupt
            && !IsTechnicalEngineName(product))
        {
            return product;
        }

        return SplitWords(product);
    }

    private static bool IsTechnicalEngineName(string name) =>
        name.EndsWith("-Shipping", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("-Win64", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith("-Win32", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(" shipping", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(" launcher", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(" benchmark", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Turns a compact executable name into readable words: "DaysGone" →
    /// "Days Gone", "NARUTO-Win64-Shipping" → "NARUTO". Technical engine
    /// markers (Win64, Shipping, Launcher, ...) are dropped entirely.
    /// </summary>
    private static string SplitWords(string name)
    {
        // Drop encoding-corrupt chars (some exes expose a mangled product name).
        var cleaned = string.Concat(name.Select(c => c == '\uFFFD' ? ' ' : c));

        // Insert a space before every uppercase letter that follows a
        // lowercase letter or a digit, then split on the common separators.
        var padded = System.Text.RegularExpressions.Regex.Replace(
            cleaned, "([a-z0-9])([A-Z])", "$1 $2");
        var words = padded
            .Split([' ', '-', '_', '.', '(', ')', '\'', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !IsTechnicalWord(w))
            .ToArray();

        return words.Length > 0
            ? string.Join(" ", words)
            : Path.GetFileNameWithoutExtension(name);
    }

    private static readonly HashSet<string> TechnicalWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "win64", "win32", "x64", "x86", "shipping", "shippingpc", "windows",
        "launcher", "launch", "benchmark", "be", "final", "binaries", "system",
        "client", "server", "test", "debug", "data"
    };

    private static bool IsTechnicalWord(string word) => TechnicalWords.Contains(word);

    private static bool IsHelperName(string name)
    {
        // Product-name markers that mean "this is a tool, not a game". Covers
        // the case where the file is named innocently (NARUTO.exe) but reports
        // "Easy Anti Cheat" / "Battl Eye" (spaced variants included), or an
        // installer identifies itself.
        var normalized = string.Concat(name.Where(c => !char.IsWhiteSpace(c)))
            .ToLowerInvariant();
        return normalized.Contains("anticheat", StringComparison.Ordinal)
            || normalized.Contains("battleye", StringComparison.Ordinal)
            || normalized.Contains("installer", StringComparison.Ordinal)
            || normalized.Contains("installshield", StringComparison.Ordinal)
            || normalized.Contains("uninstall", StringComparison.Ordinal)
            || normalized.Contains("launcher", StringComparison.Ordinal)
            || normalized.Contains("redistributable", StringComparison.Ordinal);
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
