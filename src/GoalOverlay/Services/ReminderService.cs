using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Win32;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Dịch vụ quản lý hẹn giờ, quét định kỳ 10 giây và kích hoạt thông báo nhắc nhở khi đến giờ (ReminderScheduler)
/// Đảm bảo:
/// 1. 100% testable qua IClock injection
/// 2. Hỗ trợ đầy đủ: Hẹn giờ chính (DueAt), Nhắc sớm (EarlyReminderMinutes), Nhắc lặp lại (RepeatReminderMinutes),
///    Hoãn báo lại (Snooze), Bỏ qua (Dismiss)
/// 3. Hỗ trợ chế độ Không làm phiền (Do Not Disturb - DND) với hàng chờ gom nhóm
/// 4. Tự động kiểm tra lại ngay khi máy tỉnh giấc (Sleep Resume), đổi giờ hệ thống (TimeChanged), hoặc dữ liệu thay đổi
/// </summary>
public class ReminderService : IReminderService, IDisposable, IRecipient<GoalsChangedMessage>, IRecipient<SettingsChangedMessage>
{
    private readonly IGoalRepository _repository;
    private readonly IGoalService _goalService;
    private readonly IClock _clock;
    private readonly ILoggerService _logger;
    private readonly TimeSpan _pollInterval;

    private Timer? _timer;
    private int _isChecking = 0;
    private bool _isDisposed = false;
    private bool _isStarted = false;
    private bool _previousDndState = false;

    // Danh sách mục tiêu đến hạn trong thời gian bật DND để gom hiển thị khi tắt DND
    private readonly HashSet<Guid> _suppressedGoalIds = new();

    public event EventHandler<GoalReminderEventArgs>? GoalReminderDue;
    public event EventHandler<GoalsReminderBatchEventArgs>? GoalsReminderBatchDue;

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

        _isStarted = true;
        _timer = new Timer(async _ => await OnTimerTickAsync(), null, _pollInterval, _pollInterval);

        // Đăng ký sự kiện hệ thống Windows
        try
        {
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
            SystemEvents.TimeChanged += SystemEvents_TimeChanged;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Không thể đăng ký SystemEvents trong ReminderService: {ex.Message}");
        }

