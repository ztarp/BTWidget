using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using BTWidget.Models;

namespace BTWidget.Services;

/// <summary>
/// Reads the standard Battery Service for connected devices that need a live
/// level or charging flag. It only reads characteristics. It does not write,
/// pair, or disconnect.
/// </summary>
internal static class BatteryGattReader
{
    private static readonly Guid BatteryService = new("0000180f-0000-1000-8000-00805f9b34fb");
    private static readonly Guid BatteryLevel = new("00002a19-0000-1000-8000-00805f9b34fb");
    private static readonly Guid BatteryPowerState = new("00002a1a-0000-1000-8000-00805f9b34fb");
    private static readonly Guid BatteryLevelStatus = new("00002bed-0000-1000-8000-00805f9b34fb");
    private static readonly Guid KeychronService = new("00010203-0405-0607-0809-0a0b0c0d1912");
    private static readonly Guid KeychronChargeState = new("00010203-0405-0607-0809-0a0b0c0d2b12");

    public static void Apply(IReadOnlyList<ConnectedDevice> devices)
    {
        foreach (var device in devices)
        {
            if (!device.Name.Contains("Keychron", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryParseAddress(device.Key, out var address))
            {
                continue;
            }

            try
            {
                ApplyAsync(device, address).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static async Task ApplyAsync(ConnectedDevice device, ulong address)
    {
        var ble = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
        if (ble == null)
        {
            return;
        }

        try
        {
            if (ble.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                return;
            }

            var level = await ReadByteAsync(ble, BatteryService, BatteryLevel);
            if (level is >= 0 and <= 100)
            {
                device.BatteryPercent = level;
            }

            if (IsStandardCharging(await ReadBytesAsync(ble, BatteryService, BatteryPowerState))
                || IsLevelStatusCharging(await ReadBytesAsync(ble, BatteryService, BatteryLevelStatus))
                || IsKeychronCharging(await ReadBytesAsync(ble, KeychronService, KeychronChargeState)))
            {
                device.IsCharging = true;
            }
        }
        finally
        {
            ble.Dispose();
        }
    }

    private static bool IsStandardCharging(byte[]? value)
    {
        if (value == null || value.Length < 1)
        {
            return false;
        }

        // Battery Power State: bits 4-5 are the charge field. 3 means charging.
        return ((value[0] >> 4) & 0x3) == 0x3;
    }

    private static bool IsLevelStatusCharging(byte[]? value)
    {
        if (value == null || value.Length < 3 || (value[0] & 0x01) == 0)
        {
            return false;
        }

        var powerState = value[1] | (value[2] << 8);
        return (powerState & 0x3) == 0x1;
    }

    private static bool IsKeychronCharging(byte[]? value)
    {
        // Keychron's wireless module stores 0 not charging, 1 charging, 2 full on power.
        return value is [1] or [2];
    }

    private static async Task<int?> ReadByteAsync(BluetoothLEDevice device, Guid serviceId, Guid characteristicId)
    {
        var bytes = await ReadBytesAsync(device, serviceId, characteristicId);
        return bytes is [var level] ? level : null;
    }

    private static async Task<byte[]?> ReadBytesAsync(BluetoothLEDevice device, Guid serviceId, Guid characteristicId)
    {
        var services = await device.GetGattServicesForUuidAsync(serviceId, BluetoothCacheMode.Cached);
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
        {
            services = await device.GetGattServicesForUuidAsync(serviceId, BluetoothCacheMode.Uncached);
        }

        if (services.Status != GattCommunicationStatus.Success)
        {
            return null;
        }

        foreach (var service in services.Services)
        {
            try
            {
                var characteristics = await service.GetCharacteristicsForUuidAsync(characteristicId, BluetoothCacheMode.Uncached);
                if (characteristics.Status != GattCommunicationStatus.Success)
                {
                    continue;
                }

                foreach (var characteristic in characteristics.Characteristics)
                {
                    if (!characteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                    {
                        continue;
                    }

                    var read = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                    if (read.Status != GattCommunicationStatus.Success || read.Value == null)
                    {
                        continue;
                    }

                    var bytes = new byte[read.Value.Length];
                    DataReader.FromBuffer(read.Value).ReadBytes(bytes);
                    return bytes;
                }
            }
            finally
            {
                service.Dispose();
            }
        }

        return null;
    }

    private static bool TryParseAddress(string key, out ulong address)
    {
        address = 0;
        return key.Length == 12
            && key.All(Uri.IsHexDigit)
            && ulong.TryParse(key, System.Globalization.NumberStyles.HexNumber, null, out address);
    }
}
