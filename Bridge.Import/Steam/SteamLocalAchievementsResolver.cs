namespace Bridge.Import.Steam;

/// <summary>
/// Reads Steam achievement definitions and unlock state from local cache files:
/// appcache/stats/UserGameStatsSchema_{appid}.bin and
/// UserGameStats_{steamid3}_{appid}.bin. Zero-config, no Web API key.
/// </summary>
public static class SteamLocalAchievementsResolver
{
    public static Bridge.Core.Entities.GameAchievementsSnapshot? TryGetAchievements(
        string appId,
        string? steamInstallPath = null,
        string language = "english")
    {
        if (!uint.TryParse(appId, out var parsedAppId))
            return null;

        steamInstallPath ??= SteamPaths.GetInstallationPath();
        if (string.IsNullOrWhiteSpace(steamInstallPath))
            return null;

        var schemaPath = Path.Combine(
            steamInstallPath,
            "appcache",
            "stats",
            $"UserGameStatsSchema_{parsedAppId}.bin");
        if (!File.Exists(schemaPath))
            return null;

        byte[] schemaBytes;
        try
        {
            schemaBytes = File.ReadAllBytes(schemaPath);
        }
        catch
        {
            return null;
        }

        Dictionary<string, object> schemaRoot;
        try
        {
            schemaRoot = BinaryVdfParser.Parse(schemaBytes);
        }
        catch
        {
            return null;
        }

        if (schemaRoot.Count == 0 ||
            schemaRoot.First().Value is not Dictionary<string, object> appNode ||
            !appNode.TryGetValue("stats", out var statsObj) ||
            statsObj is not Dictionary<string, object> statsNode)
        {
            return null;
        }

        var unlockTimes = ReadUnlockTimes(steamInstallPath, parsedAppId);
        var achievements = new List<Bridge.Core.Entities.GameAchievement>();

        foreach (var (groupId, groupValue) in statsNode)
        {
            if (groupValue is not Dictionary<string, object> group ||
                !IsAchievementsGroup(group) ||
                !group.TryGetValue("bits", out var bitsObj) ||
                bitsObj is not Dictionary<string, object> bits ||
                bits.Count == 0)
            {
                continue;
            }

            foreach (var (bitKey, bitValue) in bits)
            {
                if (bitValue is not Dictionary<string, object> bitNode)
                    continue;

                var apiName = TryGetString(bitNode, "name", out var parsedApiName)
                    ? parsedApiName
                    : $"ACH_{bitKey}";

                var display = bitNode.TryGetValue("display", out var displayObj) &&
                              displayObj is Dictionary<string, object> displayNode
                    ? displayNode
                    : null;

                var hidden = display is not null &&
                             display.TryGetValue("hidden", out var hiddenObj) &&
                             hiddenObj switch
                             {
                                 int hiddenInt => hiddenInt != 0,
                                 uint hiddenUint => hiddenUint != 0,
                                 string hiddenText => hiddenText is not ("0" or ""),
                                 _ => false
                             };

                var name = ReadLocalized(display, "name", language);
                var description = ReadLocalized(display, "desc", language);
                var icon = display is not null && TryGetString(display, "icon", out var iconValue)
                    ? iconValue
                    : string.Empty;
                var iconGray = display is not null && TryGetString(display, "icon_gray", out var iconGrayValue)
                    ? iconGrayValue
                    : string.Empty;

                var isUnlocked = unlockTimes.TryGetValue((groupId, bitKey), out var unlockedAt);
                achievements.Add(new Bridge.Core.Entities.GameAchievement
                {
                    ApiName = apiName,
                    Name = name,
                    Description = description,
                    IsHidden = hidden,
                    IsUnlocked = isUnlocked,
                    UnlockedAt = isUnlocked ? unlockedAt : null,
                    IconUrl = BuildIconUrl(appId, icon),
                    IconLockedUrl = BuildIconUrl(appId, string.IsNullOrEmpty(iconGray) ? icon : iconGray),
                });
            }
        }

        if (achievements.Count == 0)
            return null;

        return new Bridge.Core.Entities.GameAchievementsSnapshot
        {
            Achievements = achievements,
            UnlockedCount = achievements.Count(static a => a.IsUnlocked),
        };
    }

