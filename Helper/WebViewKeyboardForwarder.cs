public static class WebViewKeyboardForwarder
{
  public static void Register(CoreWebView2 core, DispatcherQueue dispatcher, Action<WebViewKeyCombo> onCombo)
  {
    var pressedKeys = new HashSet<VirtualKey>();

    core.AcceleratorKeyPressed += (s, e) =>
    {
      var key = (VirtualKey)e.VirtualKey;

      if (e.KeyEventKind == CoreWebView2KeyEventKind.KeyDown ||
              e.KeyEventKind == CoreWebView2KeyEventKind.SystemKeyDown)
        pressedKeys.Add(key);
      else
        pressedKeys.Remove(key);

      if (e.KeyEventKind != CoreWebView2KeyEventKind.KeyDown &&
              e.KeyEventKind != CoreWebView2KeyEventKind.SystemKeyDown)
        return;

      bool alt = pressedKeys.Contains(VirtualKey.Menu);
      bool ctrl = pressedKeys.Contains(VirtualKey.Control);
      bool shift = pressedKeys.Contains(VirtualKey.Shift);

      // Alt+0~9 → chuyển tab theo index
      if (alt && key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
      {
        int index = key - VirtualKey.Number0;
        dispatcher.TryEnqueue(() => onCombo(new WebViewKeyCombo(WebViewKeyAction.SwitchTab, index)));
        e.Handled = true;
        return;
      }

      // Ctrl+Tab hoặc Shift+Tab → next/prev tab
      if (ctrl && key == VirtualKey.Tab)
      {
        var action = shift ? WebViewKeyAction.PrevTab : WebViewKeyAction.NextTab;
        dispatcher.TryEnqueue(() => onCombo(new WebViewKeyCombo(action)));
        e.Handled = true;
        return;
      }

      // Alt+` → next tab (Alt+~)
      if (alt && (int)key == 192) // VK 192 = `~
      {
        dispatcher.TryEnqueue(() => onCombo(new WebViewKeyCombo(WebViewKeyAction.NextTab)));
        e.Handled = true;
      }
    };
  }
}

public enum WebViewKeyAction { SwitchTab, NextTab, PrevTab }

public record WebViewKeyCombo(WebViewKeyAction Action, int TabIndex = -1);