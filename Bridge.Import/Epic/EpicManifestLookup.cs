using System.Text.Json;

namespace Bridge.Import.Epic;

/// Resolves Epic catalog metadata from local .item manifests.
public static class EpicManifestLookup
{
    public static string? TryGetSandboxId(string appName, string? manifestsDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(appName))
            return null;

        manifestsDirectory ??= EpicPaths.ManifestsDirectory;
        if (!Directory.Exists(manifestsDirectory))
            return null;

        foreach (var file in Directory.EnumerateFiles(manifestsDirectory, "*.item"))
        {
            EpicManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<EpicManifest>(File.ReadAllText(file));
            }
            catch (JsonException)
            {
                continue;
            }

            if (manifest is null ||
                !string.Equals(manifest.AppName, appName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return string.IsNullOrWhiteSpace(manifest.CatalogNamespace)
                ? null
                : manifest.CatalogNamespace.Trim();
        }

        return null;
    }
}
