using System;
using System.Windows;
using GoalOverlay.Models;

namespace GoalOverlay.Native;

/// <summary>
/// Tính toán tọa độ hiển thị (Left, Top) cho cửa sổ Overlay HUD theo DPI, độ phân giải và góc màn hình.
/// Tách rời khỏi WPF Window để phục vụ Unit Test tự động cho mọi tỷ lệ DPI và độ phân giải màn hình.
/// </summary>
public static class OverlayPositionCalculator
{
    public const double DefaultMargin = 16.0;

    /// <summary>
    /// Tính toán tọa độ (Left, Top) theo đơn vị Device-Independent Pixels (DIPs) của WPF
    /// </summary>
    /// <param name="workArea">Vùng làm việc khả dụng của màn hình (DIPs)</param>
    /// <param name="windowWidth">Chiều rộng thực tế của cửa sổ Overlay (DIPs)</param>
    /// <param name="windowHeight">Chiều cao thực tế của cửa sổ Overlay (DIPs)</param>
    /// <param name="corner">Góc định vị (mặc định TopRight)</param>
    /// <param name="margin">Khoảng cách lề so với mép WorkArea (mặc định 16px)</param>
    /// <returns>Tọa độ Point (Left, Top)</returns>
    public static Point CalculatePosition(
        Rect workArea,
        double windowWidth,
        double windowHeight,
        ScreenCorner corner = ScreenCorner.TopRight,
        double margin = DefaultMargin)
    {
        double left;
        double top;

        switch (corner)
        {
            case ScreenCorner.TopLeft:
                left = workArea.Left + margin;
                top = workArea.Top + margin;
                break;

            case ScreenCorner.BottomLeft:
                left = workArea.Left + margin;
                top = workArea.Bottom - windowHeight - margin;
                break;

            case ScreenCorner.BottomRight:
                left = workArea.Right - windowWidth - margin;
                top = workArea.Bottom - windowHeight - margin;
                break;

            case ScreenCorner.TopRight:
            default:
                left = workArea.Right - windowWidth - margin;
                top = workArea.Top + margin;
                break;
        }

        // Đảm bảo cửa sổ không bị đẩy ra ngoài vùng hiển thị của WorkArea
        left = Math.Max(workArea.Left, left);
        top = Math.Max(workArea.Top, top);

        return new Point(left, top);
    }

    /// <summary>
    /// Quy đổi tọa độ và kích thước từ pixel vật lý (Physical Pixels) sang DIPs theo hệ số DPI
    /// </summary>
    /// <param name="physicalLeft">Tọa độ X bên trái (physical)</param>
    /// <param name="physicalTop">Tọa độ Y bên trên (physical)</param>
    /// <param name="physicalRight">Tọa độ X bên phải (physical)</param>
    /// <param name="physicalBottom">Tọa độ Y bên dưới (physical)</param>
    /// <param name="dpiScaleX">Hệ số scale X (1.0 = 100%, 1.25 = 125%, 1.5 = 150%, 2.0 = 200%)</param>
    /// <param name="dpiScaleY">Hệ số scale Y</param>
    /// <returns>Rect biểu diễn vùng làm việc bằng đơn vị DIPs</returns>
    public static Rect ConvertPhysicalWorkAreaToDips(
        int physicalLeft,
        int physicalTop,
        int physicalRight,
        int physicalBottom,
        double dpiScaleX,
        double dpiScaleY)
    {
        double scaleX = dpiScaleX > 0 ? dpiScaleX : 1.0;
        double scaleY = dpiScaleY > 0 ? dpiScaleY : 1.0;

        double left = physicalLeft / scaleX;
        double top = physicalTop / scaleY;
        double width = (physicalRight - physicalLeft) / scaleX;
        double height = (physicalBottom - physicalTop) / scaleY;

        return new Rect(left, top, width, height);
    }
}
