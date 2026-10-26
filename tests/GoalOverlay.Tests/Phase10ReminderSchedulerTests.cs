using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.ViewModels;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

/// <summary>
/// Bộ kiểm thử Unit Test cho Giai đoạn 10:
/// 1. ReminderScheduler quét định kỳ và kích hoạt theo IClock
/// 2. Điều kiện đến hạn: chưa xong, ReminderEnabled bật, có DueAt
/// 3. Nhắc sớm (EarlyReminderMinutes)
/// 4. Nhắc lặp lại chu kỳ (RepeatReminderMinutes)
/// 5. Hoãn nhắc lại (Snooze)
/// 6. Bỏ qua lời nhắc (Dismiss)
/// 7. Chế độ Không làm phiền (Do Not Disturb - DND) gom hàng chờ và nhả khi tắt DND
/// 8. Hoàn thành mục tiêu từ cửa sổ nhắc nhở
/// </summary>
public class Phase10ReminderSchedulerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _goalService;
    private readonly ReminderService _reminderService;

    public Phase10ReminderSchedulerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_Phase10Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        _clock = new TestClock(new DateTime(2026, 10, 26, 10, 0, 0)); // 10:00 ngày 26/10/2026
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
    public async Task GoalWithoutDueAt_NeverTriggersReminder()
    {
        // 1. Thêm mục tiêu không có giờ hẹn
        await _goalService.AddDailyGoalAsync("Mục tiêu cả ngày không hẹn giờ", dueAt: null);

        // 2. Chạy quét
        var due = await _reminderService.CheckRemindersAsync();

        // 3. Kỳ vọng: không có mục tiêu nào đến hạn
        Assert.Empty(due);
    }

    [Fact]
    public async Task FutureGoal_DoesNotTriggerReminder()
    {
        // 1. Thêm mục tiêu hẹn vào 11:00 (trong khi clock là 10:00)
        await _goalService.AddDailyGoalAsync("Họp lúc 11 giờ", dueAt: new DateTime(2026, 10, 26, 11, 0, 0));

        // 2. Chạy quét tại 10:00
        var due = await _reminderService.CheckRemindersAsync();

        // 3. Kỳ vọng: chưa đến hạn
        Assert.Empty(due);
    }

    [Fact]
    public async Task DueNowGoal_TriggersReminder_AndBatchEventFired()
    {
        var targetDue = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Báo cáo tuần", dueAt: targetDue);

        IReadOnlyList<Goal>? batchReceived = null;
        _reminderService.GoalsReminderBatchDue += (s, e) => { batchReceived = e.Goals; };

        // Quét đúng lúc 10:00
        var due = await _reminderService.CheckRemindersAsync();

        Assert.Single(due);
        Assert.Equal(goal.Id, due[0].Id);
        Assert.NotNull(batchReceived);
        Assert.Single(batchReceived);

        // Kiểm tra LastNotifiedAt và NotifyCount được cập nhật và lưu trữ
        var savedGoals = await _repository.LoadGoalsAsync();
        var savedGoal = savedGoals.First(g => g.Id == goal.Id);
        Assert.Equal(_clock.Now, savedGoal.LastNotifiedAt);
        Assert.Equal(1, savedGoal.NotifyCount);
    }

    [Fact]
    public async Task EarlyReminder_TriggersAtEarlyTime_ThenAtDueTime()
    {
        // Thiết lập nhắc trước 15 phút
        var settings = await _goalService.GetSettingsAsync();
        settings.EarlyReminderMinutes = 15;
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 30, 0);
        var goal = await _goalService.AddDailyGoalAsync("Đi phỏng vấn", dueAt: dueAt);

        // Lúc 10:10 (trước 20 phút) -> Chưa đến hạn nhắc sớm (10:15)
        _clock.Set(new DateTime(2026, 10, 26, 10, 10, 0));
        var due1 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due1);

        // Lúc 10:15 (đúng mốc nhắc sớm 15 phút) -> Kích hoạt nhắc sớm!
        _clock.Set(new DateTime(2026, 10, 26, 10, 15, 0));
        var due2 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due2);
        Assert.Equal(goal.Id, due2[0].Id);

        // Lúc 10:20 (vẫn trong khoảng nhắc sớm nhưng đã nhắc mốc này rồi) -> Không nhắc lại
        _clock.Set(new DateTime(2026, 10, 26, 10, 20, 0));
        var due3 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due3);

        // Lúc 10:30 (đúng giờ hẹn chính) -> Kích hoạt nhắc chính!
        _clock.Set(new DateTime(2026, 10, 26, 10, 30, 0));
        var due4 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due4);
        Assert.Equal(goal.Id, due4[0].Id);
    }

    [Fact]
    public async Task RepeatReminder_TriggersAfterConfiguredInterval()
    {
        // Cấu hình nhắc lặp lại mỗi 10 phút
        var settings = await _goalService.GetSettingsAsync();
        settings.RepeatReminderMinutes = 10;
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Uống thuốc", dueAt: dueAt);

        // 1. Nhắc lần đầu tại 10:00
        _clock.Set(dueAt);
        var due1 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due1);

        // 2. Tại 10:05 (chưa đủ 10 phút lặp lại) -> Không nhắc
        _clock.Set(new DateTime(2026, 10, 26, 10, 5, 0));
        var due2 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due2);

        // 3. Tại 10:10 (đã qua 10 phút lặp lại) -> Nhắc lại lần 2!
        _clock.Set(new DateTime(2026, 10, 26, 10, 10, 0));
        var due3 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due3);
        Assert.Equal(2, due3[0].NotifyCount);
    }

    [Fact]
    public async Task SnoozeGoal_TriggersWhenSnoozeExpires()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Kiểm tra email", dueAt: dueAt);

        // Nhắc lần 1 lúc 10:00
        _clock.Set(dueAt);
        await _reminderService.CheckRemindersAsync();

        // Người dùng bấm Snooze 15 phút -> SnoozedUntil = 10:15
        await _reminderService.SnoozeGoalAsync(goal.Id, TimeSpan.FromMinutes(15));

        // Tại 10:10 (chưa hết 15 phút hoãn) -> Không nhắc
        _clock.Set(new DateTime(2026, 10, 26, 10, 10, 0));
        var due1 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due1);

        // Tại 10:15 (đã hết 15 phút hoãn) -> Đến hạn báo lại!
        _clock.Set(new DateTime(2026, 10, 26, 10, 15, 0));
        var due2 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due2);
        Assert.Equal(goal.Id, due2[0].Id);
        Assert.Null(due2[0].SnoozedUntil); // Snooze đã được reset sau khi báo
    }

    [Fact]
    public async Task DismissGoal_DoesNotTriggerAgainInCurrentSlot()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Nộp đề xuất", dueAt: dueAt);

        // Nhắc lần 1 lúc 10:00
        _clock.Set(dueAt);
        await _reminderService.CheckRemindersAsync();

        // Người dùng bấm Bỏ qua (Dismiss)
        await _reminderService.DismissGoalAsync(goal.Id);

        // Clock chạy tới 10:30 -> Không nhắc lại vì đã dismiss
        _clock.Set(new DateTime(2026, 10, 26, 10, 30, 0));
        var due = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due);
    }

    [Fact]
    public async Task DoNotDisturb_SuppressesNotification_AndReleasesWhenDisabled()
    {
        // Bật chế độ Không làm phiền (DND)
        var settings = await _goalService.GetSettingsAsync();
        settings.DoNotDisturb = true;
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Cuộc hẹn kín", dueAt: dueAt);

        bool batchEventFired = false;
        _reminderService.GoalsReminderBatchDue += (s, e) => { batchEventFired = true; };

        // Quét lúc 10:00 khi DND đang bật
        _clock.Set(dueAt);
        var due = await _reminderService.CheckRemindersAsync();

        // Trả về mục đến hạn nhưng KHÔNG phát sự kiện popup
        Assert.Single(due);
        Assert.False(batchEventFired);

        // Tắt chế độ Không làm phiền
        settings.DoNotDisturb = false;
        await _goalService.SaveSettingsAsync(settings);

        // Quét lại khi DND đã tắt -> Phát sự kiện gom nhóm!
        var released = await _reminderService.CheckRemindersAsync();
        Assert.Single(released);
        Assert.Equal(goal.Id, released[0].Id);
        Assert.True(batchEventFired);
    }

    [Fact]
    public async Task CompleteGoalFromReminder_MarksGoalDone()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Gửi hợp đồng", dueAt: dueAt);

        // Đánh dấu hoàn thành từ lời nhắc
        await _reminderService.CompleteGoalFromReminderAsync(goal.Id);

        var savedGoals = await _repository.LoadGoalsAsync();
        var saved = savedGoals.First(g => g.Id == goal.Id);
        Assert.True(saved.IsDone);
        Assert.True(saved.ReminderDismissed);
        Assert.Null(saved.SnoozedUntil);
    }

    [Fact]
    public async Task ReminderDisabled_NeverTriggersAnyGoal()
    {
        // Tắt hoàn toàn ReminderEnabled
        var settings = await _goalService.GetSettingsAsync();
        settings.ReminderEnabled = false;
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        await _goalService.AddDailyGoalAsync("Mục tiêu đến giờ nhưng tắt nhắc nhở", dueAt: dueAt);

        _clock.Set(dueAt);
        var due = await _reminderService.CheckRemindersAsync();

        Assert.Empty(due);
    }

    [Fact]
    public void ReminderGoalItemViewModel_CalculatesOverdueCorrectly()
    {
        DateTime now = new DateTime(2026, 10, 26, 10, 15, 0);
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);

        var goal = new Goal
        {
            Text = "Nộp báo cáo thuế",
            DueAt = dueAt,
            Type = GoalType.Daily
        };

        var itemVm = new ReminderGoalItemViewModel(
            goal,
            now,
            new List<int> { 5, 10, 30, 60 },
            _ => { },
            (_, _) => { },
            _ => { });

        Assert.True(itemVm.IsOverdue);
        Assert.Equal(15, itemVm.OverdueMinutes);
        Assert.Equal("Đã quá 15 phút", itemVm.OverdueText);
        Assert.Equal("10:00", itemVm.DueTimeString);
    }

    [Fact]
    public void ReminderWindowViewModel_DeduplicatesGoals_AndClosesWhenEmpty()
    {
        DateTime now = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal1 = new Goal { Id = Guid.NewGuid(), Text = "Mục tiêu 1", DueAt = now };
        var goal2 = new Goal { Id = Guid.NewGuid(), Text = "Mục tiêu 2", DueAt = now };

        var windowVm = new ReminderWindowViewModel(_reminderService, _clock, new[] { goal1, goal2 });

        Assert.Equal(2, windowVm.Items.Count);
        Assert.Contains("(2 mục tiêu)", windowVm.TitleCountText);

        // Thêm trùng goal1 -> danh sách vẫn giữ 2 mục (không bị trùng lặp)
        windowVm.AddGoals(new[] { goal1 });
        Assert.Equal(2, windowVm.Items.Count);

        bool closeRequested = false;
        windowVm.RequestClose += () => { closeRequested = true; };

        // Hoàn thành mục 1
        var item1 = windowVm.Items[0];
        item1.CompleteCommand.Execute(null);

        Assert.Single(windowVm.Items);
        Assert.False(closeRequested);

        // Bỏ qua mục 2 -> danh sách rỗng -> kích hoạt yêu cầu đóng cửa sổ!
        var item2 = windowVm.Items[0];
        item2.DismissCommand.Execute(null);

        Assert.Empty(windowVm.Items);
        Assert.True(closeRequested);
    }
}
