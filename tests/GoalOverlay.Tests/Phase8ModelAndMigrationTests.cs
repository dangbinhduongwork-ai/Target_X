using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

/// <summary>
/// Bộ kiểm thử Unit Test cho Giai đoạn 8 (V1.1 Model Expansion & Data Migration)
/// Đảm bảo:
/// 1. Tương thích ngược đọc file goals.json V1.0 (mảng phẳng không có trường mới)
/// 2. Nâng cấp an toàn lên SchemaVersion 2 và tạo bản sao lưu goals.v1.bak khi ghi lần đầu
/// 3. Carry-over từ hôm qua sang hôm nay bỏ giờ (DueAt = null) và reset toàn bộ trường nhắc
/// 4. Sắp xếp mục tiêu có giờ hẹn lên trước (tăng dần theo DueAt), không có giờ xếp sau theo CreatedAt
/// 5. SetDueTimeAsync và MarkDoneAsync reset SnoozedUntil và cập nhật ReminderDismissed
/// 6. Cấu hình AppSettings đầy đủ các trường mới của V1.1
/// 7. Khả năng chống chịu file hỏng (corrupt file resilience)
/// </summary>
public class Phase8ModelAndMigrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _goalService;

    public Phase8ModelAndMigrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_Phase8Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        _clock = new TestClock(new DateTime(2026, 10, 26, 9, 0, 0));
        _repository = new JsonGoalRepository(_logger, _testDirectory);
        _goalService = new GoalService(_repository, _clock, _logger);
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
    public async Task ReadV1JsonArray_LoadsSuccessfully_WithDefaultValuesForNewFields()
    {
        // 1. Giả lập file goals.json chuẩn V1.0 (không có SchemaVersion hay các trường nhắc nhở mới)
        string v1Json = @"[
            {
                ""Id"": ""aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"",
                ""Text"": ""Mục tiêu chuẩn V1"",
                ""Type"": 0,
                ""Date"": ""2026-10-26"",
                ""Month"": """",
                ""IsDone"": false,
                ""CreatedAt"": ""2026-10-26T07:00:00Z""
            }
        ]";
        string goalsFile = Path.Combine(_testDirectory, "goals.json");
        await File.WriteAllTextAsync(goalsFile, v1Json);

        // 2. Nạp dữ liệu qua repository
        var goals = await _repository.LoadGoalsAsync();

        // 3. Kiểm tra mục tiêu nạp thành công và các trường mới mang giá trị mặc định an toàn
        Assert.Single(goals);
        var goal = goals[0];
        Assert.Equal("Mục tiêu chuẩn V1", goal.Text);
        Assert.Null(goal.DueAt);
        Assert.Null(goal.SnoozedUntil);
        Assert.Null(goal.LastNotifiedAt);
        Assert.Equal(0, goal.NotifyCount);
        Assert.False(goal.ReminderDismissed);
    }

    [Fact]
    public async Task SaveV1JsonArray_UpgradesToSchemaVersion2_AndCreatesV1Backup()
    {
        // 1. Chuẩn bị file V1.0 ban đầu
        string v1Json = @"[
            {
                ""Id"": ""bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"",
                ""Text"": ""Mục tiêu V1 trước nâng cấp"",
                ""Type"": 0,
                ""Date"": ""2026-10-26"",
                ""Month"": """",
                ""IsDone"": false,
                ""CreatedAt"": ""2026-10-26T07:00:00Z""
            }
        ]";
        string goalsFile = Path.Combine(_testDirectory, "goals.json");
        string v1BakFile = Path.Combine(_testDirectory, "goals.v1.bak");
        await File.WriteAllTextAsync(goalsFile, v1Json);

        // 2. Đọc và thực hiện ghi lần đầu
        var goals = await _repository.LoadGoalsAsync();
        Assert.Single(goals);

        // Thêm trường DueAt cho mục tiêu
        goals[0].DueAt = new DateTime(2026, 10, 26, 14, 0, 0);
        await _repository.SaveGoalsAsync(goals);

        // 3. Kiểm tra file backup goals.v1.bak được tạo và giữ nguyên định dạng V1.0 gốc
        Assert.True(File.Exists(v1BakFile), "Bản sao lưu goals.v1.bak phải tồn tại sau khi nâng cấp.");
        string bakContent = await File.ReadAllTextAsync(v1BakFile);
        Assert.Contains("Mục tiêu V1 trước nâng cấp", bakContent);
        Assert.StartsWith("[", bakContent.TrimStart()); // V1 gốc là mảng JSON

        // 4. Kiểm tra file chính goals.json đã được nâng cấp lên SchemaVersion 2
        string upgradedContent = await File.ReadAllTextAsync(goalsFile);
        Assert.Contains("\"SchemaVersion\": 2", upgradedContent);
        Assert.Contains("\"Goals\":", upgradedContent);

        // 5. Đọc lại và xác nhận dữ liệu toàn vẹn
        var reloaded = await _repository.LoadGoalsAsync();
        Assert.Single(reloaded);
        Assert.Equal("Mục tiêu V1 trước nâng cấp", reloaded[0].Text);
        Assert.Equal(new DateTime(2026, 10, 26, 14, 0, 0), reloaded[0].DueAt);
    }

    [Fact]
    public async Task SubsequentSave_DoesNotOverwriteExistingV1Bak()
    {
        // 1. Tạo file V1.0 ban đầu
        string originalV1Json = @"[{""Id"":""cccccccc-cccc-cccc-cccc-cccccccccccc"",""Text"":""Original V1"",""Type"":0,""Date"":""2026-10-26"",""Month"":"""",""IsDone"":false,""CreatedAt"":""2026-10-26T07:00:00Z""}]";
        string goalsFile = Path.Combine(_testDirectory, "goals.json");
        string v1BakFile = Path.Combine(_testDirectory, "goals.v1.bak");
        await File.WriteAllTextAsync(goalsFile, originalV1Json);

        // Lần ghi 1 -> Nâng cấp lên V2 và tạo goals.v1.bak
        var goals1 = await _repository.LoadGoalsAsync();
        await _repository.SaveGoalsAsync(goals1);
        Assert.True(File.Exists(v1BakFile));

        // Lần ghi 2 -> Ghi thêm mục tiêu mới vào V2
        var goals2 = await _repository.LoadGoalsAsync();
        goals2.Add(new Goal { Text = "Mục tiêu V2 mới", Date = new DateOnly(2026, 10, 26) });
        await _repository.SaveGoalsAsync(goals2);

        // File goals.v1.bak KHÔNG được ghi đè bởi dữ liệu lần 2
        string bakContent = await File.ReadAllTextAsync(v1BakFile);
        Assert.Contains("Original V1", bakContent);
        Assert.DoesNotContain("Mục tiêu V2 mới", bakContent);
    }

    [Fact]
    public async Task CarryOverUnfinishedGoals_ClearsDueAt_AndResetsReminderFields()
    {
        // 1. Tạo mục tiêu hôm qua chưa hoàn thành kèm hẹn giờ và trạng thái nhắc nhở
        var yesterday = new DateOnly(2026, 10, 25);
        _clock.Set(new DateTime(2026, 10, 25, 10, 0, 0));

        var goal = await _goalService.AddDailyGoalAsync(
            "Mục tiêu dở dang hôm qua",
            date: yesterday,
            dueAt: new DateTime(2026, 10, 25, 16, 30, 0));

        // Cập nhật các trường nhắc nhở giả định đã kích hoạt hôm qua
        var allGoals = await _repository.LoadGoalsAsync();
        var target = allGoals.First(g => g.Id == goal.Id);
        target.SnoozedUntil = new DateTime(2026, 10, 25, 17, 0, 0);
        target.LastNotifiedAt = new DateTime(2026, 10, 25, 16, 30, 0);
        target.NotifyCount = 2;
        target.ReminderDismissed = true;
        await _repository.SaveGoalsAsync(allGoals);

        // 2. Chuyển sang ngày hôm nay và kích hoạt carry-over
        _clock.Set(new DateTime(2026, 10, 26, 8, 0, 0));
        int carriedCount = await _goalService.CarryOverUnfinishedGoalsAsync();
        Assert.Equal(1, carriedCount);

        // 3. Kiểm tra mục tiêu: ngày hôm nay, DueAt bị xóa (null), toàn bộ trường nhắc được reset
        var todayGoals = await _goalService.GetTodayGoalsAsync();
        var carriedGoal = todayGoals.First(g => g.Id == goal.Id);

        Assert.Equal(new DateOnly(2026, 10, 26), carriedGoal.Date);
        Assert.Null(carriedGoal.DueAt); // Bỏ giờ
        Assert.Null(carriedGoal.SnoozedUntil); // Reset snooze
        Assert.Null(carriedGoal.LastNotifiedAt); // Reset last notified
        Assert.Equal(0, carriedGoal.NotifyCount); // Reset số lần thông báo
        Assert.False(carriedGoal.ReminderDismissed); // Reset dismissed
    }

    [Fact]
    public async Task Sorting_GoalsWithDueTimeFirstAscending_GoalsWithoutDueTimeSecondByCreatedAt()
    {
        // Tạo các mục tiêu có giờ và không có giờ xen kẽ
        var today = new DateOnly(2026, 10, 26);

        // Mục tiêu 1: Không hẹn giờ, tạo lúc 08:00
        _clock.Set(new DateTime(2026, 10, 26, 8, 0, 0));
        var gNoDue1 = await _goalService.AddDailyGoalAsync("Không giờ 1 (tạo 08:00)", date: today);

        // Mục tiêu 2: Hẹn giờ 15:00, tạo lúc 08:30
        _clock.Set(new DateTime(2026, 10, 26, 8, 30, 0));
        var gDue15 = await _goalService.AddDailyGoalAsync("Hẹn 15:00", date: today, dueAt: new DateTime(2026, 10, 26, 15, 0, 0));

        // Mục tiêu 3: Hẹn giờ 10:00, tạo lúc 09:00
        _clock.Set(new DateTime(2026, 10, 26, 9, 0, 0));
        var gDue10 = await _goalService.AddDailyGoalAsync("Hẹn 10:00", date: today, dueAt: new DateTime(2026, 10, 26, 10, 0, 0));

        // Mục tiêu 4: Không hẹn giờ, tạo lúc 07:30
        _clock.Set(new DateTime(2026, 10, 26, 7, 30, 0));
        var gNoDue2 = await _goalService.AddDailyGoalAsync("Không giờ 2 (tạo 07:30)", date: today);

        // Mục tiêu 5: Hẹn giờ 11:30, tạo lúc 09:30
        _clock.Set(new DateTime(2026, 10, 26, 9, 30, 0));
        var gDue11 = await _goalService.AddDailyGoalAsync("Hẹn 11:30", date: today, dueAt: new DateTime(2026, 10, 26, 11, 30, 0));

        // Lấy danh sách mục tiêu hôm nay
        var sorted = await _goalService.GetTodayGoalsAsync();

        // Thứ tự kỳ vọng:
        // 1. gDue10 (10:00)
        // 2. gDue11 (11:30)
        // 3. gDue15 (15:00)
        // 4. gNoDue2 (CreatedAt: 07:30)
        // 5. gNoDue1 (CreatedAt: 08:00)
        Assert.Equal(5, sorted.Count);
        Assert.Equal(gDue10.Id, sorted[0].Id);
        Assert.Equal(gDue11.Id, sorted[1].Id);
        Assert.Equal(gDue15.Id, sorted[2].Id);
        Assert.Equal(gNoDue2.Id, sorted[3].Id);
        Assert.Equal(gNoDue1.Id, sorted[4].Id);
    }

    [Fact]
    public async Task SetDueTimeAsync_UpdatesDueAt_AndResetsReminderFields()
    {
        var goal = await _goalService.AddDailyGoalAsync("Cần đổi giờ hẹn");

        // Giả lập mục tiêu đang có snooze và dismissal
        var allGoals = await _repository.LoadGoalsAsync();
        var target = allGoals.First(g => g.Id == goal.Id);
        target.SnoozedUntil = DateTime.UtcNow.AddMinutes(10);
        target.ReminderDismissed = true;
        target.NotifyCount = 3;
        target.LastNotifiedAt = DateTime.UtcNow;
        await _repository.SaveGoalsAsync(allGoals);

        // Cập nhật DueTime mới
        var newDue = new DateTime(2026, 10, 26, 16, 45, 0);
        bool success = await _goalService.SetDueTimeAsync(goal.Id, newDue);
        Assert.True(success);

        // Kiểm tra
        var reloaded = await _goalService.GetTodayGoalsAsync();
        var updated = reloaded.First(g => g.Id == goal.Id);
        Assert.Equal(newDue, updated.DueAt);
        Assert.Null(updated.SnoozedUntil);
        Assert.Null(updated.LastNotifiedAt);
        Assert.Equal(0, updated.NotifyCount);
        Assert.False(updated.ReminderDismissed);

        // Xóa DueTime (set null)
        await _goalService.SetDueTimeAsync(goal.Id, null);
        var cleared = (await _goalService.GetTodayGoalsAsync()).First(g => g.Id == goal.Id);
        Assert.Null(cleared.DueAt);
    }

    [Fact]
    public async Task MarkDone_ResetsSnooze_AndSetsReminderDismissedTrue()
    {
        var goal = await _goalService.AddDailyGoalAsync("Mục tiêu thử nghiệm MarkDone", dueAt: new DateTime(2026, 10, 26, 10, 0, 0));

        // Đặt snooze
        var allGoals = await _repository.LoadGoalsAsync();
        var target = allGoals.First(g => g.Id == goal.Id);
        target.SnoozedUntil = new DateTime(2026, 10, 26, 10, 15, 0);
        target.ReminderDismissed = false;
        await _repository.SaveGoalsAsync(allGoals);

        // Đánh dấu hoàn thành qua MarkDoneAsync
        bool doneSuccess = await _goalService.MarkDoneAsync(goal.Id, true);
        Assert.True(doneSuccess);

        var updated1 = (await _goalService.GetTodayGoalsAsync()).First(g => g.Id == goal.Id);
        Assert.True(updated1.IsDone);
        Assert.Null(updated1.SnoozedUntil);
        Assert.True(updated1.ReminderDismissed);

        // Thử lại với ToggleGoalAsync: chuyển về chưa xong rồi chuyển sang hoàn thành
        await _goalService.ToggleGoalAsync(goal.Id); // về false
        var unDone = (await _goalService.GetTodayGoalsAsync()).First(g => g.Id == goal.Id);
        Assert.False(unDone.IsDone);

        // Giả lập snooze lại khi chưa xong
        var repoGoals = await _repository.LoadGoalsAsync();
        var reTarget = repoGoals.First(g => g.Id == goal.Id);
        reTarget.SnoozedUntil = DateTime.UtcNow.AddMinutes(5);
        reTarget.ReminderDismissed = false;
        await _repository.SaveGoalsAsync(repoGoals);

        // Toggle sang hoàn thành
        await _goalService.ToggleGoalAsync(goal.Id);
        var toggledDone = (await _goalService.GetTodayGoalsAsync()).First(g => g.Id == goal.Id);
        Assert.True(toggledDone.IsDone);
        Assert.Null(toggledDone.SnoozedUntil);
        Assert.True(toggledDone.ReminderDismissed);
    }

    [Fact]
    public async Task AppSettings_HasAllPhase8ReminderSettings_WithCorrectDefaults()
    {
        var settings = new AppSettings();

        // Kiểm tra toàn bộ giá trị mặc định theo yêu cầu của Giai đoạn 8
        Assert.True(settings.ReminderEnabled);
        Assert.True(settings.ReminderSoundEnabled);
        Assert.Equal(new List<int> { 5, 10, 30, 60 }, settings.SnoozeOptionsMinutes);
        Assert.Equal(0, settings.RepeatReminderMinutes);
        Assert.Equal(0, settings.EarlyReminderMinutes);
        Assert.False(settings.DoNotDisturb);

        // Lưu và đọc lại để đảm bảo serialization roundtrip
        settings.ReminderSoundEnabled = false;
        settings.RepeatReminderMinutes = 15;
        settings.EarlyReminderMinutes = 5;
        settings.DoNotDisturb = true;
        settings.SnoozeOptionsMinutes = new List<int> { 10, 20, 45 };

        await _repository.SaveSettingsAsync(settings);
        var loaded = await _repository.LoadSettingsAsync();

        Assert.True(loaded.ReminderEnabled);
        Assert.False(loaded.ReminderSoundEnabled);
        Assert.Equal(15, loaded.RepeatReminderMinutes);
        Assert.Equal(5, loaded.EarlyReminderMinutes);
        Assert.True(loaded.DoNotDisturb);
        Assert.Equal(new List<int> { 10, 20, 45 }, loaded.SnoozeOptionsMinutes);
    }

    [Fact]
    public async Task CorruptJsonFile_RenamesToCorruptAndReturnsEmpty_UnderV2()
    {
        string goalsFile = Path.Combine(_testDirectory, "goals.json");
        string corruptedJson = "{ \"SchemaVersion\": 2, \"Goals\": [ { \"Text\": \"Dang do thi cut dien... ";
        await File.WriteAllTextAsync(goalsFile, corruptedJson);

        var loaded = await _repository.LoadGoalsAsync();

        Assert.NotNull(loaded);
        Assert.Empty(loaded);

        // File hỏng được đổi tên thành .corrupt-*
        var corruptFiles = Directory.GetFiles(_testDirectory, "goals.json.corrupt-*");
        Assert.NotEmpty(corruptFiles);
        string corruptContent = await File.ReadAllTextAsync(corruptFiles[0]);
        Assert.Equal(corruptedJson, corruptContent);
    }
}
