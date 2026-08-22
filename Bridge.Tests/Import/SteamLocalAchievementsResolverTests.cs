using System.Buffers.Binary;
using System.Text;
using Bridge.Import.Steam;

namespace Bridge.Tests.Import;

public class BinaryVdfParserTests
{
    [Fact]
    public void Parse_ReadsNestedStringAndIntValues()
    {
        var bytes = WriteRoot("cache", new Dictionary<string, object>
        {
            ["1"] = new Dictionary<string, object>
            {
                ["type"] = "ACHIEVEMENTS",
                ["bits"] = new Dictionary<string, object>
                {
                    ["0"] = new Dictionary<string, object>
                    {
                        ["name"] = "ACH_TEST",
                        ["display"] = new Dictionary<string, object>
                        {
                            ["name"] = new Dictionary<string, object> { ["english"] = "Test Achievement" },
                            ["desc"] = new Dictionary<string, object> { ["english"] = "Do the thing" },
                            ["icon"] = "abc.jpg",
                        },
                    },
                },
            },
        });

        var root = BinaryVdfParser.Parse(bytes);
        Assert.True(root.TryGetValue("cache", out var cacheObj));
        Assert.IsType<Dictionary<string, object>>(cacheObj);
    }

    internal static byte[] WriteRoot(string rootKey, Dictionary<string, object> content)
    {
        using var stream = new MemoryStream();
        stream.WriteByte(0x00);
        WriteCString(stream, rootKey);
        WriteDictionary(stream, content);
        stream.WriteByte(0x08);
        return stream.ToArray();
    }

    private static void WriteDictionary(Stream stream, Dictionary<string, object> content)
    {
        foreach (var (key, value) in content)
        {
            switch (value)
            {
                case Dictionary<string, object> child:
                    stream.WriteByte(0x00);
                    WriteCString(stream, key);
                    WriteDictionary(stream, child);
                    break;
                case string text:
                    stream.WriteByte(0x01);
                    WriteCString(stream, key);
                    WriteCString(stream, text);
                    break;
                case int number:
                    stream.WriteByte(0x02);
                    WriteCString(stream, key);
                    stream.Write(BitConverter.GetBytes(number), 0, 4);
                    break;
                case uint number:
                    stream.WriteByte(0x02);
                    WriteCString(stream, key);
                    stream.Write(BitConverter.GetBytes(number), 0, 4);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported VDF value type: {value.GetType()}");
            }
        }

        stream.WriteByte(0x08);
    }

    private static void WriteCString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
        stream.WriteByte(0);
    }
}

public class SteamLocalAchievementsResolverTests : IDisposable
{
    private readonly string _tempDir;

    public SteamLocalAchievementsResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bridge-steam-ach-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void TryGetAchievements_ReadsSchemaAndUnlockTimes()
    {
        const string appId = "480";
        const string accountId = "919060658";
        var steamRoot = Path.Combine(_tempDir, "Steam");
        var statsDir = Path.Combine(steamRoot, "appcache", "stats");
        Directory.CreateDirectory(statsDir);
        Directory.CreateDirectory(Path.Combine(steamRoot, "userdata", accountId));

        File.WriteAllBytes(
            Path.Combine(statsDir, $"UserGameStatsSchema_{appId}.bin"),
            BinaryVdfParserTests.WriteRoot(appId, new Dictionary<string, object>
            {
                ["stats"] = new Dictionary<string, object>
                {
                    ["1"] = new Dictionary<string, object>
                    {
                        ["type"] = "ACHIEVEMENTS",
                        ["bits"] = new Dictionary<string, object>
                        {
                            ["0"] = new Dictionary<string, object>
                            {
                                ["name"] = "ACH_WIN_ONE_GAME",
                                ["display"] = new Dictionary<string, object>
                                {
                                    ["name"] = new Dictionary<string, object> { ["english"] = "Winner" },
                                    ["desc"] = new Dictionary<string, object> { ["english"] = "Win one game" },
                                    ["icon"] = "winner.jpg",
                                    ["icon_gray"] = "winner_gray.jpg",
                                },
                            },
                            ["1"] = new Dictionary<string, object>
                            {
                                ["name"] = "ACH_HIDDEN",
                                ["display"] = new Dictionary<string, object>
                                {
                                    ["hidden"] = 1,
                                    ["name"] = new Dictionary<string, object> { ["english"] = "Secret" },
                                    ["desc"] = new Dictionary<string, object> { ["english"] = "Shhh" },
                                },
                            },
                        },
                    },
                },
            }));

        File.WriteAllBytes(
            Path.Combine(statsDir, $"UserGameStats_{accountId}_{appId}.bin"),
            BinaryVdfParserTests.WriteRoot("cache", new Dictionary<string, object>
            {
                ["1"] = new Dictionary<string, object>
                {
                    ["data"] = 1,
                    ["AchievementTimes"] = new Dictionary<string, object>
                    {
                        ["0"] = 1_700_000_000,
                    },
                },
            }));

        var snapshot = SteamLocalAchievementsResolver.TryGetAchievements(appId, steamRoot);
        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.TotalCount);
        Assert.Equal(1, snapshot.UnlockedCount);

        var winner = Assert.Single(snapshot.Achievements, a => a.ApiName == "ACH_WIN_ONE_GAME");
        Assert.True(winner.IsUnlocked);
        Assert.Equal("Winner", winner.Name);
        Assert.Equal("Win one game", winner.Description);
        Assert.Contains("winner.jpg", winner.IconUrl, StringComparison.Ordinal);

        var hidden = Assert.Single(snapshot.Achievements, a => a.ApiName == "ACH_HIDDEN");
        Assert.False(hidden.IsUnlocked);
        Assert.True(hidden.IsHidden);
    }

    [Fact]
    public void TryGetAchievements_ReadsLegacyTypeFourSchema()
    {
        const string appId = "440";
        var steamRoot = Path.Combine(_tempDir, "SteamLegacy");
        var statsDir = Path.Combine(steamRoot, "appcache", "stats");
        Directory.CreateDirectory(statsDir);

        File.WriteAllBytes(
            Path.Combine(statsDir, $"UserGameStatsSchema_{appId}.bin"),
            BinaryVdfParserTests.WriteRoot(appId, new Dictionary<string, object>
            {
                ["stats"] = new Dictionary<string, object>
                {
                    ["266"] = new Dictionary<string, object>
                    {
                        ["type"] = "4",
                        ["type_int"] = 4,
                        ["bits"] = new Dictionary<string, object>
                        {
                            ["0"] = new Dictionary<string, object>
                            {
                                ["name"] = "ACH_LEGACY",
                                ["display"] = new Dictionary<string, object>
                                {
                                    ["name"] = new Dictionary<string, object> { ["english"] = "Legacy Winner" },
                                    ["desc"] = new Dictionary<string, object> { ["english"] = "Old schema" },
                                    ["icon"] = "legacy.jpg",
                                },
                            },
                        },
                    },
                },
            }));

        var snapshot = SteamLocalAchievementsResolver.TryGetAchievements(appId, steamRoot);
        Assert.NotNull(snapshot);
        Assert.Single(snapshot!.Achievements);
        Assert.Equal("Legacy Winner", snapshot.Achievements[0].Name);
    }

    [Fact]
    public void TryGetAchievements_ReturnsNullWhenSchemaMissing()
    {
        var steamRoot = Path.Combine(_tempDir, "missing");
        Directory.CreateDirectory(steamRoot);

        Assert.Null(SteamLocalAchievementsResolver.TryGetAchievements("480", steamRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
