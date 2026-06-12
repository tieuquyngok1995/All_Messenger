using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Helper;

public static class WebViewProfileHelper
{
    // Cache environment by profile name — avoid recreating it every time you navigate
    private static readonly ConcurrentDictionary<string, CoreWebView2Environment> _cache = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    // The root directory containing all profiles
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllinOneMessenger",
        "Profiles"
    );

    /// <summary>
    /// Get or create a new CoreWebView2Environment for the specified profile.
    /// Each profile has its own userData folder — ensuring independent sessions between applications.
    /// </summary>
    /// <param name="profileName">Profile name, e.g., "Teams", "Messenger", "Zalo"</param>
    public static async Task<CoreWebView2Environment> GetOrCreateAsync(string profileName)
    {
        // Return immediately if already in cache
        if (_cache.TryGetValue(profileName, out var cached))
            return cached;

        // Use separate locks for each profile — avoid creating two parallel environments for the same profile.
        var sem = _locks.GetOrAdd(profileName, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            if (_cache.TryGetValue(profileName, out cached))
                return cached;

            string profilePath = Path.Combine(BasePath, profileName);
            Directory.CreateDirectory(profilePath);

            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = string.Join(" ",
                [
                    "--disable-features=msSmartScreen",
                    "--disable-background-networking",
                    "--disable-background-timer-throttling",
                    "--disable-backgrounding-occluded-windows",
                    "--disable-sync",
                    "--disable-translate",
                    "--disable-default-apps",
                    "--no-first-run",
                    "--autoplay-policy=no-user-gesture-required",
                    "--password-store=basic",
                ])
            };

            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                null,
                profilePath,
                options
            );

            _cache[profileName] = env;
            return env;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Deletes all data (cookies, cache, localStorage) of a profile on disk.
    /// Called when the user wants to log out completely.
    /// </summary>
    public static void DeleteProfileData(string profileName)
    {
        InvalidateProfile(profileName);

        string profilePath = Path.Combine(BasePath, profileName);
        if (Directory.Exists(profilePath))
        {
            try { Directory.Delete(profilePath, recursive: true); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebViewProfileHelper] Cannot delete '{profileName}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Clear the cache of a profile from memory — used when you want to reset the session (for example, after a user logs out).
    /// </summary>
    public static void InvalidateProfile(string profileName)
    {
        _cache.TryRemove(profileName, out _);
    }
}