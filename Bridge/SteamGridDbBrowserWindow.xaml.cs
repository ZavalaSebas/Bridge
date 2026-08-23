using System.Windows;
using System.Windows.Input;
using Bridge.Services;
using Microsoft.Web.WebView2.Core;
using Wpf.Ui.Controls;

namespace Bridge;

public partial class SteamGridDbBrowserWindow : FluentWindow
{
    private const string BlankPageProbeScript = """
        (function() {
          try {
            var body = document.body;
            if (!body) return true;
            if (body.getBoundingClientRect().height < 40) return true;
            var text = (body.innerText || '').replace(/\s+/g, ' ').trim();
            if (text.length > 40) return false;
            var app = document.querySelector('#app, #root, main, [role="main"]');
            if (app && (app.innerHTML || '').length > 250) return false;
            return true;
          } catch (e) {
            return true;
          }
        })()
        """;

    private bool _authFlowActive;
    private bool _postAuthApiLoad;
    private bool _refreshInProgress;
    private int _refreshGeneration;
    private CancellationTokenSource? _lifetimeCts;
    private TaskCompletionSource<bool>? _navigationDone;

    public SteamGridDbBrowserWindow()
    {
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        _lifetimeCts = new CancellationTokenSource();

        try
        {
            await Browser.EnsureCoreWebView2Async();
            var core = Browser.CoreWebView2;
            core.NavigationStarting += OnNavigationStarting;
            core.NavigationCompleted += OnNavigationCompleted;
            core.NewWindowRequested += OnNewWindowRequested;
            Browser.Source = new Uri(SteamGridDbUrls.ApiPreferences);
        }
        catch (Exception ex)
        {
            App.LogException(ex);
            SafeLauncher.TryOpenUrl(SteamGridDbUrls.ApiPreferences);
            Close();
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        _authFlowActive = true;
        Browser.CoreWebView2?.Navigate(e.Uri);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target))
            return;

        if (IsSteamHost(target))
        {
            _authFlowActive = true;
            return;
        }

        if (Browser.Source is not Uri current)
            return;

        if (IsSteamHost(current) && IsSteamGridDbHost(target))
        {
            _authFlowActive = true;
            return;
        }

        if (IsSteamGridDbHost(current) && !IsSteamGridDbHost(target))
            _authFlowActive = true;
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _navigationDone?.TrySetResult(e.IsSuccess);

        if (!e.IsSuccess || Browser.CoreWebView2 is null)
            return;

        if (Browser.Source is not Uri uri || !IsSteamGridDbHost(uri))
            return;

        if (_refreshInProgress)
            return;

        var afterAuth = _authFlowActive;
        _authFlowActive = false;

        if (afterAuth && !IsApiPreferencesPage(uri))
        {
            _postAuthApiLoad = true;
            Browser.CoreWebView2.Navigate(SteamGridDbUrls.ApiPreferences);
            return;
        }

        if (!IsApiPreferencesPage(uri))
            return;

        var fixBlankPage = _postAuthApiLoad || afterAuth;
        _postAuthApiLoad = false;

        if (!fixBlankPage)
        {
            var token = _lifetimeCts?.Token ?? CancellationToken.None;
            try
            {
                await Task.Delay(1500, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (!fixBlankPage && !await IsPageBlankAsync())
            return;

        if (fixBlankPage || await IsPageBlankAsync())
            await RefreshUntilContentAsync();
    }

    private async Task RefreshUntilContentAsync()
    {
        if (_refreshInProgress)
            return;

        _refreshInProgress = true;
        var generation = ++_refreshGeneration;
        var token = _lifetimeCts?.Token ?? CancellationToken.None;

        try
        {
            for (var attempt = 0; attempt < 6; attempt++)
            {
                if (token.IsCancellationRequested || generation != _refreshGeneration)
                    return;

                await Task.Delay(attempt == 0 ? 600 : 900, token);

                if (Browser.CoreWebView2 is null)
                    return;

                if (!await IsPageBlankAsync())
                    return;

                Browser.CoreWebView2.Navigate(SteamGridDbUrls.ApiPreferences);
                await WaitForNavigationAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // Window closed.
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task WaitForNavigationAsync(CancellationToken token)
    {
        _navigationDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = token.Register(() => _navigationDone.TrySetCanceled(token));
        try
        {
            await _navigationDone.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
        }
        catch (TimeoutException)
        {
            // Keep retry loop going.
        }
    }

    private async Task<bool> IsPageBlankAsync()
    {
        if (Browser.CoreWebView2 is null)
            return false;

        try
        {
            var result = await Browser.CoreWebView2.ExecuteScriptAsync(BlankPageProbeScript);
            return string.Equals(result, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSteamHost(Uri uri)
    {
        var host = uri.Host;
        return host.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase) ||
               host.Contains("steampowered.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSteamGridDbHost(Uri uri) =>
        uri.Host.Contains("steamgriddb.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsApiPreferencesPage(Uri uri) =>
        IsSteamGridDbHost(uri) &&
        uri.AbsolutePath.Contains("/profile/preferences/api", StringComparison.OrdinalIgnoreCase);

    private void OpenExternal_Click(object sender, RoutedEventArgs e) =>
        SafeLauncher.TryOpenUrl(Browser.Source?.ToString() ?? SteamGridDbUrls.ApiPreferences);

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _postAuthApiLoad = true;
        Browser.CoreWebView2?.Navigate(SteamGridDbUrls.ApiPreferences);
        await RefreshUntilContentAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetimeCts?.Cancel();
        _lifetimeCts?.Dispose();
        _lifetimeCts = null;

        if (Browser.CoreWebView2 is { } core)
        {
            core.NavigationStarting -= OnNavigationStarting;
            core.NavigationCompleted -= OnNavigationCompleted;
            core.NewWindowRequested -= OnNewWindowRequested;
        }

        Browser.Dispose();
        base.OnClosed(e);
    }
}
