using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Triển khai IGoalRepository lưu trữ dữ liệu bằng file JSON
/// Áp dụng cơ chế ghi an toàn (.tmp -> File.Replace -> giữ .bak) và tự động xử lý file hỏng (.corrupt-timestamp)
/// </summary>
public class JsonGoalRepository : IGoalRepository
{
    private readonly ILoggerService _logger;
    private readonly string _baseDirectory;
    private readonly string _goalsFilePath;
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public string BaseDirectory => _baseDirectory;
    public string GoalsFilePath => _goalsFilePath;
    public string SettingsFilePath => _settingsFilePath;

    public JsonGoalRepository(ILoggerService logger, string? baseDirectory = null)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseDirectory = Path.Combine(appData, "GoalOverlay");
        }
        else
        {
            _baseDirectory = baseDirectory;
        }

        try
        {
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Không thể tạo thư mục lưu trữ dữ liệu tại '{_baseDirectory}'", ex);
        }

        _goalsFilePath = Path.Combine(_baseDirectory, "goals.json");
        _settingsFilePath = Path.Combine(_baseDirectory, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<List<Goal>> LoadGoalsAsync()
    {
        if (!File.Exists(_goalsFilePath))
        {
            return new List<Goal>();
        }

        try
        {
            string json = await File.ReadAllTextAsync(_goalsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<Goal>();
            }

            var goals = JsonSerializer.Deserialize<List<Goal>>(json, _jsonOptions);
            return goals ?? new List<Goal>();
        }
        catch (JsonException jsonEx)
        {
            HandleCorruptFile(_goalsFilePath, jsonEx);
            return new List<Goal>();
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi đọc file '{_goalsFilePath}'", ex);
            return new List<Goal>();
        }
    }

    public async Task SaveGoalsAsync(IEnumerable<Goal> goals)
    {
        string json = JsonSerializer.Serialize(goals, _jsonOptions);
        await SafeWriteFileAsync(_goalsFilePath, json);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
        {
            var defaultSettings = new AppSettings();
            await SaveSettingsAsync(defaultSettings);
            return defaultSettings;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_settingsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppSettings();
            }

            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            return settings ?? new AppSettings();
        }
        catch (JsonException jsonEx)
        {
            HandleCorruptFile(_settingsFilePath, jsonEx);
            return new AppSettings();
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi đọc cấu hình từ '{_settingsFilePath}'", ex);
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, _jsonOptions);
        await SafeWriteFileAsync(_settingsFilePath, json);
    }

    /// <summary>
    /// Ghi file an toàn:
    /// 1. Ghi ra file .tmp
    /// 2. Dùng File.Replace để tráo đổi nguyên tử và giữ lại 1 bản sao lưu .bak
    /// 3. Nếu file đích chưa tồn tại thì Move trực tiếp
    /// </summary>
    private async Task SafeWriteFileAsync(string destinationPath, string content)
    {
        string tempFile = destinationPath + ".tmp";
        string backupFile = destinationPath + ".bak";

        try
        {
            // Đảm bảo thư mục cha tồn tại
            string? parentDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            // 1. Ghi nội dung ra file tạm .tmp
            await File.WriteAllTextAsync(tempFile, content);

            // 2. Thực hiện tráo đổi an toàn và lưu bản .bak
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Replace(tempFile, destinationPath, backupFile, ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    // Fallback cho môi trường không hỗ trợ Replace nguyên tử
                    File.Copy(destinationPath, backupFile, overwrite: true);
                    File.Move(tempFile, destinationPath, overwrite: true);
                }
            }
            else
            {
                File.Move(tempFile, destinationPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi nghiêm trọng khi ghi file an toàn tới '{destinationPath}'", ex);
            // Dọn dẹp file tạm nếu còn sót lại
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Bỏ qua lỗi dọn file tạm
            }

            throw;
        }
    }

    /// <summary>
    /// Xử lý khi phát hiện file JSON bị hỏng (corrupted):
    /// Đổi tên file lỗi thành [filename].corrupt-[timestamp] để bảo lưu bằng chứng và khởi tạo lại dữ liệu rỗng
    /// </summary>
    private void HandleCorruptFile(string filePath, Exception cause)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string corruptPath = $"{filePath}.corrupt-{timestamp}";

            if (File.Exists(filePath))
            {
                File.Move(filePath, corruptPath, overwrite: true);
            }

            _logger.Error($"Phát hiện file JSON bị hỏng '{filePath}'. Đã đổi tên thành '{corruptPath}' và khởi tạo lại dữ liệu rỗng.", cause);
        }
        catch (Exception ex)
        {
            _logger.Error($"Không thể đổi tên file hỏng '{filePath}'", ex);
        }
    }
}
