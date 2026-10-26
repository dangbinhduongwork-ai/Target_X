using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Triển khai dịch vụ nghiệp vụ quản lý mục tiêu
/// Tích hợp IClock để hỗ trợ kiểm thử chuyển ngày/tháng và cơ chế Carry-over mục tiêu chưa hoàn thành
/// </summary>
public class GoalService : IGoalService
{
    private readonly IGoalRepository _repository;
    private readonly IClock _clock;
    private readonly ILoggerService _logger;

    public GoalService(IGoalRepository repository, IClock clock, ILoggerService logger)
    {
        _repository = repository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Goal>> GetTodayGoalsAsync()
    {
        try
        {
            await EnsureRecurringGoalsForTodayAsync();

            var allGoals = await _repository.LoadGoalsAsync();
            DateOnly today = _clock.Today;

            // Sắp xếp: mục tiêu có giờ lên trước (tăng dần theo giờ), không có giờ xếp sau theo CreatedAt
            var todayGoals = allGoals
                .Where(g => g.Type == GoalType.Daily && g.Date == today)
                .OrderBy(g => g.DueAt.HasValue ? 0 : 1)
                .ThenBy(g => g.DueAt)
                .ThenBy(g => g.CreatedAt)
                .ToList();

            return todayGoals;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi lấy danh sách mục tiêu hôm nay", ex);
            return Array.Empty<Goal>();
        }
    }

    public async Task<IReadOnlyList<Goal>> GetCurrentMonthGoalsAsync()
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            string currentMonth = _clock.CurrentMonth;

            // Sắp xếp: mục tiêu có giờ/hạn chót lên trước (tăng dần theo DueAt), không có giờ xếp sau theo CreatedAt
            var monthGoals = allGoals
                .Where(g => g.Type == GoalType.Monthly && g.Month == currentMonth)
                .OrderBy(g => g.DueAt.HasValue ? 0 : 1)
                .ThenBy(g => g.DueAt)
                .ThenBy(g => g.CreatedAt)
                .ToList();

            return monthGoals;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi lấy danh sách mục tiêu tháng này", ex);
            return Array.Empty<Goal>();
        }
    }

    public async Task<Goal> AddDailyGoalAsync(string text, DateOnly? date = null, TimeOnly? reminderTime = null, DateTime? dueAt = null)
    {
        try
        {
            string cleanText = (text ?? string.Empty).Trim();
            if (cleanText.Length > 120)
            {
                cleanText = cleanText[..120];
            }

            DateOnly targetDate = date ?? _clock.Today;
            DateTime? calculatedDueAt = dueAt;
            if (!calculatedDueAt.HasValue && reminderTime.HasValue)
            {
                calculatedDueAt = targetDate.ToDateTime(reminderTime.Value);
            }

            var allGoals = await _repository.LoadGoalsAsync();
            var newGoal = new Goal
            {
                Id = Guid.NewGuid(),
                Text = cleanText,
                Type = GoalType.Daily,
                Date = targetDate,
                Month = string.Empty,
                IsDone = false,
                CreatedAt = _clock.Now,
                DueAt = calculatedDueAt,
                SnoozedUntil = null,
                LastNotifiedAt = null,
                NotifyCount = 0,
                ReminderDismissed = false
            };

            allGoals.Add(newGoal);
            await _repository.SaveGoalsAsync(allGoals);

            _logger.Info($"Đã thêm mục tiêu ngày mới '{newGoal.Text}' cho ngày {newGoal.Date}" +
                         (newGoal.DueAt.HasValue ? $" kèm hẹn giờ {newGoal.DueAt:HH:mm}" : string.Empty));
            return newGoal;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi thêm mục tiêu ngày '{text}'", ex);
            throw;
        }
    }

    public async Task<bool> UpdateGoalReminderAsync(Guid id, bool hasReminder, TimeOnly? reminderTime)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var target = allGoals.FirstOrDefault(g => g.Id == id);
            if (target == null)
            {
                _logger.Warn($"Không tìm thấy mục tiêu {id} để cập nhật hẹn giờ.");
                return false;
            }

            if (hasReminder && reminderTime.HasValue)
            {
                DateOnly baseDate = target.Type == GoalType.Daily && target.Date != default ? target.Date : _clock.Today;
                target.DueAt = baseDate.ToDateTime(reminderTime.Value);
            }
            else
            {
                target.DueAt = null;
            }

            target.ReminderDismissed = false;
            target.SnoozedUntil = null;
            target.LastNotifiedAt = null;
            target.NotifyCount = 0;

            await _repository.SaveGoalsAsync(allGoals);
            _logger.Info($"Đã cập nhật hẹn giờ mục tiêu '{target.Text}': HasReminder={hasReminder}, DueAt={target.DueAt}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi cập nhật hẹn giờ mục tiêu {id}", ex);
            return false;
        }
    }

    public async Task<bool> SetDueTimeAsync(Guid id, DateTime? dueAt)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var target = allGoals.FirstOrDefault(g => g.Id == id);
            if (target == null)
            {
                _logger.Warn($"Không tìm thấy mục tiêu {id} để cập nhật DueTime.");
                return false;
            }

            target.DueAt = dueAt;
            target.SnoozedUntil = null;
            target.LastNotifiedAt = null;
            target.NotifyCount = 0;
            target.ReminderDismissed = false;

            await _repository.SaveGoalsAsync(allGoals);
            _logger.Info($"Đã cập nhật DueTime cho mục tiêu '{target.Text}': {dueAt}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi cập nhật DueTime mục tiêu {id}", ex);
            return false;
        }
    }

    public async Task<Goal> AddMonthlyGoalAsync(string text, string? month = null, DateTime? dueAt = null)
    {
        try
        {
            string cleanText = (text ?? string.Empty).Trim();
            if (cleanText.Length > 120)
            {
                cleanText = cleanText[..120];
            }

            var allGoals = await _repository.LoadGoalsAsync();
            var newGoal = new Goal
            {
                Id = Guid.NewGuid(),
                Text = cleanText,
                Type = GoalType.Monthly,
                Date = default,
                Month = string.IsNullOrWhiteSpace(month) ? _clock.CurrentMonth : month,
                IsDone = false,
                CreatedAt = _clock.Now,
                DueAt = dueAt,
                SnoozedUntil = null,
                LastNotifiedAt = null,
                NotifyCount = 0,
                ReminderDismissed = false
            };

            allGoals.Add(newGoal);
            await _repository.SaveGoalsAsync(allGoals);

            _logger.Info($"Đã thêm mục tiêu tháng mới '{newGoal.Text}' cho tháng {newGoal.Month}" +
                         (newGoal.DueAt.HasValue ? $" kèm hạn {newGoal.DueAt:yyyy-MM-dd HH:mm}" : string.Empty));
            return newGoal;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi thêm mục tiêu tháng '{text}'", ex);
            throw;
        }
    }

    public async Task<bool> UpdateGoalTextAsync(Guid id, string newText)
    {
        try
        {
            string cleanText = (newText ?? string.Empty).Trim();
            if (cleanText.Length > 120)
            {
                cleanText = cleanText[..120];
            }

            var allGoals = await _repository.LoadGoalsAsync();
            var goal = allGoals.FirstOrDefault(g => g.Id == id);
            if (goal == null)
            {
                _logger.Warn($"Không tìm thấy mục tiêu {id} để sửa tiêu đề.");
                return false;
            }

            goal.Text = cleanText;
            await _repository.SaveGoalsAsync(allGoals);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi sửa mục tiêu {id}", ex);
            return false;
        }
    }

    public async Task<bool> MarkDoneAsync(Guid id, bool isDone)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var goal = allGoals.FirstOrDefault(g => g.Id == id);
            if (goal == null)
            {
                _logger.Warn($"Không tìm thấy mục tiêu {id} để cập nhật trạng thái hoàn thành.");
                return false;
            }

            goal.IsDone = isDone;
            if (isDone)
            {
                goal.SnoozedUntil = null;
                goal.ReminderDismissed = true;
            }

            await _repository.SaveGoalsAsync(allGoals);
            _logger.Info($"Đã cập nhật trạng thái mục tiêu '{goal.Text}' thành: {(goal.IsDone ? "Hoàn thành" : "Chưa xong")}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi cập nhật trạng thái hoàn thành mục tiêu {id}", ex);
            return false;
        }
    }

    public async Task<bool> ToggleGoalAsync(Guid id)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var goal = allGoals.FirstOrDefault(g => g.Id == id);
            if (goal == null)
            {
                _logger.Warn($"Không tìm thấy mục tiêu {id} để đổi trạng thái hoàn thành.");
                return false;
            }

            goal.IsDone = !goal.IsDone;
            if (goal.IsDone)
            {
                goal.SnoozedUntil = null;
                goal.ReminderDismissed = true;
            }

            await _repository.SaveGoalsAsync(allGoals);
            _logger.Info($"Đã đổi trạng thái mục tiêu '{goal.Text}' thành: {(goal.IsDone ? "Hoàn thành" : "Chưa xong")}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi chuyển đổi trạng thái mục tiêu {id}", ex);
            return false;
        }
    }

    public async Task<bool> DeleteGoalAsync(Guid id)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            int removed = allGoals.RemoveAll(g => g.Id == id);
            if (removed > 0)
            {
                await _repository.SaveGoalsAsync(allGoals);
                _logger.Info($"Đã xóa thành công mục tiêu {id}");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi xóa mục tiêu {id}", ex);
            return false;
        }
    }

    public async Task<int> CarryOverUnfinishedGoalsAsync()
    {
        try
        {
            var settings = await _repository.LoadSettingsAsync();
            if (!settings.CarryOverUnfinished)
            {
                return 0;
            }

            var allGoals = await _repository.LoadGoalsAsync();
            DateOnly today = _clock.Today;

            // Tìm các mục tiêu hàng ngày trước ngày hôm nay mà chưa hoàn thành (IsDone == false)
            var unfinishedOldGoals = allGoals
                .Where(g => g.Type == GoalType.Daily && !g.IsDone && g.Date < today)
                .ToList();

            if (unfinishedOldGoals.Count == 0)
            {
                return 0;
            }

            foreach (var goal in unfinishedOldGoals)
            {
                goal.Date = today;
                goal.DueAt = null; // Bỏ giờ khi carry-over sang hôm nay
                goal.SnoozedUntil = null;
                goal.LastNotifiedAt = null;
                goal.NotifyCount = 0;
                goal.ReminderDismissed = false;
            }

            await _repository.SaveGoalsAsync(allGoals);
            _logger.Info($"Đã chuyển tiếp {unfinishedOldGoals.Count} mục tiêu chưa xong sang ngày hôm nay ({today}) - Đã bỏ DueAt và reset các trường nhắc");

            return unfinishedOldGoals.Count;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi thực hiện chuyển tiếp mục tiêu chưa hoàn thành sang hôm nay", ex);
            return 0;
        }
    }

    #region Quản lý Mục tiêu Định kỳ (Recurring Goals theo thứ trong tuần)

    public async Task<IReadOnlyList<RecurringGoal>> GetRecurringGoalsAsync()
    {
        try
        {
            var list = await _repository.LoadRecurringGoalsAsync();
            return list.OrderByDescending(r => r.CreatedAt).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi lấy danh sách mục tiêu lặp lại", ex);
            return Array.Empty<RecurringGoal>();
        }
    }

    public async Task<RecurringGoal> AddRecurringGoalAsync(string text, IEnumerable<DayOfWeek> daysOfWeek, TimeOnly? reminderTime = null)
    {
        try
        {
            string cleanText = (text ?? string.Empty).Trim();
            if (cleanText.Length > 120)
            {
                cleanText = cleanText[..120];
            }

            var daysList = (daysOfWeek ?? Enumerable.Empty<DayOfWeek>()).Distinct().ToList();
            if (daysList.Count == 0)
            {
                throw new ArgumentException("Mục tiêu định kỳ phải chọn ít nhất một thứ trong tuần.", nameof(daysOfWeek));
            }

            var recurringGoals = await _repository.LoadRecurringGoalsAsync();
            var newRecurring = new RecurringGoal
            {
                Id = Guid.NewGuid(),
                Text = cleanText,
                ReminderTime = reminderTime,
                DaysOfWeek = daysList,
                IsEnabled = true,
                CreatedAt = _clock.Now
            };

            recurringGoals.Add(newRecurring);
            await _repository.SaveRecurringGoalsAsync(recurringGoals);
            _logger.Info($"Đã thêm mục tiêu lặp lại '{newRecurring.Text}' cho các ngày: {newRecurring.GetDaysDisplay()}");

            // Nếu hôm nay là một trong các ngày được chọn, tự động sinh goal cho hôm nay ngay lập tức
            if (newRecurring.DaysOfWeek.Contains(_clock.Today.DayOfWeek))
            {
                await EnsureRecurringGoalsForTodayAsync();
            }

            return newRecurring;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi thêm mục tiêu lặp lại '{text}'", ex);
            throw;
        }
    }

    public async Task<bool> UpdateRecurringGoalAsync(RecurringGoal recurringGoal)
    {
        try
        {
            var allRecurring = await _repository.LoadRecurringGoalsAsync();
            var target = allRecurring.FirstOrDefault(r => r.Id == recurringGoal.Id);
            if (target == null) return false;

            target.Text = recurringGoal.Text;
            target.ReminderTime = recurringGoal.ReminderTime;
            target.DaysOfWeek = recurringGoal.DaysOfWeek;
            target.IsEnabled = recurringGoal.IsEnabled;

            await _repository.SaveRecurringGoalsAsync(allRecurring);
            _logger.Info($"Đã cập nhật mục tiêu lặp lại '{target.Text}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi cập nhật mục tiêu lặp lại {recurringGoal.Id}", ex);
            return false;
        }
    }

    public async Task<bool> ToggleRecurringGoalAsync(Guid id)
    {
        try
        {
            var allRecurring = await _repository.LoadRecurringGoalsAsync();
            var target = allRecurring.FirstOrDefault(r => r.Id == id);
            if (target == null) return false;

            target.IsEnabled = !target.IsEnabled;
            await _repository.SaveRecurringGoalsAsync(allRecurring);
            _logger.Info($"Đã chuyển trạng thái mục tiêu lặp lại '{target.Text}' thành: {(target.IsEnabled ? "Bật" : "Tắt")}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi bật/tắt mục tiêu lặp lại {id}", ex);
            return false;
        }
    }

    public async Task<bool> DeleteRecurringGoalAsync(Guid id)
    {
        try
        {
            var allRecurring = await _repository.LoadRecurringGoalsAsync();
            int removed = allRecurring.RemoveAll(r => r.Id == id);
            if (removed > 0)
            {
                await _repository.SaveRecurringGoalsAsync(allRecurring);
                _logger.Info($"Đã xóa mục tiêu lặp lại {id}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi xóa mục tiêu lặp lại {id}", ex);
            return false;
        }
    }

    public async Task<int> EnsureRecurringGoalsForTodayAsync()
    {
        try
        {
            DateOnly today = _clock.Today;
            DayOfWeek todayDayOfWeek = today.DayOfWeek;

            var allRecurring = await _repository.LoadRecurringGoalsAsync();
            var activeToday = allRecurring
                .Where(r => r.IsEnabled && r.DaysOfWeek.Contains(todayDayOfWeek))
                .ToList();

            if (activeToday.Count == 0)
            {
                return 0;
            }

            var allGoals = await _repository.LoadGoalsAsync();
            int addedCount = 0;
            bool recurringUpdated = false;

            foreach (var rule in activeToday)
            {
                // Kiểm tra xem hôm nay đã có goal nào được tạo từ rule này chưa (hoặc cùng Text chưa xong/đã xong)
                bool alreadyExistsToday = allGoals.Any(g =>
                    g.Type == GoalType.Daily &&
                    g.Date == today &&
                    (g.RecurringGoalId == rule.Id || g.Text.Equals(rule.Text, StringComparison.OrdinalIgnoreCase)));

                if (!alreadyExistsToday)
                {
                    DateTime? dueAt = null;
                    if (rule.ReminderTime.HasValue)
                    {
                        dueAt = today.ToDateTime(rule.ReminderTime.Value);
                    }

                    var newGoal = new Goal
                    {
                        Id = Guid.NewGuid(),
                        Text = rule.Text,
                        Type = GoalType.Daily,
                        Date = today,
                        DueAt = dueAt,
                        RecurringGoalId = rule.Id,
                        IsDone = false,
                        CreatedAt = _clock.Now
                    };

                    allGoals.Add(newGoal);
                    rule.LastGeneratedDate = today;
                    recurringUpdated = true;
                    addedCount++;
                }
            }

            if (addedCount > 0)
            {
                await _repository.SaveGoalsAsync(allGoals);
                if (recurringUpdated)
                {
                    await _repository.SaveRecurringGoalsAsync(allRecurring);
                }
                _logger.Info($"Đã tự động khởi tạo {addedCount} mục tiêu lặp lại cho ngày hôm nay ({today})");
            }

            return addedCount;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi tự động khởi tạo mục tiêu lặp lại cho hôm nay", ex);
            return 0;
        }
    }

    #endregion

    public async Task<AppSettings> GetSettingsAsync()
    {
        return await _repository.LoadSettingsAsync();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await _repository.SaveSettingsAsync(settings);
    }
}
