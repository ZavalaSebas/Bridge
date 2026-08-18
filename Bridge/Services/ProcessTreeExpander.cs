namespace Bridge.Services;

/// <summary>
/// Expands a process tree for playtime tracking: each poll adds children whose
/// parent is already in the set. Unit-tested separately from OS snapshots.
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
