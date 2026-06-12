using All_in_One_Messenger.Services;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Helper;

public static class WebViewNotificationHelper
{
    /// <summary> 
    /// Inject script
    /// Call EnsureCoreWebView2Async after, BEFORE set the Source. 
    /// Block the window.Notification API and send messages to WinUI.
    /// </summary>
    public static async Task InjectNotificationHookAsync(CoreWebView2 webView)
    {
        const string script = """
            (function () {
                if (window.__allMessengerHooked) return;
                window.__allMessengerHooked = true;

                // ── Helpers ──────────────────────────────────────────────────────────────

                function postMessage(payload) {
                    try {
                        window.chrome.webview.postMessage(JSON.stringify(payload));
                    } catch (e) {
                        console.warn("[AllMessenger] postMessage failed:", e);
                    }
                }

                function postNotification(title, body, icon) {
                    postMessage({
                        type: "notification",
                        title: title || "",
                        body: body || "",
                        icon: icon || "",
                    });
                }

                function postBadge(count) {
                    postMessage({ type: "badge", count });
                }

                // ── Hook 1: window.Notification constructor ──────────────────────────────
                // Capture Messenger, Zalo, Mattermost (using new Notification() from page context).
                const _OriginalNotification = window.Notification;

                function HookedNotification(title, options = {}) {
                    postNotification(title, options.body, options.icon);

                    // Returns a mock object instead of calling the actual API.
                    // WebView2 cannot display OS notifications directly → calling the actual API will
                    const mock = Object.assign(Object.create(_OriginalNotification.prototype), {
                        title: title || "",
                        body: options.body || "",
                        icon: options.icon || "",
                        tag: options.tag || "",
                        data: options.data ?? null,
                        silent: options.silent ?? false,
                        onclick: null,
                        onclose: null,
                        onerror: null,
                        onshow: null,
                        close() {
                            if (typeof this.onclose === "function")
                                this.onclose(new Event("close"));
                        },
                    });

                    // Inform the page that the notification has successfully "displayed"
                    setTimeout(() => {
                        if (typeof mock.onshow === "function") mock.onshow(new Event("show"));
                    }, 0);

                    return mock;
                }

                HookedNotification.prototype = _OriginalNotification.prototype;
                Object.defineProperty(HookedNotification, "permission", {
                    get: () => "granted",
                });
                HookedNotification.requestPermission = () => Promise.resolve("granted");
                HookedNotification.maxActions = _OriginalNotification.maxActions || 2;
                window.Notification = HookedNotification;

                // ── Hook 2: ServiceWorkerRegistration.showNotification ───────────────────
                // Capture notifications from Teams and apps calling showNotification() from the page/worker context.
                // (Service Worker runs in its own thread – cannot be hooked directly,
                // but many apps still call it via the registration object in the page context.)
                const _origShow = ServiceWorkerRegistration?.prototype?.showNotification;
                if (_origShow) {
                    ServiceWorkerRegistration.prototype.showNotification = function (
                        title,
                        options = {},
                    ) {
                        postNotification(title, options.body, options.icon);
                        return _origShow.call(this, title, options);
                    };
                }

                // ── Hook 3: Track the badge number in document.title ────────────────────────
                // Fallback for Teams (usually not using new Notification() when the app is open):
                // title changes to "(N) Microsoft Teams", "(N) Messenger", "(N) Zalo".
                // Only fire when the count CHANGES from the last count; send toast when the count INCREASES.
                let _prevCount = -1;

                function getUnreadCount() {
                    const titleCount = parseInt(
                        document.title.match(/^\((\d+)\)/)?.[1] ?? "0",
                        10,
                    );

                    // Mattermost channel unread
                    // <li class="SidebarChannel unread">
                    const channelCount = document.querySelectorAll(
                        "li.SidebarChannel.unread",
                    ).length;

                    return titleCount + channelCount;
                }

                function updateBadge() {
                    const count = getUnreadCount();

                    if (count === _prevCount) return;

                    // First run: sync badge without sending toast
                    const isFirstRun = _prevCount === -1;
                    const increased = !isFirstRun && count > _prevCount;

                    _prevCount = count;
                    postBadge(count);

                    if (increased) postNotification("New messages", "", "");
                }

                // ── Gắn observer lên <title> ──────────────────────────────────────────────

                function attachTitleObserver() {
                    const titleEl = document.querySelector("title");
                    if (!titleEl) return false;
                    new MutationObserver(updateBadge).observe(titleEl, {
                        childList: true,
                        characterData: true,
                        subtree: true,
                    });
                    updateBadge();
                    return true;
                }

                if (!attachTitleObserver()) {
                    // Script runs before HTML is parsed → <title> does not exist.
                    // Use subtree:true to observe the entire DOM tree until <title> appears.
                    const rootObserver = new MutationObserver(() => {
                        if (attachTitleObserver()) rootObserver.disconnect();
                    });
                    rootObserver.observe(document.documentElement || document.getRootNode(), {
                        childList: true,
                        subtree: true,
                    });

                    // DOMContentLoaded as the final fallback
                    document.addEventListener(
                        "DOMContentLoaded",
                        () => {
                            if (attachTitleObserver()) rootObserver.disconnect();
                        },
                        { once: true },
                    );
                }

                // ── Attach observer to Mattermost sidebar ──────────────────────────────────

                // Listen for unread classes on Sidebar Channels when the DOM is ready.
                document.addEventListener(
                    "DOMContentLoaded",
                    () => {
                        updateBadge();
                        const sidebar = document.querySelector("#SidebarContainer");
                        if (sidebar) {
                            new MutationObserver(updateBadge).observe(sidebar, {
                                subtree: true,
                                childList: true,
                                attributes: true,
                                attributeFilter: ["class"],
                            });
                        }
                    },
                    { once: true },
                );
            })();             
            """;

        await webView.AddScriptToExecuteOnDocumentCreatedAsync(script);
    }

