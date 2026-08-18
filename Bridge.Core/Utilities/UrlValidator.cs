namespace Bridge.Core.Utilities;

/// <summary>
/// Shared URL scheme checks for links opened in a browser/launcher and for
/// artwork fetched over HTTP.
/// </summary>
public static class UrlValidator
{
    private static readonly HashSet<string> AllowedOpenSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http",
        "https",
        "steam",
        "com.epicgames.launcher"
    };

    /// <summary>True when the URL is safe to pass to Process.Start(UseShellExecute).</summary>
    public static bool IsSafeToOpen(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        return AllowedOpenSchemes.Contains(uri.Scheme);
    }

    /// <summary>True for http/https URLs suitable to store on a game or download as artwork.</summary>
    public static bool IsSafeHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        return !IsBlockedHost(uri);
    }

    /// <summary>Rejects loopback, private, and link-local hosts (SSRF mitigation).</summary>
    public static bool IsBlockedHost(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
            return true;

        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!System.Net.IPAddress.TryParse(uri.Host, out var address))
            return false;

        if (System.Net.IPAddress.IsLoopback(address))
            return true;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16
            if (bytes[0] == 10)
                return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                return true;
            if (bytes[0] == 192 && bytes[1] == 168)
                return true;
            if (bytes[0] == 169 && bytes[1] == 254)
                return true;
        }

        return false;
    }

    /// <summary>Sanitizes a link URL before persisting; returns null when rejected.</summary>
    public static string? SanitizePersistedUrl(string? url) =>
        IsSafeHttpUrl(url) || IsSafeToOpen(url) ? url!.Trim() : null;
}
