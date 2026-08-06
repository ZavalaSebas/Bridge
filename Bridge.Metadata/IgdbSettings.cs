namespace Bridge.Metadata;

/// <summary>
/// IGDB (via Twitch's OAuth2) requires a Client ID + Secret from a free Twitch
/// Developer account — see https://dev.twitch.tv/console/apps. Never hardcode
/// real values here (see DEVELOPMENT.md "No hardcoded secrets"); the `Bridge`
/// app is responsible for loading/saving these from user input, not this class.
/// </summary>
public class IgdbSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
