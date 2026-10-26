using System.Collections.Generic;

namespace GoalOverlay.Models;

/// <summary>
/// Tài liệu bọc danh sách mục tiêu kèm số phiên bản lược đồ (SchemaVersion)
/// Hỗ trợ kiểm soát phiên bản schema của file goals.json từ V1.1 trở đi
/// </summary>
public class GoalsDocument
{
    /// <summary>
    /// Số phiên bản lược đồ JSON: V1.0 không có trường này (flat array), V1.1 = 2
    /// </summary>
    public int SchemaVersion { get; set; } = 2;

    /// <summary>
    /// Danh sách toàn bộ mục tiêu
    /// </summary>
    public List<Goal> Goals { get; set; } = new();

    /// <summary>
    /// Danh sách toàn bộ mục tiêu định kỳ lặp lại theo thứ trong tuần
    /// </summary>
    public List<RecurringGoal> RecurringGoals { get; set; } = new();
}
