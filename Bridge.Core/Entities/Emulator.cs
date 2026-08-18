namespace Bridge.Core.Entities;

/// <summary>User-configured emulator install with one or more launch profiles.</summary>
public class EmulatorProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Guid> PlatformIds { get; set; } = [];
    public List<string> ImageExtensions { get; set; } = [];
    public string Executable { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    // Set for Bridge-managed RetroArch profiles; optional for custom emulators.
    public string CorePath { get; set; } = string.Empty;
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
