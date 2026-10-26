using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Dữ liệu sự kiện khi có mục tiêu đến giờ hẹn nhắc nhở
/// </summary>
public class GoalReminderEventArgs : EventArgs
{
    public Goal Goal { get; }

    public GoalReminderEventArgs(Goal goal)
    {
        Goal = goal;
    }
}

/// <summary>
/// Giao diện dịch vụ quản lý hẹn giờ và kích hoạt thông báo nhắc nhở khi đến giờ
/// Toàn bộ logic phụ thuộc thời gian đều được trừu tượng hóa qua IClock để phục vụ Unit Test
/// </summary>
public interface IReminderService
{
    /// <summary>
    /// Sự kiện phát ra khi có mục tiêu đến hạn cần hiển thị cửa sổ nhắc nhở
    /// </summary>
    event EventHandler<GoalReminderEventArgs>? GoalReminderDue;

    /// <summary>
    /// Quét và kiểm tra danh sách mục tiêu đến hạn tại thời điểm hiện tại.
    /// Phương thức async có thể được gọi trực tiếp trong Unit Test mà không cần đợi Timer.
    /// </summary>
    Task<IReadOnlyList<Goal>> CheckRemindersAsync();

    /// <summary>
    /// Hoãn nhắc lại sau một khoảng thời gian (Snooze)
    /// </summary>
    Task SnoozeGoalAsync(Guid goalId, TimeSpan duration);

    /// <summary>
    /// Đóng/Bỏ qua lời nhắc hiện tại của mục tiêu (Dismiss)
    /// </summary>
    Task DismissGoalAsync(Guid goalId);

    /// <summary>
    /// Đánh dấu hoàn thành mục tiêu trực tiếp từ cửa sổ nhắc nhở
    /// </summary>
    Task CompleteGoalFromReminderAsync(Guid goalId);

    /// <summary>
    /// Bắt đầu bộ định thời ngầm quét định kỳ
    /// </summary>
    void Start();

    /// <summary>
    /// Dừng bộ định thời ngầm
    /// </summary>
    void Stop();
}
