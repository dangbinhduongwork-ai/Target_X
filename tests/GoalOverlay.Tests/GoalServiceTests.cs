using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

public class GoalServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _service;

    public GoalServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_ServiceTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        _clock = new TestClock(new DateTime(2026, 10, 26, 10, 0, 0));
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
        catch
        {
            // Bỏ qua lỗi dọn dẹp thư mục tạm
        }
    }

    [Fact]
    public async Task Filter_ReturnsOnlyGoalsForToday()
    {
        // Thêm mục tiêu hôm nay (2026-10-26)
        await _service.AddDailyGoalAsync("Mục tiêu hôm nay");

        // Thêm mục tiêu hôm qua (2026-10-25)
        await _service.AddDailyGoalAsync("Mục tiêu hôm qua", date: new DateOnly(2026, 10, 25));

        // Thêm mục tiêu ngày mai (2026-10-27)
        await _service.AddDailyGoalAsync("Mục tiêu ngày mai", date: new DateOnly(2026, 10, 27));

        var todayGoals = await _service.GetTodayGoalsAsync();

        Assert.Single(todayGoals);
        Assert.Equal("Mục tiêu hôm nay", todayGoals[0].Text);
        Assert.Equal(new DateOnly(2026, 10, 26), todayGoals[0].Date);
    }

    [Fact]
    public async Task Filter_ReturnsOnlyGoalsForCurrentMonth()
    {
        // Thêm mục tiêu tháng này (2026-10)
        await _service.AddMonthlyGoalAsync("Mục tiêu tháng 10");

        // Thêm mục tiêu tháng trước (2026-09)
        await _service.AddMonthlyGoalAsync("Mục tiêu tháng 9", month: "2026-09");

        // Thêm mục tiêu tháng sau (2026-11)
        await _service.AddMonthlyGoalAsync("Mục tiêu tháng 11", month: "2026-11");

        var monthGoals = await _service.GetCurrentMonthGoalsAsync();

        Assert.Single(monthGoals);
        Assert.Equal("Mục tiêu tháng 10", monthGoals[0].Text);
        Assert.Equal("2026-10", monthGoals[0].Month);
    }

    [Fact]
    public async Task MidnightTransition_PastMidnight_ReturnsNewDayGoals()
    {
        // 1. Lúc 23:59:50 ngày 2026-10-26
        _clock.SetTime(new DateTime(2026, 10, 26, 23, 59, 50));
        await _service.AddDailyGoalAsync("Hoàn thành báo cáo ngày 26");

        var goalsDay26 = await _service.GetTodayGoalsAsync();
        Assert.Single(goalsDay26);
        Assert.Equal("Hoàn thành báo cáo ngày 26", goalsDay26[0].Text);

        // 2. Thời gian trôi qua nửa đêm sang 00:00:10 ngày 2026-10-27
        _clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal(new DateOnly(2026, 10, 27), _clock.Today);

        // Danh sách hôm nay của ngày 27 phải trống
        var goalsDay27Initial = await _service.GetTodayGoalsAsync();
        Assert.Empty(goalsDay27Initial);

        // Thêm mục tiêu mới cho ngày 27
        await _service.AddDailyGoalAsync("Tập thể dục sáng ngày 27");

        var goalsDay27 = await _service.GetTodayGoalsAsync();
        Assert.Single(goalsDay27);
        Assert.Equal("Tập thể dục sáng ngày 27", goalsDay27[0].Text);
    }

    [Fact]
    public async Task MonthTransition_PastNewMonth_ReturnsNewMonthGoals()
    {
        // 1. Ngày cuối tháng 10: 2026-10-31 23:59:50
        _clock.SetTime(new DateTime(2026, 10, 31, 23, 59, 50));
        await _service.AddMonthlyGoalAsync("Tổng kết tài chính tháng 10");

        var octGoals = await _service.GetCurrentMonthGoalsAsync();
        Assert.Single(octGoals);
        Assert.Equal("Tổng kết tài chính tháng 10", octGoals[0].Text);

        // 2. Chuyển sang tháng 11: 2026-11-01 00:00:10
        _clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Equal("2026-11", _clock.CurrentMonth);

        // Mục tiêu tháng 11 ban đầu trống
        var novGoalsInitial = await _service.GetCurrentMonthGoalsAsync();
        Assert.Empty(novGoalsInitial);

        // Thêm mục tiêu tháng 11
        await _service.AddMonthlyGoalAsync("Chạy bộ 50km tháng 11");

        var novGoals = await _service.GetCurrentMonthGoalsAsync();
        Assert.Single(novGoals);
        Assert.Equal("Chạy bộ 50km tháng 11", novGoals[0].Text);
    }

    [Fact]
    public async Task CarryOver_MovesUnfinishedDailyGoalsToToday_WhenEnabled()
    {
        // Bắt đầu ngày hôm qua (2026-10-25)
        _clock.SetTime(new DateTime(2026, 10, 25, 9, 0, 0));

        // Mục tiêu 1: chưa hoàn thành (IsDone = false)
        var unfinishedGoal = await _service.AddDailyGoalAsync("Đọc 30 trang sách");

        // Mục tiêu 2: đã hoàn thành (IsDone = true)
        var finishedGoal = await _service.AddDailyGoalAsync("Uống 2L nước");
        await _service.ToggleGoalAsync(finishedGoal.Id);

        // Chuyển sang ngày hôm nay (2026-10-26)
        _clock.SetTime(new DateTime(2026, 10, 26, 8, 0, 0));

        // Chạy nghiệp vụ carry-over
        int movedCount = await _service.CarryOverUnfinishedGoalsAsync();

        Assert.Equal(1, movedCount);

        // Mục tiêu chưa hoàn thành đã được chuyển sang hôm nay (2026-10-26)
        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Single(todayGoals);
        Assert.Equal(unfinishedGoal.Id, todayGoals[0].Id);
        Assert.Equal(new DateOnly(2026, 10, 26), todayGoals[0].Date);

        // Mục tiêu đã hoàn thành vẫn giữ nguyên ngày hôm qua (2026-10-25)
        var all = await _repository.LoadGoalsAsync();
        var completedOld = all.First(g => g.Id == finishedGoal.Id);
        Assert.Equal(new DateOnly(2026, 10, 25), completedOld.Date);
        Assert.True(completedOld.IsDone);
    }

    [Fact]
    public async Task CarryOver_DoesNotMove_WhenDisabledInSettings()
    {
        // Tắt cấu hình CarryOverUnfinished
        var settings = await _service.GetSettingsAsync();
        settings.CarryOverUnfinished = false;
        await _service.SaveSettingsAsync(settings);

        // Tạo mục tiêu hôm qua
        _clock.SetTime(new DateTime(2026, 10, 25, 9, 0, 0));
        await _service.AddDailyGoalAsync("Chưa hoàn thành");

        // Sang hôm nay
        _clock.SetTime(new DateTime(2026, 10, 26, 8, 0, 0));

        int moved = await _service.CarryOverUnfinishedGoalsAsync();

        Assert.Equal(0, moved);

        // Hôm nay không có mục tiêu nào
        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Empty(todayGoals);
    }

    [Fact]
    public async Task CrudOperations_AddUpdateToggleDelete_WorkAsExpected()
    {
        // 1. Thêm (Add)
        var goal = await _service.AddDailyGoalAsync("Viết Unit Test");
        Assert.False(goal.IsDone);
        Assert.Equal("Viết Unit Test", goal.Text);

        // 2. Sửa text (Update)
        bool updateSuccess = await _service.UpdateGoalTextAsync(goal.Id, "Viết Unit Test xUnit");
        Assert.True(updateSuccess);

        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Equal("Viết Unit Test xUnit", todayGoals[0].Text);

        // 3. Đổi trạng thái hoàn thành (Toggle)
        bool toggleSuccess = await _service.ToggleGoalAsync(goal.Id);
        Assert.True(toggleSuccess);

        todayGoals = await _service.GetTodayGoalsAsync();
        Assert.True(todayGoals[0].IsDone);

        // 4. Xóa (Delete)
        bool deleteSuccess = await _service.DeleteGoalAsync(goal.Id);
        Assert.True(deleteSuccess);

        todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Empty(todayGoals);
    }

    [Fact]
    public async Task AddDailyGoalAsync_TrimsLeadingAndTrailingWhitespace()
    {
        var goal = await _service.AddDailyGoalAsync("   Mục tiêu có khoảng trắng thừa   ");
        Assert.Equal("Mục tiêu có khoảng trắng thừa", goal.Text);
    }

    [Fact]
    public async Task AddMonthlyGoalAsync_TrimsWhitespace()
    {
        var goal = await _service.AddMonthlyGoalAsync("   Mục tiêu tháng   ");
        Assert.Equal("Mục tiêu tháng", goal.Text);
    }

    [Fact]
    public async Task NonExistentId_ReturnsFalseForUpdateToggleDelete()
    {
        var randomId = Guid.NewGuid();

        bool updateResult = await _service.UpdateGoalTextAsync(randomId, "New text");
        Assert.False(updateResult);

        bool toggleResult = await _service.ToggleGoalAsync(randomId);
        Assert.False(toggleResult);

        bool deleteResult = await _service.DeleteGoalAsync(randomId);
        Assert.False(deleteResult);
    }

    [Fact]
    public async Task CarryOver_WithMultipleUnfinishedGoals_MovesAllAndPreservesCreatedAt()
    {
        _clock.SetTime(new DateTime(2026, 10, 25, 10, 0, 0));

        var g1 = await _service.AddDailyGoalAsync("Nhiệm vụ 1 hôm qua");
        var g2 = await _service.AddDailyGoalAsync("Nhiệm vụ 2 hôm qua");
        var g3 = await _service.AddDailyGoalAsync("Nhiệm vụ 3 đã xong");
        await _service.ToggleGoalAsync(g3.Id);

        _clock.SetTime(new DateTime(2026, 10, 26, 8, 30, 0));

        int moved = await _service.CarryOverUnfinishedGoalsAsync();
        Assert.Equal(2, moved);

        var todayGoals = await _service.GetTodayGoalsAsync();
        Assert.Equal(2, todayGoals.Count);
        Assert.Contains(todayGoals, g => g.Id == g1.Id && g.CreatedAt == g1.CreatedAt);
        Assert.Contains(todayGoals, g => g.Id == g2.Id && g.CreatedAt == g2.CreatedAt);
    }
}

