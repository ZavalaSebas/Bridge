namespace Bridge.Core.Entities;

/// <summary>Persisted hero/banner modes stored in <see cref="Game.BackgroundImage"/>.</summary>
public static class HeroBackground
{
    public const string BlackSentinel = "bridge:hero/black";

    public enum Kind
    {
        Default,
        Black,
        Custom
    }

    public static bool IsBlack(string? value) =>
        string.Equals(value?.Trim(), BlackSentinel, StringComparison.Ordinal);

    public static bool IsDefault(string? value) =>
        string.IsNullOrWhiteSpace(value);

    public static bool IsCustom(string? value) =>
        !IsDefault(value) && !IsBlack(value);

    public static Kind KindFromValue(string? value)
    {
        if (IsBlack(value))
            return Kind.Black;

        if (IsCustom(value))
            return Kind.Custom;

        return Kind.Default;
    }

    public static string ValueFromKind(Kind kind, string? customUrl = null) => kind switch
    {
        Kind.Black => BlackSentinel,
        Kind.Custom => customUrl?.Trim() ?? string.Empty,
        _ => string.Empty
    };

    /// <summary>
    /// Steam's local librarycache hero applies only when the user has not chosen
    /// a custom/black banner, unless a metadata refresh explicitly overwrites.
    /// </summary>
    public static bool ShouldFillHeroFromSteamLocal(string? currentBackground, bool overwrite) =>
        overwrite || IsDefault(currentBackground);

    /// <summary>Icon/cover local art fills only missing values unless overwriting.</summary>
    public static bool ShouldFillArtwork(string? current, bool overwrite) =>
        overwrite || string.IsNullOrWhiteSpace(current);
}
