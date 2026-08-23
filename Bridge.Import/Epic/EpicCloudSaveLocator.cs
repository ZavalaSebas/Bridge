namespace Bridge.Import.Epic;

/// <summary>
/// Finds Epic Games Store cloud-save caches under
/// <c>%LOCALAPPDATA%\EpicGamesLauncher\Saved\Cloud\{account}\{appName}</c>.
/// Only games that use Epic Cloud have a folder; it is a launcher cache, not
/// always the live save directory the game writes to.
/// </summary>
public static class EpicCloudSaveLocator
{
    public static string? TryFind(string? localAppData, string? appName)
    {
        if (string.IsNullOrWhiteSpace(localAppData) || string.IsNullOrWhiteSpace(appName))
            return null;

        var cloudRoot = Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Cloud");
        if (!Directory.Exists(cloudRoot))
            return null;

        var target = appName.Trim();
        string? best = null;
        var bestWrite = DateTime.MinValue;

        try
        {
            foreach (var accountDir in Directory.EnumerateDirectories(cloudRoot))
            {
                string? candidate = null;
                try
                {
                    candidate = Directory.EnumerateDirectories(accountDir)
                        .FirstOrDefault(dir =>
                            Path.GetFileName(dir).Equals(target, StringComparison.OrdinalIgnoreCase));
                }
                catch (IOException)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var write = Directory.GetLastWriteTimeUtc(candidate);
                if (best is null || write >= bestWrite)
                {
                    best = candidate;
                    bestWrite = write;
                }
            }
        }
        catch (IOException)
        {
            return best;
        }

        return best;
    }
}
