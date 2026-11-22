using System;
using System.Collections.Generic;
using System.Linq;

namespace ClaudeLauncher;

public sealed record MonitorInfo(string Id, int Number, string Name, Native.RECT Frame, Native.RECT Work, double Scale, bool IsMain);

// Monitors and window placement with Win32. The process is per-monitor DPI aware,
// so every rectangle here is in physical pixels of the virtual desktop.
public static class Arranger
{
    public static List<MonitorInfo> Monitors()
    {
        var raw = new List<(IntPtr h, Native.MONITORINFOEX info)>();
        Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr h, IntPtr hdc, ref Native.RECT r, IntPtr d) =>
        {
            var mi = new Native.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFOEX>() };
            if (Native.GetMonitorInfo(h, ref mi)) raw.Add((h, mi));
            return true;
        }, IntPtr.Zero);

        var sorted = raw
            .OrderByDescending(m => (m.info.dwFlags & 1) != 0) // primary first
            .ThenBy(m => m.info.rcMonitor.Left)
            .ToList();

        var list = new List<MonitorInfo>();
        for (int i = 0; i < sorted.Count; i++)
        {
            var (h, mi) = sorted[i];
            double scale = 1;
            if (Native.GetDpiForMonitor(h, 0, out uint dx, out _) == 0) scale = dx / 96.0;
            list.Add(new MonitorInfo(mi.szDevice, i + 1, FriendlyName(mi.szDevice), mi.rcMonitor, mi.rcWork, scale, (mi.dwFlags & 1) != 0));
        }
        return list;
    }

    private static string FriendlyName(string device)
    {
        try
        {
            var dd = new Native.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<Native.DISPLAY_DEVICE>() };
            if (Native.EnumDisplayDevices(device, 0, ref dd, 0) && !string.IsNullOrWhiteSpace(dd.DeviceString))
                return dd.DeviceString;
        }
        catch { }
        return device.Replace(@"\\.\", "");
    }

    // Largest visible, captioned top-level window of the process (minimized windows count).
    public static IntPtr MainWindow(int pid)
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = -1;
        Native.EnumWindows((hwnd, _) =>
        {
            Native.GetWindowThreadProcessId(hwnd, out int wpid);
            if (wpid != pid || !Native.IsWindowVisible(hwnd)) return true;
            if (Native.GetWindow(hwnd, Native.GW_OWNER) != IntPtr.Zero) return true;
            long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE).ToInt64();
            if ((ex & Native.WS_EX_TOOLWINDOW) != 0) return true;
            if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (Native.GetWindowTextLength(hwnd) == 0) return true;
            Native.GetWindowRect(hwnd, out var r);
            long area = (long)r.Width * r.Height;
            if (area > bestArea) { bestArea = area; best = hwnd; }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    public static void Activate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        Native.SetForegroundWindow(hwnd);
    }

    // Place the visible frame exactly on rect: Windows 10/11 add invisible resize borders
    // around most windows, so the outer rect is grown by the difference.
    public static void Place(IntPtr hwnd, Native.RECT rect)
    {
        if (Native.IsIconic(hwnd) || Native.IsZoomed(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        int l = 0, t = 0, r = 0, b = 0;
        if (Native.GetWindowRect(hwnd, out var outer) &&
            Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out Native.RECT frame, System.Runtime.InteropServices.Marshal.SizeOf<Native.RECT>()) == 0)
        {
            l = frame.Left - outer.Left; t = frame.Top - outer.Top;
            r = outer.Right - frame.Right; b = outer.Bottom - frame.Bottom;
        }
        uint flags = Native.SWP_NOZORDER | Native.SWP_NOACTIVATE;
        int x = rect.Left - l, y = rect.Top - t, w = rect.Width + l + r, h = rect.Height + t + b;
        // Twice: moving to a monitor with another DPI rescales the window on the first call.
        Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, flags);
        Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, flags);
    }

    // Window i goes to zone i; extra windows wrap around to the first zones.
    public static void Arrange(IList<IntPtr> windows, MonitorInfo monitor, IList<ZRect> zones, bool gap)
    {
        if (windows.Count == 0 || zones.Count == 0) return;
        double g = gap ? Math.Round(8 * monitor.Scale) : 0;
        var v = monitor.Work;
        double ax = v.Left + g / 2, ay = v.Top + g / 2, aw = v.Width - g, ah = v.Height - g;
        for (int i = 0; i < windows.Count; i++)
        {
            var z = zones[i % zones.Count];
            double x1 = Math.Round(ax + z.X * aw), x2 = Math.Round(ax + z.Right * aw);
            double y1 = Math.Round(ay + z.Y * ah), y2 = Math.Round(ay + z.Bottom * ah);
            var rect = new Native.RECT
            {
                Left = (int)(x1 + g / 2), Top = (int)(y1 + g / 2),
                Right = (int)(x2 - g / 2), Bottom = (int)(y2 - g / 2),
            };
            Place(windows[i], rect);
        }
    }
}
