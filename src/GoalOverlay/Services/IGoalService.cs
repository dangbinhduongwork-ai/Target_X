using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ nghiệp vụ quản lý mục tiêu và thiết lập
/// </summary>
public interface IGoalService
{
    Task<IReadOnlyList<Goal>> GetTodayGoalsAsync();
    Task<IReadOnlyList<Goal>> GetCurrentMonthGoalsAsync();

    Task<Goal> AddDailyGoalAsync(string text, DateOnly? date = null, TimeOnly? reminderTime = null, DateTime? dueAt = null);
    Task<Goal> AddMonthlyGoalAsync(string text, string? month = null, DateTime? dueAt = null);

    Task<bool> UpdateGoalTextAsync(Guid id, string newText);
    Task<bool> UpdateGoalReminderAsync(Guid id, bool hasReminder, TimeOnly? reminderTime);
    Task<bool> SetDueTimeAsync(Guid id, DateTime? dueAt);
    Task<bool> MarkDoneAsync(Guid id, bool isDone);
    Task<bool> ToggleGoalAsync(Guid id);
    Task<bool> DeleteGoalAsync(Guid id);

    Task<int> CarryOverUnfinishedGoalsAsync();

    // Quản lý mục tiêu định kỳ lặp lại theo thứ trong tuần
    Task<IReadOnlyList<RecurringGoal>> GetRecurringGoalsAsync();
    Task<RecurringGoal> AddRecurringGoalAsync(string text, IEnumerable<DayOfWeek> daysOfWeek, TimeOnly? reminderTime = null);
    Task<bool> UpdateRecurringGoalAsync(RecurringGoal recurringGoal);
    Task<bool> ToggleRecurringGoalAsync(Guid id);
    Task<bool> DeleteRecurringGoalAsync(Guid id);
    Task<int> EnsureRecurringGoalsForTodayAsync();

    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
}
