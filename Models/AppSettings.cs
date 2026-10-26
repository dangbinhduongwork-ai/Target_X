namespace GoalOverlay.Models;

/// <summary>
/// Cấu hình người dùng cho ứng dụng GoalOverlay
/// Lưu trữ tại %APPDATA%\GoalOverlay\settings.json
/// </summary>
public class AppSettings
{
    public bool AutoStartWithWindows { get; set; } = false;
    public double OverlayOpacity { get; set; } = 0.95;
    public bool AlwaysOnTop { get; set; } = true;
    public double WindowTop { get; set; } = 40.0;
    public double WindowLeft { get; set; } = -1.0; // -1 biểu thị vị trí mặc định (bám sát mép phải màn hình)
    public bool IsPositionLocked { get; set; } = true;
    public bool IsOverlayVisible { get; set; } = true;
}
