namespace Bridge.Services;

/// <summary>
/// Folds a display name to a comparable form so already-imported detection can
/// match names that differ only in whitespace/punctuation/edition markers:
/// "Alan Wake" == "AlanWake", "Genshin Impact game" == "GenshinImpact",
/// "Fallout 3 goty" == "Fallout3". Pure and unit-tested.
/// </summary>
public static class InstalledNameNormalizer
{
    private static readonly string[] Suffixes =
    [
        "goty", "gameoftheyear", "edition", "complete",
        "remastered", "deluxe", "ultimate", "enhanced",
        "launcher", "launch"
    ];

    public static string Normalize(string name)
    {
        var clean = new string(name
            .Where(c => !char.IsWhiteSpace(c) && c is not '_' and not '.' and not '-' and not ':' and not '\'' and not '(' and not ')')
            .ToArray())
            .ToLowerInvariant();

        bool changed;
        do
        {
            changed = false;
            foreach (var suffix in Suffixes)
            {
                if (clean.EndsWith(suffix, StringComparison.Ordinal))
                {
                    clean = clean[..^suffix.Length];
                    changed = true;
                    break;
                }
            }
        }
        while (changed);

        return clean;
    }
}
