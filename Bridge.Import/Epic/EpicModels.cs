using System.Text.Json.Serialization;

namespace Bridge.Import.Epic;

/// <summary>Shape of %PROGRAMDATA%\Epic\UnrealEngineLauncher\LauncherInstalled.dat.</summary>
internal sealed class LauncherInstalledData
{
    [JsonPropertyName("InstallationList")]
    public List<InstalledApp>? InstallationList { get; set; }
}

internal sealed class InstalledApp
{
    [JsonPropertyName("InstallLocation")]
    public string? InstallLocation { get; set; }

    [JsonPropertyName("AppName")]
    public string? AppName { get; set; }

    [JsonPropertyName("AppID")]
    public long AppId { get; set; }

    [JsonPropertyName("AppVersion")]
    public string? AppVersion { get; set; }
}

/// <summary>Shape of the per-game .item manifests under EpicGamesLauncher\Data\Manifests.</summary>
internal sealed class EpicManifest
{
    [JsonPropertyName("DisplayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("AppName")]
    public string? AppName { get; set; }

    [JsonPropertyName("InstallLocation")]
    public string? InstallLocation { get; set; }

    [JsonPropertyName("LaunchExecutable")]
    public string? LaunchExecutable { get; set; }

    [JsonPropertyName("AppCategories")]
    public List<string>? AppCategories { get; set; }

    [JsonPropertyName("CompatibleApps")]
    public List<string>? CompatibleApps { get; set; }

    [JsonPropertyName("TechnicalType")]
    public string? TechnicalType { get; set; }
}
