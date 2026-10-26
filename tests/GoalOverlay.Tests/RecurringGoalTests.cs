using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

public class RecurringGoalTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _service;

    public RecurringGoalTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_RecurringTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        // 2026-10-26 là Thứ Hai (DayOfWeek.Monday)
        _clock = new TestClock(new DateTime(2026, 10, 26, 8, 30, 0));
        _repository = new JsonGoalRepository(_logger, _testDirectory);
        _service = new GoalService(_repository, _clock, _logger);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task AddRecurringGoal_SavesRuleWithSelectedDays()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday };
        var recurring = await _service.AddRecurringGoalAsync("Tập gym buổi sáng", days, new TimeOnly(7, 0));

        Assert.NotNull(recurring);
        Assert.Equal("Tập gym buổi sáng", recurring.Text);
        Assert.Equal(3, recurring.DaysOfWeek.Count);
        Assert.True(recurring.IsEnabled);

        var allRecurring = await _service.GetRecurringGoalsAsync();
        Assert.Single(allRecurring);
        Assert.Equal("T2, T4, T6", allRecurring[0].GetDaysDisplay());
    }

    [Fact]
    public async Task AddRecurringGoal_AutomaticallyCreatesGoalIfTodayMatches()
    {
        // Clock là 2026-10-26 (Thứ Hai). Thêm mục tiêu lặp lại có chứa Thứ Hai
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Thursday };
        await _service.AddRecurringGoalAsync("Họp đầu tuần", days, new TimeOnly(9, 0));

        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Single(todayGoals);
        Assert.Equal("Họp đầu tuần", todayGoals[0].Text);
        Assert.NotNull(todayGoals[0].RecurringGoalId);
        Assert.Equal(new DateTime(2026, 10, 26, 9, 0, 0), todayGoals[0].DueAt);
    }

    [Fact]
    public async Task EnsureRecurringGoalsForToday_DoesNotDuplicateOnSameDay()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday };
        await _service.AddRecurringGoalAsync("Uống 2L nước", days);

        // Gọi ensure lần 1
        int count1 = await _service.EnsureRecurringGoalsForTodayAsync();
        // Gọi ensure lần 2
        int count2 = await _service.EnsureRecurringGoalsForTodayAsync();

        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Single(todayGoals);
        Assert.Equal(0, count2);
    }

    [Fact]
    public async Task EnsureRecurringGoalsForToday_DoesNotCreateWhenDayDoesNotMatch()
    {
        // 2026-10-26 là Thứ Hai. Tạo mục tiêu chỉ cho Thứ Bảy và Chủ Nhật
        var weekendDays = new[] { DayOfWeek.Saturday, DayOfWeek.Sunday };
        await _service.AddRecurringGoalAsync("Dọn dẹp phòng", weekendDays);

        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Empty(todayGoals);
    }

    [Fact]
    public async Task ToggleRecurringGoal_DisablesRuleAndPreventsGeneration()
    {
        var days = new[] { DayOfWeek.Monday };
        var rule = await _service.AddRecurringGoalAsync("Đọc sách 30 phút", days);

        // Tắt mục tiêu định kỳ
        await _service.ToggleRecurringGoalAsync(rule.Id);

        var allRecurring = await _service.GetRecurringGoalsAsync();
        Assert.False(allRecurring[0].IsEnabled);

        // Xóa goal hôm nay đã tạo khi add ban đầu để kiểm tra xem rule tắt có sinh lại không
        var todayGoals = await _repository.LoadGoalsAsync();
        todayGoals.Clear();
        await _repository.SaveGoalsAsync(todayGoals);

        int count = await _service.EnsureRecurringGoalsForTodayAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DeleteRecurringGoal_RemovesRuleSuccessfully()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday };
        var rule = await _service.AddRecurringGoalAsync("Chạy bộ 5km", days);

        var allBefore = await _service.GetRecurringGoalsAsync();
        Assert.Single(allBefore);

        bool deleted = await _service.DeleteRecurringGoalAsync(rule.Id);
        Assert.True(deleted);

        var allAfter = await _service.GetRecurringGoalsAsync();
        Assert.Empty(allAfter);
    }
}
