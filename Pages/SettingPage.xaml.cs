using All_in_One_Messenger.Helper;
using All_in_One_Messenger.Models;
using All_in_One_Messenger.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Pages;

public sealed partial class SettingPage : Page
{
    private bool _isLoading;

    // Custom server list — bound to ListView in XAML.
    public ObservableCollection<CustomServerInfo> CustomServers { get; } = [];

    public event EventHandler? OnServersReordered;

    // 42 icons (6 × 7) from Segoe Fluent Icons, prioritizing icons suitable for the chat server.
    private static readonly (string Label, string Glyph)[] _iconOptions =
    [
        // ── Nhắn tin ──────────────────────────────────
        ("Globe",           "\uE774"),   // Globe
        ("Chat",            "\uE8BD"),   // Chat bubble
        ("Message",         "\uE715"),   // Message
        ("Quick Note",      "\uE70B"),   // Quick Note
        ("Micro",           "\uE720"),   // Microphone — voice channel
        ("Airplane",        "\uE709"),   // Airplane
        ("Shop",            "\uE719"),   // Shop
        // ── Giao tiếp ─────────────────────────────────
        ("Phone",           "\uE717"),   // Phone
        ("Video call",      "\uE714"),   // Video camera
        ("Headset",         "\uE95B"),   // Headset — Discord / voice
        ("Work",            "\uE821"),   // Work
        ("Group",           "\uEC26"),   // Send
        ("Book",            "\uE736"),   // ReadingMode
        ("OEM",             "\uE74C"),   // OEM
        // ── Cộng đồng ─────────────────────────────────
        ("Person",          "\uE77B"),   // Person
        ("System",          "\uE770"),   // System
        ("Emoji",           "\uE899"),   // Emoji
        ("Star",            "\uE734"),   // Heart favorite
        ("Report Document", "\uE9F9"),   // ReportDocument
        ("Tinder",          "\uECAD"),   // Tinder
        ("Monitor",         "\uE7F4"),   // Monitor
        // ── Giải trí ──────────────────────────────────
        ("Game",            "\uE7FC"),   // Game controller
        ("Camera",          "\uE722"),   // Camera
        ("Music",           "\uE8D6"),   // Music note — music bot servers
        ("Video",           "\uE714"),   // Video
        ("Cloud",           "\uE753"),   // Cloud
        ("Alert",           "\uF6C5"),   // Alert
        ("Leaf Two",        "\uF1E8"),   // LeafTwo
        // ── Công nghệ ─────────────────────────────────
        ("Connect",         "\uE703"),   // Connect
        ("Mobile Phone",    "\uE8EA"),   // Mobile phone
        ("Report Hacked",   "\uE730"),   // ReportHacked
        ("Magazine",        "\uE8A1"),   // PreviewLink
        ("Effects",         "\uE794"),   // Effects
        ("Cloud Search",    "\uEDE4"),   // CloudSearch
        ("Gripper Tool",    "\uE75E"),   // GripperTool
        // ── Tiện ích ──────────────────────────────────
        ("Health",          "\uE95E"),   // Health
        ("Face",            "\uEB68"),   // NUIFace
        ("Color",           "\uE790"),   // Color
        ("Lock",            "\uE72E"),   // Lock
        ("Calendar",        "\uE787"),   // Calendar — event servers
        ("Windows Insider", "\uF1AD"),   // WindowsInsider
        ("Expressive",      "\uF6B8"),   // ExpressiveInputEntry
     ];

    public SettingPage()
    {
        InitializeComponent();

        Loaded += SettingPage_Loaded;
    }

    /// <summary>
    /// Handling page load settings.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void SettingPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;

        var mode = LoadNotificationMode();
        if (mode == NotificationService.NotificationModeSilent)
            RadioSilent.IsChecked = true;
        else
            RadioToast.IsChecked = true;

