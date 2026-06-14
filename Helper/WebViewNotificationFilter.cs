using System;
using System.Collections.Concurrent;

namespace All_in_One_Messenger.Helper;

public class NotificationFilter
{
    private class NotificationState
    {
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Icon { get; set; } = "";
        public DateTime LastReceivedAt { get; set; }
        public DateTime FirstSpamAt { get; set; }
        public bool IsSpamming { get; set; }
    }

    private static readonly TimeSpan SpamTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(2);
    private static readonly ConcurrentDictionary<string, NotificationState> _states = new();

    /// <summary>
    /// Returns true if the notification should be processed, false if it should be ignored.
    /// </summary>
    public static bool ShouldProcess(string appId, string title, string body, string icon)
    {
        var now = DateTime.UtcNow;
        var key = appId;

        var state = _states.GetOrAdd(key, _ => new NotificationState());

        lock (state)
        {
            // First time (no state yet) → skip, save
            if (state.LastReceivedAt == default)
            {
                state.Title = title;
                state.Body = body;
                state.Icon = icon;
                state.LastReceivedAt = now;
                return true;
            }

            var elapsed = now - state.LastReceivedAt;

            // Update delivery time
            state.LastReceivedAt = now;

            // Debounce: Message arrives within 2 seconds → ignore
            if (elapsed < DebounceWindow)
            {
                return false;
            }

            // Other content → valid, reset spam
            bool isSameContent = state.Title == title && state.Body == body && state.Icon == icon;
            if (!isSameContent)
            {
                state.Title = title;
                state.Body = body;
                state.Icon = icon;
                state.IsSpamming = false;
                state.FirstSpamAt = default;
                return true;
            }

            // Similar content + more than 2 seconds have passed → flagged as spam
            if (!state.IsSpamming)
            {
                state.IsSpamming = true;
                state.FirstSpamAt = now;
                return false;
            }

            // Spamming → Check in 5 minutes
            var spamDuration = now - state.FirstSpamAt;
            if (spamDuration >= SpamTimeout)
            {
                state.IsSpamming = false;
                state.FirstSpamAt = default;
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Delete all saved stages.
    /// </summary>
    public static void ClearAllStates()
    {
        _states.Clear();
    }
}