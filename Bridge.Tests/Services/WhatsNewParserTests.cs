using Bridge.Services;

namespace Bridge.Tests.Services;

public class WhatsNewParserTests
{
    private const string SampleChangelog = """
        # Changelog

        ## [Unreleased]

        ## [0.4.0] - 2026-08-18

        ### Added
        - **English / Spanish UI** — localized resources and a language picker.
        - **Library backup & restore** — portable `.zip` backups.

        ### Changed
        - **Settings hub** — unified preferences overlay.

        ### Fixed
        - **SQLite "file is not a database"** — startup validates the DB header.

        ## [0.3.0] - 2026-08-18

        ### Added
        - **Self-updater** — checks GitHub Releases.
        """;

    [Fact]
    public void TryParseRelease_FindsMatchingVersionSections()
    {
        var ok = WhatsNewParser.TryParseRelease(SampleChangelog, new Version(0, 4, 0), out var release);

        Assert.True(ok);
        Assert.Equal(new Version(0, 4, 0), release.Version);
        Assert.Equal("2026-08-18", release.ReleaseDate);
        Assert.Equal(3, release.Sections.Count);
        Assert.Equal("Added", release.Sections[0].Name);
        Assert.Equal(2, release.Sections[0].Items.Count);
        Assert.Equal("English / Spanish UI", release.Sections[0].Items[0].Title);
        Assert.Contains("localized resources", release.Sections[0].Items[0].Description!, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParseRelease_ReturnsFalseWhenVersionMissing()
    {
        var ok = WhatsNewParser.TryParseRelease(SampleChangelog, new Version(9, 9, 9), out _);
        Assert.False(ok);
    }

    [Fact]
    public void TryGetCurrentReleaseNotes_ParsesEmbeddedChangelog()
    {
        var ok = WhatsNewParser.TryGetCurrentReleaseNotes(out var release);

        Assert.True(ok);
        Assert.Equal(Config.AssemblyVersion.Major, release.Version.Major);
        Assert.Equal(Config.AssemblyVersion.Minor, release.Version.Minor);
        Assert.Equal(Config.AssemblyVersion.Build, release.Version.Build);
        Assert.NotEmpty(release.Sections);
    }
}
