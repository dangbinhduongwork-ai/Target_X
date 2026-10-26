using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace GoalOverlay.Native;

/// <summary>
/// Các hàm P/Invoke Win32 API cấp hệ thống phục vụ cấu hình cửa sổ Overlay HUD:
/// ToolWindow, NoActivate (không cướp focus), Click-Through và quản lý Z-order Topmost
/// </summary>
public static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    // Cờ WS_EX_TOOLWINDOW: Biến cửa sổ thành ToolWindow (ẩn khỏi Alt+Tab và Taskbar)
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    // Cờ WS_EX_NOACTIVATE: Ngăn Windows kích hoạt focus khi người dùng click vào cửa sổ
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // Cờ WS_EX_TRANSPARENT: Cho phép các sự kiện chuột xuyên qua cửa sổ (Click-Through)
    public const int WS_EX_TRANSPARENT = 0x00000020;

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // Các hằng số phím tắt toàn cục
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint VK_G = 0x47; // Phím G: Ẩn/Hiện Overlay
    public const uint VK_M = 0x4D; // Phím M: Mở cửa sổ Quản lý mục tiêu
    public const int WM_HOTKEY = 0x0312;
    public const int OVERLAY_HOTKEY_ID = 9001;
    public const int MANAGEMENT_HOTKEY_ID = 9002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    public const uint FLASHW_STOP = 0;
    public const uint FLASHW_CAPTION = 1;
    public const uint FLASHW_TRAY = 2;
    public const uint FLASHW_ALL = 3;
    public const uint FLASHW_TIMERNOFG = 12;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    // Cấu trúc RECT của Win32
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    /// <summary>
    /// Lấy Extended Window Style an toàn cho cả 32-bit và 64-bit
    /// </summary>
    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
        {
            return GetWindowLongPtr64(hWnd, nIndex);
        }
        return new IntPtr(GetWindowLong32(hWnd, nIndex));
    }

    /// <summary>
    /// Thiết lập Extended Window Style an toàn cho cả 32-bit và 64-bit
    /// </summary>
    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        }
        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    /// <summary>
    /// Áp dụng cờ ToolWindow (ẩn Alt+Tab) và NoActivate (không cướp focus bàn phím)
    /// </summary>
    public static void SetToolWindowAndNoActivate(IntPtr hWnd)
    {
        IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        long newExStyle = exStyle.ToInt64() | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(newExStyle));
    }

    /// <summary>
    /// Áp dụng cờ WS_EX_NOACTIVATE (không cướp focus bàn phím) cho cửa sổ thông báo
    /// </summary>
    public static void SetNoActivateOnly(IntPtr hWnd)
    {
        IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        long newExStyle = exStyle.ToInt64() | WS_EX_NOACTIVATE;
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(newExStyle));
    }

    /// <summary>
    /// Nhấp nháy icon trên Taskbar để báo hiệu người dùng có lời nhắc nhở mới
    /// </summary>
    public static void FlashWindow(IntPtr hWnd)
    {
        try
        {
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = hWnd,
                dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };
            FlashWindowEx(ref info);
        }
        catch { }
    }

    /// <summary>
    /// Bật hoặc tắt chế độ Click-Through (cho chuột click xuyên qua cửa sổ)
    /// </summary>
    public static void SetClickThrough(IntPtr hWnd, bool enable)
    {
        IntPtr exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        long current = exStyle.ToInt64();

        long newExStyle = enable
            ? current | WS_EX_TRANSPARENT
            : current & ~WS_EX_TRANSPARENT;

        SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(newExStyle));
    }

    /// <summary>
    /// Gia cố cửa sổ luôn nằm trên cùng Z-Order mà không chiếm kích hoạt focus
    /// </summary>
    public static void SetTopmost(IntPtr hWnd)
    {
        SetWindowPos(
            hWnd,
            HWND_TOPMOST,
            0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    public class DisplayMonitorInfo
    {
        public IntPtr Handle { get; set; }
        public MONITORINFO Info { get; set; }
        public bool IsPrimary => (Info.dwFlags & 1) != 0;
        public double DpiScaleX { get; set; } = 1.0;
        public double DpiScaleY { get; set; } = 1.0;
    }

    /// <summary>
    /// Lấy danh sách các màn hình được sắp xếp: Màn hình chính luôn ở vị trí 0, các màn hình phụ sắp theo tọa độ X tăng dần
    /// </summary>
    public static List<DisplayMonitorInfo> GetOrderedMonitors(Visual? visual = null)
    {
        var monitors = new List<DisplayMonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hMon, ref mi))
            {
                double scaleX = 1.0;
                double scaleY = 1.0;

                try
                {
                    if (GetDpiForMonitor(hMon, 0, out uint dpiX, out uint dpiY) == 0 && dpiX > 0 && dpiY > 0)
                    {
                        scaleX = dpiX / 96.0;
                        scaleY = dpiY / 96.0;
                    }
                    else if (visual != null)
                    {
                        DpiScale dpi = VisualTreeHelper.GetDpi(visual);
                        scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                        scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                    }
                }
                catch
                {
                    if (visual != null)
                    {
                        DpiScale dpi = VisualTreeHelper.GetDpi(visual);
                        scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                        scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                    }
                }

                monitors.Add(new DisplayMonitorInfo
                {
                    Handle = hMon,
                    Info = mi,
                    DpiScaleX = scaleX,
                    DpiScaleY = scaleY
                });
            }
            return true;
        }, IntPtr.Zero);

        // Màn hình chính luôn ở vị trí 0, các màn hình phụ được sắp xếp từ trái sang phải theo tọa độ X
        return monitors
            .OrderByDescending(m => m.IsPrimary)
            .ThenBy(m => m.Info.rcMonitor.Left)
            .ToList();
    }

    /// <summary>
    /// Lấy danh sách tên hiển thị thân thiện cho ComboBox chọn màn hình
    /// </summary>
    public static List<string> GetMonitorDisplayNames()
    {
        var monitors = GetOrderedMonitors();
        var list = new List<string>();

        for (int i = 0; i < monitors.Count; i++)
        {
            if (i == 0)
            {
                list.Add("Màn hình 1 (Chính)");
            }
            else
            {
                list.Add($"Màn hình {i + 1}");
            }
        }

        if (list.Count == 0)
        {
            list.Add("Màn hình 1 (Chính)");
        }

        return list;
    }

    /// <summary>
    /// Lấy vùng làm việc (WorkArea) của màn hình được chỉ định theo monitorIndex,
    /// quy đổi từ Physical Pixels của Win32 sang Device-Independent Pixels (DIPs) của WPF
    /// </summary>
    public static Rect GetMonitorWorkArea(int monitorIndex, Visual visual)
    {
        var monitors = GetOrderedMonitors(visual);

        if (monitors.Count > 0)
        {
            int index = Math.Clamp(monitorIndex, 0, monitors.Count - 1);
            var selected = monitors[index];

            return OverlayPositionCalculator.ConvertPhysicalWorkAreaToDips(
                selected.Info.rcWork.Left,
                selected.Info.rcWork.Top,
                selected.Info.rcWork.Right,
                selected.Info.rcWork.Bottom,
                selected.DpiScaleX,
                selected.DpiScaleY);
        }

        // Mặc định fallback về SystemParameters.WorkArea của màn hình chính
        return SystemParameters.WorkArea;
    }
}
