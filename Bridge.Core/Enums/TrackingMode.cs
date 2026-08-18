namespace Bridge.Core.Enums;

/// <summary>
/// How the launcher decides a game session is still running. Directory and
/// process-tree modes cover launchers that spawn the real game and exit.
/// </summary>
public enum TrackingMode
{
    /// <summary>Process tree for File/Emulator actions, install directory for Url actions.</summary>
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
