namespace Bridge.Core.Utilities;

/// <summary>
/// Validates that a resolved file path stays inside an expected root directory.
/// Used by library importers so manifest/VDF paths cannot escape install folders.
/// </summary>
public static class PathContainment
{
    /// <summary>
    /// Combines <paramref name="rootDirectory"/> and <paramref name="relativePath"/>,
    /// resolves to a full path, and returns it only when it lies under
    /// <paramref name="rootDirectory"/> (case-insensitive on Windows).
    /// </summary>
    public static string? TryResolveUnderRoot(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (Path.IsPathRooted(relativePath))
            return null;

        if (relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is ".."))
            return null;

        var rootFull = Path.GetFullPath(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var candidate = Path.GetFullPath(Path.Combine(rootFull, relativePath));

        return IsUnderRoot(candidate, rootFull) ? candidate : null;
    }

    /// <summary>
    /// Returns true when <paramref name="filePath"/> is the root or a file under it.
    /// </summary>
    public static bool IsUnderRoot(string filePath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootDirectory))
            return false;

        var pathFull = Path.GetFullPath(filePath);
        var rootFull = Path.GetFullPath(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (pathFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            return false;

        return pathFull.Length > rootFull.Length &&
               pathFull[rootFull.Length] is ('\\' or '/');
    }

    /// <summary>
    /// Process-path prefix match with a directory boundary (Steam2 must not match Steam).
    /// </summary>
    public static bool IsPathUnderDirectory(string filePath, string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(installDirectory))
            return false;

        if (!filePath.StartsWith(installDirectory, StringComparison.OrdinalIgnoreCase))
            return false;

        return filePath.Length == installDirectory.Length ||
               filePath[installDirectory.Length] is ('\\' or '/');
    }
}
