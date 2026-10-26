using System;
using System.Collections.Generic;
using System.Linq;

namespace GoalOverlay.Models;

/// <summary>
/// Đại diện cho mục tiêu định kỳ lặp lại theo các ngày trong tuần (T2, T3, T4, T5, T6, T7, CN).
/// Mặc định cứ đến ngày đã tích trong tuần là tự động xuất hiện trong danh sách mục tiêu hàng ngày.
/// </summary>
public class RecurringGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Nội dung mục tiêu
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Giờ hẹn trong ngày (nếu có)
    /// </summary>
    public TimeOnly? ReminderTime { get; set; }

    /// <summary>
    /// Các thứ trong tuần được chọn (Monday -> Sunday)
    /// </summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    /// <summary>
    /// Trạng thái kích hoạt (bật/tắt lặp lại)
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Thời điểm khởi tạo mục tiêu lặp lại
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Ngày cuối cùng mục tiêu này đã được sinh ra trong danh sách hàng ngày (để tránh tạo trùng trong cùng 1 ngày)
    /// </summary>
    public DateOnly? LastGeneratedDate { get; set; }

    /// <summary>
    /// Kiểm tra xem thứ của ngày targetDate có nằm trong danh sách lặp lại hay không
    /// </summary>
    public bool IsActiveOn(DayOfWeek dayOfWeek)
    {
        return IsEnabled && DaysOfWeek.Contains(dayOfWeek);
    }

    /// <summary>
    /// Chuỗi tóm tắt các ngày trong tuần hiển thị người dùng (vd: "T2, T4, T6", "Hàng ngày", "T2 - T6")
    /// </summary>
    public string GetDaysDisplay()
    {
        if (DaysOfWeek == null || DaysOfWeek.Count == 0)
        {
            return "Chưa chọn ngày";
        }

        if (DaysOfWeek.Count == 7)
        {
            return "Hàng ngày (T2 - CN)";
        }

        var weekDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        if (DaysOfWeek.Count == 5 && weekDays.All(DaysOfWeek.Contains))
        {
            return "Các ngày trong tuần (T2 - T6)";
        }

        var weekend = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };
        if (DaysOfWeek.Count == 2 && weekend.All(DaysOfWeek.Contains))
        {
            return "Cuối tuần (T7, CN)";
        }

        // Sắp xếp thứ tự từ T2 (Monday) đến CN (Sunday)
        var ordered = DaysOfWeek
            .Distinct()
            .OrderBy(d => d == DayOfWeek.Sunday ? 7 : (int)d)
            .Select(d => d switch
            {
                DayOfWeek.Monday => "T2",
                DayOfWeek.Tuesday => "T3",
                DayOfWeek.Wednesday => "T4",
                DayOfWeek.Thursday => "T5",
                DayOfWeek.Friday => "T6",
                DayOfWeek.Saturday => "T7",
                DayOfWeek.Sunday => "CN",
                _ => d.ToString()
            });

        return string.Join(", ", ordered);
    }
}