    private static Dictionary<(string Group, string Bit), DateTime> ReadUnlockTimes(
        string steamInstallPath,
        uint appId)
    {
        var result = new Dictionary<(string, string), DateTime>();
        var statsDir = Path.Combine(steamInstallPath, "appcache", "stats");
        var userDataDir = Path.Combine(steamInstallPath, "userdata");
        if (!Directory.Exists(userDataDir))
            return result;

        foreach (var accountDir in Directory.GetDirectories(userDataDir))
        {
            var accountName = Path.GetFileName(accountDir);
            if (!uint.TryParse(accountName, out var accountId) || accountId == 0)
                continue;

            var userStatsPath = Path.Combine(statsDir, $"UserGameStats_{accountId}_{appId}.bin");
            if (!File.Exists(userStatsPath))
                continue;

            byte[] userBytes;
            try
            {
                userBytes = File.ReadAllBytes(userStatsPath);
            }
            catch
            {
                continue;
            }

            Dictionary<string, object> userRoot;
            try
            {
                userRoot = BinaryVdfParser.Parse(userBytes);
            }
            catch
            {
                continue;
            }

            if (!userRoot.TryGetValue("cache", out var cacheObj) ||
                cacheObj is not Dictionary<string, object> cache)
            {
                continue;
            }

            foreach (var (groupId, groupValue) in cache)
            {
                if (groupId is "crc" or "PendingChanges" ||
                    groupValue is not Dictionary<string, object> group ||
                    !group.TryGetValue("AchievementTimes", out var timesObj) ||
                    timesObj is not Dictionary<string, object> times)
                {
                    continue;
                }

                foreach (var (bitId, timestampObj) in times)
                {
                    if (!TryConvertToInt64(timestampObj, out var unixSeconds) || unixSeconds <= 0)
                        continue;

                    DateTime unlockedAt;
                    try
                    {
                        unlockedAt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        continue;
                    }

                    var key = (groupId, bitId);
                    if (!result.TryGetValue(key, out var existing) || unlockedAt > existing)
                        result[key] = unlockedAt;
                }
            }
        }

        return result;
    }

    // Steam stores this as the string "ACHIEVEMENTS" in newer schemas, or "4"
    // with a matching type_int in older ones (TF2, CS2, Dota, etc.).
    private static bool IsAchievementsGroup(Dictionary<string, object> group)
    {
        if (TryGetString(group, "type", out var type))
        {
            if (string.Equals(type, "ACHIEVEMENTS", StringComparison.OrdinalIgnoreCase))
                return true;

            if (type == "4")
                return true;
        }

        return group.TryGetValue("type_int", out var typeIntObj) &&
               TryConvertToInt64(typeIntObj, out var typeInt) &&
               typeInt == 4;
    }

    private static string ReadLocalized(Dictionary<string, object>? node, string key, string language)
    {
        if (node is null || !node.TryGetValue(key, out var valueObj))
            return string.Empty;

        if (valueObj is Dictionary<string, object> localized)
        {
            if (TryGetString(localized, language, out var localizedValue))
                return localizedValue;
            if (TryGetString(localized, "english", out var englishValue))
                return englishValue;
        }

        return valueObj.ToString() ?? string.Empty;
    }

    private static string? BuildIconUrl(string appId, string iconFile)
    {
        if (string.IsNullOrWhiteSpace(iconFile))
            return null;

        return $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{appId}/{iconFile}";
    }

    private static bool TryGetString(Dictionary<string, object> node, string key, out string value)
    {
        value = string.Empty;
        if (!node.TryGetValue(key, out var obj))
            return false;

        value = obj switch
        {
            string text => text,
            _ => obj.ToString() ?? string.Empty,
        };
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryConvertToInt64(object value, out long result)
    {
        switch (value)
        {
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue when ulongValue <= long.MaxValue:
                result = (long)ulongValue;
                return true;
            case float floatValue:
                result = (long)floatValue;
                return true;
            case double doubleValue:
                result = (long)doubleValue;
                return true;
            case string text when long.TryParse(text, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
