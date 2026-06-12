using All_in_One_Messenger.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System;

namespace All_in_One_Messenger.Pages;

/// <summary>
/// This WebView page dynamically creates a custom chat server.
/// </summary>
public sealed partial class CustomServerPage : WebViewPageBase
{
    private readonly WebView2 _webView;
    private readonly string _appId;
    private readonly Uri _startUri;
    private readonly Grid? _errorOverlay;

    public override WebView2 WebView => _webView;
    public override string AppId => _appId;
    public override Uri StartUri => _startUri;

    public CustomServerPage(CustomServerInfo info)
    {
        _appId = info.Id;

        _startUri = Uri.TryCreate(
            info.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? info.Url : "https://" + info.Url,
            UriKind.Absolute, out var uri) ? uri : new Uri("https://" + info.Url);

        _webView = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _errorOverlay = BuildErrorOverlay();

        var grid = new Grid();
        grid.Children.Add(_webView);
        grid.Children.Add(_errorOverlay);
        Content = grid;

        InitWebView();
    }

    /// <summary>
    /// Handling when CoreWebView2 is Ready.
    /// </summary>
    /// <param name="core"></param>
    protected override void OnCoreWebView2Ready(CoreWebView2 core)
    {
        core.NavigationCompleted += OnNavigationCompleted;

        // Custom servers don't have a fixed URL to determine login,
        // so assume a session is established as soon as navigation is successful.
        WebViewNotificationHelper.AttachSessionDetector(_appId, core, url => true, resetOnFalse: false);
    }

    /// <summary>
    /// Navigates the WebView to the new URL (used when the user edits the server from Settings).
    /// </summary>
    /// <param name="url"></param>
    public void NavigateTo(string url)
    {
        var uri = Uri.TryCreate(
            url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "https://" + url,
            UriKind.Absolute, out var u) ? u : new Uri("https://" + url);

        if (_webView.CoreWebView2 is not null)
            _webView.CoreWebView2.Navigate(uri.ToString());
        else
            _webView.Source = uri;
    }

    /// <summary>
    /// Processing after navigator successful.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        // Only process the first navigation or when the URL is StartUri
        bool failed = !args.IsSuccess && IsConnectionError(args.WebErrorStatus);

        _webView.Visibility = failed ? Visibility.Collapsed : Visibility.Visible;
        if (_errorOverlay is not null)
            _errorOverlay.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Check for errors when connecting to the webview..
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    private static bool IsConnectionError(CoreWebView2WebErrorStatus status) => status is
        CoreWebView2WebErrorStatus.CannotConnect or
        CoreWebView2WebErrorStatus.HostNameNotResolved or
        CoreWebView2WebErrorStatus.Timeout or
        CoreWebView2WebErrorStatus.ServerUnreachable or
        CoreWebView2WebErrorStatus.ConnectionAborted or
        CoreWebView2WebErrorStatus.ConnectionReset or
        CoreWebView2WebErrorStatus.Disconnected or
        CoreWebView2WebErrorStatus.OperationCanceled;

    private Grid BuildErrorOverlay()
    {
        var overlay = new Grid
        {
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var retryButton = new Button
        {
            Content = "Thử lại",
            HorizontalAlignment = HorizontalAlignment.Center,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"]
        };
        retryButton.Click += (_, _) =>
        {
            if (_errorOverlay is not null)
                _errorOverlay.Visibility = Visibility.Collapsed;
            _webView.Visibility = Visibility.Visible;
            _webView.Reload();
        };

        var openSettingsButton = new Button
        {
            Content = "Mở Cài đặt",
            HorizontalAlignment = HorizontalAlignment.Center,
            Style = (Style)Application.Current.Resources["DefaultButtonStyle"]
        };
        openSettingsButton.Click += (_, _) =>
            App.MainWindow?.NavigateToSettings();

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        buttonRow.Children.Add(retryButton);
        buttonRow.Children.Add(openSettingsButton);

        var content = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new FontIcon
        {
            Glyph = "\uE814",           // WiFiWarning / no connection
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Opacity = 0.6
        });
        content.Children.Add(new TextBlock
        {
            Text = "Không kết nối được server",
            FontSize = 20,
            FontWeight = new Windows.UI.Text.FontWeight(600),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        content.Children.Add(new TextBlock
        {
            Text = "Kiểm tra lại địa chỉ URL trong Cài đặt hoặc kết nối mạng.",
            FontSize = 13,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            MaxWidth = 340
        });
        content.Children.Add(buttonRow);

        overlay.Children.Add(content);
        return overlay;
    }
}
