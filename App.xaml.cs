using All_in_One_Messenger.Helper;
using All_in_One_Messenger.Services;
using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace All_in_One_Messenger;

/// <summary>
/// Main application layer — initializes and manages the entire app lifecycle.
/// </summary>
public partial class App : Application
{
    public static MainWindow? MainWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            AppLogger.Log($"[App] InitializeComponent error: {ex.Message}", ex);
            throw;
        }

        // Catch exceptions on UI thread

        this.UnhandledException += (_, e) =>
        {
            AppLogger.Log($"[App] UnhandledException error: {e.Message}", e.Exception);
            e.Handled = true;
        };

        // Catch exceptions on background thread and native interop
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLogger.Log("[AppDomain] UnhandledException", e.ExceptionObject as Exception);

        // Catch the missed task that was not awaited (unobserved async exception)
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Log($"[TaskScheduler] UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>
    /// Called when the application starts.
    /// </summary>
    /// <param name="args">Information about the startup request.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (MainWindow is not null)
        {
            // App is already running, activated from toast → focus only, do not create a new one
            // NotificationInvoked handler will automatically BringToFront, no need to do anything here
            return;
        }

        // First launch → create a new window
        MainWindow = new MainWindow();

        // Initialize NotificationService after MainWindow is created
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
        NotificationService.Instance.Initialize(MainWindow.DispatcherQueue, hwnd);

        MainWindow.Activate();
    }
}
