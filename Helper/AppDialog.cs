using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace All_in_One_Messenger.Helper;

public static class AppDialog
{
    /// <summary>
    /// Dialog confirms 2 buttons: [Primary] / [Close].
    /// </summary>
    /// <returns>true if the user clicks the Primary button, false if they click Close or close the dialog.</returns>
    public static async Task<bool> ShowConfirmAsync(
        XamlRoot xamlRoot,
        string title,
        string message,
        string primaryButtonText = "Đồng ý",
        string closeButtonText = "Hủy")
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = xamlRoot.Content is FrameworkElement element ? element.ActualTheme : ElementTheme.Default
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    /// <summary>
    /// Dialog confirms 3 options: Primary / Secondary / Close.
    /// Returns ContentDialogResult directly for the caller to handle the 3 branches themselves.
    /// </summary>
    public static async Task<ContentDialogResult> ShowConfirmOptionsAsync(
        XamlRoot xamlRoot,
        string title,
        string message,
        string primaryButtonText = "Có",
        string secondaryButtonText = "Không",
        string closeButtonText = "Hủy")
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = xamlRoot.Content is FrameworkElement element ? element.ActualTheme : ElementTheme.Default
        };

        return await dialog.ShowAsync();
    }

    /// <summary>
    /// Simple notification dialog, with only one close button.
    /// </summary>
    public static async Task ShowMessageAsync(
        XamlRoot xamlRoot,
        string title,
        string message,
        string closeButtonText = "OK")
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = closeButtonText,
            RequestedTheme = xamlRoot.Content is FrameworkElement element ? element.ActualTheme : ElementTheme.Default
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// Error Dialog (alias of ShowMessageAsync, default title is "Error").
    /// </summary>
    public static Task ShowErrorAsync(XamlRoot xamlRoot, string message, string title = "Lỗi")
        => ShowMessageAsync(xamlRoot, title, message);

    /// <summary>
    /// Create a Progress dialog (with a ProgressBar showing the progress percentage).
    /// Do not directly await this function — call controller.Show() and then handle the background work.
    /// Use controller.UpdateProgress(...) to update and controller.Close() when finished.
    /// </summary>
    /// <param name="isIndeterminate">true = progress bar with an indefinite rotation (unknown percentage), false = a specific percentage.</param>
    /// <param name="showCancelButton">true = display a Cancel button for the user to stop the process.</param>
    /// <param name="onCancel">Callback is called when the user clicks Cancel.</param>
    public static ProgressDialogController CreateProgressDialog(
        XamlRoot xamlRoot,
        string title,
        string message,
        bool isIndeterminate = false,
        bool showCancelButton = false,
        Action? onCancel = null)
    {
        return new ProgressDialogController(xamlRoot, title, message, isIndeterminate, showCancelButton, onCancel);
    }
}

/// <summary>
/// Controller manages a Progress ContentDialog.
/// Allows updating progress percentage/message and closing the dialog safely.
/// Even when called from a background thread, thanks to DispatcherQueue.
/// </summary>
public class ProgressDialogController
{
    private readonly ContentDialog _dialog;
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _messageText;
    private readonly TextBlock _percentText;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Action? _onCancel;

    public ProgressDialogController(
        XamlRoot xamlRoot,
        string title,
        string message,
        bool isIndeterminate,
        bool showCancelButton,
        Action? onCancel)
    {
        _onCancel = onCancel;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        };

        _progressBar = new ProgressBar
        {
            IsIndeterminate = isIndeterminate,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Width = 320
        };

        _percentText = new TextBlock
        {
            Text = "0%",
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = isIndeterminate ? Visibility.Collapsed : Visibility.Visible
        };

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(_messageText);
        stack.Children.Add(_progressBar);
        stack.Children.Add(_percentText);

        _dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = stack,
            RequestedTheme = xamlRoot.Content is FrameworkElement element ? element.ActualTheme : ElementTheme.Default
        };

        if (showCancelButton)
        {
            _dialog.CloseButtonText = "Hủy";
            _dialog.CloseButtonClick += (s, e) => _onCancel?.Invoke();
        }
    }

    /// <summary>
    /// Display dialog (fire-and-forget). Don't wait here as it still needs updating.
    /// Progress bar while the dialog is open.
    /// </summary>
    public void Show() => _ = _dialog.ShowAsync();

    /// <summary>
    /// Update progress percentage (0-100) and display message (if passed).
    /// Safe to call from background thread.
    /// </summary>
    public void UpdateProgress(double percent, string? message = null)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _progressBar.Value = percent;
            _percentText.Text = $"{percent:0}%";
            if (message != null)
                _messageText.Text = message;
        });
    }

    /// <summary>
    /// Close the progress dialog, call it when processing is complete.
    /// Safe to call from a background thread.
    /// </summary>
    public void Close()
    {
        _dispatcherQueue.TryEnqueue(() => _dialog.Hide());
    }

}