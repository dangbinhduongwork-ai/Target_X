using System;
using System.Windows.Interop;
using GoalOverlay.Native;

namespace GoalOverlay.Services;

/// <summary>
/// Quản lý phím tắt toàn cục Ctrl + Alt + G để bật/tắt hiển thị Overlay HUD
/// Bắt lỗi trùng phím an toàn, ghi log và thông báo nhẹ, không làm sập ứng dụng
/// </summary>
public class HotkeyService : IHotkeyService
{
    private readonly ILoggerService _logger;
    private Action? _callback;
    private HwndSource? _hwndSource;

    public bool IsRegistered { get; private set; }
    public string StatusMessage { get; private set; } = "Chưa đăng ký";

    public HotkeyService(ILoggerService logger)
    {
        _logger = logger;
    }

    public bool Register(IntPtr hWnd, Action callback)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.Warn("Handle cửa sổ không hợp lệ khi đăng ký phím tắt.");
            StatusMessage = "Lỗi: Handle không hợp lệ";
            return false;
        }

        _callback = callback;

        try
        {
            uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
            uint vk = NativeMethods.VK_G;

            bool success = NativeMethods.RegisterHotKey(hWnd, NativeMethods.OVERLAY_HOTKEY_ID, modifiers, vk);

            if (success)
            {
                IsRegistered = true;
                StatusMessage = "Đang kích hoạt: Ctrl + Alt + G";

                _hwndSource = HwndSource.FromHwnd(hWnd);
                _hwndSource?.AddHook(HwndHook);

                _logger.Info("Đã đăng ký thành công phím tắt toàn cục Ctrl + Alt + G.");
                return true;
            }
            else
            {
                IsRegistered = false;
                StatusMessage = "Không thể đăng ký Ctrl + Alt + G (bị trùng với ứng dụng khác)";
                _logger.Warn("Không thể đăng ký phím tắt toàn cục Ctrl + Alt + G. Có thể phím này đã bị một phần mềm khác chiếm giữ.");
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
                _hwndSource?.RemoveHook(HwndHook);
                IsRegistered = false;
                StatusMessage = "Đã hủy đăng ký";
                _logger.Info("Đã gỡ phím tắt toàn cục Ctrl + Alt + G.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi hủy đăng ký phím tắt", ex);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.OVERLAY_HOTKEY_ID)
        {
            _callback?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
