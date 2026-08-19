using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Bridge.Services;

public sealed record WhatsNewItem(string Title, string? Description);

public sealed record WhatsNewSection(string Name, IReadOnlyList<WhatsNewItem> Items);

public sealed record WhatsNewRelease(Version Version, string? ReleaseDate, IReadOnlyList<WhatsNewSection> Sections);

/// <summary>
/// Reads summarized release notes for a version from the embedded
/// <c>CHANGELOG.md</c> (Keep a Changelog format).
/// </summary>
public static partial class WhatsNewParser
{
    private const string ChangelogResourceName = "Bridge.Changelog.md";

    private static readonly string[] IncludedSections =
    [
        "Added",
        "Changed",
        "Fixed"
    ];

    public static string ReadEmbeddedChangelog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ChangelogResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ChangelogResourceName}' was not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static bool TryParseRelease(string changelog, Version version, out WhatsNewRelease release)
    {
        release = null!;

        if (!TryFindReleaseBlock(changelog, version, out var block, out var releaseDate))
            return false;

        var sections = new List<WhatsNewSection>();
        foreach (var sectionName in IncludedSections)
        {
            var items = ParseSectionItems(block, sectionName);
            if (items.Count > 0)
                sections.Add(new WhatsNewSection(sectionName, items));
        }

        if (sections.Count == 0)
            return false;

        release = new WhatsNewRelease(Normalize(version), releaseDate, sections);
        return true;
    }

    public static bool TryGetCurrentReleaseNotes(out WhatsNewRelease release)
    {
        release = null!;
        var changelog = ReadEmbeddedChangelog();
        return TryParseRelease(changelog, Config.AssemblyVersion, out release);
    }

    private static bool TryFindReleaseBlock(
        string changelog,
        Version version,
        out string block,
        out string? releaseDate)
    {
        block = string.Empty;
        releaseDate = null;

        var versionLabel = version.ToString(3);
        var lines = changelog.Replace("\r\n", "\n").Split('\n');
        var start = -1;

        for (var i = 0; i < lines.Length; i++)
        {
            if (!TryParseReleaseHeading(lines[i], out var headingVersion, out var headingDate))
                continue;

            if (!VersionMatches(headingVersion, versionLabel))
                continue;

            start = i + 1;
            releaseDate = headingDate;
            break;
        }

        if (start < 0)
            return false;

        var end = lines.Length;
        for (var i = start; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## [", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        block = string.Join('\n', lines, start, end - start);
        return !string.IsNullOrWhiteSpace(block);
    }

    private static List<WhatsNewItem> ParseSectionItems(string block, string sectionName)
    {
        var items = new List<WhatsNewItem>();
        var lines = block.Split('\n');
        var inSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line["### ".Length..].Trim(), sectionName, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection || !line.StartsWith("- ", StringComparison.Ordinal))
                continue;

            if (TryParseBullet(line, out var title, out var description))
                items.Add(new WhatsNewItem(title, description));
        }

        return items;
    }

    private static bool TryParseReleaseHeading(string line, out string version, out string? releaseDate)
    {
        version = string.Empty;
        releaseDate = null;

        var match = ReleaseHeadingRegex().Match(line.Trim());
        if (!match.Success)
            return false;

        version = match.Groups["version"].Value.Trim();
        releaseDate = match.Groups["date"].Success
            ? match.Groups["date"].Value.Trim()
            : null;
        return true;
    }

    private static bool TryParseBullet(string line, out string title, out string? description)
    {
        title = string.Empty;
        description = null;

        var match = BulletRegex().Match(line);
        if (!match.Success)
            return false;

        title = match.Groups["title"].Value.Trim();
        description = match.Groups["desc"].Success
            ? match.Groups["desc"].Value.Trim()
            : null;

        return title.Length > 0;
    }

    private static bool VersionMatches(string headingVersion, string targetVersion) =>
        string.Equals(NormalizeVersionLabel(headingVersion), NormalizeVersionLabel(targetVersion), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeVersionLabel(string value)
    {
        if (Version.TryParse(value.TrimStart('v', 'V'), out var version))
            return version.ToString(3);

        return value.Trim().TrimStart('v', 'V');
    }

    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, version.Build);

    [GeneratedRegex(@"^##\s*\[(?<version>[^\]]+)\](?:\s*-\s*(?<date>.+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseHeadingRegex();

    [GeneratedRegex(@"^-\s*\*\*(?<title>.+?)\*\*(?:\s*—\s*(?<desc>.+))?$", RegexOptions.CultureInvariant)]
    private static partial Regex BulletRegex();
}
