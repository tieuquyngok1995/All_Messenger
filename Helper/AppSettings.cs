using All_in_One_Messenger.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Storage;

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

    // ── Server icon folder path ───────────────────────────────────────────────────────────
    private static readonly string _serverIcons = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllinOneMessenger",
        "ServerIcons");

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

    public static string SaveIconLocally(StorageFile file)
    {
        try
        {
            Directory.CreateDirectory(_serverIcons);
            var ext = Path.GetExtension(file.Name).ToLowerInvariant();
            var dest = Path.Combine(_serverIcons, $"{Guid.NewGuid()}{ext}");
            File.Copy(file.Path, dest, overwrite: false);
            return dest;
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[AppSettings] SaveIconLocally error: {ex.Message}", ex);
            return string.Empty;
        }
    }

    public static void DeleteIconIfLocal(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (File.Exists(path)
                && Path.GetFullPath(path).StartsWith(
                       Path.GetFullPath(_serverIcons), StringComparison.OrdinalIgnoreCase))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"[AppSettings] DeleteIconIfLocal:{path} error: {ex.Message}", ex);
        }
    }

    public static void SaveCustomServers(List<CustomServerInfo> servers) => Set(CustomServersKey, JsonSerializer.Serialize(servers));
}
