using System.Text.Json;
using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class EpicAchievementsClientParsingTests
{
    [Fact]
    public void TryParseCatalog_ReadsDefinitionsAndGlobalPercent()
    {
        const string json = """
            {
              "data": {
                "Achievement": {
                  "productAchievementsRecordBySandbox": {
                    "sandboxId": "sandbox-1",
                    "totalAchievements": 2,
                    "achievements": [
                      {
                        "achievement": {
                          "name": "ACH_FIRST",
                          "hidden": false,
                          "unlockedDisplayName": "First",
                          "lockedDisplayName": "First",
                          "unlockedDescription": "Do the thing",
                          "lockedDescription": "Do the thing",
                          "unlockedIconLink": "https://example/unlocked.png",
                          "lockedIconLink": "https://example/locked.png",
                          "rarity": { "percent": 12.5 }
                        }
                      }
                    ]
                  }
                }
              }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var catalog = EpicAchievementCatalog.TryParse(document);

        var achievement = Assert.Single(catalog!.Achievements);
        Assert.Equal("sandbox-1", catalog.SandboxId);
        Assert.Equal("ACH_FIRST", achievement.Name);
        Assert.Equal("First", achievement.UnlockedDisplayName);
        Assert.Equal(12.5, achievement.GlobalUnlockPercent);
    }

    [Fact]
    public void TryParsePlayerRecord_ReadsUnlockState()
    {
        const string json = """
            {
              "data": {
                "PlayerAchievement": {
                  "playerAchievementGameRecordsBySandbox": {
                    "records": [
                      {
                        "totalUnlocked": 1,
                        "playerAchievements": [
                          {
                            "playerAchievement": {
                              "achievementName": "ACH_FIRST",
                              "unlocked": true,
                              "unlockDate": "2024-01-15T10:30:00.000Z",
                              "progress": 1
                            }
                          }
                        ]
                      }
                    ]
                  }
                }
              }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var record = EpicPlayerAchievementRecord.TryParse(document, "sandbox-1");

        Assert.NotNull(record);
        Assert.Equal(1, record!.UnlockedCount);
        var state = Assert.Single(record.ByName.Values);
        Assert.True(state.Unlocked);
        Assert.Equal("ACH_FIRST", state.AchievementName);
        Assert.NotNull(state.UnlockedAt);
    }
}
