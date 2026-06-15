using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using System;
using System.Diagnostics;
using Windows.Foundation;

namespace All_in_One_Messenger.Helper;

public abstract class WebViewPageBase : Page
{
    private bool _isReady = false;
    public bool IsReady => _isReady;

    public abstract WebView2 WebView { get; }
    public abstract string AppId { get; }
    public abstract Uri StartUri { get; }

    private TypedEventHandler<object, WindowVisibilityChangedEventArgs>? _visibilityHandler;

    protected WebViewPageBase()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Handling the initialization of the webview.
    /// </summary>
    protected async void InitWebView()
    {
        try
        {
            var env = await WebViewProfileHelper.GetOrCreateAsync(AppId);
            await WebView.EnsureCoreWebView2Async(env);

            var core = WebView.CoreWebView2;

            ConfigureWebView(core, WebView);

            core.PermissionRequested += (s, a) =>
                WebViewNotificationHelper.AllowNotificationPermission(s, a);

            core.WebMessageReceived += (s, e) =>
                WebViewNotificationHelper.HandleWebMessage(AppId, e);

            await WebViewNotificationHelper.InjectNotificationHookAsync(core);

            // Hook to page-specific setup (session detector, v.v.)
            OnCoreWebView2Ready(core);

            // Apply the app theme to the WebView immediately after initialization
            ApplyColorSchemeFromCurrentTheme();

            core.NavigationCompleted += (s, e) => { if (!_isReady) _isReady = true; };

            WebView.Source = StartUri;
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[WebViewPageBase] InitWebView:{AppId} error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Override to add page-specific logic (e.g., session detector, special configuration).
    /// </summary>
    /// <param name="core"></param>
    protected virtual void OnCoreWebView2Ready(CoreWebView2 core) { }

    /// <summary>
    /// Configure for WebView.
    /// </summary>
    /// <param name="core"></param>
    private static void ConfigureWebView(CoreWebView2 core, WebView2 webViewControl)
    {
        core.NewWindowRequested += async (s, e) =>
        {
            e.Handled = true;

            if (!string.IsNullOrEmpty(e.Uri) && e.Uri != "about:blank")
            {
                // There's a real URI → open an external browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri,
                    UseShellExecute = true
                });
                return;
            }

            // Blank popup → create a hidden WebView2 to catch the redirect, DO NOT use the current core.
            var deferral = e.GetDeferral();
            try
            {
                var hiddenWebView = new WebView2
                {
                    Visibility = Visibility.Collapsed
                };

                if (webViewControl.Parent is Panel parent)
                    parent.Children.Add(hiddenWebView);

                await hiddenWebView.EnsureCoreWebView2Async(
                    webViewControl.CoreWebView2.Environment);

                e.NewWindow = hiddenWebView.CoreWebView2;
                deferral.Complete();

                // Capture the actual URL after the redirect
                hiddenWebView.CoreWebView2.NavigationStarting += (ns, ne) =>
                {
                    if (ne.Uri.StartsWith("about:") ||
                        ne.Uri.StartsWith("data:") ||
                        ne.Uri.StartsWith("blob:"))
                        return;

                    ne.Cancel = true;

                    // Open the actual URL in an external browser
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = ne.Uri,
                        UseShellExecute = true
                    });

                    // Clean up hidden webview
                    hiddenWebView.Close();
                    if (webViewControl.Parent is Panel p)
                        p.Children.Remove(hiddenWebView);
                };
            }
            catch
            {
                deferral.Complete();
            }
        };

        // Disable unnecessary browser features to reduce interference and optimize performance
        var settings = core.Settings;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsBuiltInErrorPageEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsGeneralAutofillEnabled = true;
        settings.IsPasswordAutosaveEnabled = true;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
    }

    /// <summary>
    /// Load and unload webview.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ActualThemeChanged += OnActualThemeChanged;

        if (App.MainWindow is null) return;

        try
        {
            _visibilityHandler = (s, args) => { };

            App.MainWindow.VisibilityChanged += _visibilityHandler;
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[WebViewPageBase] OnLoaded:{AppId} error: {ex.Message}", ex);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ActualThemeChanged -= OnActualThemeChanged;

        if (App.MainWindow is not null && _visibilityHandler is not null)
            App.MainWindow.VisibilityChanged -= _visibilityHandler;
    }

    /// <summary>
    /// Theme synchronization
    /// </summary>
    private void ApplyColorSchemeFromCurrentTheme()
    {
        if (WebView.CoreWebView2 is null) return;
        WebView.CoreWebView2.Profile.PreferredColorScheme = ActualTheme == ElementTheme.Dark
            ? CoreWebView2PreferredColorScheme.Dark
            : CoreWebView2PreferredColorScheme.Light;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyColorSchemeFromCurrentTheme();
    }
}