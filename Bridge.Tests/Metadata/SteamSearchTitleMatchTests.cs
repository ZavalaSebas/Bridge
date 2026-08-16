using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class SteamSearchTitleMatchTests
{
    private static List<string> Tokenize(string name)
    {
        var method = typeof(SteamMetadataProvider).GetMethod(
            "Tokenize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (List<string>)method!.Invoke(null, [name])!;
    }

    private static bool TitleContains(IReadOnlyCollection<string> queryWords, string title)
    {
        var method = typeof(SteamMetadataProvider).GetMethod(
            "TitleContains",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (bool)method!.Invoke(null, [queryWords, title])!;
    }

    [Fact]
    public void ExactTitle_Matches()
    {
        var words = Tokenize("Risk of Rain 2");
        Assert.True(TitleContains(words, "Risk of Rain 2"));
    }

    [Fact]
    public void TitleWithEditionSuffix_StillMatches()
    {
        var words = Tokenize("Fallout 3");
        Assert.True(TitleContains(words, "Fallout 3 - Game of the Year Edition"));
    }

    [Fact]
    public void UnrelatedFirstResult_DoesNotMatch()
    {
        // Genshin Impact isn't on Steam, but the store's first search result is
        // "Dream of Corpse Lady" — its title shares none of the searched words.
        var words = Tokenize("Genshin Impact");
        Assert.False(TitleContains(words, "Dream of Corpse Lady"));
    }

    [Fact]
    public void SequencedTitle_DistinguishesFromOriginal()
    {
        // "2" is kept as a token so Risk of Rain 2 doesn't match plain
        // Risk of Rain (or vice versa).
        var words = Tokenize("Risk of Rain 2");
        Assert.False(TitleContains(words, "Risk of Rain"));
    }

    [Fact]
    public void Tokenize_DropsCommonFiller()
    {
        var words = Tokenize("The Evil Within");
        Assert.DoesNotContain("the", words);
        Assert.DoesNotContain("of", words);
        Assert.Contains("evil", words);
        Assert.Contains("within", words);
    }

    [Fact]
    public void RomanNumeralToken_IsPreserved()
    {
        var words = Tokenize("Grand Theft Auto V");
        Assert.Contains("v", words);
    }
}