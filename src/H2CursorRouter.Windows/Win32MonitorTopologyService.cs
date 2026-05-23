using System.Runtime.InteropServices;
using System.Text;
using H2CursorRouter.Core.Geometry;

namespace H2CursorRouter.Windows;

public sealed class Win32MonitorTopologyService : IMonitorTopologyService
{
    private System.Threading.Timer? _timer;
    private string? _lastSignature;

    public event EventHandler? TopologyChanged;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var displaySettingsMonitors = GetMonitorsFromDisplaySettings();
        return displaySettingsMonitors.Count > 0
            ? displaySettingsMonitors
            : GetMonitorsFromMonitorHandles();
    }

    private static IReadOnlyList<MonitorInfo> GetMonitorsFromDisplaySettings()
    {
        var monitors = new List<MonitorInfo>();
        for (uint index = 0; ; index++)
        {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, index, ref device, 0))
            {
                break;
            }

            if ((device.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) == 0 ||
                (device.StateFlags & DisplayDeviceStateFlags.MirroringDriver) != 0 ||
                string.IsNullOrWhiteSpace(device.DeviceName))
            {
                continue;
            }

            var mode = new DevMode { Size = (ushort)Marshal.SizeOf<DevMode>() };
            if (!EnumDisplaySettingsEx(device.DeviceName, EnumCurrentSettings, ref mode, 0) ||
                mode.PelsWidth == 0 ||
                mode.PelsHeight == 0)
            {
                continue;
            }

            monitors.Add(new MonitorInfo(
                device.DeviceName.TrimEnd('\0'),
                new IntRect(
                    mode.PositionX,
                    mode.PositionY,
                    mode.PositionX + (int)mode.PelsWidth,
                    mode.PositionY + (int)mode.PelsHeight),
                (device.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0));
        }

        return monitors;
    }

    private static IReadOnlyList<MonitorInfo> GetMonitorsFromMonitorHandles()
    {
        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx();
            info.Size = Marshal.SizeOf<MonitorInfoEx>();
            if (GetMonitorInfo(monitor, ref info))
            {
                monitors.Add(new MonitorInfo(
                    (info.DeviceName ?? string.Empty).TrimEnd('\0'),
                    new IntRect(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom),
                    (info.Flags & 1) == 1));
            }

            return true;
        }, IntPtr.Zero);
        return monitors;
    }

    public string GetTopologySignature()
    {
        var builder = new StringBuilder();
        foreach (var monitor in GetMonitors().OrderBy(monitor => monitor.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(monitor.DeviceName)
                .Append(':')
                .Append(monitor.Bounds.Left)
                .Append(',')
                .Append(monitor.Bounds.Top)
                .Append(',')
                .Append(monitor.Bounds.Right)
                .Append(',')
                .Append(monitor.Bounds.Bottom)
                .Append('|');
        }

        return builder.ToString();
    }

    public void StartWatching(TimeSpan interval)
    {
        _lastSignature = GetTopologySignature();
        _timer?.Dispose();
        _timer = new System.Threading.Timer(_ =>
        {
            var current = GetTopologySignature();
            if (!string.Equals(current, _lastSignature, StringComparison.Ordinal))
            {
                _lastSignature = current;
                TopologyChanged?.Invoke(this, EventArgs.Empty);
            }
        }, null, interval, interval);
    }

    public void Dispose() => _timer?.Dispose();

    private delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);

    private const int EnumCurrentSettings = -1;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string? deviceName, uint deviceNumber, ref DisplayDevice displayDevice, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettingsEx(string deviceName, int modeNumber, ref DevMode devMode, uint flags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clipRect, MonitorEnumProc callback, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [Flags]
    private enum DisplayDeviceStateFlags : uint
    {
        AttachedToDesktop = 0x00000001,
        PrimaryDevice = 0x00000004,
        MirroringDriver = 0x00000008
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public DisplayDeviceStateFlags StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FormName;

        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
