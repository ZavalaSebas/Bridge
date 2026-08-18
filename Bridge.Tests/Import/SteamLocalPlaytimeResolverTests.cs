using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

// Real localconfig.vdf layout: Playtime (minutes) and LastPlayed (unix) per appid.
public class SteamLocalPlaytimeResolverTests : IDisposable
{
    private readonly string _tempDir;

    public SteamLocalPlaytimeResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-steamplaytime-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    private string WriteLocalConfig(string steamRoot, string userId, string content)
    {
        var configDir = Path.Combine(steamRoot, "userdata", userId, "config");
        Directory.CreateDirectory(configDir);
        var path = Path.Combine(configDir, "localconfig.vdf");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void GetPlaytimes_ReadsMinutesAndConvertsToSeconds()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        WriteLocalConfig(steamRoot, "919060658", """
            "UserLocalConfigStore"
            {
                "Software"
                {
                    "Valve"
                    {
                        "Steam"
                        {
                            "apps"
                            {
                                "218"
                                {
                                    "LastPlayed"      "1592262171"
                                    "Playtime"        "101"
                                }
                            }
                        }
                    }
                }
            }
            """);

        var playtimes = SteamLocalPlaytimeResolver.GetPlaytimes(steamRoot);

        Assert.NotNull(playtimes);
        Assert.True(playtimes.TryGetValue("218", out var playtime));
        Assert.Equal(101UL * 60, playtime.PlaytimeSeconds); // minutes -> seconds
        Assert.Equal(new DateTime(2020, 6, 15, 23, 2, 51, DateTimeKind.Utc), playtime.LastActivity);
    }

    [Fact]
    public void GetPlaytimes_ZeroPlaytimeIsSkipped()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        WriteLocalConfig(steamRoot, "919060658", """
            "UserLocalConfigStore"
            {
                "Software" { "Valve" { "Steam" { "apps"
                {
                    "218" { "LastPlayed" "1592262171" "Playtime" "0" }
                } } } }
            }
            """);

        var playtimes = SteamLocalPlaytimeResolver.GetPlaytimes(steamRoot);

        Assert.True(playtimes is null || !playtimes.ContainsKey("218"));
    }

    [Fact]
    public void GetPlaytimes_MissingLastPlayed_KeepsNullActivity()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        WriteLocalConfig(steamRoot, "919060658", """
            "UserLocalConfigStore"
            {
                "Software" { "Valve" { "Steam" { "apps"
                {
                    "218" { "Playtime" "30" }
                } } } }
            }
            """);

        var playtimes = SteamLocalPlaytimeResolver.GetPlaytimes(steamRoot);

        Assert.NotNull(playtimes);
        Assert.True(playtimes.TryGetValue("218", out var playtime));
        Assert.Equal(30UL * 60, playtime.PlaytimeSeconds);
        Assert.Null(playtime.LastActivity);
    }

    [Fact]
    public void GetPlaytimes_MultipleAccounts_TakesMaxPlaytimeAndLatestActivity()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        WriteLocalConfig(steamRoot, "111111", """
            "UserLocalConfigStore"
            {
                "Software" { "Valve" { "Steam" { "apps"
                {
                    "218" { "LastPlayed" "1000000000" "Playtime" "50" }
                } } } }
            }
            """);
        WriteLocalConfig(steamRoot, "222222", """
            "UserLocalConfigStore"
            {
                "Software" { "Valve" { "Steam" { "apps"
                {
                    "218" { "LastPlayed" "2000000000" "Playtime" "80" }
                } } } }
            }
            """);

        var playtimes = SteamLocalPlaytimeResolver.GetPlaytimes(steamRoot);

        Assert.NotNull(playtimes);
        Assert.True(playtimes.TryGetValue("218", out var playtime));
        Assert.Equal(80UL * 60, playtime.PlaytimeSeconds); // largest minutes win
        Assert.Equal(new DateTime(2033, 5, 18, 3, 33, 20, DateTimeKind.Utc), playtime.LastActivity); // most recent wins
    }

    [Fact]
    public void GetPlaytimes_MissingUserDataDir_ReturnsNull()
    {
        var result = SteamLocalPlaytimeResolver.GetPlaytimes(Path.Combine(_tempDir, "NoSteamHere"));

        Assert.Null(result);
    }

    [Fact]
    public void GetPlaytimes_NoConfigFile_ReturnsNull()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        Directory.CreateDirectory(Path.Combine(steamRoot, "userdata", "919060658"));

        var result = SteamLocalPlaytimeResolver.GetPlaytimes(steamRoot);

        Assert.Null(result);
    }

    [Fact]
    public void GetPlaytimes_MalformedConfig_IsSkipped()
    {
        var steamRoot = Path.Combine(_tempDir, "Steam");
        WriteLocalConfig(steamRoot, "919060658", "{{{ not valid vdf at all");

        var result = SteamLocalPlaytimeResolver.GetPlaytimes(steamRoot);

        Assert.Null(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
