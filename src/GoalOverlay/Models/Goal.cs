using System;
using System.Text.Json.Serialization;

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

    // =========================================================================
    // V1.1 Giai đoạn 8: Mở rộng Model Hẹn giờ & Nhắc nhở (Reminders)
    // Các trường đều có giá trị mặc định tương thích ngược 100% với file JSON V1.0
    // =========================================================================

    /// <summary>
    /// Thời hạn hoàn thành / Giờ hẹn của mục tiêu (giờ địa phương).
    /// Daily: ngày mục tiêu + giờ đã chọn. Monthly: ngày hạn (+ giờ tùy chọn).
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Mốc thời gian báo lại sau khi người dùng bấm Snooze (hoãn báo lại)
    /// </summary>
    public DateTime? SnoozedUntil { get; set; }

    /// <summary>
    /// Thời điểm thông báo nhắc nhở gần nhất đã được kích hoạt
    /// </summary>
    public DateTime? LastNotifiedAt { get; set; }

    /// <summary>
    /// Số lần đã thông báo nhắc nhở
    /// </summary>
    public int NotifyCount { get; set; } = 0;

    /// <summary>
    /// Người dùng đã đóng/bỏ qua lời nhắc cho lần đến giờ này hay chưa (tránh spam nhắc liên tục)
    /// </summary>
    public bool ReminderDismissed { get; set; } = false;

    // =========================================================================
    // Backward-compatibility aliases & helper properties
    // Sử dụng [JsonIgnore] để chỉ serialize các trường chuẩn V1.1
    // =========================================================================

    [JsonIgnore]
    public bool HasReminder
    {
        get => DueAt.HasValue;
        set
        {
            if (!value) DueAt = null;
        }
    }

    [JsonIgnore]
    public TimeOnly? ReminderTime
    {
        get => DueAt.HasValue ? TimeOnly.FromDateTime(DueAt.Value) : null;
        set
        {
            if (value.HasValue)
            {
                var d = Date != default ? Date.ToDateTime(value.Value) : DateTime.Today.Add(value.Value.ToTimeSpan());
                DueAt = d;
            }
            else
            {
                DueAt = null;
            }
        }
    }

    [JsonIgnore]
    public DateTime? SnoozeUntil
    {
        get => SnoozedUntil;
        set => SnoozedUntil = value;
    }

    [JsonIgnore]
    public DateTime? LastRemindedAt
    {
        get => LastNotifiedAt;
        set => LastNotifiedAt = value;
    }

    [JsonIgnore]
    public bool IsReminderDismissed
    {
        get => ReminderDismissed;
        set => ReminderDismissed = value;
    }
}
