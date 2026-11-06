using System.Runtime.InteropServices;

namespace BTWidget.Native;

internal static class BluetoothApi
{
    internal const int ErrorNoMoreItems = 259;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DeviceInfo
    {
        public int Size;
        public ulong Address;
        public uint ClassOfDevice;
        public bool Connected;
        public bool Remembered;
        public bool Authenticated;
        public SystemTime LastSeen;
        public SystemTime LastUsed;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SearchParams
    {
        public int Size;
        public bool ReturnAuthenticated;
        public bool ReturnRemembered;
        public bool ReturnUnknown;
        public bool ReturnConnected;
        public bool IssueInquiry;
        public byte TimeoutMultiplier;
        public IntPtr Radio;
    }

    [DllImport("BluetoothApis.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr BluetoothFindFirstDevice(ref SearchParams search, ref DeviceInfo device);

    [DllImport("BluetoothApis.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool BluetoothFindNextDevice(IntPtr find, ref DeviceInfo device);

    [DllImport("BluetoothApis.dll", SetLastError = true)]
    internal static extern bool BluetoothFindDeviceClose(IntPtr find);
}
