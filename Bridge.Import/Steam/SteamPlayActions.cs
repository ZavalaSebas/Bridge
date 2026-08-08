using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Import.Steam;

/// <summary>
/// Builds the automatic Steam play action for a Steam-imported game, mirroring
/// Playnite's SteamPlayController (PROJECT_FOUNDATION.md §28.26): Playnite never
/// stores a GameAction for Steam games — the plugin resolves one at runtime and
/// launches via the steam:// URI, never the local .exe (Steamworks DRM — running
/// the exe directly without the Steam client fails, which is why Playnite doesn't).
///
/// The launched action is a URL action: steam://rungameid/{appid}, tracked by
/// directory (watch processes running from the game's InstallDirectory) since the
/// launched process is steam.exe, not the game.
/// </summary>
public static class SteamPlayActions
{
    /// <summary>
    /// Returns the runtime play action for a Steam-imported game, or null if the
    /// game isn't Steam-imported (custom game or ExternalId isn't a numeric appid).
    /// Does NOT check whether Steam is installed — the caller decides that.
    /// </summary>
    public static GameAction? CreatePlayAction(Game game)
    {
        if (game.IsCustomGame)
        {
            return null;
        }

        if (!uint.TryParse(game.ExternalId, out var appId))
        {
            return null;
        }

        return new GameAction
        {
            Type = GameActionType.Url,
            Name = "Play via Steam",
            IsPlayAction = true,
            Path = $"steam://rungameid/{appId}",
            TrackingMode = TrackingMode.Directory
        };
    }
}
