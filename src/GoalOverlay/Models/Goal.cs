using System;

namespace GoalOverlay.Models;

/// <summary>
/// Đại diện cho một mục tiêu theo dõi (hàng ngày hoặc hàng tháng)
/// </summary>
public class Goal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Text { get; set; } = string.Empty;

    public GoalType Type { get; set; } = GoalType.Daily;

    /// <summary>
    /// Ngày của mục tiêu (dùng cho mục tiêu hàng ngày GoalType.Daily)
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Tháng của mục tiêu theo định dạng "yyyy-MM" (dùng cho mục tiêu hàng tháng GoalType.Monthly)
    /// </summary>
    public string Month { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái đã hoàn thành hay chưa
    /// </summary>
    public bool IsDone { get; set; } = false;

    /// <summary>
    /// Thời điểm khởi tạo mục tiêu
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
