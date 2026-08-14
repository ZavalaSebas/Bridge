namespace Bridge.Services;

/// <summary>
/// Pure logic for process-tree playtime tracking — the algorithm Playnite's
/// MonitorProcessTree uses (PROJECT_FOUNDATION.md §28.10): start with the
/// launched PID, then every poll expand to include any process whose parent is
/// already in the tree, and prune to the ones still alive. This is what makes
/// launcher-based games (Genshin's launcher.exe, Epic/GOG frontends) track
/// correctly: the launcher spawns the real game and exits, but the game stays
/// in the tree as a descendant, so the session survives until the game itself
/// closes.
///
/// Split from the OS snapshot (ProcessTreeSnapshot) so the expansion can be
/// unit-tested with synthetic process tables — no real processes needed.
/// </summary>
public static class ProcessTreeExpander
{
    /// <summary>
    /// Given the previous tree (as a set of PIDs) and a snapshot of every live
    /// process with its parent, returns the new tree: every snapshot process
    /// whose parent is in the tree joins it, then dead PIDs are pruned.
    /// </summary>
    public static IReadOnlySet<int> ExpandAndPrune(
        IEnumerable<int> previousTree,
        IReadOnlyList<ProcessEntry> snapshot)
    {
        var tree = new HashSet<int>(previousTree);
        var alive = new HashSet<int>(snapshot.Select(e => e.Pid));

        // Grow: a process joins the tree when its parent is already in it. The
        // snapshot is iterated repeatedly because the launcher can be several
        // hops up (launcher -> updater -> game); each pass adds one more level.
        bool changed;
        do
        {
            changed = false;
            foreach (var entry in snapshot)
            {
                if (tree.Contains(entry.ParentPid) && tree.Add(entry.Pid))
                {
                    changed = true;
                }
            }
        } while (changed);

        // Prune to live processes only. The snapshot is a point-in-time list,
        // so anything in the tree but not in it has already exited.
        tree.IntersectWith(alive);
        return tree;
    }
}
