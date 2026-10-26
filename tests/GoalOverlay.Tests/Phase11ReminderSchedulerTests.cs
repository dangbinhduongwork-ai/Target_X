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
/// Bộ kiểm thử Unit Test chuyên sâu cho Giai đoạn 11:
/// Kiểm tra toàn diện 11 kịch bản trọng yếu của ReminderScheduler với TestClock:
/// 1. Nhắc đúng 1 lần cho mỗi mốc giờ
/// 2. Hoãn báo lại (Snooze) nhắc đúng thời gian chỉ định
/// 3. Đánh dấu hoàn thành (MarkDone) lập tức hủy nhắc
/// 4. Tắt và mở lại ứng dụng không nhắc lại các mục đã nhắc
/// 5. Các mục tiêu bị lỡ khi tắt máy/tắt app được gom vào 1 lần quét duy nhất khi mở lại
/// 6. Nhiều mục tiêu cùng phút được gom vào cùng một cửa sổ (GoalsReminderBatchDue)
/// 7. Nhắc lặp lại chu kỳ (RepeatReminderMinutes)
/// 8. Nhắc sớm trước giờ hẹn (EarlyReminderMinutes)
/// 9. Mục không có giờ hẹn (DueAt == null) tuyệt đối không bao giờ nhắc
/// 10. Đồng hồ hệ thống bị chỉnh lùi không gây kích hoạt nhắc trùng lặp
/// 11. Chế độ Không làm phiền (Do Not Disturb) gom vào hàng chờ và nhả khi tắt DND
/// 12. Snooze được lưu trữ bền vững qua các lần khởi động lại ứng dụng
/// </summary>
public class Phase11ReminderSchedulerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _goalService;
    private readonly ReminderService _reminderService;

    public Phase11ReminderSchedulerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_Phase11Test_" + Guid.NewGuid().ToString("N"));
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
    public async Task Scenario1_TriggersExactlyOnce_ForGivenDueSlot()
    {
        // 1. Mục tiêu hẹn giờ lúc 10:00
        var goal = await _goalService.AddDailyGoalAsync("Họp giao ban", dueAt: new DateTime(2026, 10, 26, 10, 0, 0));

        int triggerCount = 0;
        _reminderService.GoalReminderDue += (s, e) => { if (e.Goal.Id == goal.Id) triggerCount++; };

        // Lúc 10:00:00 -> Kích hoạt nhắc lần 1
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        var due1 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due1);
        Assert.Equal(1, triggerCount);

        // Lúc 10:00:30 (quét định kỳ 30s sau) -> Không nhắc lại
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 30));
        var due2 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due2);
        Assert.Equal(1, triggerCount);

        // Lúc 10:02:00 -> Vẫn không nhắc lại
        _clock.Set(new DateTime(2026, 10, 26, 10, 2, 0));
        var due3 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due3);
        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public async Task Scenario2_SnoozeGoal_TriggersExactlyAtSnoozeTime()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Tập hít thở", dueAt: dueAt);

        // Nhắc lúc 10:00
        _clock.Set(dueAt);
        await _reminderService.CheckRemindersAsync();

        // Hoãn báo lại (Snooze) 15 phút -> Hẹn tới 10:15
        await _reminderService.SnoozeGoalAsync(goal.Id, TimeSpan.FromMinutes(15));

        // Tại 10:14 (chưa tới 10:15) -> Không nhắc
        _clock.Set(new DateTime(2026, 10, 26, 10, 14, 0));
        var checkEarly = await _reminderService.CheckRemindersAsync();
        Assert.Empty(checkEarly);

        // Tại 10:15:00 (đúng thời gian hoãn) -> Kích hoạt báo lại
        _clock.Set(new DateTime(2026, 10, 26, 10, 15, 0));
        var checkDue = await _reminderService.CheckRemindersAsync();
        Assert.Single(checkDue);
        Assert.Equal(goal.Id, checkDue[0].Id);
        Assert.Null(checkDue[0].SnoozedUntil); // Đã reset Snooze sau khi báo
    }

    [Fact]
    public async Task Scenario3_CompletedGoal_CancelsAnyPendingReminder()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Hoàn thành báo cáo thuế", dueAt: dueAt);

        // Đánh dấu hoàn thành trước giờ hẹn
        await _goalService.MarkDoneAsync(goal.Id, true);

        // Đến giờ hẹn 10:00
        _clock.Set(dueAt);
        var due = await _reminderService.CheckRemindersAsync();

        // Kỳ vọng: Mục tiêu đã hoàn thành thì hủy nhắc nhở
        Assert.Empty(due);
    }

    [Fact]
    public async Task Scenario4_AppRestart_DoesNotRetrigger_AlreadyRemindedGoal()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Gửi hợp đồng ký kết", dueAt: dueAt);

        // App đang chạy, quét lúc 10:00 -> Đã nhắc và ghi nhận LastNotifiedAt xuống file
        _clock.Set(dueAt);
        var firstCheck = await _reminderService.CheckRemindersAsync();
        Assert.Single(firstCheck);

        // Mô phỏng tắt ứng dụng (Dispose ReminderService hiện tại)
        _reminderService.Dispose();

        // Khởi động lại ứng dụng lúc 10:05 (Tạo instance ReminderService mới dùng chung repository)
        _clock.Set(new DateTime(2026, 10, 26, 10, 5, 0));
        using var newReminderService = new ReminderService(_repository, _goalService, _clock, _logger);
        var secondCheck = await newReminderService.CheckRemindersAsync();

        // Kỳ vọng: Không nhắc lại vì đã ghi nhận LastNotifiedAt cho mốc 10:00
        Assert.Empty(secondCheck);
    }

    [Fact]
    public async Task Scenario5_MissedGoals_WhileOffline_GatheredInSingleBatchOnStartup()
    {
        // Giả lập 2 mục tiêu có giờ hẹn lúc máy tính đang tắt: 08:00 và 09:00
        var goal1 = await _goalService.AddDailyGoalAsync("Uống vitamin sáng", dueAt: new DateTime(2026, 10, 26, 8, 0, 0));
        var goal2 = await _goalService.AddDailyGoalAsync("Kiểm tra thư từ", dueAt: new DateTime(2026, 10, 26, 9, 0, 0));

        // Người dùng bật máy và mở app lúc 10:00
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));

        IReadOnlyList<Goal>? batchReceived = null;
        _reminderService.GoalsReminderBatchDue += (s, e) => { batchReceived = e.Goals; };

        var due = await _reminderService.CheckRemindersAsync();

        // Kỳ vọng: Cả 2 mục tiêu bị lỡ đều được gom chung vào 1 lần quét và 1 sự kiện batch
        Assert.Equal(2, due.Count);
        Assert.NotNull(batchReceived);
        Assert.Equal(2, batchReceived.Count);
        Assert.Contains(batchReceived, g => g.Id == goal1.Id);
        Assert.Contains(batchReceived, g => g.Id == goal2.Id);
    }

    [Fact]
    public async Task Scenario6_MultipleGoals_SameMinute_GroupedInSingleBatch()
    {
        // 2 mục tiêu cùng hẹn giờ lúc 10:30
        DateTime sameMinute = new DateTime(2026, 10, 26, 10, 30, 0);
        var goalA = await _goalService.AddDailyGoalAsync("Tác vụ A cùng giờ", dueAt: sameMinute);
        var goalB = await _goalService.AddDailyGoalAsync("Tác vụ B cùng giờ", dueAt: sameMinute);

        _clock.Set(sameMinute);

        IReadOnlyList<Goal>? batch = null;
        _reminderService.GoalsReminderBatchDue += (s, e) => { batch = e.Goals; };

        var due = await _reminderService.CheckRemindersAsync();

        Assert.Equal(2, due.Count);
        Assert.NotNull(batch);
        Assert.Equal(2, batch.Count);
    }

    [Fact]
    public async Task Scenario7_RepeatReminder_TriggersAfterConfiguredInterval()
    {
        var settings = await _goalService.GetSettingsAsync();
        settings.RepeatReminderMinutes = 15; // Nhắc lặp lại mỗi 15 phút
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Mục tiêu nhắc lặp lại", dueAt: dueAt);

        // Lần 1: Nhắc lúc 10:00
        _clock.Set(dueAt);
        var due1 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due1);

        // Tại 10:10 (mới qua 10 phút, chưa đủ 15 phút) -> Không nhắc
        _clock.Set(new DateTime(2026, 10, 26, 10, 10, 0));
        var due2 = await _reminderService.CheckRemindersAsync();
        Assert.Empty(due2);

        // Tại 10:15 (đủ 15 phút) -> Kích hoạt nhắc lại lần 2
        _clock.Set(new DateTime(2026, 10, 26, 10, 15, 0));
        var due3 = await _reminderService.CheckRemindersAsync();
        Assert.Single(due3);
        Assert.Equal(2, due3[0].NotifyCount);
    }

    [Fact]
    public async Task Scenario8_EarlyReminder_TriggersBeforeDueTime()
    {
        var settings = await _goalService.GetSettingsAsync();
        settings.EarlyReminderMinutes = 10; // Nhắc trước 10 phút
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 30, 0);
        var goal = await _goalService.AddDailyGoalAsync("Họp quan trọng với đối tác", dueAt: dueAt);

        // Lúc 10:15 (trước 15 phút) -> Chưa đến hạn nhắc trước (10:20)
        _clock.Set(new DateTime(2026, 10, 26, 10, 15, 0));
        Assert.Empty(await _reminderService.CheckRemindersAsync());

        // Lúc 10:20 (đúng trước 10 phút) -> Kích hoạt nhắc sớm!
        _clock.Set(new DateTime(2026, 10, 26, 10, 20, 0));
        var earlyCheck = await _reminderService.CheckRemindersAsync();
        Assert.Single(earlyCheck);

        // Lúc 10:25 (vẫn trước giờ hẹn nhưng đã nhắc sớm rồi) -> Không nhắc trùng
        _clock.Set(new DateTime(2026, 10, 26, 10, 25, 0));
        Assert.Empty(await _reminderService.CheckRemindersAsync());

        // Lúc 10:30 (đúng giờ hẹn chính) -> Kích hoạt nhắc chính!
        _clock.Set(dueAt);
        var mainCheck = await _reminderService.CheckRemindersAsync();
        Assert.Single(mainCheck);
    }

    [Fact]
    public async Task Scenario9_GoalWithoutDueAt_NeverTriggersReminder()
    {
        await _goalService.AddDailyGoalAsync("Mục tiêu thông thường không hẹn giờ", dueAt: null);

        // Kiểm tra tại nhiều mốc thời gian khác nhau
        _clock.Set(new DateTime(2026, 10, 26, 10, 0, 0));
        Assert.Empty(await _reminderService.CheckRemindersAsync());

        _clock.Set(new DateTime(2026, 10, 26, 15, 30, 0));
        Assert.Empty(await _reminderService.CheckRemindersAsync());

        _clock.Set(new DateTime(2026, 10, 26, 23, 59, 59));
        Assert.Empty(await _reminderService.CheckRemindersAsync());
    }

    [Fact]
    public async Task Scenario10_ClockSteppedBackwards_DoesNotTriggerDuplicateReminder()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Thanh toán tiền điện", dueAt: dueAt);

        // Đã nhắc lúc 10:00
        _clock.Set(dueAt);
        var first = await _reminderService.CheckRemindersAsync();
        Assert.Single(first);

        // Người dùng hoặc hệ thống chỉnh lùi giờ lại 09:50
        _clock.Set(new DateTime(2026, 10, 26, 9, 50, 0));
        var backwardsCheck = await _reminderService.CheckRemindersAsync();

        // Kỳ vọng: Giờ lùi về quá khứ không kích hoạt nhắc lại
        Assert.Empty(backwardsCheck);
    }

    [Fact]
    public async Task Scenario11_DoNotDisturb_SuppressesAndReleasesWhenDisabled()
    {
        var settings = await _goalService.GetSettingsAsync();
        settings.DoNotDisturb = true; // Bật chế độ Không làm phiền
        await _goalService.SaveSettingsAsync(settings);

        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Mục tiêu trong cuộc họp kín", dueAt: dueAt);

        bool batchFired = false;
        _reminderService.GoalsReminderBatchDue += (s, e) => { batchFired = true; };

        // Quét lúc 10:00 -> DND chặn sự kiện cửa sổ
        _clock.Set(dueAt);
        var checkDndOn = await _reminderService.CheckRemindersAsync();
        Assert.Single(checkDndOn);
        Assert.False(batchFired);

        // Tắt chế độ Không làm phiền
        settings.DoNotDisturb = false;
        await _goalService.SaveSettingsAsync(settings);

        // Quét lại -> Giải phóng các mục bị chặn vào cửa sổ gom!
        var checkDndOff = await _reminderService.CheckRemindersAsync();
        Assert.Single(checkDndOff);
        Assert.True(batchFired);
        Assert.Equal(goal.Id, checkDndOff[0].Id);
    }

    [Fact]
    public async Task Scenario12_SnoozePersistedAcrossAppRestart_TriggersAtCorrectSnoozeTime()
    {
        DateTime dueAt = new DateTime(2026, 10, 26, 10, 0, 0);
        var goal = await _goalService.AddDailyGoalAsync("Nộp đề cương đồ án", dueAt: dueAt);

        // Nhắc lần 1 lúc 10:00
        _clock.Set(dueAt);
        await _reminderService.CheckRemindersAsync();

        // Snooze 20 phút (tới 10:20)
        await _reminderService.SnoozeGoalAsync(goal.Id, TimeSpan.FromMinutes(20));

        // Tắt app (Dispose ReminderService)
        _reminderService.Dispose();

        // Khởi động lại app lúc 10:10 (mới qua 10 phút, chưa tới 10:20)
        _clock.Set(new DateTime(2026, 10, 26, 10, 10, 0));
        using var newService = new ReminderService(_repository, _goalService, _clock, _logger);
        var earlyCheck = await newService.CheckRemindersAsync();
        Assert.Empty(earlyCheck);

        // Đồng hồ chạy tới 10:20 (đúng giờ hoãn)
        _clock.Set(new DateTime(2026, 10, 26, 10, 20, 0));
        var dueCheck = await newService.CheckRemindersAsync();
        Assert.Single(dueCheck);
        Assert.Equal(goal.Id, dueCheck[0].Id);
    }
}
