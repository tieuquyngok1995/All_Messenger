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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Pages;

public sealed partial class SettingPage : Page
{
    public event EventHandler? OnServersReordered;
    // Custom server list — bound to ListView in XAML.
    public ObservableCollection<CustomServerInfo> CustomServers { get; } = [];

    private bool _isLoading;
    private readonly UpdateService _updateService = new();

    // 42 icons (6 × 7) from Segoe MDL2 Assets, prioritizing icons suitable for the chat server.
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

        var defaults = new[]
            {
                (Id: AppConst.TabMessenger, Name: AppConst.AppIdMessenger, Url:"https://www.messenger.com/",   IconGlyph:"\uE8BD", Order: 0),
                (Id: AppConst.TabZalo,      Name: AppConst.AppIdZalo,      Url:"https://chat.zalo.me/",        IconGlyph:"\uec42", Order: 1),
                (Id: AppConst.TabTeams,     Name: AppConst.AppIdTeams,     Url:"https://teams.microsoft.com/", IconGlyph:"\uE902", Order: 2),
            };
        var servers = AppSettings.GetCustomServers();
        bool changed = false;
        foreach (var (id, name, url, icon, order) in defaults.Reverse())
        {
            if (!servers.Any(s => s.Id == id))
            {
                servers.Insert(0, new CustomServerInfo
                {
                    Id = id,
                    Name = name,
                    Url = url,
                    IconGlyph = icon,
                    Order = order,
                    IsEnable = true
                });
                changed = true;
            }
        }

        if (changed)
            AppSettings.SaveCustomServers(servers);

        CustomServers.Clear();
        foreach (var server in servers)
            CustomServers.Add(server);

        VersionText.Text = $"Phiên bản hiện tại: {GetCurrentVersion()}";
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
    private async void DeleteServer_Click(object sender, RoutedEventArgs e)
    {
        var result = await AppDialog.ShowConfirmAsync(XamlRoot, "Xóa", "Bạn có chắc muốn xóa?");
        if (!result) return;
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

    /// <summary>
    /// Event: Check update new version
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdate.IsEnabled = false;

        try
        {
            var releaseResult = await _updateService.GetLatestReleaseAsync();

            if (!releaseResult.Success || releaseResult.Data == null)
            {
                await AppDialog.ShowMessageAsync(this.XamlRoot, "Không thể kiểm tra cập nhật", releaseResult.ErrorMessage ?? "Lỗi không xác định.");
                return;
            }

            var releaseInfo = releaseResult.Data;
            var currentVersion = GetCurrentVersion();
            var latestVersion = NormalizeVersion(releaseInfo.TagName);

            if (string.IsNullOrEmpty(latestVersion))
            {
                await AppDialog.ShowMessageAsync(this.XamlRoot, "Không thể kiểm tra cập nhật", "Không tìm thấy thông tin phiên bản từ GitHub.");
                return;
            }

            if (!IsNewerVersion(currentVersion, latestVersion))
            {
                await AppDialog.ShowMessageAsync(this.XamlRoot, "Cập nhật", $"Phiên bản hiện tại ({currentVersion}) đã là mới nhất.");
                return;
            }

            var asset = PickInstallerAsset(releaseInfo.Assets);

            var dialog = new ContentDialog
            {
                Title = "Có phiên bản mới",
                Content = $"Phiên bản hiện tại: {currentVersion}\nPhiên bản mới: {latestVersion}\n\nBạn có muốn tải về và cài đặt ngay không?",
                PrimaryButtonText = "Tải về và Cài đặt",
                SecondaryButtonText = "Mở trang GitHub",
                CloseButtonText = "Để sau",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = CheckUpdate.XamlRoot,
                RequestedTheme = ActualTheme
            };

            var dialogResult = await dialog.ShowAsync();

            if (dialogResult == ContentDialogResult.Primary)
            {
                if (asset == null || string.IsNullOrEmpty(asset.DownloadUrl))
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(releaseInfo.HtmlUrl));
                    return;
                }

                var tempPath = Path.Combine(Path.GetTempPath(), asset.Name);
                var progressDowload = AppDialog.CreateProgressDialog(this.XamlRoot, "Đang tải về", $"Đang tải {asset.Name}...");
                progressDowload.Show();
                ServiceResult<string> downloadResult;
                try
                {
                    var progress = new Progress<double>(percent =>
                        progressDowload.UpdateProgress(percent, $"Đang tải {asset.Name}... ({percent:0}%)"));

                    downloadResult = await _updateService.DownloadFileAsync(asset.DownloadUrl, tempPath, progress);
                }
                finally
                {
                    progressDowload.Close();
                }

                if (!downloadResult.Success || downloadResult.Data == null)
                {
                    await AppDialog.ShowMessageAsync(this.XamlRoot, "Lỗi tải về", downloadResult.ErrorMessage ?? "Lỗi không xác định.");
                    return;
                }

                if (!RunInstaller(downloadResult.Data, out var runError))
                {
                    await AppDialog.ShowMessageAsync(this.XamlRoot, "Lỗi cài đặt", runError ?? "Không thể chạy file cài đặt.");
                    return;
                }

                await Task.Delay(300);
                Application.Current.Exit();
            }
            else if (dialogResult == ContentDialogResult.Secondary)
            {
                await Windows.System.Launcher.LaunchUriAsync(new Uri(releaseInfo.HtmlUrl));
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[SettingPage] CheckUpdate_Click error: {ex.Message}", ex);
        }
        finally
        {
            CheckUpdate.IsEnabled = true;
        }
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

    private static string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return version?.Split('+')[0] ?? "0.0.0";
    }

    private static string NormalizeVersion(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return string.Empty;
        var v = tag.Trim();
        if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
        var dashIdx = v.IndexOf('-');
        if (dashIdx >= 0) v = v.Substring(0, dashIdx);
        return v;
    }

    private static bool IsNewerVersion(string current, string latest)
    {
        if (TryParseVersion(current, out var curV) && TryParseVersion(latest, out var latV))
            return latV! > curV!;

        return !string.Equals(current, latest, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseVersion(string s, out Version? v)
    {
        v = null;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (!s.Contains('.')) s += ".0";
        return Version.TryParse(s, out v);
    }

    private static GitHubReleaseAsset? PickInstallerAsset(List<GitHubReleaseAsset> assets)
    {
        var preferred = assets.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));

        return preferred ?? assets.FirstOrDefault();
    }

    private static bool RunInstaller(string filePath, out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true,
                Arguments = "/CLOSEAPPLICATIONS /RESTARTAPPLICATIONS"
            });
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Không thể chạy file cài đặt: {ex.Message}";
            return false;
        }
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
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
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
