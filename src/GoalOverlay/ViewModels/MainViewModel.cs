using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoalOverlay.Models;
using GoalOverlay.Native;
using GoalOverlay.Services;

namespace GoalOverlay.ViewModels;

/// <summary>
/// ViewModel chính cho MainWindow điều khiển các Tab: Hôm nay, Tháng này và Cài đặt (Live Preview)
/// </summary>
public partial class MainViewModel : ObservableObject, IRecipient<GoalsChangedMessage>
{
    private readonly IGoalService _goalService;
    private readonly IGoalRepository _repository;
    private readonly IClock _clock;
    private readonly ILoggerService _logger;
    private readonly IAutostartService _autostartService;
    private readonly IHotkeyService _hotkeyService;

    public ObservableCollection<GoalItemViewModel> TodayGoals { get; } = new();
    public ObservableCollection<GoalItemViewModel> MonthGoals { get; } = new();
    public ObservableCollection<string> AvailableMonitors { get; } = new();
    public ObservableCollection<int> RepeatOptions { get; } = new() { 0, 5, 10, 15, 30 };
    public ObservableCollection<int> EarlyReminderOptions { get; } = new() { 0, 5, 10, 15, 30 };

    [ObservableProperty]
    private string _newDailyText = string.Empty;

    [ObservableProperty]
    private TimeOnly? _newDailyTime;

    [ObservableProperty]
    private string _newMonthlyText = string.Empty;

    [ObservableProperty]
    private DateTime? _newMonthlyDueDate;

    [ObservableProperty]
    private TimeOnly? _newMonthlyDueTime;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private string _warningMessage = string.Empty;

    // Cài đặt hiển thị & hệ thống
    [ObservableProperty]
    private double _opacity = 0.95;

    [ObservableProperty]
    private double _fontSize = 13.0;

    [ObservableProperty]
    private string _textColor = "#F8FAFC";

    [ObservableProperty]
    private int _selectedMonitorIndex = 0;

    [ObservableProperty]
    private bool _clickThrough = false;

    [ObservableProperty]
    private bool _carryOverUnfinished = true;

    [ObservableProperty]
    private bool _autostartEnabled = false;

    [ObservableProperty]
    private string _hotkeyStatusMessage = "Đang kích hoạt: Ctrl + Alt + G";

    // Cài đặt Nhắc nhở (Reminders)
    [ObservableProperty]
    private bool _reminderEnabled = true;

    [ObservableProperty]
    private bool _reminderSoundEnabled = true;

    [ObservableProperty]
    private int _repeatReminderMinutes = 0;

    [ObservableProperty]
    private int _earlyReminderMinutes = 0;

    [ObservableProperty]
    private bool _doNotDisturb = false;

    private bool _isInitializingSettings = true;

    partial void OnNewDailyTimeChanged(TimeOnly? value)
    {
        CheckDailyTimeWarning();
    }

    private void CheckDailyTimeWarning()
    {
        if (NewDailyTime.HasValue)
        {
            DateTime target = _clock.Today.ToDateTime(NewDailyTime.Value);
            if (target < _clock.Now)
            {
                WarningMessage = "⚠️ Giờ này đã qua, sẽ nhắc ngay khi lưu";
                return;
            }
        }
        WarningMessage = string.Empty;
    }

    public MainViewModel(
        IGoalService goalService,
        IGoalRepository repository,
        IClock clock,
        ILoggerService logger,
        IAutostartService autostartService,
        IHotkeyService hotkeyService)
    {
        _goalService = goalService;
        _repository = repository;
        _clock = clock;
        _logger = logger;
        _autostartService = autostartService;
        _hotkeyService = hotkeyService;

        WeakReferenceMessenger.Default.Register<GoalsChangedMessage>(this);
    }

