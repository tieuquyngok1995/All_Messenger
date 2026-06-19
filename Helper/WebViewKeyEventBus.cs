using System;

namespace All_in_One_Messenger.Helper;

public enum WebViewKeyAction { SwitchTab, NextTab, PrevTab }

public record WebViewKeyCombo(WebViewKeyAction Action, int TabIndex = -1);

public static class WebViewKeyEventBus
{
    public static event Action<WebViewKeyCombo>? KeyComboReceived;
    public static void Raise(WebViewKeyCombo combo) => KeyComboReceived?.Invoke(combo);
}