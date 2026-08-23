using System.IO;
using System.Text.Json;

namespace Bridge.Services;

/// <summary>Per-game UI preferences that are not part of the core game record.</summary>
public static class GameDisplayPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string SettingsFile => Config.GameDisplayPreferencesFilePath;
    private static string LegacySettingsFile => Path.Combine(Config.AppDataPath, "game-display-preferences.json");

    public static bool GetHeroCoverLarge(Guid gameId)
    {
        if (gameId == Guid.Empty)
            return false;

        return Load().HeroCoverLarge.TryGetValue(gameId.ToString(), out var large) && large;
    }

    public static void SetHeroCoverLarge(Guid gameId, bool large)
    {
        if (gameId == Guid.Empty)
            return;

        var model = Load();
        model.HeroCoverLarge[gameId.ToString()] = large;
        Save(model);
    }

    private static StoreModel Load()
    {
        try
        {
            if (TryLoadFromFile(SettingsFile) is { } current)
                return current;

            return TryLoadFromFile(LegacySettingsFile) ?? new StoreModel();
        }
        catch
        {
            return new StoreModel();
        }
    }

    private static void Save(StoreModel model)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(model, JsonOptions));
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private static StoreModel? TryLoadFromFile(string path)
    {
        if (!File.Exists(path))
            return null;

        return JsonSerializer.Deserialize<StoreModel>(File.ReadAllText(path));
    }

    private sealed class StoreModel
    {
        public Dictionary<string, bool> HeroCoverLarge { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
