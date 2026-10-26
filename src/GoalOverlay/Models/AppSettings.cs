namespace GoalOverlay.Models;

/// <summary>
/// Cấu hình người dùng cho ứng dụng GoalOverlay
/// Lưu trữ tại %APPDATA%\GoalOverlay\settings.json
/// </summary>
public class AppSettings
{
    public double Opacity { get; set; } = 0.95;

    public double FontSize { get; set; } = 13.0;

    public string TextColor { get; set; } = "#F8FAFC";

    public ScreenCorner Corner { get; set; } = ScreenCorner.TopRight;

    public int MonitorIndex { get; set; } = 0;

    public bool ClickThrough { get; set; } = false;

    public bool HotkeyEnabled { get; set; } = true;

    public bool AutostartEnabled { get; set; } = false;

    public bool CarryOverUnfinished { get; set; } = true;

    // Các thuộc tính phụ trợ lưu vị trí tọa độ khi kéo thả overlay
    public double WindowTop { get; set; } = 40.0;
    public double WindowLeft { get; set; } = -1.0;
    public bool IsPositionLocked { get; set; } = true;

    public double OverlayOpacity
    {
        get => Opacity;
        set => Opacity = value;
    }
}
