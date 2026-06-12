using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace All_in_One_Messenger.Helper;

/// <summary>
/// Simple file-based settings store for unpackaged apps.
/// Replaces Windows.Storage.ApplicationData.Current.LocalSettings
/// which requires MSIX package identity.
/// Settings are persisted to %LOCALAPPDATA%\AllinOneMessenger\settings.json
/// </summary>
internal static class AppSettings
{
    // ── Custom Servers ───────────────────────────────────────────────────────────
    private const string CustomServersKey = "CustomServers";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    // ── Setting file path ───────────────────────────────────────────────────────────
    private static readonly string _settingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllinOneMessenger",
        "settings.json");

    private static readonly Dictionary<string, string> _cache = Load();

    /// <summary>
    /// Load settings in file.
    /// </summary>
    /// <returns></returns>
    private static Dictionary<string, string> Load()
    {
        if (File.Exists(_settingsFile))
        {
            var json = File.ReadAllText(_settingsFile);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        return [];
    }

    /// <summary>
    /// Save settings to file.
    /// </summary>
    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
        File.WriteAllText(_settingsFile,
            JsonSerializer.Serialize(_cache, JsonOptions));
    }

    public static string? Get(string key) => _cache.TryGetValue(key, out var v) ? v : null;

    public static void Set(string key, string value)
    {
        _cache[key] = value;
        Save();
    }

    public static List<CustomServerInfo> GetCustomServers()
    {
        var json = Get(CustomServersKey);
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<CustomServerInfo>>(json) ?? [];
            return [.. list.OrderBy(s => s.Order)];
        }
        catch { return []; }
    }

    public static void SaveCustomServers(List<CustomServerInfo> servers) => Set(CustomServersKey, JsonSerializer.Serialize(servers));
}
