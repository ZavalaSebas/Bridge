namespace Bridge.Metadata;

/// <summary>
/// SteamGridDB API key from https://www.steamgriddb.com/profile/preferences/api .
/// Loaded/saved by the Bridge app — never hardcode real keys here.
/// </summary>
public class SteamGridDbSettings
{
    public string ApiKey { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