        // Lắng nghe thông điệp dữ liệu thay đổi và cài đặt thay đổi
        WeakReferenceMessenger.Default.Register<GoalsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this);

        _logger.Info($"Đã khởi động ReminderService (chu kỳ quét: {_pollInterval.TotalSeconds}s).");

        // Quét ngay lần đầu tiên khi ứng dụng khởi động
        TriggerCheck();
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;

        if (_isStarted)
        {
            try
            {
                SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
                SystemEvents.TimeChanged -= SystemEvents_TimeChanged;
            }
            catch { }

            WeakReferenceMessenger.Default.UnregisterAll(this);
            _isStarted = false;
        }
    }

    private void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _logger.Info("[Hệ thống] Máy tính vừa thức dậy từ chế độ Sleep -> Kích hoạt kiểm tra nhắc nhở ngay.");
            TriggerCheck();
        }
    }

    private void SystemEvents_TimeChanged(object? sender, EventArgs e)
    {
        _logger.Info("[Hệ thống] Người dùng đã thay đổi giờ hệ thống Windows -> Kích hoạt kiểm tra nhắc nhở ngay.");
        TriggerCheck();
    }

    public void Receive(GoalsChangedMessage message)
    {
        TriggerCheck();
    }

    public void Receive(SettingsChangedMessage message)
    {
        bool newDnd = message.Settings.DoNotDisturb;
        if (_previousDndState && !newDnd)
        {
            _logger.Info("[Cài đặt] Chế độ Không làm phiền (DND) vừa được tắt -> Kích hoạt gom nhóm nhắc nhở ngay.");
            TriggerCheck();
        }
        _previousDndState = newDnd;
    }

    public void TriggerCheck()
    {
        if (_isDisposed) return;

        _ = Task.Run(async () =>
        {
            if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0) return;
            try
            {
                await CheckRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.Error("Lỗi khi TriggerCheck nhắc nhở mục tiêu", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isChecking, 0);
            }
        });
    }

    private async Task OnTimerTickAsync()
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
        {
            return;
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
            var settings = await _goalService.GetSettingsAsync();
            _previousDndState = settings.DoNotDisturb;

            // Nếu người dùng tắt hoàn toàn hệ thống nhắc nhở -> Không nhắc mục nào
            if (!settings.ReminderEnabled)
            {
                return Array.Empty<Goal>();
            }

            var allGoals = await _repository.LoadGoalsAsync();
            DateTime now = _clock.Now;
            DateOnly today = _clock.Today;
            bool dataChanged = false;

            foreach (var goal in allGoals)
            {
                // Chỉ nhắc các mục chưa hoàn thành và CÓ giờ hẹn DueAt
                if (goal.IsDone || !goal.DueAt.HasValue)
                {
                    continue;
                }

                // Mục hàng ngày thuộc ngày tương lai: không nhắc trong ngày hôm nay
                if (goal.Type == GoalType.Daily && goal.Date > today)
                {
                    continue;
                }

                DateTime dueAt = goal.DueAt.Value;

                // 1. Kiểm tra trạng thái Hoãn báo lại (Snooze)
                if (goal.SnoozedUntil.HasValue)
                {
                    if (now >= goal.SnoozedUntil.Value)
                    {
                        dueGoals.Add(goal);
                        goal.LastNotifiedAt = now;
                        goal.NotifyCount++;
                        goal.SnoozedUntil = null; // Đã đến giờ hoãn, gỡ cờ Snooze
                        dataChanged = true;
                    }
                    continue;
                }

                // 2. Nếu người dùng đã chọn Bỏ qua (Dismiss) cho mốc hẹn này -> Bỏ qua
                if (goal.ReminderDismissed)
                {
                    continue;
                }

                // 3. Kiểm tra Nhắc sớm (Early Reminder) nếu cấu hình EarlyReminderMinutes > 0
                if (settings.EarlyReminderMinutes > 0)
                {
                    DateTime earlyTime = dueAt.AddMinutes(-settings.EarlyReminderMinutes);
                    if (now >= earlyTime && now < dueAt)
                    {
                        // Chưa từng nhắc cho mốc sớm này
                        bool alreadyRemindedEarly = goal.LastNotifiedAt.HasValue && goal.LastNotifiedAt.Value >= earlyTime;
                        if (!alreadyRemindedEarly)
                        {
                            dueGoals.Add(goal);
                            goal.LastNotifiedAt = now;
                            goal.NotifyCount++;
                            dataChanged = true;
                            continue;
                        }
                    }
                }

                // 4. Kiểm tra Giờ hẹn chính (DueAt) và Nhắc lặp lại chu kỳ (RepeatReminderMinutes)
                if (now >= dueAt)
                {
                    // Lần nhắc đầu tiên khi đến hoặc quá giờ hẹn
                    bool remindedAtOrAfterDue = goal.LastNotifiedAt.HasValue && goal.LastNotifiedAt.Value >= dueAt;

                    if (!remindedAtOrAfterDue)
                    {
                        dueGoals.Add(goal);
                        goal.LastNotifiedAt = now;
                        goal.NotifyCount++;
                        dataChanged = true;
                    }
                    else if (settings.RepeatReminderMinutes > 0)
                    {
                        // Đã từng nhắc sau giờ hẹn, kiểm tra xem đã qua chu kỳ lặp lại chưa
                        DateTime nextRepeatTime = goal.LastNotifiedAt!.Value.AddMinutes(settings.RepeatReminderMinutes);
                        if (now >= nextRepeatTime)
                        {
                            dueGoals.Add(goal);
                            goal.LastNotifiedAt = now;
                            goal.NotifyCount++;
                            dataChanged = true;
                        }
                    }
                }
            }

            if (dataChanged)
            {
                await _repository.SaveGoalsAsync(allGoals);
            }

            // 5. Xử lý Chế độ Không làm phiền (Do Not Disturb - DND)
            if (settings.DoNotDisturb)
            {
                foreach (var g in dueGoals)
                {
                    _suppressedGoalIds.Add(g.Id);
                }

                if (dueGoals.Count > 0)
                {
                    _logger.Info($"[DND Bật] Đã tạm hoãn thông báo cho {dueGoals.Count} mục tiêu do chế độ Không làm phiền đang bật.");
                }

                return dueGoals;
            }

            // DND đang tắt: nếu trước đó có các mục bị hoãn trong hàng chờ DND, gom lại toàn bộ
            if (_suppressedGoalIds.Count > 0)
            {
                foreach (var goalId in _suppressedGoalIds.ToList())
                {
                    var suppressed = allGoals.FirstOrDefault(g => g.Id == goalId && !g.IsDone);
                    if (suppressed != null && !dueGoals.Any(d => d.Id == suppressed.Id))
                    {
                        dueGoals.Add(suppressed);
                    }
                }
                _suppressedGoalIds.Clear();
            }

            // 6. Phát sự kiện nếu có mục tiêu đến hạn
            if (dueGoals.Count > 0)
            {
                // Phát sự kiện đơn lẻ cho từng mục (tương thích ngược)
                foreach (var dueGoal in dueGoals)
                {
                    string timeDisplay = dueGoal.DueAt.HasValue ? dueGoal.DueAt.Value.ToString("HH:mm") : string.Empty;
                    _logger.Info($"[Nhắc nhở] Mục tiêu '{dueGoal.Text}' đến hạn ({timeDisplay}).");
                    GoalReminderDue?.Invoke(this, new GoalReminderEventArgs(dueGoal));
                }

                // Phát sự kiện gom nhóm (để hiển thị trên MỘT ReminderWindow duy nhất)
                GoalsReminderBatchDue?.Invoke(this, new GoalsReminderBatchEventArgs(dueGoals));
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
                _suppressedGoalIds.Remove(goalId);

                _logger.Info($"Mục tiêu '{target.Text}' được hoãn báo lại {duration.TotalMinutes} phút (đến: {target.SnoozedUntil}).");
                WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
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
                _suppressedGoalIds.Remove(goalId);

                _logger.Info($"Đã đóng/bỏ qua lời nhắc cho mục tiêu '{target.Text}'.");
                WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
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
            await _goalService.MarkDoneAsync(goalId, true);
            _suppressedGoalIds.Remove(goalId);
            _logger.Info($"Mục tiêu {goalId} đã được đánh dấu hoàn thành từ cửa sổ nhắc nhở.");
            WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
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
