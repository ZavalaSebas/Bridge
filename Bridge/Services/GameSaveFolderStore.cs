using System.IO;
using System.Text.Json;

namespace Bridge.Services;

/// <summary>
/// Per-game save folders chosen in More → Set Save Location (Steam, Epic, and
/// other PC games). ROM games keep using RetroArch paths instead.
/// </summary>
public static class GameSaveFolderStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string SettingsFile => Config.GameSaveFoldersFilePath;

    public static string? Get(Guid gameId)
    {
        if (gameId == Guid.Empty)
            return null;

        if (!Load().Folders.TryGetValue(gameId.ToString(), out var path))
            return null;

        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    public static void Set(Guid gameId, string? folderPath)
    {
        if (gameId == Guid.Empty)
            return;

        var model = Load();
        var key = gameId.ToString();
        if (string.IsNullOrWhiteSpace(folderPath))
            model.Folders.Remove(key);
        else
            model.Folders[key] = folderPath.Trim();

        Save(model);
    }

    public static IReadOnlyDictionary<Guid, string> GetAll()
    {
        var result = new Dictionary<Guid, string>();
        foreach (var (key, path) in Load().Folders)
        {
            if (Guid.TryParse(key, out var id) && !string.IsNullOrWhiteSpace(path))
                result[id] = path;
        }

        return result;
    }

    private static StoreModel Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<StoreModel>(File.ReadAllText(SettingsFile), JsonOptions)
                    ?? new StoreModel();
        }
        catch
        {
            // Corrupt/missing settings — fall back to empty.
        }

        return new StoreModel();
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

    private sealed class StoreModel
    {
        public Dictionary<string, string> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
