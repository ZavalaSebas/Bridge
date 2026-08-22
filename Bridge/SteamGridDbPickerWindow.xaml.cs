using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Bridge.Converters;
using Bridge.Metadata;
using Bridge.Resources;
using Bridge.Services;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class SteamGridDbPickerWindow : FluentWindow
{
    private readonly SteamGridDbClient _client;
    private readonly SteamGridDbAssetKind _assetKind;
    private readonly string _assetKindLabel;
    private readonly ObservableCollection<SteamGridDbGame> _games = [];
    private readonly ObservableCollection<SteamGridDbAsset> _assets = [];

    public string? SelectedImageUrl { get; private set; }

    public SteamGridDbPickerWindow(SteamGridDbClient client, string initialQuery, string mediaField)
    {
        InitializeComponent();
        _client = client;
        _assetKind = SteamGridDbClient.MediaFieldToKind(mediaField);
        _assetKindLabel = MediaPickerLabels.AssetKindLabel(_assetKind);
        TitleSubtitleText.Text = _assetKindLabel;
        AssetKindBadgeText.Text = _assetKindLabel;
        Title = $"{Strings.SteamGridDb} — {_assetKindLabel}";
        ResultsList.ItemTemplate = (DataTemplate)FindResource(_assetKind switch
        {
            SteamGridDbAssetKind.Hero => "SgdbHeroTileTemplate",
            SteamGridDbAssetKind.Cover => "SgdbCoverTileTemplate",
            _ => "SgdbIconTileTemplate"
        });
        ResultsList.ItemsPanel = (ItemsPanelTemplate)FindResource(
            _assetKind == SteamGridDbAssetKind.Hero ? "ArtworkSingleColumnPanel" : "ArtworkTwoColumnPanel");

        GamesList.ItemsSource = _games;
        ResultsList.ItemsSource = _assets;
        ResultsList.SelectionChanged += (_, _) =>
        {
            SelectButton.IsEnabled = ResultsList.SelectedItem is SteamGridDbAsset;
            UpdatePreview();
        };
        ResultsList.MouseDoubleClick += (_, _) => _ = SelectAssetAsync();
        QueryBox.Text = initialQuery;
        QueryBox.KeyDown += QueryBox_KeyDown;
        PreviewScrollViewer.SizeChanged += (_, _) => UpdatePreview();
        Loaded += async (_, _) =>
        {
            QueryBox.Focus();
            QueryBox.SelectAll();
            await SearchGamesAsync();
        };
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = SearchGamesAsync();
        }
    }

    private async Task SearchGamesAsync()
    {
        var query = QueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        _games.Clear();
        _assets.Clear();
        ResultsList.SelectedItem = null;
        UpdatePreview();
        UpdateGamesSidebar();
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        SelectButton.IsEnabled = false;
        StatusText.Text = Strings.Searching;
        SearchButton.IsEnabled = false;
        Cursor = Cursors.Wait;

        try
        {
            var found = await _client.SearchGamesAsync(query);
            foreach (var game in found)
                _games.Add(game);

            if (found.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                ArtworkHeadingText.Text = string.Empty;
                StatusText.Text = Strings.NoResults;
                return;
            }

            UpdateGamesSidebar();
            GamesList.SelectedIndex = 0;
            StatusText.Text = found.Count == 1
                ? string.Format(Strings.SteamGridDbArtworkForFormat, found[0].Name)
                : Strings.SteamGridDbPickGameHint;
        }
        catch
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            StatusText.Text = Strings.SearchFailed;
        }
        finally
        {
            SearchButton.IsEnabled = true;
            Cursor = Cursors.Arrow;
        }
    }

    private async void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GamesList.SelectedItem is SteamGridDbGame game)
            await LoadAssetsAsync(game);
    }

    private async Task LoadAssetsAsync(SteamGridDbGame game)
    {
        _assets.Clear();
        ResultsList.SelectedItem = null;
        UpdatePreview();
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        SelectButton.IsEnabled = false;
        ArtworkHeadingText.Text = string.Format(Strings.SteamGridDbArtworkForFormat, game.Name);
        StatusText.Text = Strings.Searching;
        Cursor = Cursors.Wait;

        try
        {
            var found = await _client.GetAssetsAsync(game.Id, _assetKind);
            foreach (var asset in found)
                _assets.Add(asset);

            EmptyStatePanel.Visibility = found.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = found.Count == 0
                ? Strings.NoResults
                : string.Format(Strings.ResultsCountFormat, found.Count);
        }
        catch
        {
            EmptyStatePanel.Visibility = Visibility.Visible;
            StatusText.Text = Strings.SearchFailed;
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void UpdateGamesSidebar()
    {
        var showSidebar = _games.Count > 1;
        GamesSidebar.Visibility = showSidebar ? Visibility.Visible : Visibility.Collapsed;
        GamesSidebarColumn.Width = showSidebar ? new GridLength(288) : new GridLength(0);
        MatchesCountText.Text = showSidebar
            ? string.Format(Strings.ResultsCountFormat, _games.Count)
            : string.Empty;
    }

    private void UpdatePreview()
    {
        if (ResultsList.SelectedItem is not SteamGridDbAsset asset)
        {
            CachedImage.SetSourceUrl(PreviewImage, null);
            PreviewPlaceholderPanel.Visibility = Visibility.Visible;
            PreviewDimensionsText.Text = string.Empty;
            ArtworkPreviewHelper.ClearFrame(PreviewFrame);
            return;
        }

        PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
        CachedImage.SetSourceUrl(PreviewImage, !string.IsNullOrWhiteSpace(asset.Url) ? asset.Url : asset.ThumbUrl);
        PreviewDimensionsText.Text = asset.Width > 0 && asset.Height > 0
            ? Strings.Format(nameof(Strings.ImageDimensionsFormat), asset.Width, asset.Height)
            : string.Empty;

        ApplyPreviewLayout(asset.Width, asset.Height);
    }

    private void ApplyPreviewLayout(int width, int height)
    {
        void Apply() => ArtworkPreviewHelper.ApplyFrame(PreviewFrame, width, height, PreviewScrollViewer);

        if (PreviewScrollViewer.ActualHeight > 80)
            Apply();
        else
            Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Search_Click(object sender, RoutedEventArgs e) => _ = SearchGamesAsync();

    private void Select_Click(object sender, RoutedEventArgs e) => _ = SelectAssetAsync();

    private async Task SelectAssetAsync()
    {
        if (ResultsList.SelectedItem is not SteamGridDbAsset asset)
            return;

        SelectButton.IsEnabled = false;
        StatusText.Text = Strings.LoadingImage;
        Cursor = Cursors.Wait;

        try
        {
            var url = asset.Url;
            await RemoteImageCache.PreloadAndWaitAsync([url]).ConfigureAwait(true);
            if (!RemoteImageCache.IsCached(url) && !string.IsNullOrWhiteSpace(asset.ThumbUrl))
            {
                url = asset.ThumbUrl;
                await RemoteImageCache.PreloadAndWaitAsync([url]).ConfigureAwait(true);
            }

            if (!RemoteImageCache.IsCached(url))
            {
                StatusText.Text = Strings.ImageSelectLoadFailed;
                SelectButton.IsEnabled = true;
                return;
            }

            SelectedImageUrl = url;
            DialogResult = true;
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }
}