    /// <summary>
    /// Handling Notification Permissions
    /// Automatically grant notification permissions when requested by the website
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    public static void AllowNotificationPermission(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
    {
        if (args.PermissionKind == CoreWebView2PermissionKind.Notifications)
            args.State = CoreWebView2PermissionState.Allow;
    }

    /// <summary>
    /// Handling messages from WebView
    /// Call in the WebMessageReceived handler of each page.
    /// Automatically forward to NotificationService if the message is in the correct format.
    /// </summary> 
    /// <param name="appId">For example: "Teams", "Messenger", "Zalo"</param>
    public static void HandleWebMessage(string appId, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string raw = e.TryGetWebMessageAsString();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return;
            string msgType = typeProp.GetString() ?? string.Empty;

            if (msgType == "badge")
            {
                int count = root.TryGetProperty("count", out var cp) ? cp.GetInt32() : 0;
                NotificationService.Instance.SetBadgeDirect(appId, count);
                return;
            }

            if (msgType != "notification") return;

            string title = root.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            string body = root.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
            string icon = root.TryGetProperty("icon", out var i) ? i.GetString() ?? string.Empty : string.Empty;

            NotificationService.Instance.HandleWebNotification(appId, title, body, icon);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebViewNotificationHelper:{appId}] Parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tracking sessions via URL
    /// Hook into NavigationCompleted to automatically detect login status based on the URL.
    /// Each app has different URL logic — passed in via predicate.
    /// </summary>
    public static void AttachSessionDetector(string appId, CoreWebView2 webView, Func<string, bool> isLoggedInUrl, bool resetOnFalse = true)
    {
        webView.NavigationCompleted += (sender, args) =>
        {
            bool loggedIn = isLoggedInUrl(sender.Source);

            // reset False=false: only set true when login is detected, do not reset when 
            // navigate to a different domain (e.g., facebook.com link preview in Messenger)
            if (loggedIn || resetOnFalse)
                NotificationService.Instance.SetSession(appId, loggedIn);

            System.Diagnostics.Debug.WriteLine($"[SessionDetector:{appId}] url={sender.Source} → loggedIn={loggedIn} (reset={resetOnFalse})");
        };
    }
}