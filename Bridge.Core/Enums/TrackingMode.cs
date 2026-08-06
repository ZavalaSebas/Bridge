namespace Bridge.Core.Enums;

/// <summary>
/// How Bridge decides a launched game's process is still running. Names and
/// semantics match Playnite's real TrackingMode 1:1 (PROJECT_FOUNDATION.md §28.8)
/// deliberately, so the reference algorithm in §28.9-28.10 maps directly onto this
/// enum with no translation. The launcher-spawns-child-and-exits case (Steam/Epic/
/// GOG/emulator frontends) is why Directory/ProcessName exist: the launched
/// process is often not the game itself.
/// </summary>
public enum TrackingMode
{
    /// <summary>Best-effort automatic choice — process tree for File/Emulator actions, directory for URL actions.</summary>
    Default,
    /// <summary>Track the launched process and every descendant it spawns.</summary>
    Process,
    /// <summary>Track only the exact process that was launched, no children.</summary>
    OriginalProcess,
    /// <summary>Track any process running from a given directory (for launchers that exit after spawning the real game).</summary>
    Directory,
    /// <summary>Track any process matching a given process name.</summary>
    ProcessName
}
