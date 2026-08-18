using Bridge.Core.Contracts;
using Bridge.Core.Import;
using Bridge.Metadata;

namespace Bridge.Services;

/// <summary>
/// Centralizes metadata provider fallback chains so MainViewModel does not
/// duplicate the same Steam/IGDB ordering in four places.
/// </summary>
public sealed class MetadataSyncService(
    IEnumerable<IGameMetadataProvider> metadataProviders,
    SteamMetadataProvider steamMetadataProvider,
    BridgeIgdbProvider bridgeIgdbProvider)
{
    private readonly IGameMetadataProvider[] _chain = metadataProviders.ToArray();

    public async Task<(GameMetadata Metadata, string ProviderName)?> SearchForManualDownloadAsync(
        string gameName,
        bool romImport,
        string? steamAppId,
        CancellationToken cancellationToken = default)
    {
        if (!romImport && !string.IsNullOrWhiteSpace(steamAppId) && uint.TryParse(steamAppId, out _))
        {
            try
            {
                var steam = await steamMetadataProvider.GetByAppIdAsync(steamAppId, cancellationToken);
                if (steam is not null)
                    return (steam, steamMetadataProvider.Name);
            }
            catch
            {
                // Fall through to the name chain.
            }
        }

        return await SearchByNameAsync(gameName, romImport ? MetadataSearchMode.RomImport : MetadataSearchMode.IgdbFirst, cancellationToken);
    }

    public Task<(GameMetadata Metadata, string ProviderName)?> SearchForAddedGameAsync(
        string gameName,
        bool romImport,
        CancellationToken cancellationToken = default) =>
        SearchByNameAsync(
            romImport ? gameName : gameName,
            romImport ? MetadataSearchMode.RomImport : MetadataSearchMode.SteamFirst,
            cancellationToken);

    public Task<(GameMetadata Metadata, string ProviderName)?> SearchByNameChainAsync(
        string gameName,
        CancellationToken cancellationToken = default) =>
        SearchByNameAsync(gameName, MetadataSearchMode.IgdbFirst, cancellationToken);

    public async Task EnrichSteamLinksFromIgdbAsync(string gameName, GameMetadata metadata, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await bridgeIgdbProvider.SearchAsync(gameName, cancellationToken) is { } igdbMetadata)
                metadata.Links.AddRange(igdbMetadata.Links);
        }
        catch
        {
            // Worker unreachable — Steam links alone are fine.
        }
    }

    private async Task<(GameMetadata Metadata, string ProviderName)?> SearchByNameAsync(
        string gameName,
        MetadataSearchMode mode,
        CancellationToken cancellationToken)
    {
        if (mode == MetadataSearchMode.SteamFirst)
        {
            try
            {
                if (await steamMetadataProvider.SearchAsync(gameName, cancellationToken) is { } steamFound)
                    return (steamFound, steamMetadataProvider.Name);
            }
            catch
            {
                // Fall through to IGDB chain.
            }
        }

        foreach (var provider in _chain)
        {
            if (ReferenceEquals(provider, steamMetadataProvider))
                continue;

            try
            {
                if (await provider.SearchAsync(gameName, cancellationToken) is { } found)
                    return (found, provider.Name);
            }
            catch
            {
                // Try next provider.
            }
        }

        if (mode is MetadataSearchMode.RomImport or MetadataSearchMode.SteamFallback)
        {
            try
            {
                if (await steamMetadataProvider.SearchAsync(gameName, cancellationToken) is { } steamFound)
                    return (steamFound, steamMetadataProvider.Name);
            }
            catch
            {
                // No metadata for this title.
            }
        }

        return null;
    }

    private enum MetadataSearchMode
    {
        IgdbFirst,
        SteamFirst,
        RomImport,
        SteamFallback
    }
}
