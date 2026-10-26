using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Quản lý cấu hình ứng dụng lưu tại %APPDATA%\GoalOverlay\settings.json
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILoggerService _logger;
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public string SettingsFilePath => _settingsFilePath;
    public AppSettings CurrentSettings { get; private set; } = new();

    public void UpdateCurrentSettings(AppSettings settings)
    {
        CurrentSettings = settings;
    }

    public SettingsService(ILoggerService logger)
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
            _logger.Error($"Không thể tạo thư mục cho settings tại '{folder}'", ex);
        }

        _settingsFilePath = Path.Combine(folder, "settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.Info($"File cài đặt chưa tồn tại tại '{_settingsFilePath}'. Tạo cấu hình mặc định.");
                CurrentSettings = new AppSettings();
                await SaveSettingsAsync(CurrentSettings);
                return CurrentSettings;
            }

            string json = await File.ReadAllTextAsync(_settingsFilePath);
            CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            _logger.Info($"Đã tải thành công cấu hình ứng dụng từ '{_settingsFilePath}'");
            return CurrentSettings;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi tải cấu hình từ '{_settingsFilePath}'", ex);
            CurrentSettings = new AppSettings();
            return CurrentSettings;
        }
    }

    public async Task<bool> SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            CurrentSettings = settings;
            string json = JsonSerializer.Serialize(settings, _jsonOptions);

            string tempFile = _settingsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, _settingsFilePath, overwrite: true);

            _logger.Info($"Đã lưu thành công cấu hình vào '{_settingsFilePath}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi lưu cấu hình vào '{_settingsFilePath}'", ex);
            return false;
        }
    }
}
