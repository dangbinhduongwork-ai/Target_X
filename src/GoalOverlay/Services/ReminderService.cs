using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Dịch vụ quản lý hẹn giờ và kích hoạt nhắc nhở khi đến giờ cho mục tiêu
/// Đảm bảo 100% testable thông qua IClock dependency injection
/// </summary>
public class ReminderService : IReminderService, IDisposable
{
    private readonly IGoalRepository _repository;
    private readonly IGoalService _goalService;
    private readonly IClock _clock;
    private readonly ILoggerService _logger;
    private readonly TimeSpan _pollInterval;

    private Timer? _timer;
    private int _isChecking = 0;
    private bool _isDisposed = false;

    public event EventHandler<GoalReminderEventArgs>? GoalReminderDue;

    public ReminderService(
        IGoalRepository repository,
        IGoalService goalService,
        IClock clock,
        ILoggerService logger,
        TimeSpan? pollInterval = null)
    {
        _repository = repository;
        _goalService = goalService;
        _clock = clock;
        _logger = logger;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(10);
    }

    public void Start()
    {
        if (_isDisposed) return;

        Stop();
        _timer = new Timer(async _ => await OnTimerTickAsync(), null, _pollInterval, _pollInterval);
        _logger.Info($"Đã khởi động ReminderService (chu kỳ quét: {_pollInterval.TotalSeconds}s).");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private async Task OnTimerTickAsync()
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
        {
            return; // Đang có tác vụ quét chạy dở, bỏ qua tick này
        }

        try
        {
            await CheckRemindersAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi trong quá trình quét định kỳ nhắc nhở mục tiêu", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    public async Task<IReadOnlyList<Goal>> CheckRemindersAsync()
    {
        var dueGoals = new List<Goal>();

        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            DateTime now = _clock.Now;
            DateOnly today = _clock.Today;
            bool dataChanged = false;

            foreach (var goal in allGoals)
            {
                // Chỉ nhắc các mục tiêu chưa hoàn thành và có cấu hình DueAt / ReminderTime
                if (goal.IsDone || (!goal.DueAt.HasValue && !goal.ReminderTime.HasValue))
                {
                    continue;
                }

                // Với mục tiêu hàng ngày: không nhắc mục tiêu ở các ngày tương lai
                if (goal.Type == GoalType.Daily && goal.Date > today)
                {
                    continue;
                }

                // 1. Kiểm tra trạng thái hoãn báo lại (Snooze)
                if (goal.SnoozedUntil.HasValue)
                {
                    if (now >= goal.SnoozedUntil.Value)
                    {
                        dueGoals.Add(goal);
                        goal.LastNotifiedAt = now;
                        goal.NotifyCount++;
                        goal.SnoozedUntil = null; // Đã kích hoạt báo lại xong, reset Snooze
                        dataChanged = true;
                    }
                    continue;
                }

                // 2. Nếu người dùng đã đóng/bỏ qua (Dismiss) lời nhắc cho lần hẹn này thì bỏ qua
                if (goal.ReminderDismissed)
                {
                    continue;
                }

                // 3. Kiểm tra giờ hẹn (DueAt hoặc ReminderTime)
                DateTime targetDateTime;
                if (goal.DueAt.HasValue)
                {
                    targetDateTime = goal.DueAt.Value;
                }
                else
                {
                    DateOnly goalDate = goal.Type == GoalType.Daily ? goal.Date : today;
                    targetDateTime = goalDate.ToDateTime(goal.ReminderTime!.Value);
                }

                // Đã đến hoặc quá giờ hẹn (now >= targetDateTime)
                if (now >= targetDateTime)
                {
                    // Kiểm tra xem đã từng kích hoạt thông báo cho mốc targetDateTime này chưa
                    bool alreadyRemindedForThisSlot = goal.LastNotifiedAt.HasValue &&
                                                     goal.LastNotifiedAt.Value >= targetDateTime;

                    if (!alreadyRemindedForThisSlot)
                    {
                        dueGoals.Add(goal);
                        goal.LastNotifiedAt = now;
                        goal.NotifyCount++;
                        dataChanged = true;
                    }
                }
            }

            if (dataChanged)
            {
                await _repository.SaveGoalsAsync(allGoals);
            }

            // Kích hoạt sự kiện nhắc nhở cho từng mục tiêu đến hạn
            foreach (var dueGoal in dueGoals)
            {
                string timeDisplay = dueGoal.DueAt.HasValue ? dueGoal.DueAt.Value.ToString("HH:mm") : dueGoal.ReminderTime?.ToString() ?? string.Empty;
                _logger.Info($"[Hẹn giờ] Mục tiêu '{dueGoal.Text}' đã đến giờ nhắc nhở ({timeDisplay}).");
                GoalReminderDue?.Invoke(this, new GoalReminderEventArgs(dueGoal));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi kiểm tra danh sách mục tiêu đến hạn", ex);
        }

        return dueGoals;
    }

    public async Task SnoozeGoalAsync(Guid goalId, TimeSpan duration)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var target = allGoals.FirstOrDefault(g => g.Id == goalId);
            if (target != null)
            {
                target.SnoozedUntil = _clock.Now.Add(duration);
                target.ReminderDismissed = false;
                await _repository.SaveGoalsAsync(allGoals);
                _logger.Info($"Mục tiêu '{target.Text}' được hoãn báo lại {duration.TotalMinutes} phút (đến: {target.SnoozedUntil}).");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi thực hiện Snooze mục tiêu {goalId}", ex);
        }
    }

    public async Task DismissGoalAsync(Guid goalId)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var target = allGoals.FirstOrDefault(g => g.Id == goalId);
            if (target != null)
            {
                target.ReminderDismissed = true;
                target.SnoozedUntil = null;
                await _repository.SaveGoalsAsync(allGoals);
                _logger.Info($"Đã đóng/bỏ qua lời nhắc cho mục tiêu '{target.Text}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi đóng lời nhắc mục tiêu {goalId}", ex);
        }
    }

    public async Task CompleteGoalFromReminderAsync(Guid goalId)
    {
        try
        {
            var allGoals = await _repository.LoadGoalsAsync();
            var target = allGoals.FirstOrDefault(g => g.Id == goalId);
            if (target != null)
            {
                target.IsDone = true;
                target.SnoozedUntil = null;
                target.ReminderDismissed = true;
                await _repository.SaveGoalsAsync(allGoals);
                _logger.Info($"Mục tiêu '{target.Text}' đã được đánh dấu hoàn thành từ lời nhắc.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi đánh dấu hoàn thành mục tiêu {goalId} từ lời nhắc", ex);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _isDisposed = true;
        }
    }
}
