using System;
using System.Windows;
using System.Windows.Media;

namespace GoalOverlay.Native;

/// <summary>
/// Hỗ trợ tính toán tỉ lệ phóng to (DPI Scaling) và định vị cửa sổ chính xác trên màn hình Windows
/// </summary>
public static class DpiHelper
{
    /// <summary>
    /// Lấy thông số tỉ lệ DPI hiện tại của Visual/Window (ví dụ 100% = 1.0, 125% = 1.25, 150% = 1.5)
    /// WPF tự động xử lý đơn vị DIPs (Device Independent Pixels, 1 DIP = 1/96 inch)
    /// nhưng khi làm việc với Win32 bounds hoặc multi-monitor có DPI khác nhau cần DpiScale để quy đổi.
    /// </summary>
    public static DpiScale GetDpi(Visual visual)
    {
        return VisualTreeHelper.GetDpi(visual);
    }

    /// <summary>
    /// Tính toán tọa độ (Left, Top) để ghim cửa sổ vào góc trên bên phải của màn hình làm việc (WorkArea)
    /// WorkArea là vùng màn hình đã trừ đi thanh Taskbar của Windows, đảm bảo overlay không bị Taskbar che khuất.
    /// </summary>
    /// <param name="windowWidth">Chiều rộng cửa sổ theo đơn vị DIPs của WPF</param>
    /// <param name="marginX">Khoảng cách lề phải (DIPs), mặc định 20px</param>
    /// <param name="marginY">Khoảng cách lề trên (DIPs), mặc định 20px</param>
    /// <returns>Tọa độ (Left, Top) chuẩn xác cho thuộc tính của WPF Window</returns>
    public static (double Left, double Top) CalculateTopRightPosition(double windowWidth, double marginX = 20, double marginY = 20)
    {
        // SystemParameters.WorkArea trả về vùng làm việc theo đơn vị DIPs
        Rect workArea = SystemParameters.WorkArea;

        // Tọa độ X = Cạnh phải của vùng làm việc - chiều rộng cửa sổ - lề
        double left = workArea.Right - windowWidth - marginX;

        // Tọa độ Y = Cạnh trên của vùng làm việc + lề
        double top = workArea.Top + marginY;

        // Đảm bảo không bị âm nếu màn hình quá nhỏ
        left = Math.Max(workArea.Left, left);
        top = Math.Max(workArea.Top, top);

        return (left, top);
    }
}
