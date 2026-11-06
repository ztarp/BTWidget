using System.Windows;
using Microsoft.Win32;

namespace BTWidget.Services;

public static class SystemEventsBridge
{
    private static App? _app;

    public static void Subscribe(App app)
    {
        _app = app;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
    }

    public static void Unsubscribe(App app)
    {
        if (!ReferenceEquals(_app, app))
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
        _app = null;
    }

    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        var app = _app;
        app?.Dispatcher.BeginInvoke(() => ThemeResources.Apply(ThemeResources.IsLightTheme()));
    }
}
