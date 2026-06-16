using All_in_One_Messenger.Services;
using Microsoft.Web.WebView2.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Helper;

public static class WebViewNotificationHelper {
    /// <summary>
    /// Inject script
    /// Call EnsureCoreWebView2Async after, BEFORE set the Source.
    /// Block the window.Notification API and send messages to WinUI.
    /// </summary>
    public static async Task InjectNotificationHookAsync(CoreWebView2 webView) {
        const string script = """
            (function () {
                if (window.__allMessengerHooked) return;
                window.__allMessengerHooked = true;

                // ════════════════════════════════════════════════════════════
                //   App detection
                // ════════════════════════════════════════════════════════════
                const host = location.hostname;
                const APP = {
                    isFacebook: /messenger\.com|facebook\.com/.test(host),
                    isZalo: /zalo\.me|chat\.zalo\.me/.test(host),
                    isTeams: /teams\.microsoft\.com|teams\.live\.com/.test(host),
                    isCustom: false,
                };

                if (!APP.isFacebook && !APP.isZalo && !APP.isTeams) {
                    APP.isCustom = true;
                }

                // ════════════════════════════════════════════════════════════
                //  Helpers
                // ════════════════════════════════════════════════════════════
                function postMessage(payload) {
                    try {
                        window.chrome.webview.postMessage(JSON.stringify(payload));
                    } catch (e) {
                        console.warn("[AllMessenger] postMessage failed:", e);
                    }
                }

                function postNotification(title, body, icon) {
                    const content = buildNotificationContent(title, body);
                    postMessage({ type: "notification", ...content, icon: icon || "" });
                }

                function postBadge(count) {
                    postMessage({ type: "badge", count });
                }

                function buildNotificationContent(title, body) {
                    if (title && body) return { title, body };

                    if (APP.isFacebook) {
                        const raw = document.title.replace(/^\(\d+\)\s*/, "").trim();
                        return {
                            title: title || raw || "Facebook",
                            body: body || "Bạn có tin nhắn mới",
                        };
                    }

                    if (APP.isTeams) {
                        const raw = document.title
                            .replace(/^\(\d+\)\s*/, "")
                            .replace(/\s*[\|–]\s*Microsoft Teams.*/i, "")
                            .trim();
                        return {
                            title: title || raw || "Microsoft Teams",
                            body: body || "Bạn có tin nhắn mới",
                        };
                    }

                    return {
                        title: title || document.title.replace(/^\(\d+\)\s*/, "").trim() || "Tin nhắn mới",
                        body: body || "Bạn có tin nhắn mới",
                    };
                }

                function postNotification(title, body, icon) {
                    const content = buildNotificationContent(title, body);
                    postMessage({
                        type: "notification",
                        title: content.title,
                        body: content.body,
                        icon: icon || "",
                    });
                }

                // ════════════════════════════════════════════════════════════
                //  Hook 1: Window notification hook
                // ════════════════════════════════════════════════════════════
                const _OriginalNotification = window.Notification;
                function HookedNotification(title, options = {}) {
                    postNotification(title, options.body, options.icon);
                    try {
                        const n = new _OriginalNotification(title, options);
                        n.close();
                        return n;
                    } catch {
                        return { title, body: options.body, icon: options.icon, close() { } };
                    }
                }
                HookedNotification.prototype = _OriginalNotification.prototype;
                Object.defineProperty(HookedNotification, "permission", { get: () => "granted" });
                HookedNotification.requestPermission = () => Promise.resolve("granted");
                HookedNotification.maxActions = _OriginalNotification.maxActions || 2;
                window.Notification = HookedNotification;

                // ════════════════════════════════════════════════════════════
                //  Hook 2: Service worker hook
                // ════════════════════════════════════════════════════════════
                const _origShow = typeof ServiceWorkerRegistration !== "undefined" ? ServiceWorkerRegistration.prototype.showNotification : null;
                if (_origShow) {
                    ServiceWorkerRegistration.prototype.showNotification = function (title, options = {}) {
                        postNotification(title, options.body, options.icon);
                        return _origShow.call(this, title, options);
                    };
                }

                // ════════════════════════════════════════════════════════════
                //  Hook 3: Badge counting from document.title
                // ════════════════════════════════════════════════════════════
                let _prevCount = -1;
                let _debounceTimer = null;
                function getUnreadCount() {
                    const match = document.title.match(/^\((\d+)\)/);
                    const titleCount = match ? parseInt(match[1], 10) : 0;

                    let domCount = 0;
                    if (APP.isCustom) {
                        const group = document.querySelector('.SidebarChannelGroup[data-rbd-draggable-id^="channels_"]');
                        domCount = group.querySelectorAll('li.SidebarChannel.unread').length;
                    }

                    if (!match) return domCount;
                    return titleCount + domCount;
                }

                function updateBadge() {
                    clearTimeout(_debounceTimer);
                    _debounceTimer = setTimeout(() => {
                        const count = getUnreadCount();
                        if (count === _prevCount) return;

                        const isFirstRun = _prevCount === -1;
                        const increased = !isFirstRun && count > _prevCount;

                        _prevCount = count;
                        postBadge(count);
                        if (increased) postNotification("", "", "");
                    }, 300);
                }

                function attachTitleObserver() {
                    const titleEl = document.querySelector("title");
                    if (!titleEl) {
                        return false;
                    }
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

                // Select the area to listen to and capture events when the channel unreads.
                document.addEventListener(
                    "DOMContentLoaded",
                    function () {
                        updateBadge();
                        const sidebar = document.querySelector(".SidebarChannelGroup");
                        if (sidebar) new MutationObserver(updateBadge).observe(sidebar, { subtree: true, childList: true, attributes: true, attributeFilter: ["class"] });
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
    public static void AllowNotificationPermission(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args) {
        if (args.PermissionKind == CoreWebView2PermissionKind.Notifications)
            args.State = CoreWebView2PermissionState.Allow;
    }

    /// <summary>
    /// Handling messages from WebView
    /// Call in the WebMessageReceived handler of each page.
    /// Automatically forward to NotificationService if the message is in the correct format.
    /// </summary>
    /// <param name="appId">For example: "Teams", "Messenger", "Zalo"</param>
    public static void HandleWebMessage(string appId, CoreWebView2WebMessageReceivedEventArgs e) {
        try {
            string raw = e.TryGetWebMessageAsString();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp)) return;
            string msgType = typeProp.GetString() ?? string.Empty;

            if (msgType == "badge") {
                int count = root.TryGetProperty("count", out var cp) ?cp.GetInt32(): 0;
                NotificationService.Instance.SetBadgeDirect(appId, count);
                return;
            }

            if (msgType != "notification") return;

            string title = root.TryGetProperty("title", out var t) ?t.GetString() ?? string.Empty : string.Empty;
            string body = root.TryGetProperty("body", out var b) ?b.GetString() ?? string.Empty : string.Empty;
            string icon = root.TryGetProperty("icon", out var i) ?i.GetString() ?? string.Empty : string.Empty;

            if (!NotificationFilter.ShouldProcess(appId, title, body, icon))
                return;

            NotificationService.Instance.HandleWebNotification(appId, title, body, icon);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[WebViewNotificationHelper] HandleWebMessage:{appId} error:{ex.Message}", ex);
        }
    }

    /// <summary>
    /// Tracking sessions via URL
    /// Hook into NavigationCompleted to automatically detect login status based on the URL.
    /// Each app has different URL logic — passed in via predicate.
    /// </summary>
    public static void AttachSessionDetector(string appId, CoreWebView2 webView, Func<string, bool> isLoggedInUrl, bool resetOnFalse = true)
{
    webView.NavigationCompleted += (sender, args) => {
            bool loggedIn = isLoggedInUrl(sender.Source);

        // reset=false: only set true when login is detected, do not reset when
        // navigate to a different domain (e.g., facebook.com link preview in Messenger)
        if (loggedIn || resetOnFalse)
            NotificationService.Instance.SetSession(appId, loggedIn);
    };
}
}