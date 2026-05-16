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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
