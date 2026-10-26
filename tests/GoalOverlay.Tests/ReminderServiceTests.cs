using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

/// <summary>
/// Bộ kiểm thử Unit Test cho tính năng Hẹn giờ và Nhắc nhở mục tiêu (V1.1)
/// Kiểm thử tính tương thích ngược dữ liệu V1.0 (Migration) và logic định thời với TestClock
/// </summary>
public class ReminderServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _goalService;
    private readonly ReminderService _reminderService;

    public ReminderServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_ReminderTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        _clock = new TestClock(new DateTime(2026, 10, 26, 9, 0, 0));
        _repository = new JsonGoalRepository(_logger, _testDirectory);
        _goalService = new GoalService(_repository, _clock, _logger);
        _reminderService = new ReminderService(_repository, _goalService, _clock, _logger);
    }

    public void Dispose()
    {
        _reminderService.Dispose();

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
    public async Task V1_0_GoalsJson_Migration_Loads_Without_Error_And_Sets_Default_Reminder_Fields()
    {
        // 1. Tạo file goals.json giả lập đúng chuẩn dữ liệu V1.0 (không có các trường reminder mới)
        string legacyV1Json = @"[
            {
                ""Id"": ""11111111-1111-1111-1111-111111111111"",
                ""Text"": ""Mục tiêu chuẩn V1.0"",
                ""Type"": 0,
                ""Date"": ""2026-10-26"",
                ""Month"": """",
                ""IsDone"": false,
                ""CreatedAt"": ""2026-10-26T08:00:00Z""
            }
        ]";

        string goalsFile = Path.Combine(_testDirectory, "goals.json");
        await File.WriteAllTextAsync(goalsFile, legacyV1Json);

        // 2. Nạp dữ liệu qua repository
        var goals = await _repository.LoadGoalsAsync();

        // 3. Kiểm tra mục tiêu cũ nạp thành công với giá trị mặc định của V1.1
        Assert.Single(goals);
        var goal = goals[0];
        Assert.Equal("Mục tiêu chuẩn V1.0", goal.Text);
        Assert.False(goal.HasReminder);
        Assert.Null(goal.ReminderTime);
        Assert.Null(goal.SnoozeUntil);
        Assert.False(goal.IsReminderDismissed);
        Assert.Null(goal.LastRemindedAt);

        // 4. Cập nhật thêm hẹn giờ và lưu lại, bảo đảm không làm hỏng file
        goal.HasReminder = true;
        goal.ReminderTime = new TimeOnly(15, 30);
        await _repository.SaveGoalsAsync(goals);

        var reloaded = await _repository.LoadGoalsAsync();
        Assert.Single(reloaded);
        Assert.True(reloaded[0].HasReminder);
        Assert.Equal(new TimeOnly(15, 30), reloaded[0].ReminderTime);
    }

    [Fact]
    public async Task AddDailyGoal_WithReminder_Saves_And_Exposes_ReminderTime()
    {
        var goal = await _goalService.AddDailyGoalAsync("Họp nhóm quan trọng", reminderTime: new TimeOnly(14, 0));

        Assert.True(goal.HasReminder);
        Assert.Equal(new TimeOnly(14, 0), goal.ReminderTime);

        var todayGoals = await _goalService.GetTodayGoalsAsync();
        var saved = todayGoals.FirstOrDefault(g => g.Id == goal.Id);
        Assert.NotNull(saved);
        Assert.True(saved.HasReminder);
        Assert.Equal(new TimeOnly(14, 0), saved.ReminderTime);
    }

    [Fact]
    public async Task CheckRemindersAsync_BeforeReminderTime_DoesNotTrigger()
    {
        // Đặt giờ đồng hồ hiện tại là 09:00:00
        _clock.Set(new DateTime(2026, 10, 26, 9, 0, 0));

        // Mục tiêu hẹn giờ 10:00
        await _goalService.AddDailyGoalAsync("Mục tiêu tương lai", reminderTime: new TimeOnly(10, 0));

        bool eventFired = false;
        _reminderService.GoalReminderDue += (s, e) => eventFired = true;

        var dueGoals = await _reminderService.CheckRemindersAsync();

        Assert.Empty(dueGoals);
        Assert.False(eventFired);
    }

    [Fact]
    public async Task CheckRemindersAsync_AtOrAfterReminderTime_TriggersReminder()
    {
        // Mục tiêu hẹn giờ 10:00
        var goal = await _goalService.AddDailyGoalAsync("Nộp báo cáo", reminderTime: new TimeOnly(10, 0));

        // Chỉnh đồng hồ đến 10:00:00
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));

        Goal? triggeredGoal = null;
        _reminderService.GoalReminderDue += (s, e) => triggeredGoal = e.Goal;

        var dueGoals = await _reminderService.CheckRemindersAsync();

        Assert.Single(dueGoals);
        Assert.Equal(goal.Id, dueGoals[0].Id);
        Assert.NotNull(triggeredGoal);
        Assert.Equal(goal.Id, triggeredGoal.Id);
    }

    [Fact]
    public async Task CheckRemindersAsync_AlreadyReminded_DoesNotTriggerAgain()
    {
        await _goalService.AddDailyGoalAsync("Nhắc 1 lần", reminderTime: new TimeOnly(10, 0));

        // Lần 1: Đồng hồ 10:00 -> Kích hoạt nhắc nhở
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        var firstCheck = await _reminderService.CheckRemindersAsync();
        Assert.Single(firstCheck);

        // Lần 2: Đồng hồ 10:01 -> Không được nhắc lại (tránh spam)
        _clock.Set(new DateTime(2026, 10, 26, 10, 1, 0));
        var secondCheck = await _reminderService.CheckRemindersAsync();
        Assert.Empty(secondCheck);
    }

    [Fact]
    public async Task CheckRemindersAsync_CompletedGoal_DoesNotTrigger()
    {
        var goal = await _goalService.AddDailyGoalAsync("Mục tiêu đã làm xong", reminderTime: new TimeOnly(10, 0));
        await _goalService.ToggleGoalAsync(goal.Id); // IsDone = true

        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        var dueGoals = await _reminderService.CheckRemindersAsync();

        Assert.Empty(dueGoals);
    }

    [Fact]
    public async Task SnoozeGoalAsync_DelaysReminderUntilSnoozeTimeExpires()
    {
        var goal = await _goalService.AddDailyGoalAsync("Tập thể dục", reminderTime: new TimeOnly(10, 0));

        // Đến giờ 10:00 -> Nhắc nhở kích hoạt
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        await _reminderService.CheckRemindersAsync();

        // Người dùng chọn Snooze hoãn 10 phút (đến 10:10)
        await _reminderService.SnoozeGoalAsync(goal.Id, TimeSpan.FromMinutes(10));

        // Tại thời điểm 10:05 (chưa hết 10 phút) -> Không nhắc
        _clock.Set(new DateTime(2026, 10, 26, 10, 5, 0));
        var midCheck = await _reminderService.CheckRemindersAsync();
        Assert.Empty(midCheck);

        // Tại thời điểm 10:10 (đến hạn Snooze) -> Kích hoạt nhắc lại
        _clock.Set(new DateTime(2026, 10, 26, 10, 10, 0));
        var snoozeCheck = await _reminderService.CheckRemindersAsync();
        Assert.Single(snoozeCheck);
        Assert.Equal(goal.Id, snoozeCheck[0].Id);
    }

    [Fact]
    public async Task DismissGoalAsync_SuppressesRemindersForToday()
    {
        var goal = await _goalService.AddDailyGoalAsync("Đọc sách", reminderTime: new TimeOnly(10, 0));

        // Người dùng chủ động Dismiss bỏ qua lời nhắc
        await _reminderService.DismissGoalAsync(goal.Id);

        _clock.Set(new DateTime(2026, 10, 26, 10, 15, 0));
        var dueGoals = await _reminderService.CheckRemindersAsync();

        Assert.Empty(dueGoals);
    }

    [Fact]
    public async Task CompleteGoalFromReminderAsync_MarksGoalAsDone_And_StopsReminders()
    {
        var goal = await _goalService.AddDailyGoalAsync("Uống nước", reminderTime: new TimeOnly(10, 0));

        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        await _reminderService.CheckRemindersAsync();

        // Người dùng bấm nút Hoàn thành trên cửa sổ nhắc nhở
        await _reminderService.CompleteGoalFromReminderAsync(goal.Id);

        var todayGoals = await _goalService.GetTodayGoalsAsync();
        var updated = todayGoals.First(g => g.Id == goal.Id);
        Assert.True(updated.IsDone);

        // Kiểm tra lại không còn nhắc
        var recheck = await _reminderService.CheckRemindersAsync();
        Assert.Empty(recheck);
    }

    [Fact]
    public async Task CarryOverUnfinishedGoals_ResetsDismissalStatusForNewDay()
    {
        // 1. Tạo mục tiêu hôm qua và dismiss
        var yesterday = new DateOnly(2026, 10, 25);
        _clock.Set(new DateTime(2026, 10, 25, 10, 0, 0));

        var goal = await _goalService.AddDailyGoalAsync("Viết tài liệu", date: yesterday, reminderTime: new TimeOnly(10, 0));
        await _reminderService.DismissGoalAsync(goal.Id);

        // Bật cài đặt CarryOver
        var settings = await _goalService.GetSettingsAsync();
        settings.CarryOverUnfinished = true;
        await _goalService.SaveSettingsAsync(settings);

        // 2. Chuyển sang ngày hôm nay
        _clock.Set(new DateTime(2026, 10, 26, 8, 0, 0));
        int carried = await _goalService.CarryOverUnfinishedGoalsAsync();
        Assert.Equal(1, carried);

        // 3. Kiểm tra mục tiêu đã chuyển sang hôm nay, DueAt bị bỏ (null) và cờ Dismiss đã được reset về false
        var todayGoals = await _goalService.GetTodayGoalsAsync();
        var carriedGoal = todayGoals.First(g => g.Id == goal.Id);
        Assert.Equal(new DateOnly(2026, 10, 26), carriedGoal.Date);
        Assert.Null(carriedGoal.DueAt);
        Assert.False(carriedGoal.IsReminderDismissed);
        Assert.Null(carriedGoal.SnoozedUntil);

        // 4. Nếu người dùng hẹn lại giờ mới cho hôm nay (10:00), mục tiêu sẽ được nhắc nhở khi đến giờ
        await _goalService.UpdateGoalReminderAsync(goal.Id, true, new TimeOnly(10, 0));
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        var dueGoals = await _reminderService.CheckRemindersAsync();
        Assert.Single(dueGoals);
        Assert.Equal(goal.Id, dueGoals[0].Id);
    }
}
