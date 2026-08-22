namespace Bridge.Metadata;

/// Maps Bridge ROM platform names to RetroAchievements console names.
public static class RetroAchievementsConsoleCatalog
{
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nintendo Entertainment System"] = ["NES", "Nintendo Entertainment System"],
        ["Super Nintendo Entertainment System"] = ["SNES", "Super Nintendo", "Super Nintendo Entertainment System"],
        ["Nintendo 64"] = ["Nintendo 64", "N64"],
        ["Game Boy"] = ["Game Boy", "GB"],
        ["Game Boy Color"] = ["Game Boy Color", "GBC"],
        ["Game Boy Advance"] = ["Game Boy Advance", "GBA"],
        ["Nintendo DS"] = ["Nintendo DS", "DS"],
        ["Sega Genesis / Mega Drive"] = ["Mega Drive", "Genesis", "Sega Genesis"],
        ["Sega Master System"] = ["Master System", "Sega Master System"],
        ["Sega Game Gear"] = ["Game Gear", "Sega Game Gear"],
        ["Atari 2600"] = ["Atari 2600"],
        ["Atari 7800"] = ["Atari 7800"],
        ["PC Engine / TurboGrafx-16"] = ["PC Engine", "TurboGrafx-16", "PC Engine/TurboGrafx-16"],
        ["Atari Lynx"] = ["Atari Lynx", "Lynx"],
        ["WonderSwan / WonderSwan Color"] = ["WonderSwan", "WonderSwan Color"],
    };

    public static int? TryResolveConsoleId(
        string platformName,
        IReadOnlyDictionary<string, int> consoleIdsByName)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            return null;

        if (consoleIdsByName.TryGetValue(platformName.Trim(), out var directId))
            return directId;

        if (Aliases.TryGetValue(platformName.Trim(), out var aliases))
        {
            foreach (var alias in aliases)
            {
                if (consoleIdsByName.TryGetValue(alias, out var aliasId))
                    return aliasId;
            }
        }

        foreach (var (name, id) in consoleIdsByName)
        {
            if (name.Contains(platformName, StringComparison.OrdinalIgnoreCase) ||
                platformName.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// Console IDs to query when matching a ROM hash. Some dual-mode GB/GBC titles
    /// (e.g. Pokémon Yellow) are indexed under Game Boy on RetroAchievements.
    /// </summary>
    public static IReadOnlyList<int> ResolveConsoleIdsForHashLookup(
        string platformName,
        IReadOnlyDictionary<string, int> consoleIdsByName)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            return [];

        var ids = new List<int>();
        var primary = TryResolveConsoleId(platformName, consoleIdsByName);
        if (primary is not null)
            ids.Add(primary.Value);

        if (string.Equals(platformName.Trim(), "Game Boy Color", StringComparison.OrdinalIgnoreCase))
        {
            var gameBoy = TryResolveConsoleId("Game Boy", consoleIdsByName);
            if (gameBoy is not null && !ids.Contains(gameBoy.Value))
                ids.Add(gameBoy.Value);
        }

        return ids;
    }
}
