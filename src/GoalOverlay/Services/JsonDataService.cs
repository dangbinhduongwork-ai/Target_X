using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Quản lý nạp và lưu dữ liệu mục tiêu bằng JSON tại %APPDATA%\GoalOverlay\goals.json
/// Mọi thao tác file đều có try-catch và ghi log qua ILoggerService
/// </summary>
public class JsonDataService : IDataService
{
    private readonly ILoggerService _logger;
    private readonly string _dataFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public string DataFilePath => _dataFilePath;

    public JsonDataService(ILoggerService logger)
    {
        _logger = logger;

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "GoalOverlay");

        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Không thể tạo thư mục lưu trữ dữ liệu tại '{folder}'", ex);
        }

        _dataFilePath = Path.Combine(folder, "goals.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<List<GoalItem>> LoadGoalsAsync()
    {
        try
        {
            if (!File.Exists(_dataFilePath))
            {
                _logger.Info($"File dữ liệu chưa tồn tại tại '{_dataFilePath}'. Tạo bộ mục tiêu mẫu ban đầu.");
                var defaultGoals = CreateDefaultGoals();
                await SaveGoalsAsync(defaultGoals);
                return defaultGoals;
            }

            string json = await File.ReadAllTextAsync(_dataFilePath);
            var goals = JsonSerializer.Deserialize<List<GoalItem>>(json, _jsonOptions);

            _logger.Info($"Đã tải thành công {goals?.Count ?? 0} mục tiêu từ '{_dataFilePath}'");
            return goals ?? new List<GoalItem>();
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi đọc file mục tiêu từ '{_dataFilePath}'", ex);
            return new List<GoalItem>();
        }
    }

    public async Task<bool> SaveGoalsAsync(IEnumerable<GoalItem> goals)
    {
        try
        {
            string json = JsonSerializer.Serialize(goals, _jsonOptions);

            string tempFile = _dataFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, _dataFilePath, overwrite: true);

            _logger.Info($"Đã lưu thành công dữ liệu mục tiêu vào '{_dataFilePath}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi ghi dữ liệu mục tiêu vào '{_dataFilePath}'", ex);
            return false;
        }
    }

    private static List<GoalItem> CreateDefaultGoals()
    {
        return new List<GoalItem>
        {
            new()
            {
                Title = "Uống 2 lít nước",
                TargetCount = 8,
                CurrentCount = 3,
                Type = GoalType.Daily,
                IsCompleted = false
            },
            new()
            {
                Title = "Tập thể dục 30 phút",
                TargetCount = 1,
                CurrentCount = 1,
                Type = GoalType.Daily,
                IsCompleted = true,
                CompletedAt = DateTime.Now
            },
            new()
            {
                Title = "Đọc xong 2 cuốn sách",
                TargetCount = 2,
                CurrentCount = 1,
                Type = GoalType.Monthly,
                IsCompleted = false
            },
            new()
            {
                Title = "Hoàn thành đồ án / công việc tháng",
                TargetCount = 10,
                CurrentCount = 6,
                Type = GoalType.Monthly,
                IsCompleted = false
            }
        };
    }
}
