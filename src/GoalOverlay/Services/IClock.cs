using System;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện trừu tượng hóa đồng hồ hệ thống phục vụ dependency injection và unit test kiểm thử đổi ngày/tháng
/// </summary>
public interface IClock
{
    DateTime Now { get; }

    DateOnly Today => DateOnly.FromDateTime(Now);

    string CurrentMonth => Now.ToString("yyyy-MM");
}
