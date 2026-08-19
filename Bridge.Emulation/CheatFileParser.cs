using System.Text.RegularExpressions;

namespace Bridge.Emulation;

/// Parses and patches RetroArch .cht files from libretro-database.
public static class CheatFileParser
{
    private static readonly Regex CountPattern = new(@"^\s*cheats\s*=\s*""?(\d+)""?\s*$", RegexOptions.Multiline);

    public static CheatParseResult Parse(string rawText)
    {
        var countMatch = CountPattern.Match(rawText);
        if (!countMatch.Success || !int.TryParse(countMatch.Groups[1].Value, out var count) || count < 0)
        {
            return new CheatParseResult(false, []);
        }

        var cheats = new List<Cheat>(count);
        for (var i = 0; i < count; i++)
        {
            var descMatch = DescPattern(i).Match(rawText);
            var enableMatch = EnablePattern(i).Match(rawText);

            if (!descMatch.Success || !enableMatch.Success)
            {
                return new CheatParseResult(false, []);
            }

            cheats.Add(new Cheat
            {
                Index = i,
                Description = descMatch.Groups[1].Value,
                Enabled = bool.Parse(enableMatch.Groups[1].Value)
            });
        }

        return new CheatParseResult(true, cheats);
    }

    public static string SetEnabled(string rawText, int index, bool enabled)
    {
        var match = EnablePattern(index).Match(rawText);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"cheat{index}_enable not found in the provided text — SetEnabled must only be called after a successful Parse of the same text.");
        }

        var valueGroup = match.Groups[1];
        var replacement = enabled ? "true" : "false";
        return rawText[..valueGroup.Index] + replacement + rawText[(valueGroup.Index + valueGroup.Length)..];
    }

    private static Regex DescPattern(int index) =>
        new($@"^\s*cheat{index}_desc\s*=\s*""([^""]*)""", RegexOptions.Multiline);

    private static Regex EnablePattern(int index) =>
        new($@"^\s*cheat{index}_enable\s*=\s*""?(true|false)""?", RegexOptions.Multiline);
}
