using All_in_One_Messenger.Helper;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace All_in_One_Messenger.Services;

public sealed class NotificationService
{
    // ── App IDs ──────────────────────────────────────────────────────────────
    private const string AppIdZalo = "Zalo";

    // Setting keys (used in conjunction with SettingPage)
    public const string NotificationModeKey = "NotificationMode";
    public const string NotificationModeToast = "Toast";
    public const string NotificationModeSilent = "Silent";

    // Fired on the UI thread whenever a tab's badge count changes (appId, count)
    public event Action<string, int>? TabBadgeChanged;

    // Singleton
    private static readonly Lazy<NotificationService> _instance = new(() => new NotificationService());
    public static NotificationService Instance => _instance.Value;

    // BadgeUpdateManager only works with packaged apps (those with the identity package).
    private static readonly bool _isPackaged = CheckIsPackaged();
    private static bool CheckIsPackaged()
    {
        try { _ = Windows.ApplicationModel.Package.Current; return true; }
        catch { return false; }
    }

    // P/Invoke required
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    // Status
    private readonly ConcurrentDictionary<string, bool> _sessionMap = new();
    private readonly ConcurrentDictionary<string, int> _badgeCounts = new();

    private DispatcherQueue? _dispatcherQueue;
    private nint _hwnd;
    private bool _isWindowActive = false;
    private string _activeTabAppId = string.Empty;

    private NotificationService() { }

    public void Initialize(DispatcherQueue dispatcherQueue, nint hwnd)
    {
        _dispatcherQueue = dispatcherQueue;
        _hwnd = hwnd;

        AppNotificationManager.Default.NotificationInvoked += OnNotificationInvoked;
        AppNotificationManager.Default.Register();
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        if (_hwnd == 0) return;

        if (IsIconic(_hwnd))
            ShowWindow(_hwnd, SW_RESTORE);

        SetForegroundWindow(_hwnd);
    }

    public void SetActiveTab(string appId) => _activeTabAppId = appId;

    public void SetWindowActive(bool active)
    {
        _isWindowActive = active;
        if (active)
        {
            ClearAllBadges();
            NotificationFilter.ClearAllStates();
        }
    }

    public void ClearBadge(string appId)
    {
        _badgeCounts[appId] = 0;
        UpdateTaskbarBadge();
        _dispatcherQueue?.TryEnqueue(() => TabBadgeChanged?.Invoke(appId, 0));
    }

    public void SetBadgeDirect(string appId, int count)
    {
        if (!HasSession(appId)) return;

        if (_isWindowActive)
            _badgeCounts[appId] = 0;
        else
            _badgeCounts[appId] = count;

        UpdateTaskbarBadge();

        if (appId != _activeTabAppId) _dispatcherQueue?.TryEnqueue(() => TabBadgeChanged?.Invoke(appId, count));
    }

    private void ClearAllBadges()
    {
        foreach (var key in _badgeCounts.Keys) _badgeCounts[key] = 0;
        UpdateTaskbarBadge();
    }

    public void SetSession(string appId, bool hasSession) => _sessionMap[appId] = hasSession;

    public bool HasSession(string appId) => _sessionMap.TryGetValue(appId, out bool v) && v;

    /// <summary>
    /// Notification entry point.
    /// </summary>
    public void HandleWebNotification(string appId, string title, string body, string? icon = null)
    {
        if (!HasSession(appId)) return;
        if (_isWindowActive) return;

        if (GetNotificationMode() != NotificationModeSilent)
            ShowToast(appId, title, body, icon);
    }

    private static string GetNotificationMode() => AppSettings.Get(NotificationModeKey) ?? NotificationModeToast;

    /// <summary>
    /// Show toast notification.
    /// </summary>
    private void ShowToast(string appId, string title, string body, string? icon)
    {
        void Show()
        {
            try
            {
                string displayName = GetAppDisplayName(appId);
                var displayTitle = !string.IsNullOrWhiteSpace(title) ? $"[{displayName}] {title}" : displayName;
                var builder = new AppNotificationBuilder().AddArgument("appId", appId).AddArgument("action", "focus").AddText(SanitizeToastText(displayTitle));

                if (!string.IsNullOrWhiteSpace(body))
                    builder.AddText(SanitizeToastText(body));

                if (appId != AppIdZalo && !string.IsNullOrWhiteSpace(icon) && Uri.TryCreate(icon, UriKind.Absolute, out var iconUri))
                    builder.SetAppLogoOverride(iconUri, AppNotificationImageCrop.Circle);

                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                AppLogger.Log($"[NotificationService] ShowToast error: {ex.Message}", ex);
            }
        }
        if (_dispatcherQueue is not null)
            _dispatcherQueue.TryEnqueue(Show);
        else
            Show();
    }
    /// <summary>
    /// Get the display name from the settings (for a custom server)
    /// </summary>
    /// <param name="appId"></param>
    /// <returns></returns>
    private static string GetAppDisplayName(string appId)
    {
        // The built-in apps already have the correct names as app IDs
        if (appId is "Teams" or "Messenger" or "Zalo")
            return appId;

        // Custom server: appId is a short GUID → look up the name from settings
        var servers = AppSettings.GetCustomServers();
        var match = servers.Find(s => s.Id == appId);
        return match?.Name is { Length: > 0 } name ? name : appId;
    }

