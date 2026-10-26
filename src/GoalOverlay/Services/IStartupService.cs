namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ đăng ký tự khởi động cùng Windows
/// </summary>
public interface IStartupService
{
    bool IsAutoStartEnabled();
    bool SetAutoStart(bool enable);
}
