using All_in_One_Messenger.Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace All_in_One_Messenger.Models;

public enum IconType { Glyph, Image }

/// <summary>
/// Information about a custom chat server added by the user.
/// </summary>
public partial class CustomServerInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = Guid.NewGuid().ToString("N")[..8];
    private string _name = string.Empty;
    private string _url = string.Empty;
    private string _iconName = "\uE774";
    private bool _isEnabled = true;
    private int _order = 0;

    /// <summary>
    /// A unique ID (short GUID) used as a tag for NavigationViewItem and WebView profile.
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
    /// Icon source type: Glyph (Segoe MDL2) or Image (URI).
    /// </summary>
    [JsonIgnore]
    public IconType IconType => IsGlyph(IconName) ? IconType.Glyph : IconType.Image;

    [JsonIgnore]
    public bool IsGlyphIcon => IconType == IconType.Glyph;

    [JsonIgnore]
    public bool IsImageIcon => IconType == IconType.Image;

    /// <summary>
    /// Returns a BitmapImage when IconName is a valid URI (Image type),
    /// or null when it is a glyph character.
    /// </summary>
    [JsonIgnore]
    public ImageSource? ImageSource
    {
        get
        {
            if (!IsImageIcon) return null;

            if (File.Exists(IconName) && Uri.TryCreate(IconName, UriKind.Absolute, out var uri))
                return new BitmapImage(uri);
            return null;
        }
    }

    /// <summary>
    /// Either a single Segoe MDL2 glyph character or an absolute image URI.
    /// </summary>
    public string IconName
    {
        get => _iconName;
        set
        {
            if (_iconName != value)
            {
                _iconName = value;
                Notify();
                Notify(nameof(IconType));
                Notify(nameof(IsGlyphIcon));
                Notify(nameof(IsImageIcon));
                Notify(nameof(ImageSource));
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; Notify(); } }
    }

    /// <summary>
    /// Display order of this server in the navigation list.
    /// </summary>
    public int Order
    {
        get => _order;
        set { if (_order != value) { _order = value; Notify(); } }
    }

    /// <summary>Visible only for user-added (non-default) servers.</summary>
    public Visibility GetCustomVisibility(string id)
        => IsDefaultServer(id) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Visible only for built-in default servers.</summary>
    public Visibility GetDefaultVisibility(string id)
        => IsDefaultServer(id) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Returns the appropriate toggle icon glyph.</summary>
    public string GetIconForServer(bool isEnabled)
        => isEnabled ? "\uE890" : "\uE921";

    private static bool IsDefaultServer(string id) => id switch
    {
        AppConst.TabZalo => true,
        AppConst.TabTeams => true,
        AppConst.TabMessenger => true,
        _ => false
    };

    private static bool IsGlyph(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        return value.Length == 1 && value[0] >= '\uE700' && value[0] <= '\uF8FF';
    }
}