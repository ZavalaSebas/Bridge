namespace Bridge.Services;

public sealed class UserProfile
{
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>One of the preset ids from <see cref="UserProfileAvatarHelper.DefaultAvatarIds"/>.</summary>
    public string DefaultAvatarId { get; set; } = UserProfileAvatarHelper.DefaultAvatarIds[0];

    /// <summary>Absolute path to a custom avatar image under AppData, or empty for a preset.</summary>
    public string CustomAvatarPath { get; set; } = string.Empty;

    public bool UseCustomAvatar { get; set; }

    public static UserProfile CreateDefault() => new()
    {
        DisplayName = string.Empty,
        DefaultAvatarId = UserProfileAvatarHelper.DefaultAvatarIds[0]
    };
}
