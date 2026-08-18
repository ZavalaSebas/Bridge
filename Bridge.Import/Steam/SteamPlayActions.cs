using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Import.Steam;

/// <summary>
/// Runtime Steam play action: steam://rungameid/{appid}, tracked by install
/// directory because the launched process is steam.exe, not the game exe.
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
