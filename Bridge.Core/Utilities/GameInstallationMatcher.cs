using Bridge.Core.Entities;
using Bridge.Core.Enums;

namespace Bridge.Core.Utilities;

/// <summary>
/// PC-game identity is an installation (exe path or install folder), not a title.
/// Steam/Epic/external copies of the same name are distinct unless they share disk.
/// </summary>
public static class GameInstallationMatcher
{
    public static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var trimmed = path.Trim();
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return trimmed;
        }
    }

    public static bool SharesInstallation(
        Game game,
        string? executablePath = null,
        string? installDirectory = null)
    {
        var exe = NormalizePath(executablePath);
        if (exe is not null)
        {
            foreach (var action in game.GameActions)
            {
                if (action.Type != GameActionType.File || string.IsNullOrWhiteSpace(action.Path))
                    continue;

                var actionPath = NormalizePath(action.Path);
                if (actionPath is not null &&
                    actionPath.Equals(exe, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (IsUnderGameInstall(game, exe) || HasFileActionUnder(game, Path.GetDirectoryName(exe)))
                return true;
        }

        var folder = NormalizePath(installDirectory);
        if (folder is null)
            return false;

        return IsUnderGameInstall(game, folder)
               || GameInstallIsUnder(game, folder)
               || HasFileActionUnder(game, folder);
    }

    public static Game? FindUserManagedAt(
        IEnumerable<Game> games,
        string? executablePath = null,
        string? installDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(executablePath) && string.IsNullOrWhiteSpace(installDirectory))
            return null;

        return games.FirstOrDefault(game =>
            GameSource.IsUserManaged(game.SourceId)
            && game.Roms.Count == 0
            && SharesInstallation(game, executablePath, installDirectory));
    }

    private static bool IsUnderGameInstall(Game game, string? path)
    {
        var root = NormalizePath(game.InstallDirectory);
        var candidate = NormalizePath(path);
        return root is not null
               && candidate is not null
               && PathContainment.IsPathUnderDirectory(candidate, root);
    }

    private static bool GameInstallIsUnder(Game game, string folder)
    {
        var root = NormalizePath(game.InstallDirectory);
        return root is not null && PathContainment.IsPathUnderDirectory(root, folder);
    }

    private static bool HasFileActionUnder(Game game, string? folder)
    {
        var root = NormalizePath(folder);
        if (root is null)
            return false;

        foreach (var action in game.GameActions)
        {
            if (action.Type != GameActionType.File || string.IsNullOrWhiteSpace(action.Path))
                continue;

            var actionPath = NormalizePath(action.Path);
            if (actionPath is not null && PathContainment.IsPathUnderDirectory(actionPath, root))
                return true;
        }

        return false;
    }
}
