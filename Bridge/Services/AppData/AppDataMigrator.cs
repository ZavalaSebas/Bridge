using System.IO;

namespace Bridge.Services;

/// <summary>
/// Applies numbered migrations to files and folders under
/// <c>%LOCALAPPDATA%\Bridge\</c>. Runs at every startup before EF migrations so
/// a new exe can reshape AppData (rename folders, reformat settings, remove
/// obsolete files) without manual user cleanup. The applied step is recorded in
/// <see cref="Config.AppDataVersionFilePath"/>.
/// </summary>
public static class AppDataMigrator
{
    public const int LatestVersion = 2;

    private static readonly Action<AppDataMigrationContext>[] Steps =
    new Action<AppDataMigrationContext>[]
    {
        AppDataMigrations.V1_InitializeLayoutAndLegacyCleanup,
        AppDataMigrations.V2_MoveLooseConfigFilesToConfigDirectory,
    };

    /// <param name="appDataPath">
    /// Override for unit tests; production passes null and uses
    /// <see cref="Config.AppDataPath"/>.
    /// </param>
    public static void MigrateToLatest(string? appDataPath = null)
    {
        var root = appDataPath ?? Config.AppDataPath;
        Directory.CreateDirectory(root);

        var ctx = new AppDataMigrationContext(root);
        var current = ReadVersion(root);

        for (var step = current; step < LatestVersion; step++)
        {
            Steps[step](ctx);
            WriteVersion(root, step + 1);
        }
    }

    internal static int ReadVersion(string root)
    {
        var path = Path.Combine(root, Config.AppDataVersionFileName);
        if (!File.Exists(path))
            return 0;

        try
        {
            var text = File.ReadAllText(path).Trim();
            return int.TryParse(text, out var version) && version >= 0 ? version : 0;
        }
        catch
        {
            return 0;
        }
    }

    internal static void WriteVersion(string root, int version)
    {
        var path = Path.Combine(root, Config.AppDataVersionFileName);
        File.WriteAllText(path, version.ToString());
    }
}
