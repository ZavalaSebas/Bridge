using Bridge.Core.Enums;
using Bridge.Services;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Bridge.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void SearchGoogle(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        SafeLauncher.TryOpenUrl(GameDetailLinkResolver.GetGoogleSearchUrl(query));
    }

    [RelayCommand]
    private void OpenGameLibrary()
    {
        if (SelectedGame is null)
            return;

        var game = SelectedGame;
        var sourceName = _sourceRepository.Get(game.SourceId)?.Name;

        if (GameDetailLinkResolver.IsRomLibrary(game, sourceName))
        {
            NavigationSection = NavigationSection.Roms;
            return;
        }

        if (string.Equals(sourceName, "Steam", StringComparison.OrdinalIgnoreCase))
        {
            var url = GameDetailLinkResolver.GetSteamLibraryUrl(game);
            if (url is not null)
                SafeLauncher.TryOpenUrl(url);
            return;
        }

        if (string.Equals(sourceName, "Epic", StringComparison.OrdinalIgnoreCase))
        {
            SafeLauncher.TryOpenUrl(GameDetailLinkResolver.GetEpicLibraryUrl(game));
        }
    }

    [RelayCommand]
    private void ToggleLibraryFilter(string? libraryName)
    {
        ToggleFilterValue(ActiveLibraryFilters, libraryName);
    }

    [RelayCommand]
    private void OpenMetacritic()
    {
        if (SelectedGame is null)
            return;

        SafeLauncher.TryOpenUrl(GameDetailLinkResolver.GetMetacriticUrl(SelectedGame));
    }

    [RelayCommand]
    private void OpenCommunityScore()
    {
        if (SelectedGame is null)
            return;

        var sourceName = _sourceRepository.Get(SelectedGame.SourceId)?.Name;
        SafeLauncher.TryOpenUrl(GameDetailLinkResolver.GetCommunityScoreUrl(SelectedGame, sourceName));
    }

    [RelayCommand]
    private void OpenHowLongToBeat()
    {
        if (SelectedGame is null)
            return;

        SafeLauncher.TryOpenUrl(GameDetailLinkResolver.GetHowLongToBeatUrl(SelectedGame));
    }

    [RelayCommand]
    private void ToggleGenreFilter(string? genreName) =>
        ToggleFilterValue(ActiveGenreFilters, genreName);

    [RelayCommand]
    private void TogglePlatformFilter(string? platformName) =>
        ToggleFilterValue(ActivePlatformFilters, platformName);

    [RelayCommand]
    private void ToggleDeveloperFilter(string? developerName) =>
        ToggleFilterValue(ActiveDeveloperFilters, developerName);

    [RelayCommand]
    private void TogglePublisherFilter(string? publisherName) =>
        ToggleFilterValue(ActivePublisherFilters, publisherName);

    [RelayCommand]
    private void ToggleFeatureFilter(string? featureName) =>
        ToggleFilterValue(ActiveFeatureFilters, featureName);

    [RelayCommand]
    private void RemoveDetailFilter(DetailFilterChip? chip)
    {
        if (chip is null)
            return;

        switch (chip.Category)
        {
            case "genre":
                RemoveFilterValue(ActiveGenreFilters, chip.Value);
                break;
            case "platform":
                RemoveFilterValue(ActivePlatformFilters, chip.Value);
                break;
            case "library":
                RemoveFilterValue(ActiveLibraryFilters, chip.Value);
                break;
            case "developer":
                RemoveFilterValue(ActiveDeveloperFilters, chip.Value);
                break;
            case "publisher":
                RemoveFilterValue(ActivePublisherFilters, chip.Value);
                break;
            case "feature":
                RemoveFilterValue(ActiveFeatureFilters, chip.Value);
                break;
        }
    }
}
