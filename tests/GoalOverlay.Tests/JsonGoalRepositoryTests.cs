using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;
using GoalOverlay.Services;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

public class JsonGoalRepositoryTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly JsonGoalRepository _repository;

    public JsonGoalRepositoryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_RepoTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
        _logger = new TestLogger();
        _repository = new JsonGoalRepository(_logger, _testDirectory);
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
    public async Task SafeWrite_CreatesJsonAndBackup_ReadsBackCorrectly()
    {
        // 1. Ghi lần đầu
        var initialGoals = new List<Goal>
        {
            new() { Text = "Mục tiêu 1", Date = new DateOnly(2026, 10, 26) }
        };
        await _repository.SaveGoalsAsync(initialGoals);

        Assert.True(File.Exists(_repository.GoalsFilePath));

        // Đọc lại lần 1
        var loaded1 = await _repository.LoadGoalsAsync();
        Assert.Single(loaded1);
        Assert.Equal("Mục tiêu 1", loaded1[0].Text);

        // 2. Ghi lần 2 để kích hoạt cơ chế backup .bak
        var updatedGoals = new List<Goal>
        {
            new() { Text = "Mục tiêu 1", Date = new DateOnly(2026, 10, 26) },
            new() { Text = "Mục tiêu 2", Date = new DateOnly(2026, 10, 26) }
        };
        await _repository.SaveGoalsAsync(updatedGoals);

        // Kiểm tra file .bak được tạo
        string backupFile = _repository.GoalsFilePath + ".bak";
        Assert.True(File.Exists(backupFile), "File .bak phải được tạo khi ghi đè file có sẵn.");

        // File chính phải chứa 2 mục tiêu
        var loaded2 = await _repository.LoadGoalsAsync();
        Assert.Equal(2, loaded2.Count);

        // File backup phải chứa nội dung cũ (1 mục tiêu)
        string bakContent = await File.ReadAllTextAsync(backupFile);
        Assert.Contains("Mục tiêu 1", bakContent);
        Assert.DoesNotContain("Mục tiêu 2", bakContent);
    }

    [Fact]
    public async Task CorruptJsonFile_RenamesToCorruptAndReturnsEmpty_WithoutCrash()
    {
        // Cố tình ghi nội dung JSON hỏng
        string invalidJson = "{\"invalid_json_missing_brace: 12345";
        await File.WriteAllTextAsync(_repository.GoalsFilePath, invalidJson);

        // Nạp dữ liệu: không được ném exception (không crash)
        var goals = await _repository.LoadGoalsAsync();

        Assert.NotNull(goals);
        Assert.Empty(goals);

        // File gốc bị đổi tên thành .corrupt-*
        var corruptFiles = Directory.GetFiles(_testDirectory, "goals.json.corrupt-*");
        Assert.NotEmpty(corruptFiles);

        // File hỏng chứa đúng nội dung lỗi đã ghi
        string corruptContent = await File.ReadAllTextAsync(corruptFiles[0]);
        Assert.Equal(invalidJson, corruptContent);
    }

    [Fact]
    public async Task CorruptSettingsFile_RenamesToCorruptAndReturnsDefault_WithoutCrash()
    {
        // Cố tình ghi file settings.json bị hỏng
        string invalidJson = "<<NOT A JSON>>";
        await File.WriteAllTextAsync(_repository.SettingsFilePath, invalidJson);

        var settings = await _repository.LoadSettingsAsync();

        Assert.NotNull(settings);
        // Trả về giá trị mặc định của AppSettings
        Assert.Equal(ScreenCorner.TopRight, settings.Corner);
        Assert.Equal(0.95, settings.Opacity);

        // Đã đổi tên file hỏng
        var corruptFiles = Directory.GetFiles(_testDirectory, "settings.json.corrupt-*");
        Assert.NotEmpty(corruptFiles);
    }

    [Fact]
    public async Task SaveSettings_PreservesAndUpdatesValues()
    {
        var settings = new AppSettings
        {
            Opacity = 0.8,
            FontSize = 15.0,
            Corner = ScreenCorner.BottomLeft,
            CarryOverUnfinished = false
        };

        await _repository.SaveSettingsAsync(settings);

        var loaded = await _repository.LoadSettingsAsync();

        Assert.Equal(0.8, loaded.Opacity);
        Assert.Equal(15.0, loaded.FontSize);
        Assert.Equal(ScreenCorner.BottomLeft, loaded.Corner);
        Assert.False(loaded.CarryOverUnfinished);
    }
}
