namespace BTWidget.Models;

public sealed class WidgetSettings
{
    public const int DefaultLowBatteryPercent = 20;

    public static readonly int[] LowBatteryChoices = [10, 15, 20, 25];

    public double? Left { get; set; }

    public double? Top { get; set; }

    public bool LaunchAtStartup { get; set; }

    public bool KeepAboveWindows { get; set; }

    public int LowBatteryPercent { get; set; } = DefaultLowBatteryPercent;

    public void Normalize()
    {
        if (Left is double left && !double.IsFinite(left))
        {
            Left = null;
        }

        if (Top is double top && !double.IsFinite(top))
        {
            Top = null;
        }

        if (Left is null || Top is null)
        {
            Left = null;
            Top = null;
        }

        LowBatteryPercent = SnapLowBattery(LowBatteryPercent);
    }

    public static int SnapLowBattery(int percent)
    {
        var best = DefaultLowBatteryPercent;
        var bestDistance = int.MaxValue;
        foreach (var choice in LowBatteryChoices)
        {
            var distance = Math.Abs(choice - percent);
            if (distance < bestDistance)
            {
                best = choice;
                bestDistance = distance;
            }
        }

        return best;
    }
}
