using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

// Uses realistic content shaped exactly like real Steam files (verified
// against actual libraryfolders.vdf / appmanifest*.acf on a real machine
// with Steam installed, PROJECT_FOUNDATION.md §28.26) — not invented syntax.
public class SteamLibraryImporterTests : IDisposable
{
    private readonly string _tempDir;

    public SteamLibraryImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-steamtest-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void GetLibraryFolders_IncludesInstallPathAndVdfEntries()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        var steamAppsDir = Path.Combine(steamRoot, "steamapps");
        var secondLibrary = Path.Combine(_tempDir, "SteamLibrary2");
        Directory.CreateDirectory(steamAppsDir);
        Directory.CreateDirectory(secondLibrary);

        File.WriteAllText(Path.Combine(steamAppsDir, "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "0"
                {
                    "path"      "{{steamRoot.Replace("\\", "\\\\")}}"
                }
                "1"
                {
                    "path"      "{{secondLibrary.Replace("\\", "\\\\")}}"
                }
            }
            """);

        var folders = SteamLibraryImporter.GetLibraryFolders(steamRoot);

        Assert.Contains(steamRoot, folders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(secondLibrary, folders, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetLibraryFolders_SkipsEntriesWhosePathDoesNotExist()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        var steamAppsDir = Path.Combine(steamRoot, "steamapps");
        Directory.CreateDirectory(steamAppsDir);

        File.WriteAllText(Path.Combine(steamAppsDir, "libraryfolders.vdf"), """
            "libraryfolders"
            {
                "0"
                {
                    "path"      "Z:\\DoesNotExist\\Steam"
                }
            }
            """);

        var folders = SteamLibraryImporter.GetLibraryFolders(steamRoot);

        Assert.DoesNotContain(@"Z:\DoesNotExist\Steam", folders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(steamRoot, folders, StringComparer.OrdinalIgnoreCase); // the root itself is always included
    }

    [Fact]
    public void ParseManifest_FullyInstalledGame_ReturnsMetadata()
    {
        var steamAppsDir = Path.Combine(_tempDir, "steamapps");
        var installDir = Path.Combine(steamAppsDir, "common", "MyGame");
        Directory.CreateDirectory(installDir);
        var manifestPath = Path.Combine(steamAppsDir, "appmanifest_12345.acf");
        File.WriteAllText(manifestPath, """
            "AppState"
            {
                "appid"     "12345"
                "universe"      "1"
                "name"      "My Game"
                "StateFlags"        "4"
                "installdir"        "MyGame"
            }
            """);

        var metadata = SteamLibraryImporter.ParseManifest(manifestPath, steamAppsDir);

        Assert.NotNull(metadata);
        Assert.Equal("12345", metadata.ExternalId);
        Assert.Equal("My Game", metadata.Name);
        Assert.True(metadata.IsInstalled);
        Assert.Equal(installDir, metadata.InstallDirectory);
    }

    [Fact]
    public void ParseManifest_NotFullyInstalled_ReturnsNull()
    {
        var steamAppsDir = Path.Combine(_tempDir, "steamapps");
        Directory.CreateDirectory(steamAppsDir);
        var manifestPath = Path.Combine(steamAppsDir, "appmanifest_999.acf");
        // StateFlags 1026 = UpdateRequired(2) + UpdateStarted(1024) — no FullyInstalled(4) bit set.
        File.WriteAllText(manifestPath, """
            "AppState"
            {
                "appid"     "999"
                "name"      "Downloading Game"
                "StateFlags"        "1026"
            }
            """);

        var metadata = SteamLibraryImporter.ParseManifest(manifestPath, steamAppsDir);

        Assert.Null(metadata);
    }

    [Fact]
    public void ParseManifest_SteamworksRedistributables_ReturnsNull()
    {
        var steamAppsDir = Path.Combine(_tempDir, "steamapps");
        Directory.CreateDirectory(steamAppsDir);
        var manifestPath = Path.Combine(steamAppsDir, "appmanifest_228980.acf");
        File.WriteAllText(manifestPath, """
            "AppState"
            {
                "appid"     "228980"
                "name"      "Steamworks Common Redistributables"
                "StateFlags"        "4"
            }
            """);

        var metadata = SteamLibraryImporter.ParseManifest(manifestPath, steamAppsDir);

        Assert.Null(metadata);
    }

    [Fact]
    public void ParseManifest_MalformedFile_ReturnsNullInsteadOfThrowing()
    {
        var steamAppsDir = Path.Combine(_tempDir, "steamapps");
        Directory.CreateDirectory(steamAppsDir);
        var manifestPath = Path.Combine(steamAppsDir, "appmanifest_bad.acf");
        File.WriteAllText(manifestPath, "{{{ not valid vdf at all");

        var metadata = SteamLibraryImporter.ParseManifest(manifestPath, steamAppsDir);

        Assert.Null(metadata);
    }

    [Fact]
    public void ParseManifest_MissingInstallDirOnDisk_LeavesInstallDirectoryEmpty()
    {
        var steamAppsDir = Path.Combine(_tempDir, "steamapps");
        Directory.CreateDirectory(steamAppsDir);
        var manifestPath = Path.Combine(steamAppsDir, "appmanifest_1.acf");
        // "installdir" points at a folder that was never actually created.
        File.WriteAllText(manifestPath, """
            "AppState"
            {
                "appid"     "1"
                "name"      "Ghost Game"
                "StateFlags"        "4"
                "installdir"        "GhostFolder"
            }
            """);

        var metadata = SteamLibraryImporter.ParseManifest(manifestPath, steamAppsDir);

        Assert.NotNull(metadata);
        Assert.Equal(string.Empty, metadata.InstallDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
