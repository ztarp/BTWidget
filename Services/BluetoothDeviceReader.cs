using System.Text;
using System.Text.RegularExpressions;
using BTWidget.Models;
using BTWidget.Native;

namespace BTWidget.Services;

/// <summary>
/// Reads connected Bluetooth devices from the local device tree.
/// It does not open a GATT session, pair, connect, or change radio state.
/// Battery data is the same property Windows Settings displays.
/// </summary>
public static partial class BluetoothDeviceReader
{
    private static readonly string[] Enumerators = ["BTHLE", "BTHENUM", "BTH", "BTHHFENUM"];

    [GeneratedRegex(@"[\\&]([0-9A-Fa-f]{12})(?:[\\&_]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex AddressInId();

    public static BluetoothSnapshot Read()
    {
        try
        {
            var nodes = new List<Node>();
            var anyEnumerator = false;
            foreach (var enumerator in Enumerators)
            {
                if (!TryGetInstanceIds(enumerator, out var ids))
                {
                    continue;
                }

                anyEnumerator = true;
                foreach (var id in ids)
                {
                    if (ShouldSkip(id))
                    {
                        continue;
                    }

                    if (TryReadNode(id, out var node))
                    {
                        nodes.Add(node);
                    }
                }
            }

            ApplyClassicConnections(nodes);

            if (!anyEnumerator && nodes.Count == 0)
            {
                return new BluetoothSnapshot
                {
                    Devices = [],
                    Error = "Couldn't read Bluetooth devices."
                };
            }

            var devices = Project(nodes);
            BatteryGattReader.Apply(devices);
            return new BluetoothSnapshot
            {
                Devices = devices
            };
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or EntryPointNotFoundException)
        {
            return new BluetoothSnapshot
            {
                Devices = [],
                Error = "Couldn't read Bluetooth devices."
            };
        }
    }

    private static bool TryGetInstanceIds(string enumerator, out List<string> ids)
    {
        ids = [];
        var flags = DeviceProperties.FilterEnumerator | DeviceProperties.FilterPresent;
        var status = DeviceProperties.GetDeviceIdListSize(out var length, enumerator, flags);
        if (status != DeviceProperties.Success || length <= 1 || length > 100_000)
        {
            return false;
        }

        var buffer = new char[length];
        status = DeviceProperties.GetDeviceIdList(enumerator, buffer, length, flags);
        if (status != DeviceProperties.Success)
        {
            return false;
        }

        var text = new string(buffer);
        foreach (var id in text.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (id.Length <= 512)
            {
                ids.Add(id);
            }
        }

        return true;
    }

    private static bool ShouldSkip(string instanceId)
    {
        if (instanceId.Contains("BTHLEDEVICE", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (instanceId.Contains("MS_BTH", StringComparison.OrdinalIgnoreCase)
            || instanceId.Contains("MS_RFCOMM", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return instanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadNode(string instanceId, out Node node)
    {
        node = new Node { InstanceId = instanceId };
        if (DeviceProperties.LocateDevNode(out var devInst, instanceId, 0) != DeviceProperties.Success)
        {
            return false;
        }

        node.Name = SanitizeName(ReadString(devInst, DeviceProperties.FriendlyName)
            ?? ReadString(devInst, DeviceProperties.Name));
        node.Address = NormalizeAddress(ReadString(devInst, DeviceProperties.Address))
            ?? AddressFromInstanceId(instanceId);
        node.Battery = ReadBattery(devInst);
        node.Charging = ReadCharging(devInst, node.Battery is not null);
        node.Connected = ReadBoolean(devInst, DeviceProperties.IsConnected) == true;
        node.IsPrimary = instanceId.Contains(@"\DEV_", StringComparison.OrdinalIgnoreCase)
            || instanceId.Contains(@"\Dev_", StringComparison.OrdinalIgnoreCase);

        if (node.Address == null && !node.Connected && node.Battery is null)
        {
            return false;
        }

        return node.Address != null || node.Connected;
    }

    private static void ApplyClassicConnections(List<Node> nodes)
    {
        List<ClassicDevice>? classic;
        try
        {
            classic = ReadClassicConnected();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            return;
        }

        if (classic == null)
        {
            return;
        }

        foreach (var device in classic)
        {
            var existing = nodes.FirstOrDefault(node =>
                node.Address != null
                && string.Equals(node.Address, device.Address, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.Connected = true;
                if (string.IsNullOrWhiteSpace(existing.Name))
                {
                    existing.Name = device.Name;
                }

                continue;
            }

            nodes.Add(new Node
            {
                InstanceId = "classic:" + device.Address,
                Address = device.Address,
                Name = device.Name,
                Connected = true,
                IsPrimary = true
            });
        }
    }

    private static List<ClassicDevice>? ReadClassicConnected()
    {
        var search = new BluetoothApi.SearchParams
        {
            Size = Marshal.SizeOf<BluetoothApi.SearchParams>(),
            ReturnConnected = true,
            IssueInquiry = false,
            Radio = IntPtr.Zero
        };
        var info = new BluetoothApi.DeviceInfo
        {
            Size = Marshal.SizeOf<BluetoothApi.DeviceInfo>()
        };

        var find = BluetoothApi.BluetoothFindFirstDevice(ref search, ref info);
        if (find == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            return (error is 0 or BluetoothApi.ErrorNoMoreItems) ? [] : null;
        }

        var found = new List<ClassicDevice>();
        try
        {
            do
            {
                if (!info.Connected)
                {
                    continue;
                }

                var address = FormatClassicAddress(info.Address);
                var name = SanitizeName(info.Name);
                if (address == null || name == null)
                {
                    continue;
                }

                found.Add(new ClassicDevice(address, name));
            }
            while (BluetoothApi.BluetoothFindNextDevice(find, ref info));
        }
        finally
        {
            BluetoothApi.BluetoothFindDeviceClose(find);
        }

        return found;
    }

    private static List<ConnectedDevice> Project(List<Node> nodes)
    {
        var groups = new Dictionary<string, List<Node>>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var key = node.Address ?? node.InstanceId;
            if (!groups.TryGetValue(key, out var group))
            {
                group = [];
                groups.Add(key, group);
            }

            group.Add(node);
        }

        var devices = new List<ConnectedDevice>();
        foreach (var group in groups.Values)
        {
            if (!group.Any(node => node.Connected))
            {
                continue;
            }

            var nameSource = group
                .OrderByDescending(node => node.IsPrimary)
                .ThenByDescending(node => NameScore(node.Name))
                .First();
            var batterySource = group
                .Where(node => node.Battery is not null)
                .OrderByDescending(node => node.IsPrimary)
                .FirstOrDefault();

            var name = nameSource.Name ?? batterySource?.Name ?? "Bluetooth device";
            devices.Add(new ConnectedDevice
            {
                Key = nameSource.Address ?? nameSource.InstanceId,
                Name = name,
                BatteryPercent = batterySource?.Battery,
                IsCharging = group.Any(node => node.Charging)
            });
        }

        return devices
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(24)
            .ToList();
    }

    private static int? ReadBattery(uint devInst)
    {
        if (!TryGetBytes(devInst, DeviceProperties.BatteryPercent, out var type, out var data)
            || type != DeviceProperties.TypeByte
            || data.Length < 1)
        {
            return null;
        }

        var value = data[0];
        return value <= 100 ? value : null;
    }

    private static bool ReadCharging(uint devInst, bool hasBattery)
    {
        if (TryGetBytes(devInst, DeviceProperties.ChargingState, out var stateType, out var state)
            && stateType == DeviceProperties.TypeByte
            && state.Length >= 1
            && state[0] == 1)
        {
            return true;
        }

        return hasBattery
            && ReadBoolean(devInst, DeviceProperties.BatteryCharging) == true;
    }

    private static bool? ReadBoolean(uint devInst, DeviceProperties.DevPropKey key)
    {
        if (!TryGetBytes(devInst, key, out var type, out var data) || data.Length < 1)
        {
            return null;
        }

        if (type == DeviceProperties.TypeBoolean || type == DeviceProperties.TypeByte)
        {
            return data[0] != 0;
        }

        return null;
    }

    private static string? ReadString(uint devInst, DeviceProperties.DevPropKey key)
    {
        if (!TryGetBytes(devInst, key, out var type, out var data)
            || type != DeviceProperties.TypeString
            || data.Length < 2)
        {
            return null;
        }

        var text = Encoding.Unicode.GetString(data).TrimEnd('\0').Trim();
        return text.Length == 0 ? null : text;
    }

    private static bool TryGetBytes(uint devInst, DeviceProperties.DevPropKey key, out uint type, out byte[] data)
    {
        type = DeviceProperties.TypeEmpty;
        data = [];
        uint size = 0;
        var status = DeviceProperties.GetDevNodeProperty(devInst, ref key, out type, null, ref size, 0);
        if (status == DeviceProperties.NoSuchValue || status == DeviceProperties.NoSuchDevNode || size == 0 || size > 1024)
        {
            return false;
        }

        if (status != DeviceProperties.BufferSmall && status != DeviceProperties.Success)
        {
            return false;
        }

        var buffer = new byte[size];
        status = DeviceProperties.GetDevNodeProperty(devInst, ref key, out type, buffer, ref size, 0);
        if (status != DeviceProperties.Success || size > buffer.Length)
        {
            return false;
        }

        if (size != buffer.Length)
        {
            Array.Resize(ref buffer, (int)size);
        }

        data = buffer;
        return true;
    }

    private static string? AddressFromInstanceId(string instanceId)
    {
        var marker = instanceId.IndexOf("DEV_", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0 && marker + 4 + 12 <= instanceId.Length)
        {
            var candidate = NormalizeAddress(instanceId.Substring(marker + 4, 12));
            if (candidate != null)
            {
                return candidate;
            }
        }

        var match = AddressInId().Match(instanceId);
        return match.Success ? NormalizeAddress(match.Groups[1].Value) : null;
    }

    private static string? NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = value.Replace(":", "", StringComparison.Ordinal).Trim();
        if (hex.Length != 12 || !hex.All(Uri.IsHexDigit))
        {
            return null;
        }

        return hex.ToUpperInvariant();
    }

    private static string? FormatClassicAddress(ulong address)
    {
        Span<char> chars = stackalloc char[12];
        for (var i = 0; i < 6; i++)
        {
            var b = (byte)((address >> (8 * (5 - i))) & 0xFF);
            chars[i * 2] = ToHex(b >> 4);
            chars[i * 2 + 1] = ToHex(b & 0xF);
        }

        return new string(chars);
    }

    private static char ToHex(int value) => (char)(value < 10 ? '0' + value : 'A' + (value - 10));

    internal static string? SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var buffer = new StringBuilder(Math.Min(name.Length, 80));
        foreach (var character in name)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            buffer.Append(character);
            if (buffer.Length >= 64)
            {
                break;
            }
        }

        var cleaned = buffer.ToString().Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    private static int NameScore(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        var score = 10;
        if (name.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Avrcp", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Audio Sink", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Generic Access", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Generic Attribute", StringComparison.OrdinalIgnoreCase))
        {
            score -= 8;
        }

        return score;
    }

    private sealed class Node
    {
        public string InstanceId { get; set; } = "";

        public string? Address { get; set; }

        public string? Name { get; set; }

        public int? Battery { get; set; }

        public bool Charging { get; set; }

        public bool Connected { get; set; }

        public bool IsPrimary { get; set; }
    }

    private readonly record struct ClassicDevice(string Address, string Name);
}
