using System.Collections.Generic;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện kho lưu trữ dữ liệu mục tiêu và cài đặt của ứng dụng
/// </summary>
public interface IGoalRepository
{
    string BaseDirectory { get; }
    string GoalsFilePath { get; }
    string SettingsFilePath { get; }

    Task<List<Goal>> LoadGoalsAsync();
    Task SaveGoalsAsync(IEnumerable<Goal> goals);

    Task<List<RecurringGoal>> LoadRecurringGoalsAsync();
    Task SaveRecurringGoalsAsync(IEnumerable<RecurringGoal> recurringGoals);

    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
