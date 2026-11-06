using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace BTWidget.Services;

public static class ThemeResources
{
    public static bool IsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                writable: false);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return true;
        }
    }

    public static void Apply(bool light)
    {
        var dictionary = light ? Light() : Dark();
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count == 0)
        {
            merged.Add(dictionary);
        }
        else
        {
            merged[0] = dictionary;
        }
    }

    private static ResourceDictionary Dark() => Build(
        background: Color.FromRgb(0x20, 0x20, 0x20),
        border: Color.FromRgb(0x3A, 0x3A, 0x3A),
        text: Color.FromRgb(0xF3, 0xF3, 0xF3),
        muted: Color.FromRgb(0xA6, 0xA6, 0xA6),
        track: Color.FromRgb(0x33, 0x33, 0x33),
        hover: Color.FromRgb(0x2E, 0x2E, 0x2E),
        pressed: Color.FromRgb(0x3A, 0x3A, 0x3A),
        menu: Color.FromRgb(0x2B, 0x2B, 0x2B),
        charging: Color.FromRgb(0x7D, 0xDE, 0xA4));

    private static ResourceDictionary Light() => Build(
        background: Color.FromRgb(0xFF, 0xFF, 0xFF),
        border: Color.FromRgb(0xE0, 0xE0, 0xE0),
        text: Color.FromRgb(0x1A, 0x1A, 0x1A),
        muted: Color.FromRgb(0x5C, 0x5C, 0x5C),
        track: Color.FromRgb(0xE8, 0xE8, 0xE8),
        hover: Color.FromRgb(0xF2, 0xF2, 0xF2),
        pressed: Color.FromRgb(0xE6, 0xE6, 0xE6),
        menu: Color.FromRgb(0xFF, 0xFF, 0xFF),
        charging: Color.FromRgb(0x0E, 0x7A, 0x45));

    private static ResourceDictionary Build(
        Color background,
        Color border,
        Color text,
        Color muted,
        Color track,
        Color hover,
        Color pressed,
        Color menu,
        Color charging)
    {
        var dictionary = new ResourceDictionary
        {
            ["WindowBackground"] = Freeze(background),
            ["WindowBorder"] = Freeze(border),
            ["TextPrimary"] = Freeze(text),
            ["TextMuted"] = Freeze(muted),
            ["BarTrack"] = Freeze(track),
            ["IconHover"] = Freeze(hover),
            ["IconPressed"] = Freeze(pressed),
            ["MenuBackground"] = Freeze(menu),
            ["AccentBrush"] = Freeze(ReadAccent()),
            ["LowBrush"] = Freeze(Color.FromRgb(0xE0, 0xA1, 0x00)),
            ["CriticalBrush"] = Freeze(Color.FromRgb(0xD1, 0x34, 0x38)),
            ["ChargingBrush"] = Freeze(charging)
        };
        return dictionary;
    }

    private static Color ReadAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent",
                writable: false);
            if (key?.GetValue("AccentColorMenu") is int raw)
            {
                var red = (byte)(raw & 0xFF);
                var green = (byte)((raw >> 8) & 0xFF);
                var blue = (byte)((raw >> 16) & 0xFF);
                return Color.FromRgb(red, green, blue);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }

        return Color.FromRgb(0x00, 0x78, 0xD4);
    }

    private static SolidColorBrush Freeze(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
