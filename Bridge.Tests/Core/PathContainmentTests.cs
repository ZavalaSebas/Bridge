using Bridge.Core.Utilities;

namespace Bridge.Tests.Core;

public class PathContainmentTests
{
    [Fact]
    public void IsPathUnderDirectory_rejects_prefix_without_boundary()
    {
        var root = @"C:\Games\Steam";
        Assert.False(PathContainment.IsPathUnderDirectory(@"C:\Games\Steam2\game.exe", root));
    }

    [Fact]
    public void IsPathUnderDirectory_accepts_child_path()
    {
        var root = @"C:\Games\Steam";
        Assert.True(PathContainment.IsPathUnderDirectory(@"C:\Games\Steam\game.exe", root));
    }

    [Fact]
    public void TryResolveUnderRoot_rejects_parent_segments()
    {
        var resolved = PathContainment.TryResolveUnderRoot(@"C:\Epic", @"..\windows\system32\calc.exe");
        Assert.Null(resolved);
    }
}
