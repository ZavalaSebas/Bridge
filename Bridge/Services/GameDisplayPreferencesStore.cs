using System.IO;
using System.Text.Json;

namespace Bridge.Services;

/// <summary>Per-game UI preferences that are not part of the core game record.</summary>
public static class GameDisplayPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string SettingsFile => Config.GameDisplayPreferencesFilePath;

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
            if (!File.Exists(SettingsFile))
                return new StoreModel();

            return JsonSerializer.Deserialize<StoreModel>(File.ReadAllText(SettingsFile)) ?? new StoreModel();
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
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(model, JsonOptions));
        }
        catch
        {
            // Persisting must never crash the app.
        }
    }

    private sealed class StoreModel
    {
        public Dictionary<string, bool> HeroCoverLarge { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
