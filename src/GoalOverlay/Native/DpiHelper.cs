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
    /// Lấy thông số tỉ lệ DPI hiện tại của Visual/Window
    /// </summary>
    public static DpiScale GetDpi(Visual visual)
    {
        return VisualTreeHelper.GetDpi(visual);
    }

    /// <summary>
    /// Tính toán tọa độ (Left, Top) để ghim cửa sổ vào góc trên bên phải của màn hình làm việc (WorkArea)
    /// </summary>
    public static (double Left, double Top) CalculateTopRightPosition(double windowWidth, double marginX = 20, double marginY = 20)
    {
        Rect workArea = SystemParameters.WorkArea;

        double left = workArea.Right - windowWidth - marginX;
        double top = workArea.Top + marginY;

        left = Math.Max(workArea.Left, left);
        top = Math.Max(workArea.Top, top);

        return (left, top);
    }
}
