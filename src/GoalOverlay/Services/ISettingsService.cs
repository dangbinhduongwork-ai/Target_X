using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ nạp và lưu trữ cấu hình người dùng
/// </summary>
public interface ISettingsService
{
    string SettingsFilePath { get; }
    AppSettings CurrentSettings { get; }
    Task<AppSettings> LoadSettingsAsync();
    Task<bool> SaveSettingsAsync(AppSettings settings);
}