    /// <summary>
    /// Creating badge icons using GDI+
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    private static nint CreateBadgeIcon(int count)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.Transparent);

        // Red circle
        using var bgBrush = new SolidBrush(Color.FromArgb(220, 53, 53));
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // Digits
        string text = count > 99 ? "99+" : count.ToString();
        float fontSize = text.Length > 2 ? 9f : text.Length > 1 ? 11f : 14f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        g.DrawString(text, font, textBrush, new RectangleF(0, 0, size, size), sf);

        return bmp.GetHicon();
    }

    /// <summary>
    /// Update the number of Badge notifications on the Taskbar.
    /// </summary>
    private void UpdateTaskbarBadge()
    {
        int total = 0;
        foreach (var c in _badgeCounts.Values) total += c;

        if (_isPackaged)
        {
            void ApplyPackaged()
            {
                try
                {
                    var badgeXml = BadgeUpdateManager.GetTemplateContent(
                        total > 0 ? BadgeTemplateType.BadgeNumber : BadgeTemplateType.BadgeGlyph);

                    var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
                    if (total > 0)
                        badgeElement?.SetAttribute("value", total.ToString());
                    else
                        badgeElement?.SetAttribute("value", "none");

                    BadgeUpdateManager.CreateBadgeUpdaterForApplication()
                                      .Update(new BadgeNotification(badgeXml));
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"[NotificationService] UpdateTaskbarBadge error: {ex.Message}", ex);
                }
            }

            if (_dispatcherQueue is not null)
                _dispatcherQueue.TryEnqueue(ApplyPackaged);
            else
                ApplyPackaged();
        }
        else
        {
            // Unpackaged: use ITaskbarList3 SetOverlayIcon
            void ApplyOverlay()
            {
                try
                {
                    var taskbar = (ITaskbarList3)new TaskbarListInstance();
                    taskbar.HrInit();

                    nint hIcon = total > 0 ? CreateBadgeIcon(total) : nint.Zero;
                    taskbar.SetOverlayIcon(_hwnd, hIcon, total > 0 ? total.ToString() : null);

                    if (hIcon != nint.Zero)
                        DestroyIcon(hIcon);
                }
                catch (Exception ex)
                {
                    AppLogger.Log($"NotificationService UpdateTaskbarBadge error: {ex.Message}", ex);
                }
            }

            if (_dispatcherQueue is not null)
                _dispatcherQueue.TryEnqueue(ApplyOverlay);
            else
                ApplyOverlay();
        }
    }

    private static string SanitizeToastText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '&' or '<' or '>' or '"' or '\'')
                continue;
            if (char.IsSurrogate(c))
                continue;

            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    #region ITaskbarList3 COM interop
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    [ComImport, Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(nint hwnd);
        void DeleteTab(nint hwnd);
        void ActivateTab(nint hwnd);
        void SetActiveAlt(nint hwnd);
        void MarkFullscreenWindow(nint hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
        void SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(nint hwnd, int tbpFlags);
        void RegisterTab(nint hwndTab, nint hwndMDI);
        void UnregisterTab(nint hwndTab);
        void SetTabOrder(nint hwndTab, nint hwndInsertBefore);
        void SetTabActive(nint hwndTab, nint hwndMDI, uint dwReserved);
        void ThumbBarAddButtons(nint hwnd, uint cButtons, nint pButton);
        void ThumbBarUpdateButtons(nint hwnd, uint cButtons, nint pButton);
        void ThumbBarSetImageList(nint hwnd, nint himl);
        void SetOverlayIcon(nint hwnd, nint hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? pszDescription);
        void SetThumbnailTooltip(nint hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? pszTip);
        void SetThumbnailClip(nint hwnd, nint prcClip);
    }

    [ComImport, Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
    [ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListInstance { }
    #endregion
}