    public async void Receive(GoalsChangedMessage message)
    {
        await LoadGoalsAsync();
    }

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        await LoadGoalsAsync();
        DetectMonitors();
    }

    private async Task LoadSettingsAsync()
    {
        _isInitializingSettings = true;
        try
        {
            var settings = await _goalService.GetSettingsAsync();
            Opacity = settings.Opacity;
            FontSize = settings.FontSize;
            TextColor = settings.TextColor;
            SelectedMonitorIndex = settings.MonitorIndex;
            ClickThrough = settings.ClickThrough;
            CarryOverUnfinished = settings.CarryOverUnfinished;

            // Nạp cấu hình nhắc nhở
            ReminderEnabled = settings.ReminderEnabled;
            ReminderSoundEnabled = settings.ReminderSoundEnabled;
            RepeatReminderMinutes = settings.RepeatReminderMinutes;
            EarlyReminderMinutes = settings.EarlyReminderMinutes;
            DoNotDisturb = settings.DoNotDisturb;

            // Nạp trạng thái tự khởi động cùng Windows và phím tắt
            AutostartEnabled = _autostartService.IsEnabled();
            HotkeyStatusMessage = _hotkeyService.StatusMessage;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi MainViewModel tải cài đặt", ex);
        }
        finally
        {
            _isInitializingSettings = false;
        }
    }

    private void DetectMonitors()
    {
        AvailableMonitors.Clear();
        var monitorNames = NativeMethods.GetMonitorDisplayNames();
        foreach (var name in monitorNames)
        {
            AvailableMonitors.Add(name);
        }

        if (SelectedMonitorIndex >= AvailableMonitors.Count)
        {
            SelectedMonitorIndex = 0;
        }
    }

    [RelayCommand]
    public async Task LoadGoalsAsync()
    {
        try
        {
            var todayList = await _goalService.GetTodayGoalsAsync();
            var monthList = await _goalService.GetCurrentMonthGoalsAsync();

            TodayGoals.Clear();
            foreach (var g in todayList)
            {
                TodayGoals.Add(new GoalItemViewModel(g));
            }

            MonthGoals.Clear();
            foreach (var g in monthList)
            {
                MonthGoals.Add(new GoalItemViewModel(g));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi nạp dữ liệu mục tiêu trong MainViewModel", ex);
        }
    }

    [RelayCommand]
    public async Task AddDailyGoalAsync()
    {
        string text = (NewDailyText ?? string.Empty).Trim();

        // 1. Validation không để rỗng
        if (string.IsNullOrWhiteSpace(text))
        {
            ValidationMessage = "Nội dung mục tiêu không được để trống.";
            return;
        }

        // 2. Validation độ dài tối đa 120 ký tự
        if (text.Length > 120)
        {
            text = text[..120];
        }

        ValidationMessage = string.Empty;
        DateTime? dueAt = NewDailyTime.HasValue ? _clock.Today.ToDateTime(NewDailyTime.Value) : null;
        var created = await _goalService.AddDailyGoalAsync(text, _clock.Today, dueAt: dueAt);
        NewDailyText = string.Empty;
        NewDailyTime = null;
        WarningMessage = string.Empty;

        var newItem = new GoalItemViewModel(created);
        newItem.RefreshStatus(_clock.Now);
        TodayGoals.Add(newItem);
        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    [RelayCommand]
    public async Task AddMonthlyGoalAsync()
    {
        string text = (NewMonthlyText ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            ValidationMessage = "Nội dung mục tiêu tháng không được để trống.";
            return;
        }

        if (text.Length > 120)
        {
            text = text[..120];
        }

        ValidationMessage = string.Empty;
        DateTime? dueAt = null;
        if (NewMonthlyDueDate.HasValue)
        {
            dueAt = NewMonthlyDueTime.HasValue
                ? NewMonthlyDueDate.Value.Date.Add(NewMonthlyDueTime.Value.ToTimeSpan())
                : NewMonthlyDueDate.Value.Date;
        }
        else if (NewMonthlyDueTime.HasValue)
        {
            dueAt = _clock.Now.Date.Add(NewMonthlyDueTime.Value.ToTimeSpan());
        }

        var created = await _goalService.AddMonthlyGoalAsync(text, dueAt: dueAt);
        NewMonthlyText = string.Empty;
        NewMonthlyDueDate = null;
        NewMonthlyDueTime = null;

        var newItem = new GoalItemViewModel(created);
        newItem.RefreshStatus(_clock.Now);
        MonthGoals.Add(newItem);
        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    [RelayCommand]
    public async Task ToggleGoalAsync(GoalItemViewModel? item)
    {
        if (item == null) return;

        await _goalService.ToggleGoalAsync(item.Model.Id);
        item.IsDone = !item.IsDone;
        item.RefreshStatus(_clock.Now);
        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    [RelayCommand]
    public async Task DeleteGoalAsync(GoalItemViewModel? item)
    {
        if (item == null) return;

        await _goalService.DeleteGoalAsync(item.Model.Id);
        TodayGoals.Remove(item);
        MonthGoals.Remove(item);
        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    // --- Xử lý sửa nhanh (In-place edit) ---
    [RelayCommand]
    public void StartEdit(GoalItemViewModel? item)
    {
        if (item == null) return;

        // Tắt edit của các item khác
        foreach (var g in TodayGoals.Concat(MonthGoals))
        {
            if (g != item && g.IsEditing)
            {
                g.IsEditing = false;
            }
        }

        item.BeginEdit();
    }

    [RelayCommand]
    public async Task SaveEditAsync(GoalItemViewModel? item)
    {
        if (item == null || !item.IsEditing) return;

        string text = (item.EditText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            item.IsEditing = false;
            return;
        }

        if (text.Length > 120)
        {
            text = text[..120];
        }

        await _goalService.UpdateGoalTextAsync(item.Model.Id, text);
        item.Text = text;

        // Cập nhật DueAt cho mục tiêu
        DateTime? newDue = null;
        if (item.Model.Type == GoalType.Daily)
        {
            if (item.EditDueTime.HasValue)
            {
                DateOnly baseDate = item.Model.Date != default ? item.Model.Date : _clock.Today;
                newDue = baseDate.ToDateTime(item.EditDueTime.Value);
            }
        }
        else
        {
            if (item.EditDueDate.HasValue)
            {
                newDue = item.EditDueTime.HasValue
                    ? item.EditDueDate.Value.Date.Add(item.EditDueTime.Value.ToTimeSpan())
                    : item.EditDueDate.Value.Date;
            }
            else if (item.EditDueTime.HasValue)
            {
                newDue = _clock.Now.Date.Add(item.EditDueTime.Value.ToTimeSpan());
            }
        }

        await _goalService.SetDueTimeAsync(item.Model.Id, newDue);
        item.DueAt = newDue;
        item.IsEditing = false;
        item.RefreshStatus(_clock.Now);

        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    [RelayCommand]
    public void CancelEdit(GoalItemViewModel? item)
    {
        if (item == null) return;
        item.IsEditing = false;
        item.EditText = item.Text;
    }

    // --- Kéo thả sắp xếp thứ tự (Reorder) ---
    public async Task ReorderDailyGoalsAsync(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= TodayGoals.Count || newIndex >= TodayGoals.Count || oldIndex == newIndex)
            return;

        TodayGoals.Move(oldIndex, newIndex);
        await PersistCurrentOrderAsync();
    }

    public async Task ReorderMonthGoalsAsync(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= MonthGoals.Count || newIndex >= MonthGoals.Count || oldIndex == newIndex)
            return;

        MonthGoals.Move(oldIndex, newIndex);
        await PersistCurrentOrderAsync();
    }

    private async Task PersistCurrentOrderAsync()
    {
        var allGoals = await _repository.LoadGoalsAsync();

        // Cập nhật lại danh sách toàn cục theo thứ tự hiện tại của TodayGoals và MonthGoals
        var todayModels = TodayGoals.Select(vm => vm.Model).ToList();
        var monthModels = MonthGoals.Select(vm => vm.Model).ToList();

        // Lấy các mục tiêu thuộc ngày khác (đã lưu trữ trước đó)
        DateOnly today = _clock.Today;
        string currentMonth = _clock.CurrentMonth;
        var otherGoals = allGoals.Where(g =>
            !(g.Type == GoalType.Daily && g.Date == today) &&
            !(g.Type == GoalType.Monthly && g.Month == currentMonth)).ToList();

        var reordered = todayModels.Concat(monthModels).Concat(otherGoals).ToList();
        await _repository.SaveGoalsAsync(reordered);

        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    // --- Xử lý Live Preview Cài đặt ---
    partial void OnOpacityChanged(double value) => ApplySettingsLive();
    partial void OnFontSizeChanged(double value) => ApplySettingsLive();
    partial void OnTextColorChanged(string value) => ApplySettingsLive();
    partial void OnSelectedMonitorIndexChanged(int value) => ApplySettingsLive();
    partial void OnClickThroughChanged(bool value) => ApplySettingsLive();
    partial void OnCarryOverUnfinishedChanged(bool value) => ApplySettingsLive();
    partial void OnReminderEnabledChanged(bool value) => ApplySettingsLive();
    partial void OnReminderSoundEnabledChanged(bool value) => ApplySettingsLive();
    partial void OnRepeatReminderMinutesChanged(int value) => ApplySettingsLive();
    partial void OnEarlyReminderMinutesChanged(int value) => ApplySettingsLive();
    partial void OnDoNotDisturbChanged(bool value) => ApplySettingsLive();

    public Task LastSettingsSaveTask { get; private set; } = Task.CompletedTask;

    private void ApplySettingsLive()
    {
        if (_isInitializingSettings) return;

        var settings = new AppSettings
        {
            Opacity = Opacity,
            FontSize = FontSize,
            TextColor = TextColor,
            MonitorIndex = SelectedMonitorIndex,
            ClickThrough = ClickThrough,
            CarryOverUnfinished = CarryOverUnfinished,
            ReminderEnabled = ReminderEnabled,
            ReminderSoundEnabled = ReminderSoundEnabled,
            RepeatReminderMinutes = RepeatReminderMinutes,
            EarlyReminderMinutes = EarlyReminderMinutes,
            DoNotDisturb = DoNotDisturb
        };

        LastSettingsSaveTask = _goalService.SaveSettingsAsync(settings);
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(settings));
    }

    partial void OnAutostartEnabledChanged(bool value)
    {
        if (_isInitializingSettings) return;

        if (value)
        {
            _autostartService.Enable();
        }
        else
        {
            _autostartService.Disable();
        }
    }

    public void UpdateHotkeyStatus(string message)
    {
        HotkeyStatusMessage = message;
    }

    [RelayCommand]
    public void SelectColor(string colorHex)
    {
        TextColor = colorHex;
    }
}

