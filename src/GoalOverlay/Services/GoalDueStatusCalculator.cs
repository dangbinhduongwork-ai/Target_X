using System;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Bộ tính toán trạng thái thời hạn và màu sắc cho mục tiêu
/// Đảm bảo logic đồng bộ 100% giữa MainWindow và Overlay HUD
/// </summary>
public static class GoalDueStatusCalculator
{
    public const string ColorOverdue = "#EF4444"; // Đỏ (Quá hạn)
    public const string ColorDueSoon = "#F59E0B"; // Vàng (Dưới 30 phút)
    public const string ColorDone = "#94A3B8";    // Xám mờ (Đã xong)
    public const string ColorNormal = "#60A5FA";  // Xanh chuẩn (Bình thường)

    /// <summary>
    /// Tính toán trạng thái thời hạn của một mục tiêu dựa trên thời điểm hiện tại
    /// </summary>
    public static GoalDueStatus CalculateStatus(Goal goal, DateTime now)
    {
        if (goal.IsDone)
        {
            return GoalDueStatus.Done;
        }

        if (!goal.DueAt.HasValue)
        {
            return GoalDueStatus.Normal;
        }

        DateTime dueAt = goal.DueAt.Value;
        if (now > dueAt)
        {
            return GoalDueStatus.Overdue;
        }

        if (dueAt - now <= TimeSpan.FromMinutes(30))
        {
            return GoalDueStatus.DueSoon;
        }

        return GoalDueStatus.Normal;
    }

    /// <summary>
    /// Lấy mã màu HEX hiển thị tương ứng với trạng thái
    /// </summary>
    public static string GetStatusColor(GoalDueStatus status, string defaultColor = ColorNormal)
    {
        return status switch
        {
            GoalDueStatus.Done => ColorDone,
            GoalDueStatus.Overdue => ColorOverdue,
            GoalDueStatus.DueSoon => ColorDueSoon,
            _ => defaultColor
        };
    }
}
