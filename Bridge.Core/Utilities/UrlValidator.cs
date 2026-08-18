using System.Net;
using System.Net.Sockets;

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

        if (IPAddress.TryParse(uri.Host, out var address))
            return IsBlockedIpAddress(address);

        return HostResolvesToBlockedAddress(uri.Host);
    }

    private static bool HostResolvesToBlockedAddress(string host)
    {
        try
        {
            foreach (var address in Dns.GetHostAddresses(host))
            {
                if (IsBlockedIpAddress(address))
                    return true;
            }
        }
        catch
        {
            // Unknown/unresolvable hostnames are rejected — artwork and links
            // must use a reachable public host.
            return true;
        }

        return false;
    }

    private static bool IsBlockedIpAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
                return true;

            // ::1 already covered by IsLoopback; fc00::/7 unique local.
            var bytes = address.GetAddressBytes();
            return bytes[0] == 0xFC || bytes[0] == 0xFD;
        }

        var ipv4 = address.GetAddressBytes();
        // 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 169.254.0.0/16
        if (ipv4[0] == 10)
            return true;
        if (ipv4[0] == 172 && ipv4[1] >= 16 && ipv4[1] <= 31)
            return true;
        if (ipv4[0] == 192 && ipv4[1] == 168)
            return true;
        if (ipv4[0] == 169 && ipv4[1] == 254)
            return true;

        return false;
    }

    /// <summary>Sanitizes a link URL before persisting; returns null when rejected.</summary>
    public static string? SanitizePersistedUrl(string? url) =>
        IsSafeHttpUrl(url) || IsSafeToOpen(url) ? url!.Trim() : null;
}
