namespace Bridge.Metadata;

/// <summary>RetroAchievements credentials from retroachievements.org/controlpanel.php.</summary>
public class RetroAchievementsSettings
{
    public string Username { get; set; } = string.Empty;
    public string WebApiKey { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConnectToken { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(WebApiKey);

    public bool IsEmulatorConfigured =>
        !string.IsNullOrWhiteSpace(Username) &&
        (!string.IsNullOrWhiteSpace(ConnectToken) || !string.IsNullOrWhiteSpace(Password));
}
