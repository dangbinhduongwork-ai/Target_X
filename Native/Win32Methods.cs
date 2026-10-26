using System;
using System.Runtime.InteropServices;

namespace GoalOverlay.Native;

/// <summary>
/// Chứa các hàm P/Invoke gọi Win32 API cấp hệ thống từ user32.dll
/// Hỗ trợ cả kiến trúc 32-bit (x86) và 64-bit (x64)
/// </summary>
public static class Win32Methods
{
    // Cầu nối an toàn: trong môi trường 64-bit (IntPtr.Size == 8) phải dùng SetWindowLongPtr/GetWindowLongPtr
    // để tránh lỗi tràn con trỏ hoặc làm crash ứng dụng khi gán cờ Window Style.

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

    /// <summary>
    /// Lấy giá trị Window Extended Style an toàn cho cả 32-bit và 64-bit
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
    /// Thiết lập giá trị Window Extended Style an toàn cho cả 32-bit và 64-bit
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
    /// Thêm cờ ToolWindow vào cửa sổ để ẩn khỏi Alt+Tab và thanh Taskbar
    /// </summary>
    public static void MakeToolWindow(IntPtr hWnd)
    {
        IntPtr exStyle = GetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE);
        long newExStyle = exStyle.ToInt64() | Win32Constants.WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hWnd, Win32Constants.GWL_EXSTYLE, new IntPtr(newExStyle));
    }

    /// <summary>
    /// Đảm bảo cửa sổ được ghim ở mức Z-Order cao nhất (luôn nổi trên cùng mọi app khác)
    /// </summary>
    public static void ReinforceTopmost(IntPtr hWnd)
    {
        SetWindowPos(
            hWnd,
            Win32Constants.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Constants.SWP_NOMOVE | Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOACTIVATE | Win32Constants.SWP_SHOWWINDOW);
    }
}
