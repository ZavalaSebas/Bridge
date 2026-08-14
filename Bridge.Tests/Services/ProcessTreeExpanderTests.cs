using Bridge.Services;

namespace Bridge.Tests.Services;

// The tree expansion is pure (no real processes) — the OS snapshot boundary
// (ProcessTreeSnapshot) is verified by launching the app. These cover the
// launcher-spawns-child-and-exits shapes: Genshin's launcher.exe, a launcher
// with an updater hop, and a game that never spawns.
public class ProcessTreeExpanderTests
{
    private static IReadOnlyList<ProcessEntry> Snapshot(params ProcessEntry[] entries) => entries;

    [Fact]
    public void ExpandAndPrune_LauncherSpawnsGame_BothStayAlive()
    {
        var snapshot = Snapshot(
            new ProcessEntry(100, 4),   // launcher, parent System
            new ProcessEntry(200, 100)); // game, child of launcher

        var tree = ProcessTreeExpander.ExpandAndPrune([100], snapshot);

        Assert.Equal(new HashSet<int> { 100, 200 }, tree);
    }

    [Fact]
    public void ExpandAndPrune_LauncherExitsAfterSpawning_GameSurvives()
    {
        // First poll: launcher (100) alive, spawns game (200).
        var tree = ProcessTreeExpander.ExpandAndPrune(
            [100],
            Snapshot(new ProcessEntry(100, 4), new ProcessEntry(200, 100)));
        Assert.Contains(200, tree);

        // Second poll: launcher gone, game still running. The game stays in the
        // tree as a descendant, so the session keeps counting.
        tree = ProcessTreeExpander.ExpandAndPrune(
            tree,
            Snapshot(new ProcessEntry(200, 100)));

        Assert.Equal(new HashSet<int> { 200 }, tree);
        Assert.NotEmpty(tree);
    }

    [Fact]
    public void ExpandAndPrune_GameExits_TreeEmpties()
    {
        // Launcher and game both dead — nothing in the snapshot, tree prunes away.
        var tree = ProcessTreeExpander.ExpandAndPrune(
            [100, 200],
            Snapshot());

        Assert.Empty(tree);
    }

    [Fact]
    public void ExpandAndPrune_MultiHopLauncher_GrandchildJoinsAcrossPolls()
    {
        // Poll 1: launcher (100) spawns updater (150).
        var tree = ProcessTreeExpander.ExpandAndPrune(
            [100],
            Snapshot(new ProcessEntry(100, 4), new ProcessEntry(150, 100)));
        Assert.Contains(150, tree);

        // Poll 2: launcher dead, updater spawns the real game (200).
        tree = ProcessTreeExpander.ExpandAndPrune(
            tree,
            Snapshot(new ProcessEntry(150, 100), new ProcessEntry(200, 150)));
        Assert.Contains(200, tree);

        // Poll 3: updater dead too, game alone keeps the session.
        tree = ProcessTreeExpander.ExpandAndPrune(
            tree,
            Snapshot(new ProcessEntry(200, 150)));
        Assert.Equal(new HashSet<int> { 200 }, tree);
    }

    [Fact]
    public void ExpandAndPrune_NeverSpawns_TreePrunesToEmpty()
    {
        // Launcher exits without ever spawning the game: nothing joins, and the
        // dead launcher is pruned — no phantom session.
        var tree = ProcessTreeExpander.ExpandAndPrune(
            [100],
            Snapshot(new ProcessEntry(100, 4)));

        tree = ProcessTreeExpander.ExpandAndPrune(tree, Snapshot());
        Assert.Empty(tree);
    }
}
