using System.Runtime.InteropServices;

namespace BTWidget.Native;

internal static class DeviceProperties
{
    internal const uint FilterEnumerator = 0x00000001;
    internal const uint FilterPresent = 0x00000100;
    internal const uint Success = 0;
    internal const uint BufferSmall = 0x1A;
    internal const uint NoSuchValue = 0x25;
    internal const uint NoSuchDevNode = 0x0D;

    internal const uint TypeEmpty = 0;
    internal const uint TypeByte = 0x3;
    internal const uint TypeBoolean = 0x11;
    internal const uint TypeString = 0x12;

    internal static readonly DevPropKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    internal static readonly DevPropKey Name = new(new Guid("b725f130-47ef-101a-a5f1-02608c9eebac"), 10);
    internal static readonly DevPropKey BatteryPercent = new(new Guid("104ea319-6ee2-4701-bd47-8ddbf425bbe5"), 2);

    // Present beside the battery percentage. False on devices that are not charging.
    internal static readonly DevPropKey BatteryCharging = new(new Guid("104ea319-6ee2-4701-bd47-8ddbf425bbe5"), 3);

    // Documented System.Devices.ChargingState: 0 not charging, 1 charging, 2 unknown.
    internal static readonly DevPropKey ChargingState = new(new Guid("49cd1f76-5626-4b17-a4e8-18b4aa1a2213"), 11);

    // Boolean connection flag used by Windows for Bluetooth devnodes.
    internal static readonly DevPropKey IsConnected = new(new Guid("83da6326-97a6-4088-9453-a1923f573b29"), 15);
    internal static readonly DevPropKey Address = new(new Guid("2bd67d8b-8beb-48d5-87e0-6cda3428040a"), 1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct DevPropKey
    {
        public Guid FormatId;
        public uint Pid;

        public DevPropKey(Guid formatId, uint pid)
        {
            FormatId = formatId;
            Pid = pid;
        }
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_Device_ID_List_SizeW")]
    internal static extern uint GetDeviceIdListSize(out uint length, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_Device_ID_ListW")]
    internal static extern uint GetDeviceIdList(string? filter, char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Locate_DevNodeW")]
    internal static extern uint LocateDevNode(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, EntryPoint = "CM_Get_DevNode_PropertyW")]
    internal static extern uint GetDevNodeProperty(
        uint devInst,
        ref DevPropKey key,
        out uint propertyType,
        byte[]? buffer,
        ref uint bufferSize,
        uint flags);
}
