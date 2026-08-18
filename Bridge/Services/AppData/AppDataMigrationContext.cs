using System.IO;

namespace Bridge.Services;

/// <summary>
/// Paths and file helpers for one AppData migration step. Each step receives a
/// context rooted at the user's Bridge folder (or a temp dir in unit tests).
/// </summary>
public sealed class AppDataMigrationContext
{
    public AppDataMigrationContext(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    public string Root { get; }

    public string Combine(params string[] segments) => Path.Combine([Root, ..segments]);

    public void EnsureDirectory(params string[] segments)
    {
        Directory.CreateDirectory(Combine(segments));
    }

    /// <summary>
    /// Moves files from <paramref name="sourceSegments"/> into
    /// <paramref name="destinationSegments"/>, skipping names that already exist
    /// at the destination. Deletes the source directory when empty afterward.
    /// </summary>
    public void MergeDirectoryContentsIfExists(string[] sourceSegments, string[] destinationSegments)
    {
        var source = Combine(sourceSegments);
        if (!Directory.Exists(source))
            return;

        var destination = Combine(destinationSegments);
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target))
                continue;

            File.Move(file, target);
        }

        if (!Directory.EnumerateFileSystemEntries(source).Any())
            Directory.Delete(source);
    }

    public void ReplaceFileTextIfExists(string[] segments, Func<string, string> transform)
    {
        var path = Combine(segments);
        if (!File.Exists(path))
            return;

        var original = File.ReadAllText(path);
        var updated = transform(original);
        if (!string.Equals(original, updated, StringComparison.Ordinal))
            File.WriteAllText(path, updated);
    }

    public void ReplaceFileLinesIfExists(string[] segments, Func<IReadOnlyList<string>, IReadOnlyList<string>> transform)
    {
        var path = Combine(segments);
        if (!File.Exists(path))
            return;

        var original = File.ReadAllLines(path);
        var updated = transform(original);
        if (original.SequenceEqual(updated))
            return;

        File.WriteAllLines(path, updated);
    }

    public void DeleteFileIfExists(params string[] segments)
    {
        var path = Combine(segments);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void DeleteDirectoryIfExists(params string[] segments)
    {
        var path = Combine(segments);
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
