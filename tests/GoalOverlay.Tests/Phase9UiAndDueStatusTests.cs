using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.ViewModels;
using GoalOverlay.Views.Converters;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

/// <summary>
/// Bộ kiểm thử Unit Test cho Giai đoạn 9:
/// 1. Tính toán GoalDueStatus (Done, Overdue, DueSoon, Normal) và mã màu HEX
/// 2. GoalItemViewModel formatting (DueTimeString, HasDueTime, RefreshStatus, BeginEdit)
/// 3. MainViewModel cảnh báo giờ quá hạn khi nhập mục tiêu hôm nay
/// 4. MainViewModel thêm mục tiêu và sửa nhanh (in-place edit) kèm thời gian
/// 5. MainViewModel đồng bộ cài đặt nhắc nhở (ReminderEnabled, Repeat, Early, DND)
/// 6. Converters hiển thị số phút nhắc trước và lặp lại
/// </summary>
public class Phase9UiAndDueStatusTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _goalService;
    private readonly MockAutostartService _autostart;
    private readonly MockHotkeyService _hotkey;

    public Phase9UiAndDueStatusTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_Phase9Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        _clock = new TestClock(new DateTime(2026, 10, 26, 14, 0, 0)); // 14:00 ngày 26/10/2026
        _repository = new JsonGoalRepository(_logger, _testDirectory);
        _goalService = new GoalService(_repository, _clock, _logger);
        _autostart = new MockAutostartService();
        _hotkey = new MockHotkeyService();
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
    public void GoalDueStatus_WhenDone_ReturnsDoneStatusAndMutedColor()
    {
        var goal = new Goal
        {
            Text = "Mục tiêu đã xong",
            DueAt = _clock.Now.AddHours(-1), // Dù đã quá hạn nhưng đã xong
            IsDone = true
        };

        var status = GoalDueStatusCalculator.CalculateStatus(goal, _clock.Now);
        var color = GoalDueStatusCalculator.GetStatusColor(status);

        Assert.Equal(GoalDueStatus.Done, status);
        Assert.Equal(GoalDueStatusCalculator.ColorDone, color);
        Assert.Equal("#94A3B8", color);
    }

    [Fact]
    public void GoalDueStatus_WhenNoDueAt_ReturnsNormalStatusAndBlueColor()
    {
        var goal = new Goal
        {
            Text = "Mục tiêu không hẹn giờ",
            DueAt = null,
            IsDone = false
        };

        var status = GoalDueStatusCalculator.CalculateStatus(goal, _clock.Now);
        var color = GoalDueStatusCalculator.GetStatusColor(status);

        Assert.Equal(GoalDueStatus.Normal, status);
        Assert.Equal(GoalDueStatusCalculator.ColorNormal, color);
        Assert.Equal("#60A5FA", color);
    }

    [Fact]
    public void GoalDueStatus_WhenOverdue_ReturnsOverdueAndRedColor()
    {
        var goal = new Goal
        {
            Text = "Mục tiêu quá hạn",
            DueAt = _clock.Now.AddMinutes(-5), // Quá hạn 5 phút
            IsDone = false
        };

        var status = GoalDueStatusCalculator.CalculateStatus(goal, _clock.Now);
        var color = GoalDueStatusCalculator.GetStatusColor(status);

        Assert.Equal(GoalDueStatus.Overdue, status);
        Assert.Equal(GoalDueStatusCalculator.ColorOverdue, color);
        Assert.Equal("#EF4444", color);
    }

    [Fact]
    public void GoalDueStatus_WhenDueSoon_ReturnsDueSoonAndYellowColor()
    {
        var goal = new Goal
        {
            Text = "Mục tiêu sắp đến hạn (còn 20 phút)",
            DueAt = _clock.Now.AddMinutes(20),
            IsDone = false
        };

        var status = GoalDueStatusCalculator.CalculateStatus(goal, _clock.Now);
        var color = GoalDueStatusCalculator.GetStatusColor(status);

        Assert.Equal(GoalDueStatus.DueSoon, status);
        Assert.Equal(GoalDueStatusCalculator.ColorDueSoon, color);
        Assert.Equal("#F59E0B", color);
    }

    [Fact]
    public void GoalDueStatus_WhenDueFarInFuture_ReturnsNormalAndBlueColor()
    {
        var goal = new Goal
        {
            Text = "Mục tiêu còn 2 tiếng nữa",
            DueAt = _clock.Now.AddHours(2),
            IsDone = false
        };

        var status = GoalDueStatusCalculator.CalculateStatus(goal, _clock.Now);
        var color = GoalDueStatusCalculator.GetStatusColor(status);

        Assert.Equal(GoalDueStatus.Normal, status);
        Assert.Equal(GoalDueStatusCalculator.ColorNormal, color);
    }

    [Fact]
    public void GoalItemViewModel_Formatting_FormatsDailyAndMonthlyCorrectly()
    {
        // Daily goal có giờ
        var dailyGoal = new Goal
        {
            Type = GoalType.Daily,
            Text = "Họp team",
            DueAt = new DateTime(2026, 10, 26, 15, 30, 0)
        };
        var vmDaily = new GoalItemViewModel(dailyGoal);
        Assert.True(vmDaily.HasDueTime);
        Assert.Equal("15:30", vmDaily.DueTimeString);

        // Monthly goal chỉ có ngày hạn chót
        var monthlyGoalOnlyDate = new Goal
        {
            Type = GoalType.Monthly,
            Text = "Báo cáo tháng",
            DueAt = new DateTime(2026, 10, 31, 0, 0, 0)
        };
        var vmMonthly1 = new GoalItemViewModel(monthlyGoalOnlyDate);
        Assert.Equal("31/10", vmMonthly1.DueTimeString);

        // Monthly goal có cả giờ
        var monthlyGoalWithTime = new Goal
        {
            Type = GoalType.Monthly,
            Text = "Nộp đồ án",
            DueAt = new DateTime(2026, 10, 31, 23, 59, 0)
        };
        var vmMonthly2 = new GoalItemViewModel(monthlyGoalWithTime);
        Assert.Equal("31/10 23:59", vmMonthly2.DueTimeString);
    }

    [Fact]
    public void GoalItemViewModel_BeginEdit_PopulatesEditProperties()
    {
        var goal = new Goal
        {
            Type = GoalType.Daily,
            Text = "Soạn tài liệu",
            DueAt = new DateTime(2026, 10, 26, 16, 45, 0)
        };

        var vm = new GoalItemViewModel(goal);
        vm.BeginEdit();

        Assert.True(vm.IsEditing);
        Assert.Equal("Soạn tài liệu", vm.EditText);
        Assert.Equal(new TimeOnly(16, 45), vm.EditDueTime);
        Assert.Equal(new DateTime(2026, 10, 26), vm.EditDueDate);
    }

    [Fact]
    public void MainViewModel_NewDailyTime_DetectsPastTimeWarning()
    {
        var mainVm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostart, _hotkey);

        // Clock đang là 14:00 ngày 26/10/2026
        // Nhập 13:30 (đã qua trong hôm nay) -> Phải hiện warning
        mainVm.NewDailyTime = new TimeOnly(13, 30);
        Assert.Contains("⚠️ Giờ này đã qua", mainVm.WarningMessage);

        // Nhập 15:00 (chưa qua) -> Không hiện warning
        mainVm.NewDailyTime = new TimeOnly(15, 0);
        Assert.Equal(string.Empty, mainVm.WarningMessage);

        // Xóa giờ -> Không hiện warning
        mainVm.NewDailyTime = null;
        Assert.Equal(string.Empty, mainVm.WarningMessage);
    }

    [Fact]
    public async Task MainViewModel_AddDailyGoal_WithTime_SavesDueAtSuccessfully()
    {
        var mainVm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostart, _hotkey);
        await mainVm.InitializeAsync();

        mainVm.NewDailyText = "Tập thể dục buổi chiều";
        mainVm.NewDailyTime = new TimeOnly(17, 15);

        await mainVm.AddDailyGoalAsync();

        var goals = await _goalService.GetTodayGoalsAsync();
        var added = goals.FirstOrDefault(g => g.Text == "Tập thể dục buổi chiều");
        Assert.NotNull(added);
        Assert.True(added.DueAt.HasValue);
        Assert.Equal(new DateTime(2026, 10, 26, 17, 15, 0), added.DueAt.Value);
    }

    [Fact]
    public async Task MainViewModel_AddMonthlyGoal_WithDateAndTime_SavesDueAtSuccessfully()
    {
        var mainVm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostart, _hotkey);
        await mainVm.InitializeAsync();

        mainVm.NewMonthlyText = "Quyết toán thuế";
        mainVm.NewMonthlyDueDate = new DateTime(2026, 10, 28);
        mainVm.NewMonthlyDueTime = new TimeOnly(20, 0);

        await mainVm.AddMonthlyGoalAsync();

        var goals = await _goalService.GetCurrentMonthGoalsAsync();
        var added = goals.FirstOrDefault(g => g.Text == "Quyết toán thuế");
        Assert.NotNull(added);
        Assert.True(added.DueAt.HasValue);
        Assert.Equal(new DateTime(2026, 10, 28, 20, 0, 0), added.DueAt.Value);
    }

    [Fact]
    public async Task MainViewModel_SaveEdit_UpdatesBothTextAndDueTime()
    {
        var initialGoal = await _goalService.AddDailyGoalAsync("Viết báo cáo", dueAt: null);
        var mainVm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostart, _hotkey);
        await mainVm.InitializeAsync();

        var itemVm = mainVm.TodayGoals.First(g => g.Model.Id == initialGoal.Id);
        mainVm.StartEdit(itemVm);

        // Chỉnh sửa cả nội dung và giờ hẹn
        itemVm.EditText = "Viết báo cáo hoàn chỉnh";
        itemVm.EditDueTime = new TimeOnly(16, 0);

        await mainVm.SaveEditAsync(itemVm);

        Assert.False(itemVm.IsEditing);
        Assert.Equal("Viết báo cáo hoàn chỉnh", itemVm.Text);
        Assert.True(itemVm.HasDueTime);
        Assert.Equal("16:00", itemVm.DueTimeString);

        // Kiểm tra trong database/repository
        var savedGoals = await _goalService.GetTodayGoalsAsync();
        var saved = savedGoals.First(g => g.Id == initialGoal.Id);
        Assert.Equal("Viết báo cáo hoàn chỉnh", saved.Text);
        Assert.Equal(new DateTime(2026, 10, 26, 16, 0, 0), saved.DueAt);
    }

    [Fact]
    public async Task MainViewModel_ReminderSettings_PersistsSuccessfully()
    {
        var mainVm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostart, _hotkey);
        await mainVm.InitializeAsync();

        // Thay đổi các cấu hình nhắc nhở
        mainVm.ReminderEnabled = true;
        mainVm.ReminderSoundEnabled = false;
        mainVm.RepeatReminderMinutes = 15;
        mainVm.EarlyReminderMinutes = 10;
        mainVm.DoNotDisturb = true;

        // Chờ tác vụ lưu ngầm hoàn tất trước khi đọc lại
        await mainVm.LastSettingsSaveTask;

        // Tải lại trực tiếp từ repository
        var reloadedSettings = await _goalService.GetSettingsAsync();
        Assert.True(reloadedSettings.ReminderEnabled);
        Assert.False(reloadedSettings.ReminderSoundEnabled);
        Assert.Equal(15, reloadedSettings.RepeatReminderMinutes);
        Assert.Equal(10, reloadedSettings.EarlyReminderMinutes);
        Assert.True(reloadedSettings.DoNotDisturb);
    }

    [Fact]
    public void ReminderConverters_ConvertFriendlyText()
    {
        var repeatConverter = new RepeatMinutesConverter();
        Assert.Equal("Tắt nhắc lặp lại", repeatConverter.Convert(0, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Mỗi 15 phút", repeatConverter.Convert(15, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));

        var earlyConverter = new EarlyMinutesConverter();
        Assert.Equal("Đúng giờ mới nhắc", earlyConverter.Convert(0, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("Trước 10 phút", earlyConverter.Convert(10, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void OverlayViewModel_DefaultState_IsExpanded()
    {
        var overlayVm = new OverlayViewModel(_goalService, _clock, _logger);
        Assert.False(overlayVm.IsCollapsed);
        Assert.True(overlayVm.IsExpanded);
    }

    [Fact]
    public void OverlayViewModel_ToggleCollapse_TogglesCorrectly()
    {
        var overlayVm = new OverlayViewModel(_goalService, _clock, _logger);

        // Ban đầu là bật ra (expanded)
        Assert.False(overlayVm.IsCollapsed);
        Assert.True(overlayVm.IsExpanded);

        // Bấm thu nhỏ (collapsed như iPhone Home button)
        overlayVm.ToggleCollapseCommand.Execute(null);
        Assert.True(overlayVm.IsCollapsed);
        Assert.False(overlayVm.IsExpanded);

        // Bấm mở rộng lại như cũ
        overlayVm.ToggleCollapseCommand.Execute(null);
        Assert.False(overlayVm.IsCollapsed);
        Assert.True(overlayVm.IsExpanded);
    }

    [Fact]
    public void SoundHelper_PlayNotificationChime_DoesNotThrow()
    {
        // Kiểm tra SoundHelper hoạt động không ném biệt lệ và không crash
        SoundHelper.PlayNotificationChime();
    }

    [Fact]
    public async Task OverlayViewModel_WhenNoGoals_HasNoGoalsIsTrue_AndNoSampleGoalsSeeded()
    {
        var overlayVm = new OverlayViewModel(_goalService, _clock, _logger);
        await overlayVm.LoadGoalsCommand.ExecuteAsync(null);

        // Khi hoàn toàn không có mục tiêu, không được tự seed dữ liệu mẫu
        Assert.Empty(overlayVm.TodayGoals);
        Assert.Empty(overlayVm.MonthGoals);
        Assert.False(overlayVm.HasGoals);
        Assert.True(overlayVm.HasNoGoals);
        Assert.False(overlayVm.HasTodayGoals);
        Assert.False(overlayVm.HasMonthGoals);
        Assert.False(overlayVm.ShowSeparator);
    }

    [Fact]
    public async Task OverlayViewModel_WhenGoalsExist_HasGoalsIsTrue_AndSeparatorVisibilityCorrect()
    {
        var overlayVm = new OverlayViewModel(_goalService, _clock, _logger);

        // 1. Chỉ có mục tiêu hôm nay
        await _goalService.AddDailyGoalAsync("Tập thể dục 30 phút");
        await overlayVm.LoadGoalsCommand.ExecuteAsync(null);

        Assert.Single(overlayVm.TodayGoals);
        Assert.Empty(overlayVm.MonthGoals);
        Assert.True(overlayVm.HasGoals);
        Assert.False(overlayVm.HasNoGoals);
        Assert.True(overlayVm.HasTodayGoals);
        Assert.False(overlayVm.HasMonthGoals);
        Assert.False(overlayVm.ShowSeparator); // Chưa hiện separator vì chưa có mục tiêu tháng

        // 2. Thêm mục tiêu tháng
        await _goalService.AddMonthlyGoalAsync("Đọc sách quản trị");
        await overlayVm.LoadGoalsCommand.ExecuteAsync(null);

        Assert.Single(overlayVm.TodayGoals);
        Assert.Single(overlayVm.MonthGoals);
        Assert.True(overlayVm.HasGoals);
        Assert.False(overlayVm.HasNoGoals);
        Assert.True(overlayVm.HasTodayGoals);
        Assert.True(overlayVm.HasMonthGoals);
        Assert.True(overlayVm.ShowSeparator); // Đã có cả 2 -> hiện separator
    }
}
