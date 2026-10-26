using System;
using System.Collections.Generic;
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

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

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

    /// <summary>
    /// Lấy vùng làm việc (WorkArea) của màn hình được chỉ định theo monitorIndex,
    /// quy đổi từ Physical Pixels của Win32 sang Device-Independent Pixels (DIPs) của WPF
    /// </summary>
    public static Rect GetMonitorWorkArea(int monitorIndex, Visual visual)
    {
        var monitors = new List<MONITORINFO>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hMon, ref mi))
            {
                monitors.Add(mi);
            }
            return true;
        }, IntPtr.Zero);

        DpiScale dpi = VisualTreeHelper.GetDpi(visual);
        double dpiX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        double dpiY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        if (monitors.Count > 0)
        {
            int index = Math.Clamp(monitorIndex, 0, monitors.Count - 1);
            var selected = monitors[index];

            // Quy đổi tọa độ vật lý sang đơn vị DIPs của WPF
            double left = selected.rcWork.Left / dpiX;
            double top = selected.rcWork.Top / dpiY;
            double width = (selected.rcWork.Right - selected.rcWork.Left) / dpiX;
            double height = (selected.rcWork.Bottom - selected.rcWork.Top) / dpiY;

            return new Rect(left, top, width, height);
        }

        // Mặc định fallback về SystemParameters.WorkArea của màn hình chính
        return SystemParameters.WorkArea;
    }
}
