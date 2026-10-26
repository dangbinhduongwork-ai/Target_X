using System;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ phím tắt toàn cục hệ thống (Global Hotkey)
/// </summary>
public interface IHotkeyService
{
    bool IsRegistered { get; }
    string StatusMessage { get; }
    bool Register(IntPtr hWnd, Action callback);
    void Unregister(IntPtr hWnd);
}
