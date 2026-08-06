namespace Bridge.Core.Entities;

/// <summary>
/// Playnite splits emulator profiles into CustomEmulatorProfile (user-configured)
/// and BuiltInEmulatorProfile (picked from a bundled catalog of known emulators
/// and their launch conventions, PROJECT_FOUNDATION.md §28.1). Bridge's MVP has
/// no bundled catalog yet (that's future scope per PLAN.md), so there is only one
/// EmulatorProfile shape for now — it's Playnite's CustomEmulatorProfile fields.
/// Reintroducing a BuiltIn variant later, once Bridge ships its own known-emulator
/// catalog, doesn't require touching this class — it would be an additional type,
/// not a rewrite of this one.
/// </summary>
public class EmulatorProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Guid> PlatformIds { get; set; } = [];
    public List<string> ImageExtensions { get; set; } = [];
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string StartupScript { get; set; } = string.Empty;
    public string PreScript { get; set; } = string.Empty;
    public string PostScript { get; set; } = string.Empty;
    public string ExitScript { get; set; } = string.Empty;
}

public class Emulator : DatabaseObject
{
    public string InstallDirectory { get; set; } = string.Empty;
    public List<EmulatorProfile> Profiles { get; set; } = [];

    public EmulatorProfile? GetProfile(string profileId) =>
        Profiles.FirstOrDefault(p => p.Id == profileId);
}
