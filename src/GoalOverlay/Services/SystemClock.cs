using System;

namespace GoalOverlay.Services;

/// <summary>
/// Triển khai mặc định của IClock sử dụng thời gian thực của hệ thống (DateTime.Now)
/// </summary>
public class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public string CurrentMonth => DateTime.Now.ToString("yyyy-MM");
}
