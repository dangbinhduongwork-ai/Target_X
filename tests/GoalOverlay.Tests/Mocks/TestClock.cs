using System;
using GoalOverlay.Services;

namespace GoalOverlay.Tests.Mocks;

/// <summary>
/// Đồng hồ giả lập phục vụ unit test: cho phép thiết lập và tua nhanh/chậm thời gian tùy ý
/// </summary>
public class TestClock : IClock
{
    public DateTime Now { get; set; }

    public TestClock(DateTime initialTime)
    {
        Now = initialTime;
    }

    public void SetTime(DateTime time)
    {
        Now = time;
    }

    public void Advance(TimeSpan duration)
    {
        Now = Now.Add(duration);
    }
}
