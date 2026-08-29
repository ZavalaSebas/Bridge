using System.IO;

namespace Bridge.Services;

public enum AppFont
{
    Inter,
    SegoeUi,
    SegoeUiVariable,
    Consolas,
    Georgia
}

/// <summary>
/// Persists the UI font family under AppData/config — mismo patrón que LanguageSettingsStore.
/// Inter (embebida) es el default.
/// </summary>
public static class FontSettingsStore
{
    private static string SettingsFile => Config.FontFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "font.txt");

    public static AppFont Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile, out var saved) ||
                TryLoadFromFile(LegacySettingsFile, out saved))
                return saved;
        }
        catch { }
        return AppFont.Inter;
    }

    public static void Save(AppFont font)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, font.ToString());
        }
        catch { }
    }

    private static bool TryLoadFromFile(string path, out AppFont font)
    {
        font = AppFont.Inter;
        return File.Exists(path) && Enum.TryParse<AppFont>(File.ReadAllText(path).Trim(), out font);
    }
}
