using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoalOverlay.Models;
using GoalOverlay.Services;

using System.Windows.Threading;

namespace GoalOverlay.ViewModels;

/// <summary>
/// ViewModel quản lý dữ liệu hiển thị trên Overlay HUD
/// Lắng nghe đồng bộ realtime dữ liệu mục tiêu, hiển thị giờ hẹn, màu trạng thái và Live Preview cài đặt
/// Tự động làm mới màu sắc mỗi 30 giây mà không giành focus của người dùng
/// </summary>
public partial class OverlayViewModel : ObservableObject, IRecipient<GoalsChangedMessage>, IRecipient<SettingsChangedMessage>
{
    private readonly IGoalService _goalService;
    private readonly IClock _clock;
    private readonly ILoggerService _logger;
    private DispatcherTimer? _statusTimer;

    public ObservableCollection<GoalItemViewModel> TodayGoals { get; } = new();
    public ObservableCollection<GoalItemViewModel> MonthGoals { get; } = new();

    [ObservableProperty]
    private string _todayProgressText = "(0/0 xong)";

    [ObservableProperty]
    private string _monthProgressText = "(0/0 xong)";

    [ObservableProperty]
    private double _opacity = 0.95;

    [ObservableProperty]
    private double _fontSize = 13.0;

    [ObservableProperty]
    private string _textColor = "#F8FAFC";

    [ObservableProperty]
    private bool _clickThrough = false;

    [ObservableProperty]
    private bool _isCollapsed = false;

    public bool IsExpanded => !IsCollapsed;

    [ObservableProperty]
    private bool _hasGoals;

    public bool HasNoGoals => !HasGoals;

    [ObservableProperty]
    private bool _hasTodayGoals;

    [ObservableProperty]
    private bool _hasMonthGoals;

    public bool ShowSeparator => HasTodayGoals && HasMonthGoals;

    partial void OnHasGoalsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoGoals));
    }

    partial void OnHasTodayGoalsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSeparator));
    }

    partial void OnHasMonthGoalsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSeparator));
    }

    partial void OnIsCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsExpanded));
    }

    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    public OverlayViewModel(IGoalService goalService, IClock clock, ILoggerService logger)
    {
        _goalService = goalService;
        _clock = clock;
        _logger = logger;

        WeakReferenceMessenger.Default.Register<GoalsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this);

        InitializeStatusTimer();
    }

    private void InitializeStatusTimer()
    {
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _statusTimer.Tick += (s, e) => RefreshItemStatuses();
        _statusTimer.Start();
    }

    public void RefreshItemStatuses()
    {
        DateTime now = _clock.Now;
        foreach (var item in TodayGoals)
        {
            item.RefreshStatus(now);
        }
        foreach (var item in MonthGoals)
        {
            item.RefreshStatus(now);
        }
    }

    public async void Receive(GoalsChangedMessage message)
    {
        await LoadGoalsAsync();
    }

    public void Receive(SettingsChangedMessage message)
    {
        // Áp dụng Live Preview tức thì
        Opacity = message.Settings.Opacity;
        FontSize = message.Settings.FontSize;
        TextColor = message.Settings.TextColor;
        ClickThrough = message.Settings.ClickThrough;
    }

    /// <summary>
    /// Nạp dữ liệu mục tiêu hôm nay và tháng này, kèm nạp cấu hình hiển thị
    /// </summary>
    [RelayCommand]
    public async Task LoadGoalsAsync()
    {
        try
        {
            await CheckAndPurgeLegacySamplesAsync();

            var settings = await _goalService.GetSettingsAsync();
            Opacity = settings.Opacity;
            FontSize = settings.FontSize;
            TextColor = settings.TextColor;
            ClickThrough = settings.ClickThrough;

            var todayList = await _goalService.GetTodayGoalsAsync();
            var monthList = await _goalService.GetCurrentMonthGoalsAsync();

            DateTime now = _clock.Now;

            TodayGoals.Clear();
            foreach (var g in todayList)
            {
                var item = new GoalItemViewModel(g);
                item.RefreshStatus(now);
                TodayGoals.Add(item);
            }

            MonthGoals.Clear();
            foreach (var g in monthList)
            {
                var item = new GoalItemViewModel(g);
                item.RefreshStatus(now);
                MonthGoals.Add(item);
            }

            UpdateProgressSummary();
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi OverlayViewModel nạp danh sách mục tiêu", ex);
        }
    }

    /// <summary>
    /// Đảo trạng thái hoàn thành của một mục tiêu
    /// </summary>
    [RelayCommand]
    public async Task ToggleGoalAsync(object? param)
    {
        GoalItemViewModel? vm = param as GoalItemViewModel;
        Goal? goal = vm?.Model ?? (param as Goal);
        if (goal == null) return;

        try
        {
            await _goalService.ToggleGoalAsync(goal.Id);
            if (vm != null)
            {
                vm.IsDone = !vm.IsDone;
                vm.RefreshStatus(_clock.Now);
            }
            else
            {
                goal.IsDone = !goal.IsDone;
            }

            UpdateProgressSummary();
            WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi toggle mục tiêu {goal.Id}", ex);
        }
    }

    /// <summary>
    /// Làm mới dữ liệu mục tiêu và kiểm tra carry-over
    /// </summary>
    [RelayCommand]
    public async Task RefreshAsync()
    {
        await _goalService.CarryOverUnfinishedGoalsAsync();
        await LoadGoalsAsync();
    }

    private void UpdateProgressSummary()
    {
        HasTodayGoals = TodayGoals.Count > 0;
        HasMonthGoals = MonthGoals.Count > 0;
        HasGoals = HasTodayGoals || HasMonthGoals;

        int todayDone = TodayGoals.Count(g => g.IsDone);
        int todayTotal = TodayGoals.Count;
        TodayProgressText = $"({todayDone}/{todayTotal} xong)";

        int monthDone = MonthGoals.Count(g => g.IsDone);
        int monthTotal = MonthGoals.Count;
        MonthProgressText = $"({monthDone}/{monthTotal} xong)";
    }

    private static readonly System.Collections.Generic.HashSet<string> LegacySampleGoalTexts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Uống 2L nước trong ngày",
        "Hoàn thành code OverlayWindow",
        "Đi bộ 15 phút buổi tối",
        "Đọc xong 1 cuốn sách chuyên môn",
        "Nghiệm thu giai đoạn 3 GoalOverlay"
    };

    private static bool _legacyPurgeChecked = false;

    private async Task CheckAndPurgeLegacySamplesAsync()
    {
        if (_legacyPurgeChecked) return;
        _legacyPurgeChecked = true;

        try
        {
            var todayList = await _goalService.GetTodayGoalsAsync();
            var monthList = await _goalService.GetCurrentMonthGoalsAsync();

            bool purged = false;
            foreach (var g in todayList)
            {
                if (LegacySampleGoalTexts.Contains(g.Text.Trim()))
                {
                    await _goalService.DeleteGoalAsync(g.Id);
                    purged = true;
                }
            }

            foreach (var g in monthList)
            {
                if (LegacySampleGoalTexts.Contains(g.Text.Trim()))
                {
                    await _goalService.DeleteGoalAsync(g.Id);
                    purged = true;
                }
            }

            if (purged)
            {
                WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi quét dọn mục tiêu mẫu cũ", ex);
        }
    }
}
