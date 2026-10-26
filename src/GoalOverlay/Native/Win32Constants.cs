using System;

namespace GoalOverlay.Native;

/// <summary>
/// Chứa các hằng số Win32 API phục vụ việc cấu hình cửa sổ Overlay nổi trên cùng và ẩn khỏi Taskbar / Alt+Tab
/// </summary>
public static class Win32Constants
{
    // Chỉ số thuộc tính Extended Window Style trong hàm GetWindowLong/SetWindowLong
    public const int GWL_EXSTYLE = -20;

    // Cờ WS_EX_TOOLWINDOW: Biến cửa sổ thành ToolWindow dạng bảng công cụ nổi
    // Ý nghĩa: Ẩn hoàn toàn cửa sổ khỏi thanh Taskbar và danh sách chuyển ứng dụng (Alt + Tab)
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    // Cờ WS_EX_NOACTIVATE: Ngăn Windows tự động kích hoạt (focus) cửa sổ khi người dùng click vào
    // Giúp người dùng thao tác bấm tăng/giảm mục tiêu mà không bị cướp focus khỏi phần mềm đang làm việc/chơi game
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // Cờ WS_EX_TOPMOST: Đảm bảo cửa sổ luôn nằm trên tất cả các cửa sổ khác
    public const int WS_EX_TOPMOST = 0x00000008;

    // Hằng số đại diện vị trí đứng đầu trong thứ tự Z-Order (luôn hiển thị trên cùng)
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    // Các cờ SetWindowPosFlags: giữ nguyên kích thước, vị trí, không kích hoạt focus
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
}
