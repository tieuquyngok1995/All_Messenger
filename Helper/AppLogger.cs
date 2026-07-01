using System;
using System.IO;

namespace All_in_One_Messenger.Helper;

public static class AppLogger
{
    // Error log file path 
    // Packaged (MSIX): %LocalAppData%\Packages\{PFN}\LocalState\error.log 
    // Unpackaged (exe): %LocalAppData%\AllinOneMessenger\error.log
    private static readonly string LogPath = GetLogPath();

    private static string GetLogPath()
    {
        try
        {
            var _ = Windows.ApplicationModel.Package.Current;
            return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "error.log");
        }
        catch
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AllinOneMessenger", "error.log");
        }
    }

    /// <summary>
    /// Log Exceptions.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="ex"></param>
    public static void Log(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
            File.AppendAllText(LogPath, entry);
        }
        catch { }
    }

    /// <summary>
    /// Log message.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="message"></param>
    public static void Log(string source, string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {message}{Environment.NewLine}{new string('-', 80)}{Environment.NewLine}";
            File.AppendAllText(LogPath, entry);
        }
        catch { }
    }
}