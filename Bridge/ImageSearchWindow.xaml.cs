using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bridge.Converters;
using Bridge.Resources;
using Bridge.Services;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class ImageSearchWindow : FluentWindow
{
    private readonly WebImageSearchService _searchService;
    private readonly ObservableCollection<ImageSearchResult> _results = [];

    /// <summary>Full-size URL of the selected image, or null if cancelled.</summary>
    public string? SelectedImageUrl { get; private set; }

    public ImageSearchWindow(WebImageSearchService searchService, string initialQuery, string mediaField = "")
    {
        InitializeComponent();
        _searchService = searchService;
        QueryBox.Text = initialQuery;
        ConfigureResultsLayout(mediaField);
        ResultsList.ItemsSource = _results;
        ResultsList.SelectionChanged += (_, _) =>
        {
            var hasSelection = ResultsList.SelectedItem is not null;
            SelectButton.IsEnabled = hasSelection;
            UpdatePreview();
        };
        ResultsList.MouseDoubleClick += (_, _) => _ = SelectAsync();
        QueryBox.KeyDown += QueryBox_KeyDown;
        PreviewScrollViewer.SizeChanged += (_, _) => UpdatePreview();
        Loaded += async (_, _) =>
        {
            QueryBox.Focus();
            QueryBox.SelectAll();
            await SearchAsync();
        };
    }

    private void ConfigureResultsLayout(string mediaField)
    {
        var (templateKey, panelKey) = mediaField switch
        {
            "Icon" => ("WebIconTileTemplate", "ArtworkTwoColumnPanel"),
            "CoverImage" => ("WebCoverTileTemplate", "ArtworkTwoColumnPanel"),
            "BackgroundImage" => ("WebHeroTileTemplate", "ArtworkSingleColumnPanel"),
            _ => ("WebHeroTileTemplate", "ArtworkSingleColumnPanel")
        };

        ResultsList.ItemTemplate = (DataTemplate)FindResource(templateKey);
        ResultsList.ItemsPanel = (ItemsPanelTemplate)FindResource(panelKey);
    }

    private void QueryBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        var query = QueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        _results.Clear();
        ResultsList.SelectedItem = null;
        UpdatePreview();
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        StatusText.Text = Strings.Searching;
        SearchButton.IsEnabled = false;
        Cursor = Cursors.Wait;

        try
        {
            var found = await _searchService.SearchAsync(query);
            foreach (var result in found)
                _results.Add(result);

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
            SearchButton.IsEnabled = true;
            Cursor = Cursors.Arrow;
        }
    }

    private void UpdatePreview()
    {
        if (ResultsList.SelectedItem is not ImageSearchResult selected)
        {
            CachedImage.SetSourceUrl(PreviewImage, null);
            PreviewPlaceholderPanel.Visibility = Visibility.Visible;
            PreviewDimensionsText.Text = string.Empty;
            ArtworkPreviewHelper.ClearFrame(PreviewFrame);
            return;
        }

        PreviewPlaceholderPanel.Visibility = Visibility.Collapsed;
        CachedImage.SetSourceUrl(PreviewImage, !string.IsNullOrWhiteSpace(selected.ImageUrl)
            ? selected.ImageUrl
            : selected.ThumbnailUrl);
        PreviewDimensionsText.Text = selected.Width > 0 && selected.Height > 0
            ? Strings.Format(nameof(Strings.ImageDimensionsFormat), selected.Width, selected.Height)
            : string.Empty;

        ApplyPreviewLayout(selected.Width, selected.Height);
    }

    private void ApplyPreviewLayout(int width, int height)
    {
        void Apply() => ArtworkPreviewHelper.ApplyFrame(PreviewFrame, width, height, PreviewScrollViewer);

        if (PreviewScrollViewer.ActualHeight > 80)
            Apply();
        else
            Dispatcher.BeginInvoke(Apply, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Search_Click(object sender, RoutedEventArgs e) => _ = SearchAsync();

    private void Select_Click(object sender, RoutedEventArgs e) => _ = SelectAsync();

    private async Task SelectAsync()
    {
        if (ResultsList.SelectedItem is not ImageSearchResult selected)
            return;

        SelectButton.IsEnabled = false;
        StatusText.Text = Strings.LoadingImage;
        Cursor = Cursors.Wait;

        try
        {
            var url = await ResolveSelectableUrlAsync(selected);
            if (url is null)
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

    private static async Task<string?> ResolveSelectableUrlAsync(ImageSearchResult result)
    {
        foreach (var url in CandidateUrls(result))
        {
            await RemoteImageCache.PreloadAndWaitAsync([url]).ConfigureAwait(true);
            if (RemoteImageCache.IsCached(url))
                return url;
        }

        return null;
    }

    private static IEnumerable<string> CandidateUrls(ImageSearchResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ImageUrl))
            yield return result.ImageUrl;

        if (!string.IsNullOrWhiteSpace(result.ThumbnailUrl) &&
            !string.Equals(result.ThumbnailUrl, result.ImageUrl, StringComparison.OrdinalIgnoreCase))
        {
            yield return result.ThumbnailUrl;
        }
    }
}
