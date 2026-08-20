using System.Net.Http;
using Bridge.Core.Entities;
using Bridge.Core.Utilities;
using Bridge.Emulation;
using Bridge.Metadata;
using Bridge.Statistics;

namespace Bridge.Services;

/// <summary>
/// Fetches completion-time estimates from howlongtobeat.com and stores them on <see cref="Game"/>.
/// </summary>
public sealed class HowLongToBeatService(HowLongToBeatClient client)
{
    public async Task<bool> TryEnrichGameAsync(Game game, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (!overwrite && TimeToBeatHelper.GetProgressTarget(game) > 0)
            return false;

        var searchName = game.Roms.Count > 0
            ? RomScanner.ToSearchName(game.Name)
            : game.Name;

        HowLongToBeatGame? match;
        try
        {
            match = await client.SearchBestMatchAsync(searchName, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (match is null || !match.HasAnyTime)
            return false;

        if (match.MainSeconds is > 0)
            game.TimeToBeatMainSeconds = match.MainSeconds;
        if (match.ExtraSeconds is > 0)
            game.TimeToBeatExtraSeconds = match.ExtraSeconds;
        if (match.CompleteSeconds is > 0)
            game.TimeToBeatCompleteSeconds = match.CompleteSeconds;

        if (!string.IsNullOrWhiteSpace(match.ProfileUrl))
        {
            var sanitized = UrlValidator.SanitizePersistedUrl(match.ProfileUrl);
            if (sanitized is not null)
            {
                var known = new HashSet<string>(game.Links.Select(l => l.Url), StringComparer.OrdinalIgnoreCase);
                if (known.Add(sanitized))
                {
                    game.Links.Add(new Link
                    {
                        Name = "HowLongToBeat",
                        Url = sanitized
                    });
                }
            }
        }

        return true;
    }
}
