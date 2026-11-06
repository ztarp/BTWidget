using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BTWidget.Models;

namespace BTWidget.Services;

public static class SettingsStore
{
    private const int MaxBytes = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 8,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly object Gate = new();

    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BTWidget");

    public static string FilePath => Path.Combine(DirectoryPath, "settings.json");

    public static WidgetSettings Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return new WidgetSettings();
                }

                var info = new FileInfo(FilePath);
                if (info.Length <= 0 || info.Length > MaxBytes)
                {
                    return new WidgetSettings();
                }

                var dto = JsonSerializer.Deserialize<SettingsDto>(File.ReadAllText(FilePath), JsonOptions);
                return FromDto(dto);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new WidgetSettings();
            }
        }
    }

    public static void Save(WidgetSettings settings)
    {
        settings.Normalize();
        var json = JsonSerializer.Serialize(ToDto(settings), JsonOptions);

        lock (Gate)
        {
            Directory.CreateDirectory(DirectoryPath);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, FilePath, overwrite: true);
        }
    }

    private static WidgetSettings FromDto(SettingsDto? dto)
    {
        var settings = new WidgetSettings();
        if (dto == null)
        {
            return settings;
        }

        settings.Left = dto.Left;
        settings.Top = dto.Top;
        settings.LaunchAtStartup = dto.LaunchAtStartup ?? false;
        settings.KeepAboveWindows = dto.KeepAboveWindows ?? dto.AlwaysOnTop ?? false;
        settings.LowBatteryPercent = dto.LowBatteryPercent ?? WidgetSettings.DefaultLowBatteryPercent;
        settings.Normalize();
        return settings;
    }

    private static SettingsDto ToDto(WidgetSettings settings) => new()
    {
        Left = settings.Left,
        Top = settings.Top,
        LaunchAtStartup = settings.LaunchAtStartup,
        KeepAboveWindows = settings.KeepAboveWindows,
        LowBatteryPercent = settings.LowBatteryPercent
    };

    private sealed class SettingsDto
    {
        public double? Left { get; set; }

        public double? Top { get; set; }

        public bool? LaunchAtStartup { get; set; }

        public bool? KeepAboveWindows { get; set; }

        public bool? AlwaysOnTop { get; set; }

        public int? LowBatteryPercent { get; set; }
    }
}
