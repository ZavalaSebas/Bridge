using Bridge.Metadata;

namespace Bridge.Tests.Metadata;

public class HowLongToBeatClientTests
{
    [Fact]
    public void PickBestMatch_PrefersExactName()
    {
        var results = new List<HowLongToBeatGame>
        {
            new() { Id = 1, Name = "Elden Ring: Shadow of the Erdtree", MainSeconds = 1000 },
            new() { Id = 2, Name = "Elden Ring", MainSeconds = 2000 }
        };

        var match = HowLongToBeatClient.PickBestMatch("Elden Ring", results);

        Assert.NotNull(match);
        Assert.Equal(2, match.Id);
        Assert.Equal(2000UL, match.MainSeconds);
    }

    [Theory]
    [InlineData("Elden Ring", "ELDENRING", 100)]
    [InlineData("Half-Life 2", "HALFLIFE2", 100)]
    public void ScoreNameMatch_TreatsPunctuationAsEquivalent(string left, string right, int expectedMinimum)
    {
        var score = HowLongToBeatClient.ScoreNameMatch(
            HowLongToBeatClient.NormalizeName(left),
            HowLongToBeatClient.NormalizeName(right));

        Assert.True(score >= expectedMinimum);
    }
}
