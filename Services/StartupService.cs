using System.IO;
using Microsoft.Win32;

namespace BTWidget.Services;

/// <summary>
/// Registers startup for the current user only. The Run value is a quoted
/// path to this executable and nothing else.
/// </summary>
public static class StartupService
{
    private const string ValueName = "BTWidget";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The startup key is unavailable.");

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exe = CurrentExecutable();
        if (exe == null)
        {
            throw new InvalidOperationException("Startup can only be registered for BTWidget.exe.");
        }

        key.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
    }

    public static string? CurrentExecutable()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(path);
        var name = Path.GetFileName(fullPath);
        if (name.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (fullPath.IndexOf('"') >= 0 || fullPath.Any(char.IsControl))
        {
            return null;
        }

        return fullPath;
    }
}
