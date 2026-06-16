using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace All_in_One_Messenger.Helper;

/// <summary>
/// Information about a custom chat server added by the user.
/// </summary>
public partial class CustomServerInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = Guid.NewGuid().ToString("N")[..8];
    private string _name = string.Empty;
    private string _url = string.Empty;
    private string _iconGlyph = "\uE774"; // Globe
    private bool _isEnabled = true;
    private bool _isDefault = false;
    public int _order = 0;

    /// <summary>
    /// A unique ID (short GUID) is used as a tag for the NavigationViewItem and WebView profile.
    /// </summary>
    public string Id
    {
        get => _id;
        set { if (_id != value) { _id = value; Notify(); } }
    }

    /// <summary>
    /// The name displayed on the menu.
    /// </summary>
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; Notify(); } }
    }

    /// <summary>
    /// Website URL.
    /// </summary>
    public string Url
    {
        get => _url;
        set { if (_url != value) { _url = value; Notify(); } }
    }

    /// <summary>
    /// Glyph icon from Segoe MDL2 Assets.
    /// </summary>
    public string IconGlyph
    {
        get => _iconGlyph;
        set { if (_iconGlyph != value) { _iconGlyph = value; Notify(); } }
    }

    public bool IsEnable
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; Notify(); } }
    }

    public bool IsDefault
    {
        get => _isDefault;
        set { if (_isDefault != value) { _isDefault = value; Notify(); } }
    }

    public bool IsCustom => !IsDefault;

    /// <summary>
    /// Arrange the display order of the custom servers.
    /// </summary>
    public int Order
    {
        get => _order;
        set { if (_order != value) { _order = value; Notify(); } }
    }
}
