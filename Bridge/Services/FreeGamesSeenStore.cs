using System.IO;
using System.Text.Json;

namespace Bridge.Services;

public static class FreeGamesSeenStore
{
    private static string FilePath => Path.Combine(Config.ConfigDirectoryPath, "free-games-seen.json");

    public static HashSet<int> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new HashSet<int>();
            var json = File.ReadAllText(FilePath);
            var ids = JsonSerializer.Deserialize<HashSet<int>>(json);
            return ids ?? new HashSet<int>();
        }
        catch { return new HashSet<int>(); }
    }

    public static void Save(HashSet<int> ids)
    {
        try
        {
            Directory.CreateDirectory(Config.ConfigDirectoryPath);
            var json = JsonSerializer.Serialize(ids);
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }

    public static void MarkSeen(IEnumerable<int> ids)
    {
        var seen = Load();
        foreach (var id in ids) seen.Add(id);
        Save(seen);
    }
}
