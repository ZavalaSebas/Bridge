using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Bridge.Services;

namespace Bridge;

public partial class ImageSearchWindow : Window
{
    private readonly WebImageSearchService _searchService;
    private readonly ObservableCollection<ImageSearchResult> _results = [];

    /// <summary>Full-size URL of the selected image, or null if cancelled.</summary>
    public string? SelectedImageUrl { get; private set; }

    public ImageSearchWindow(WebImageSearchService searchService, string initialQuery)
    {
        InitializeComponent();
        _searchService = searchService;
        QueryBox.Text = initialQuery;
        ResultsList.ItemsSource = _results;
        Loaded += async (_, _) =>
        {
            QueryBox.Focus();
            QueryBox.SelectAll();
            await SearchAsync();
        };
    }

    private async Task SearchAsync()
    {
        var query = QueryBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        _results.Clear();
        StatusText.Text = "Searching...";
        Cursor = Cursors.Wait;

        try
        {
            var found = await _searchService.SearchAsync(query);
            foreach (var result in found)
                _results.Add(result);
            StatusText.Text = found.Count == 0 ? "No results." : $"{found.Count} results";
        }
        catch
        {
            StatusText.Text = "Search failed.";
        }
        finally
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void Search_Click(object sender, RoutedEventArgs e) => _ = SearchAsync();

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is ImageSearchResult selected)
        {
            SelectedImageUrl = selected.ImageUrl;
            DialogResult = true;
        }
    }
}
