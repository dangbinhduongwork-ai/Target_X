using System;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ phím tắt toàn cục hệ thống (Global Hotkey)
/// Hỗ trợ đăng ký phím tắt ẩn/hiện Overlay HUD (Ctrl+Alt+G) và mở Quản lý mục tiêu (Ctrl+Alt+M)
/// </summary>
public interface IHotkeyService
{
    bool IsRegistered { get; }
    string StatusMessage { get; }

    /// <summary>
    /// Đăng ký phím tắt cơ bản (Ctrl + Alt + G)
    /// </summary>
    bool Register(IntPtr hWnd, Action callback);

    /// <summary>
    /// Đăng ký cả 2 phím tắt: Ctrl + Alt + G (HUD) và Ctrl + Alt + M (Quản lý mục tiêu)
    /// </summary>
    bool Register(IntPtr hWnd, Action toggleOverlayCallback, Action? openManagementCallback)
    {
        return Register(hWnd, toggleOverlayCallback);
    }

    void Unregister(IntPtr hWnd);
}
