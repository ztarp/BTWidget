namespace BTWidget.Models;

public enum BatteryLevel
{
    Normal,
    Low,
    Critical
}

public sealed class ConnectedDevice
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public int? BatteryPercent { get; set; }

    public bool IsCharging { get; set; }
}

public sealed class BluetoothSnapshot
{
    public static BluetoothSnapshot Empty { get; } = new()
    {
        Devices = []
    };

    public required IReadOnlyList<ConnectedDevice> Devices { get; init; }

    public string? Error { get; init; }
}

public sealed class DeviceRow
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string BatteryText { get; init; }

    public required string AccessibleName { get; init; }

    public double BatteryValue { get; init; }

    public bool HasBattery { get; init; }

    public bool IsCharging { get; init; }

    public bool IsLow { get; init; }

    public BatteryLevel Level { get; init; }

    public static DeviceRow From(ConnectedDevice device, int lowBatteryPercent)
    {
        var level = Classify(device.BatteryPercent, lowBatteryPercent);
        var batteryText = device.BatteryPercent is int percent ? $"{percent}%" : "—";
        var accessible = device.BatteryPercent is int known
            ? $"{device.Name}, {known} percent"
            : $"{device.Name}, battery not available";

        if (device.IsCharging)
        {
            accessible += ", charging";
        }

        if (level == BatteryLevel.Critical)
        {
            accessible += ", battery very low";
        }
        else if (level == BatteryLevel.Low)
        {
            accessible += ", battery low";
        }

        return new DeviceRow
        {
            Key = device.Key,
            Name = device.Name,
            BatteryText = batteryText,
            AccessibleName = accessible,
            BatteryValue = device.BatteryPercent ?? 0,
            HasBattery = device.BatteryPercent is not null,
            IsCharging = device.IsCharging,
            IsLow = level != BatteryLevel.Normal,
            Level = level
        };
    }

    public static BatteryLevel Classify(int? percent, int lowBatteryPercent)
    {
        if (percent is not int value)
        {
            return BatteryLevel.Normal;
        }

        if (value <= Math.Min(10, lowBatteryPercent))
        {
            return BatteryLevel.Critical;
        }

        if (value <= lowBatteryPercent)
        {
            return BatteryLevel.Low;
        }

        return BatteryLevel.Normal;
    }
}
