using System;
using System.Windows.Interop;
using GoalOverlay.Native;

namespace GoalOverlay.Services;

/// <summary>
/// Quản lý phím tắt toàn cục hệ thống:
/// - Ctrl + Alt + G: Bật/tắt hiển thị Overlay HUD
/// - Ctrl + Alt + M: Mở ngay cửa sổ Quản lý mục tiêu
/// Bắt lỗi trùng phím an toàn, ghi log và thông báo nhẹ, không làm sập ứng dụng
/// </summary>
public class HotkeyService : IHotkeyService
{
    private readonly ILoggerService _logger;
    private Action? _toggleOverlayCallback;
    private Action? _openManagementCallback;
    private HwndSource? _hwndSource;

    public bool IsRegistered { get; private set; }
    public string StatusMessage { get; private set; } = "Chưa đăng ký";

    public HotkeyService(ILoggerService logger)
    {
        _logger = logger;
    }

    public bool Register(IntPtr hWnd, Action callback)
    {
        return Register(hWnd, callback, null);
    }

    public bool Register(IntPtr hWnd, Action toggleOverlayCallback, Action? openManagementCallback)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.Warn("Handle cửa sổ không hợp lệ khi đăng ký phím tắt.");
            StatusMessage = "Lỗi: Handle không hợp lệ";
            return false;
        }

        _toggleOverlayCallback = toggleOverlayCallback;
        _openManagementCallback = openManagementCallback;

        try
        {
            uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;

            // 1. Đăng ký phím tắt Ctrl + Alt + G (Ẩn/Hiện Overlay)
            bool gSuccess = NativeMethods.RegisterHotKey(hWnd, NativeMethods.OVERLAY_HOTKEY_ID, modifiers, NativeMethods.VK_G);

            // 2. Đăng ký phím tắt Ctrl + Alt + M (Mở Quản lý mục tiêu) nếu có callback
            bool mSuccess = false;
            if (_openManagementCallback != null)
            {
                mSuccess = NativeMethods.RegisterHotKey(hWnd, NativeMethods.MANAGEMENT_HOTKEY_ID, modifiers, NativeMethods.VK_M);
            }

            if (gSuccess || mSuccess)
            {
                IsRegistered = true;
                _hwndSource = HwndSource.FromHwnd(hWnd);
                _hwndSource?.AddHook(HwndHook);

                if (gSuccess && mSuccess)
                {
                    StatusMessage = "Đang kích hoạt: Ctrl + Alt + G (Overlay) | Ctrl + Alt + M (Quản lý)";
                    _logger.Info("Đã đăng ký thành công cả 2 phím tắt: Ctrl + Alt + G và Ctrl + Alt + M.");
                }
                else if (gSuccess)
                {
                    StatusMessage = "Đang kích hoạt: Ctrl + Alt + G (Overlay)";
                    _logger.Info("Đã đăng ký thành công phím tắt Ctrl + Alt + G.");
                }
                else
                {
                    StatusMessage = "Đang kích hoạt: Ctrl + Alt + M (Quản lý)";
                    _logger.Info("Đã đăng ký thành công phím tắt Ctrl + Alt + M.");
                }

                return true;
            }
            else
            {
                IsRegistered = false;
                StatusMessage = "Không thể đăng ký phím tắt (bị trùng với ứng dụng khác)";
                _logger.Warn("Không thể đăng ký phím tắt toàn cục. Có thể phím đã bị phần mềm khác chiếm giữ.");
                return false;
            }
        }
        catch (Exception ex)
        {
            IsRegistered = false;
            StatusMessage = "Lỗi khi đăng ký phím tắt";
            _logger.Error("Ngoại lệ khi đăng ký phím tắt toàn cục", ex);
            return false;
        }
    }

    public void Unregister(IntPtr hWnd)
    {
        try
        {
            if (IsRegistered && hWnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(hWnd, NativeMethods.OVERLAY_HOTKEY_ID);
                NativeMethods.UnregisterHotKey(hWnd, NativeMethods.MANAGEMENT_HOTKEY_ID);

                _hwndSource?.RemoveHook(HwndHook);
                IsRegistered = false;
                StatusMessage = "Đã hủy đăng ký";
                _logger.Info("Đã gỡ các phím tắt toàn cục.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi hủy đăng ký phím tắt", ex);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int hotkeyId = wParam.ToInt32();
            if (hotkeyId == NativeMethods.OVERLAY_HOTKEY_ID)
            {
                _toggleOverlayCallback?.Invoke();
                handled = true;
            }
            else if (hotkeyId == NativeMethods.MANAGEMENT_HOTKEY_ID)
            {
                _openManagementCallback?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }
}
