namespace GoalOverlay.Services;

/// <summary>
/// Giao diện quản lý tự khởi động cùng Windows
/// </summary>
public interface IAutostartService
{
    bool IsEnabled();
    bool Enable();
    bool Disable();
    void VerifyAndUpdatePath();
}
