using All_in_One_Messenger.Helper;
using All_in_One_Messenger.Pages;
using All_in_One_Messenger.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using WinRT.Interop;

namespace All_in_One_Messenger;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly AppWindow _appWindow;

    // ── Glyph ────────────────────────────────────────────────────────────────
    private const string GlyphDark = "\uF0CE";
    private const string Glyphlight = "\uEC8A";

    // ── Assets ───────────────────────────────────────────────────────────────
    private readonly BitmapImage _messengerLight;
    private readonly BitmapImage _messengerDark;
    private readonly BitmapImage _zaloLight;
    private readonly BitmapImage _zaloDark;
    private readonly BitmapImage _teamsLight;
    private readonly BitmapImage _teamsDark;

    private readonly Dictionary<string, (FrameworkElement Page, string AppId, NavigationViewItem MenuItem)> _tabs = null!;
    private readonly Dictionary<string, CustomServerPage> _customPages = [];

    private bool _isTabHeld = false;
    private string _activeTab = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        // InitializeAssets
        _messengerLight = new(new Uri(CONST.AssetMessengerLight));
        _messengerDark = new(new Uri(CONST.AssetMessengerDark));
        _zaloLight = new(new Uri(CONST.AssetZaloLight));
        _zaloDark = new(new Uri(CONST.AssetZaloDark));
        _teamsLight = new(new Uri(CONST.AssetTeamsLight));
        _teamsDark = new(new Uri(CONST.AssetTeamsDark));

        // InitializeTabs
        _tabs = new()
        {
            [CONST.TabZalo] = (ZaloPage, CONST.AppIdZalo, ZaloTab),
            [CONST.TabTeams] = (TeamsPage, CONST.AppIdTeams, TeamsTab),
            [CONST.TabMessenger] = (MessengerPage, CONST.AppIdMessenger, MessengerTab),
            [CONST.TabSettings] = (SettingPage, string.Empty, null!),
        };

        // Load saved custom servers
        var servers = AppSettings.GetCustomServers().OrderBy(s => s.Order).ToList();
        foreach (var server in servers)
        {
            if (server.Name.Equals(CONST.AppIdZalo) || server.Name.Equals(CONST.AppIdTeams) || server.Name.Equals(CONST.AppIdMessenger))
            {
                var tab = _tabs.FirstOrDefault(x => x.Value.AppId == server.Name).Value;
                if (tab.MenuItem == null) continue;

                tab.MenuItem.Visibility = server.IsEnable ? Visibility.Visible : Visibility.Collapsed;
                NavView.MenuItems.Remove(tab.MenuItem);
                NavView.MenuItems.Add(tab.MenuItem);
            }
            else
                AddCustomServerTab(server);
        }

        WebViewProfileHelper.CleanupUnusedProfiles(
            servers.Select(s => s.Id),
            exclude: [CONST.AppIdTeams, CONST.AppIdMessenger, CONST.AppIdZalo]
        );

        // InitializeWindow
        var hwnd = WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
        _appWindow.SetIcon("Assets\\icon.ico");
        _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        _appWindow.Resize(new Windows.Graphics.SizeInt32(1100, 720));
        SystemBackdrop = new MicaBackdrop();

        // InitializeTheme
        ApplyTheme(LoadTheme());
        UpdateIcons();

        // RegisterEvents
        ((FrameworkElement)Content).Loaded += OnWindowLoaded;
        Activated += (_, a) => NotificationService.Instance.SetWindowActive(a.WindowActivationState != WindowActivationState.Deactivated);

        NotificationService.Instance.TabBadgeChanged += OnTabBadgeChanged;

        // Register for KeyDown after the content is ready
        Content.KeyUp += Window_KeyUp;
        Content.KeyDown += Window_KeyDown;
        WebViewKeyEventBus.KeyComboReceived += OnShortcutFromWebView;

        NavView.Loaded += (_, _) => UpdateNavItemTooltips();
        SettingPage.OnServersReordered += (_, _) => RebuildCustomNavItems();
    }

    #region Events window load page
    /// <summary>
    /// Find and load the necessary information to start.
    /// Delay waiting for information to load.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ((FrameworkElement)Content).Loaded -= OnWindowLoaded;

        // Wait until ALL WebViews finish their first navigation (up to 20s)
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            var (_, messenger) = GetWebViewInfo(CONST.AppIdMessenger);
            var (_, teams) = GetWebViewInfo(CONST.AppIdTeams);
            var (_, zalo) = GetWebViewInfo(CONST.AppIdZalo);
            if (messenger && teams && zalo) break;
            await Task.Delay(100);
        }

        await HideSplashAsync();
        WelcomeView.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Handling hidden splash screen.
    /// </summary>
    /// <returns></returns>
    private Task HideSplashAsync()
    {
        var tcs = new TaskCompletionSource();
        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(1200)),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var storyboard = new Storyboard();
        Storyboard.SetTarget(fade, SplashView);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);
        storyboard.Completed += (_, _) =>
        {
            SplashView.Visibility = Visibility.Collapsed;
            tcs.SetResult();
        };
        storyboard.Begin();
        return tcs.Task;
    }

    /// <summary>
    /// Update the tooltips for the items on the menu.
    /// </summary>
    private void UpdateNavItemTooltips()
    {
        var menuItems = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .Where(i => i.Visibility == Visibility.Visible)
            .ToList();

        for (int i = 0; i < menuItems.Count; i++)
        {
            var item = menuItems[i];
            string name = item.Content?.ToString() ?? string.Empty;
            string shortcut = $"Alt + {i + 1}";
            ToolTipService.SetToolTip(item, $"{name}\n({shortcut})");
        }
    }

    /// <summary>
    /// Get the app's WebView information by app ID.
    /// </summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    private (WebView2? WebView, bool IsReady) GetWebViewInfo(string appId) => appId switch
    {
        CONST.AppIdZalo => (ZaloPage.WebView, ZaloPage.IsReady),
        CONST.AppIdTeams => (TeamsPage.WebView, TeamsPage.IsReady),
        CONST.AppIdMessenger => (MessengerPage.WebView, MessengerPage.IsReady),
        _ => _customPages.TryGetValue(appId, out var p) ? (p.WebView, p.IsReady) : (null, false)
    };
    #endregion

    #region Event screen operation
    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_activeTab)) return;

        if (_tabs.TryGetValue(_activeTab, out var entry))
        {
            try
            {
                var (webView, _) = GetWebViewInfo(entry.AppId);
                webView?.Reload();
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[MainWindow] Reload_Click: {entry.AppId} error: {ex.Message}", ex);
            }
        }
    }

    private void Window_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Tab)
        {
            _isTabHeld = false;
        }
    }

    private void Window_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Track Alt key held
        if (e.Key == VirtualKey.Menu)
        {
            _isTabHeld = true;
            e.Handled = true;
            return;
        }

        if (!_isTabHeld) return;

        var menuItems = NavView.MenuItems.OfType<NavigationViewItem>().Where(i => i.Visibility == Visibility.Visible).ToList();
        int currentIndex = NavView.SelectedItem is NavigationViewItem selectedItem ? menuItems.IndexOf(selectedItem) : -1;

        switch (e.Key)
        {
            // Alt + ` (~) 
            case (VirtualKey)0xC0:
                int nextIndex = (currentIndex + 1) % menuItems.Count;
                NavView.SelectedItem = menuItems[nextIndex];
                e.Handled = true;
                break;

            case VirtualKey.Number1: case VirtualKey.NumberPad1: SelectTabByIndex(menuItems, 0); e.Handled = true; break;
            case VirtualKey.Number2: case VirtualKey.NumberPad2: SelectTabByIndex(menuItems, 1); e.Handled = true; break;
            case VirtualKey.Number3: case VirtualKey.NumberPad3: SelectTabByIndex(menuItems, 2); e.Handled = true; break;
            case VirtualKey.Number4: case VirtualKey.NumberPad4: SelectTabByIndex(menuItems, 3); e.Handled = true; break;
            case VirtualKey.Number5: case VirtualKey.NumberPad5: SelectTabByIndex(menuItems, 4); e.Handled = true; break;
            case VirtualKey.Number6: case VirtualKey.NumberPad6: SelectTabByIndex(menuItems, 5); e.Handled = true; break;
            case VirtualKey.Number7: case VirtualKey.NumberPad7: SelectTabByIndex(menuItems, 6); e.Handled = true; break;
            case VirtualKey.Number8: case VirtualKey.NumberPad8: SelectTabByIndex(menuItems, 7); e.Handled = true; break;
            case VirtualKey.Number9: case VirtualKey.NumberPad9: SelectTabByIndex(menuItems, 8); e.Handled = true; break;
        }
    }

    private void OnShortcutFromWebView(WebViewKeyCombo combo) => HandleShortcut(combo);

    private void HandleShortcut(WebViewKeyCombo combo)
    {
        var menuItems = NavView.MenuItems.OfType<NavigationViewItem>().Where(i => i.Visibility == Visibility.Visible).ToList();
        int currentIndex = NavView.SelectedItem is NavigationViewItem selectedItem ? menuItems.IndexOf(selectedItem) : -1;
        if (menuItems.Count == 0) return;

        switch (combo.Action)
        {
            case WebViewKeyAction.SwitchTab:
                SelectTabByIndex(menuItems, combo.TabIndex - 1);
                break;

            case WebViewKeyAction.NextTab:
                int next = (currentIndex + 1 + menuItems.Count) % menuItems.Count;
                NavView.SelectedItem = menuItems[next];
                break;

            case WebViewKeyAction.PrevTab:
                int prevIndex = (currentIndex - 1 + menuItems.Count) % menuItems.Count;
                NavView.SelectedItem = menuItems[prevIndex];
                break;
        }
    }

    private void SelectTabByIndex(List<NavigationViewItem> menuItems, int index)
    {
        if (index < menuItems.Count)
            NavView.SelectedItem = menuItems[index];
    }
    #endregion

    #region Events on the menu

    internal void AddCustomServerTab(CustomServerInfo info)
    {
        if (_tabs.ContainsKey(info.Id)) return;

        var page = new CustomServerPage(info)
        {
            Visibility = Visibility.Collapsed
        };
        ContentGrid.Children.Add(page);

        var navItem = new NavigationViewItem
        {
            Content = info.Name,
            Tag = info.Id,
            Icon = new FontIcon
            {
                Glyph = info.IconGlyph,
                FontFamily = new FontFamily("Segoe Fluent Icons")
            }
        };
        NavView.MenuItems.Add(navItem);
        UpdateNavItemTooltips();

        _customPages[info.Id] = page;
        _tabs[info.Id] = (page, info.Id, null!);
    }

    internal void RemoveCustomServerTab(string id)
    {
        if (!_tabs.TryGetValue(id, out var entry)) return;

        // If it's the active tab, show welcome screen
        if (_activeTab == id)
        {
            WelcomeView.Visibility = Visibility.Visible;
            NavView.SelectedItem = null;
            _activeTab = string.Empty;
        }

        // Remove page from visual tree
        ContentGrid.Children.Remove((UIElement)entry.Page);

        // Remove NavItem
        var navItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == id);
        if (navItem is not null)
            NavView.MenuItems.Remove(navItem);

        _tabs.Remove(id);
        _customPages.Remove(id);
    }

    internal void UpdateCustomServerTab(string id, string name, string glyph, string url)
    {
        var navItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == id);
        if (navItem is not null)
        {
            navItem.Content = name;
            if (navItem.Icon is FontIcon fi)
                fi.Glyph = glyph;
        }

        if (_customPages.TryGetValue(id, out var page))
            page.NavigateTo(url);
    }

    internal async void NavigateToSettings()
    {
        NavView.SelectedItem = NavView.SettingsItem;
        await SwitchTab(CONST.TabSettings);
    }

    internal async void NavigateToTab(string appId)
    {
        string? tag = _tabs.FirstOrDefault(kv => kv.Value.AppId == appId).Key;
        if (tag is null) return;

        var navItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == tag);

        if (navItem is not null)
            NavView.SelectedItem = navItem;

        await SwitchTab(tag);
    }

    private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            if (_activeTab == CONST.TabSettings) return;
            await SwitchTab(CONST.TabSettings);
            return;
        }

        string tag = args.SelectedItem is NavigationViewItem item
            ? item.Tag?.ToString() ?? string.Empty : string.Empty;

        if (tag == _activeTab) return;

        await SwitchTab(tag);
    }

    private async Task SwitchTab(string page)
    {
        if (WelcomeView.Visibility == Visibility.Visible)
            WelcomeView.Visibility = Visibility.Collapsed;

        var suspendTasks = new List<Task>();
        foreach (var (tag, (element, appId, _)) in _tabs)
        {
            if (tag == page)
            {
                element.Visibility = Visibility.Visible;
                await OnTabShown(appId);
            }
            else if (element.Visibility == Visibility.Visible)
            {
                element.Visibility = Visibility.Collapsed;
                suspendTasks.Add(OnTabHidden(appId));
            }
        }

        await Task.WhenAll(suspendTasks);
        _activeTab = page;
    }

    private Task OnTabShown(string appId)
    {
        NotificationService.Instance.SetActiveTab(appId);

        var (webView, isReady) = GetWebViewInfo(appId);
        try
        {
            if (isReady && webView?.CoreWebView2 != null && webView.CoreWebView2.IsSuspended)
                webView.CoreWebView2.Resume();
        }
        catch (Exception ex)
        {
            AppLogger.Log($"MainWindow OnTabShown:{appId} error: {ex.Message}", ex);
        }

        if (!string.IsNullOrEmpty(appId))
            NotificationService.Instance.ClearBadge(appId);

        return Task.CompletedTask;
    }

    private async Task OnTabHidden(string appId)
    {
        try
        {
            var (webView, isReady) = GetWebViewInfo(appId);
            if (webView?.CoreWebView2 != null && isReady && !webView.CoreWebView2.IsSuspended)
            {
                await webView.CoreWebView2.TrySuspendAsync();
            }
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // TrySuspendAsync fails with COMException (0x8007139F) when the WebView
            // is mid-navigation or in an invalid state for suspension.
            // This is expected and safe to ignore — suspend is a best-effort optimization.
        }
        catch (Exception ex)
        {
            AppLogger.Log($"MainWindow OnTabHidden:{appId} error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Remove the badge from the open tab.
    /// </summary>
    /// <param name="appId"></param>
    /// <param name="count"></param>
    private void OnTabBadgeChanged(string appId, int count)
    {
        // Find the tag corresponding to the appId
        string? tag = _tabs.FirstOrDefault(kv => kv.Value.AppId == appId).Key;
        if (tag is null) return;

        var navItem = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => i.Tag?.ToString() == tag);

        if (navItem is null) return;

        navItem.InfoBadge = count > 0 ? new InfoBadge { Value = count } : null;
    }

    /// <summary>
    /// Rearrange the custom servers in the menu.
    /// </summary>
    private void RebuildCustomNavItems()
    {
        var fixedTags = new HashSet<string> { "MessengerPage", "ZaloPage", "TeamsPage" };
        var servers = SettingPage.CustomServers.ToList();

        // Delete old custom item + hide/show 3 default tabs based on IsEnable, in one iteration
        var fixedItems = NavView.MenuItems.OfType<NavigationViewItem>()
            .Where(i => fixedTags.Contains(i.Tag?.ToString() ?? string.Empty))
            .ToDictionary(i => i.Tag?.ToString() ?? String.Empty, i => i);

        NavView.MenuItems.Clear();
        foreach (var s in servers.OrderBy(s => s.Order))
        {
            if (fixedItems.TryGetValue(s.Id, out var fixedItem))
            {
                fixedItem.Visibility = s.IsEnable ? Visibility.Visible : Visibility.Collapsed;
                NavView.MenuItems.Add(fixedItem);
                continue;
            }

            var navItem = new NavigationViewItem
            {
                Content = s.Name,
                Tag = s.Id,
                Visibility = s.IsEnable ? Visibility.Visible : Visibility.Collapsed,
                Icon = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = s.IconGlyph }
            };
            ToolTipService.SetToolTip(navItem, s.Name);
            NavView.MenuItems.Add(navItem);
        }

        // Refresh tooltip shortcut number
        UpdateNavItemTooltips();
    }
    #endregion

    #region Theme
    private static void SaveTheme(string theme)
    {
        AppSettings.Set(CONST.ThemeKey, theme);
    }

    private static string LoadTheme()
    {
        return AppSettings.Get(CONST.ThemeKey) ?? CONST.ThemeSystem;
    }

    private void ApplyTheme(string theme)
    {
        switch (theme)
        {
            case CONST.ThemeDark:
                DarkModeToggle.IsChecked = true;
                ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
                ThemeIcon.Glyph = Glyphlight;
                ApplyTitleBarTheme(true);
                break;

            case CONST.ThemeLight:
                ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
                ThemeIcon.Glyph = GlyphDark;
                ApplyTitleBarTheme(false);
                break;

            default:
                ((FrameworkElement)Content).RequestedTheme = ElementTheme.Default;
                ApplyTitleBarTheme(false);
                break;
        }
    }

    private void DarkMode_Checked(object sender, RoutedEventArgs e)
    {
        ThemeIcon.Glyph = Glyphlight;
        ((FrameworkElement)Content).RequestedTheme = ElementTheme.Dark;
        SaveTheme(CONST.ThemeDark);
        UpdateIcons();
        ApplyTitleBarTheme(true);
    }

    private void DarkMode_Unchecked(object sender, RoutedEventArgs e)
    {
        ThemeIcon.Glyph = GlyphDark;
        ((FrameworkElement)Content).RequestedTheme = ElementTheme.Light;
        SaveTheme(CONST.ThemeLight);
        UpdateIcons();
        ApplyTitleBarTheme(false);
    }

    private void ApplyTitleBarTheme(bool isDark)
    {
        if (isDark)
        {
            _appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

            _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
            _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(90, 255, 255, 255);

            _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 80, 80, 80);
        }
        else
        {
            _appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
            _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

            _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
            _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 0, 0, 0);

            _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);
        }
    }
    private void UpdateIcons()
    {
        ElementTheme theme = ((FrameworkElement)Content).ActualTheme;

        if (theme == ElementTheme.Dark)
        {
            MessengerIcon.Source = _messengerLight;
            ZaloIcon.Source = _zaloLight;
            TeamsIcon.Source = _teamsLight;
        }
        else
        {
            MessengerIcon.Source = _messengerDark;
            ZaloIcon.Source = _zaloDark;
            TeamsIcon.Source = _teamsDark;
        }
    }
    #endregion
}
