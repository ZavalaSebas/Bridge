using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
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

    public ImageSearchWindow(WebImageSearchService searchService, string initialQuery)
    {
        InitializeComponent();
        _searchService = searchService;
        QueryBox.Text = initialQuery;
        ResultsList.ItemsSource = _results;
        ResultsList.SelectionChanged += (_, _) =>
            SelectButton.IsEnabled = ResultsList.SelectedItem is not null;
        ResultsList.MouseDoubleClick += (_, _) => Select_Click(this, new RoutedEventArgs());
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
        StatusText.Text = Strings.Searching;
        Cursor = Cursors.Wait;

        try
        {
            var found = await _searchService.SearchAsync(query);
            foreach (var result in found)
                _results.Add(result);
            StatusText.Text = found.Count == 0
                ? Strings.NoResults
                : string.Format(Strings.ResultsCountFormat, found.Count);
        }
        catch
        {
            StatusText.Text = Strings.SearchFailed;
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
