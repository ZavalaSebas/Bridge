using Bridge.Core.Entities;
using Bridge.Core.Enums;
using Bridge.Statistics;

namespace Bridge.Tests.Statistics;

public class GameGroupResolverTests
{
    private static readonly Guid Bethesda = Guid.NewGuid();
    private static readonly Guid CDPR = Guid.NewGuid();
    private static readonly Guid Steam = Guid.NewGuid();
    private static readonly Guid Epic = Guid.NewGuid();

    private static GameGroupResolver CreateResolver() => new(
        companyNames: new Dictionary<Guid, string> { [Bethesda] = "Bethesda", [CDPR] = "CD Projekt Red" },
        platformNames: new Dictionary<Guid, string>(),
        genreNames: new Dictionary<Guid, string>(),
        sourceNames: new Dictionary<Guid, string> { [Steam] = "Steam", [Epic] = "Epic" });

    [Fact]
    public void GetGroupKey_Name_GroupsByFirstLetter()
    {
        var resolver = CreateResolver();

        Assert.Equal("A", resolver.GetGroupKey(new Game { Name = "alpha" }, GameGroupField.Name));
        Assert.Equal("B", resolver.GetGroupKey(new Game { Name = "Borderlands" }, GameGroupField.Name));
        Assert.Equal("Unknown", resolver.GetGroupKey(new Game { Name = "" }, GameGroupField.Name));
    }

    [Fact]
    public void GetGroupKey_Developer_ResolvesNameOrUnknown()
    {
        var resolver = CreateResolver();

        Assert.Equal("Bethesda", resolver.GetGroupKey(new Game { DeveloperIds = [Bethesda] }, GameGroupField.Developer));
        Assert.Equal("Unknown", resolver.GetGroupKey(new Game(), GameGroupField.Developer));
    }

    [Fact]
    public void GetGroupKey_Library_ResolvesSourceName()
    {
        var resolver = CreateResolver();

        Assert.Equal("Steam", resolver.GetGroupKey(new Game { SourceId = Steam }, GameGroupField.Library));
        Assert.Equal("Manual", resolver.GetGroupKey(new Game { SourceId = Guid.Empty }, GameGroupField.Library));
    }

    [Fact]
    public void GetGroupKey_IsInstalled_GroupsByState()
    {
        var resolver = CreateResolver();

        Assert.Equal("Installed", resolver.GetGroupKey(new Game { IsInstalled = true }, GameGroupField.IsInstalled));
        Assert.Equal("Not installed", resolver.GetGroupKey(new Game { IsInstalled = false }, GameGroupField.IsInstalled));
    }

    [Fact]
    public void GetGroupKey_CompletionStatus_GroupsByPlayedFlag()
    {
        var resolver = CreateResolver();

        Assert.Equal("Played", resolver.GetGroupKey(new Game { PlaytimeSeconds = 60 }, GameGroupField.CompletionStatus));
        Assert.Equal("Not Played", resolver.GetGroupKey(new Game { PlaytimeSeconds = 0 }, GameGroupField.CompletionStatus));
    }

    [Fact]
    public void GetGroupKey_Playtime_BucketsByDuration()
    {
        var resolver = CreateResolver();

        Assert.Equal("Not played", resolver.GetGroupKey(new Game { PlaytimeSeconds = 0 }, GameGroupField.PlaytimeSeconds));
        Assert.Equal("Less than 1 hour", resolver.GetGroupKey(new Game { PlaytimeSeconds = 3000 }, GameGroupField.PlaytimeSeconds));
        Assert.Equal("100+ hours", resolver.GetGroupKey(new Game { PlaytimeSeconds = 3600 * 500 }, GameGroupField.PlaytimeSeconds));
    }

    [Fact]
    public void GetGroupKey_InstallSize_GroupsByBucket()
    {
        var resolver = CreateResolver();

        Assert.Equal("Not installed", resolver.GetGroupKey(new Game(), GameGroupField.InstallSizeBytes));
        Assert.Equal("Less than 1 GB", resolver.GetGroupKey(new Game { IsInstalled = true, InstallSizeBytes = 500 }, GameGroupField.InstallSizeBytes));
        Assert.Equal("100+ GB", resolver.GetGroupKey(new Game { IsInstalled = true, InstallSizeBytes = 200UL * 1024 * 1024 * 1024 }, GameGroupField.InstallSizeBytes));
    }

    [Fact]
    public void GetGroupKey_InstallDrive_UsesPathRoot()
    {
        var resolver = CreateResolver();

        Assert.Equal("C:\\", resolver.GetGroupKey(new Game { InstallDirectory = @"C:\Games\Game" }, GameGroupField.InstallDrive));
        Assert.Equal("Unknown", resolver.GetGroupKey(new Game(), GameGroupField.InstallDrive));
    }

    [Fact]
    public void GetGroupKey_ReleaseYear_UsesYearOrUnknown()
    {
        var resolver = CreateResolver();

        Assert.Equal("2020", resolver.GetGroupKey(new Game { ReleaseDate = new ReleaseDate(2020) }, GameGroupField.ReleaseYear));
        Assert.Equal("Unknown", resolver.GetGroupKey(new Game(), GameGroupField.ReleaseYear));
    }

    [Fact]
    public void GetGroupKey_None_ReturnsEmpty()
    {
        var resolver = CreateResolver();

        Assert.Equal(string.Empty, resolver.GetGroupKey(new Game { Name = "Any" }, GameGroupField.None));
    }
}
