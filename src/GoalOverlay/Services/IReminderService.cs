using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Dữ liệu sự kiện khi có một mục tiêu đơn lẻ đến giờ hẹn nhắc nhở (tương thích ngược)
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
/// Dữ liệu sự kiện khi có một hoặc nhiều mục tiêu đến hạn trong cùng một thời điểm (Gom nhóm)
/// </summary>
public class GoalsReminderBatchEventArgs : EventArgs
{
    public IReadOnlyList<Goal> Goals { get; }

    public GoalsReminderBatchEventArgs(IReadOnlyList<Goal> goals)
    {
        Goals = goals;
    }
}

/// <summary>
/// Giao diện dịch vụ quản lý hẹn giờ và kích hoạt thông báo nhắc nhở khi đến giờ (ReminderScheduler)
/// Toàn bộ logic phụ thuộc thời gian đều được trừu tượng hóa qua IClock để phục vụ Unit Test
/// </summary>
public interface IReminderService
{
    /// <summary>
    /// Sự kiện phát ra khi có mục tiêu đơn lẻ đến hạn (tương thích ngược)
    /// </summary>
    event EventHandler<GoalReminderEventArgs>? GoalReminderDue;

    /// <summary>
    /// Sự kiện gom nhóm phát ra khi có một hoặc nhiều mục tiêu đến hạn cần hiển thị trên ReminderWindow
    /// </summary>
    event EventHandler<GoalsReminderBatchEventArgs>? GoalsReminderBatchDue;

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
    /// Kích hoạt kiểm tra ngay lập tức (không chờ hết chu kỳ timer 10s)
    /// </summary>
    void TriggerCheck();

    /// <summary>
    /// Bắt đầu bộ định thời ngầm quét định kỳ (mỗi 10s)
    /// </summary>
    void Start();

    /// <summary>
    /// Dừng bộ định thời ngầm
    /// </summary>
    void Stop();
}