        var servers = AppSettings.GetCustomServers();
        if (servers.Count == 0)
        {
            var defaults = new[]
            {
                (Id: AppConst.TabMessenger, Name: AppConst.AppIdMessenger, Url:"https://www.messenger.com/",   IconGlyph:"\uE8BD", Order: 0),
                (Id: AppConst.TabZalo,      Name: AppConst.AppIdZalo,      Url:"https://chat.zalo.me/",        IconGlyph:"\uec42", Order: 1),
                (Id: AppConst.TabTeams,     Name: AppConst.AppIdTeams,     Url:"https://teams.microsoft.com/", IconGlyph:"\uE902", Order: 2),
            };

            foreach (var (id, name, url, icon, order) in defaults)
            {
                servers.Add(new CustomServerInfo
                {
                    Id = id,
                    Name = name,
                    Url = url,
                    IconGlyph = icon,
                    Order = order,
                    IsEnable = true
                });
            }
            AppSettings.SaveCustomServers(servers);
        }

        CustomServers.Clear();
        foreach (var server in servers)
            CustomServers.Add(server);

        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        VersionText.Text = $"Phiên bản hiện tại: {version?.Split('+')[0] ?? "Unknown"}";
        _isLoading = false;
    }

    /// <summary>
    /// Event: change mode notification.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void NotificationModeGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (RadioSilent.IsChecked == true)
            SaveNotificationMode(NotificationService.NotificationModeSilent);
        else
            SaveNotificationMode(NotificationService.NotificationModeToast);
    }

    /// <summary>
    /// Event: Add custom server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void AddServer_Click(object sender, RoutedEventArgs e)
    {
        var result = await ShowServerDialogAsync("Thêm chat server", "Thêm");
        if (result is null) return;

        var (name, url, glyph) = result.Value;

        var servers = AppSettings.GetCustomServers();
        var info = new CustomServerInfo { Name = name, Url = url, IconGlyph = glyph, Order = servers.Count };

        servers.Add(info);
        AppSettings.SaveCustomServers(servers);

        CustomServers.Add(info);
        App.MainWindow?.AddCustomServerTab(info);
    }

    /// <summary>
    /// Event: Edit custom server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void EditServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;
        var server = CustomServers.FirstOrDefault(s => s.Id == id);
        if (server is null) return;

        var result = await ShowServerDialogAsync(
            "Chỉnh sửa server", "Lưu",
            server.Name, server.Url, server.IconGlyph);
        if (result is null) return;

        var (name, url, glyph) = result.Value;

        // Update model — INPC automatically refreshes ListView
        server.Name = name;
        server.Url = url;
        server.IconGlyph = glyph;

        // Persist
        var servers = AppSettings.GetCustomServers();
        var saved = servers.FirstOrDefault(s => s.Id == id);
        if (saved is not null)
        {
            saved.Name = name;
            saved.Url = url;
            saved.IconGlyph = glyph;
        }
        AppSettings.SaveCustomServers(servers);

        App.MainWindow?.UpdateCustomServerTab(id, name, glyph, url);
    }

    /// <summary>
    /// Event: Show and hidden server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void HiddenServerToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string id) return;
        if (btn is null) return;

        // Persist
        var servers = AppSettings.GetCustomServers();
        var saved = servers.FirstOrDefault(s => s.Id == id);

        if (btn.IsChecked == true && saved is not null) saved.IsEnable = true;
        if (btn.IsChecked == false && saved is not null) saved.IsEnable = false;

        AppSettings.SaveCustomServers(servers);
        OnServersReordered?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Event: Delete custom server
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DeleteServer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string id) return;

        var server = CustomServers.FirstOrDefault(s => s.Id == id);
        if (server is null) return;

        CustomServers.Remove(server);

        var servers = AppSettings.GetCustomServers();
        servers.RemoveAll(s => s.Id == id);
        for (int i = 0; i < servers.Count; i++) servers[i].Order = i;
        AppSettings.SaveCustomServers(servers);

        App.MainWindow?.RemoveCustomServerTab(id);
    }

    private void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {

    }

    /// <summary>
    /// Event: Displaying the server dialog
    /// </summary>
    /// <returns></returns>
    private Task<(string Name, string Url, string IconGlyph)?> ShowServerDialogAsync(
        string title, string primaryButton, string initName = "", string initUrl = "", string initGlyph = "")
    {
        if (string.IsNullOrEmpty(initGlyph))
            initGlyph = _iconOptions[0].Glyph;

        var nameBox = new TextBox
        {
            Header = "Tên hiển thị",
            PlaceholderText = "e.g. Discord",
            Text = initName
        };
        var urlBox = new TextBox
        {
            Header = "URL",
            PlaceholderText = "e.g. https://discord.com/app",
            Text = initUrl
        };
        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            FontSize = 12,
            Visibility = Visibility.Collapsed
        };

        string selectedGlyph = initGlyph;
        var iconPicker = BuildIconPicker(selectedGlyph, g => selectedGlyph = g);

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(nameBox);
        panel.Children.Add(urlBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Chọn icon",
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        });
        panel.Children.Add(iconPicker);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 520
            },
            PrimaryButtonText = primaryButton,
            CloseButtonText = "Hủy",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme
        };

        // Validate in real-time as user types — hide errors when editing begins
        nameBox.TextChanged += (_, _) => errorText.Visibility = Visibility.Collapsed;
        urlBox.TextChanged += (_, _) => errorText.Visibility = Visibility.Collapsed;

        // Returns the result after the dialog closes
        var tcs = new TaskCompletionSource<(string, string, string)?>();

        // Block closing the dialog if validation fails
        dialog.PrimaryButtonClick += (d, args) =>
        {
            var name = nameBox.Text.Trim();
            var url = urlBox.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
            {
                errorText.Text = "Vui lòng điền đầy đủ tên và URL.";
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                errorText.Text = "URL không hợp lệ.";
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
                return;
            }

            tcs.TrySetResult((name, url, selectedGlyph));
        };

        dialog.CloseButtonClick += (_, _) =>
        {
            tcs.TrySetResult(null);
        };

        _ = dialog.ShowAsync();
        return tcs.Task;
    }

    private void ServerListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // Update the Order to its current position in the ObservableCollection
        for (int i = 0; i < CustomServers.Count; i++)
            CustomServers[i].Order = i;

        AppSettings.SaveCustomServers([.. CustomServers]);
        OnServersReordered?.Invoke(this, EventArgs.Empty);
    }

    private static string LoadNotificationMode() => AppSettings.Get(NotificationService.NotificationModeKey) ?? NotificationService.NotificationModeToast;

    private static void SaveNotificationMode(string mode) => AppSettings.Set(NotificationService.NotificationModeKey, mode);

    /// <summary>
    /// Creates a 5-column icon grid. The user selects an icon — radio-inclusive.
    /// onChanged is called every time the selection changes.
    /// </summary>
    private static UIElement BuildIconPicker(string initialGlyph, Action<string> onChanged)
    {
        const int cols = 7;
        var toggles = new Dictionary<string, ToggleButton>(_iconOptions.Length);
        bool updating = false;

        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

        for (int i = 0; i < _iconOptions.Length; i++)
        {
            int row = i / cols;
            int col = i % cols;
            if (col == 0)
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });

            var (label, glyph) = _iconOptions[i];
            var g = glyph;

            var tb = new ToggleButton
            {
                Width = 44,
                Height = 44,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                IsChecked = g == initialGlyph
            };
            ToolTipService.SetToolTip(tb, label);
            tb.Content = new FontIcon
            {
                Glyph = g,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 18
            };

            tb.Checked += (_, _) =>
            {
                if (updating) return;
                updating = true;
                foreach (var (k, v) in toggles)
                    if (k != g) v.IsChecked = false;
                updating = false;
                onChanged(g);
            };

            toggles[g] = tb;
            Grid.SetRow(tb, row);
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        return grid;
    }

}
