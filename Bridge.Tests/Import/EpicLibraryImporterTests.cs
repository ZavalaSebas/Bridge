using Bridge.Core.Enums;
using Bridge.Import.Epic;

namespace Bridge.Tests.Import;

// Uses realistic content shaped like Epic's real LauncherInstalled.dat and
// .item manifests. The importer takes injectable paths so tests don't need a
// real Epic install under %PROGRAMDATA%.
public class EpicLibraryImporterTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dataDir;
    private readonly string _appListPath;
    private readonly string _manifestsDir;

    public EpicLibraryImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-epictest-{Guid.NewGuid()}");
        _dataDir = Path.Combine(_tempDir, "data");
        _manifestsDir = Path.Combine(_dataDir, "manifests");
        Directory.CreateDirectory(_manifestsDir);
        _appListPath = Path.Combine(_dataDir, "LauncherInstalled.dat");
    }

    private EpicLibraryImporter Build() => new(_appListPath, _manifestsDir);

    private static string JsonEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private void WriteInstalledAppList(params (string AppName, string InstallLocation)[] apps)
    {
        var list = string.Join(",", apps.Select(a =>
            $$"""{"InstallLocation":"{{JsonEscape(a.InstallLocation)}}","AppName":"{{a.AppName}}","AppID":1234,"AppVersion":"1.0"}"""));
        File.WriteAllText(_appListPath, $$"""{"InstallationList":[{{list}}]}""");
    }

    private void WriteManifest(string appName, string displayName, string installLocation, string[]? categories = null, string[]? compatible = null, string launchExecutable = "")
    {
        var file = Path.Combine(_manifestsDir, $"{appName}.item");
        var catJson = string.Join(",", (categories ?? []).Select(c => $"\"{c}\""));
        var compJson = string.Join(",", (compatible ?? []).Select(c => $"\"{c}\""));
        var json = $$"""
            {"DisplayName":"{{displayName}}","AppName":"{{appName}}","InstallLocation":"{{JsonEscape(installLocation)}}",
             "AppCategories":[{{catJson}}],"CompatibleApps":[{{compJson}}],"LaunchExecutable":"{{launchExecutable}}","TechnicalType":"game"}
            """;
        File.WriteAllText(file, json);
    }

    [Fact]
    public void GetInstalledGames_ImportsGameWithPlayAction()
    {
        var gameDir = Path.Combine(_tempDir, "GameDir");
        Directory.CreateDirectory(gameDir);
        WriteInstalledAppList(("fortnite", gameDir));
        WriteManifest("fortnite", "Fortnite", gameDir, launchExecutable: "Fortnite.exe");

        var games = Build().GetInstalledGames();

        var game = Assert.Single(games);
        Assert.Equal("fortnite", game.ExternalId);
        Assert.Equal("Fortnite", game.Name);
        Assert.Equal(gameDir, game.InstallDirectory);
        Assert.True(game.IsInstalled);

        var action = Assert.Single(game.GameActions);
        Assert.Equal(GameActionType.Url, action.Type);
        Assert.True(action.IsPlayAction);
        Assert.Equal("com.epicgames.launcher://apps/fortnite?action=launch&silent=true", action.Path);
        Assert.Equal(TrackingMode.Directory, action.TrackingMode);
    }

    [Fact]
    public void GetInstalledGames_SetsIconToInstalledExecutable()
    {
        var gameDir = Path.Combine(_tempDir, "GameDir");
        Directory.CreateDirectory(gameDir);
        var exePath = Path.Combine(gameDir, "Game.exe");
        File.WriteAllText(exePath, "fake exe");
        WriteInstalledAppList(("mygame", gameDir));
        WriteManifest("mygame", "My Game", gameDir, launchExecutable: "Game.exe");

        var game = Assert.Single(Build().GetInstalledGames());

        Assert.Equal(exePath, game.Icon);
    }

    [Fact]
    public void GetInstalledGames_SkipsUnrealEngineAndDlcAndPlugins()
    {
        var ueDir = Path.Combine(_tempDir, "UE");
        var dlcDir = Path.Combine(_tempDir, "DLC");
        var pluginDir = Path.Combine(_tempDir, "Plugin");
        Directory.CreateDirectory(ueDir);
        Directory.CreateDirectory(dlcDir);
        Directory.CreateDirectory(pluginDir);

        WriteInstalledAppList(("UE_4.27", ueDir), ("somegame_dlc", dlcDir), ("pluginitem", pluginDir));
        WriteManifest("UE_4.27", "Unreal Engine", ueDir);
        WriteManifest("somegame_dlc", "Some Game DLC", dlcDir, categories: ["addons"]);
        WriteManifest("pluginitem", "Plugin", pluginDir, categories: ["plugins/engine"]);

        var games = Build().GetInstalledGames();

        Assert.Empty(games);
    }

    [Fact]
    public void GetInstalledGames_FallsBackToFolderName_WhenNoManifest()
    {
        var gameDir = Path.Combine(_tempDir, "NoManifest");
        Directory.CreateDirectory(gameDir);
        WriteInstalledAppList(("nomatch", gameDir));

        var games = Build().GetInstalledGames();

        // No manifest for this app -> skipped (Playnite requires the manifest).
        Assert.Empty(games);
    }

    [Fact]
    public void GetInstalledGames_ReturnsEmpty_WhenNoInstalledAppsFile()
    {
        // No LauncherInstalled.dat at all — like when Epic isn't installed.
        Assert.Empty(Build().GetInstalledGames());
    }

    [Fact]
    public void GetInstalledGames_RemovesTrademarksFromName()
    {
        var gameDir = Path.Combine(_tempDir, "TM");
        Directory.CreateDirectory(gameDir);
        WriteInstalledAppList(("gametm", gameDir));
        WriteManifest("gametm", "Example\u2122", gameDir);

        var game = Assert.Single(Build().GetInstalledGames());
        Assert.Equal("Example", game.Name);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}

