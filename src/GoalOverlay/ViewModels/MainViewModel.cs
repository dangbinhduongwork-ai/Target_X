using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoalOverlay.Models;
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

    public ObservableCollection<GoalItemViewModel> TodayGoals { get; } = new();
    public ObservableCollection<GoalItemViewModel> MonthGoals { get; } = new();
    public ObservableCollection<string> AvailableMonitors { get; } = new();

    [ObservableProperty]
    private string _newDailyText = string.Empty;

    [ObservableProperty]
    private string _newMonthlyText = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

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

    private bool _isInitializingSettings = true;

    public MainViewModel(IGoalService goalService, IGoalRepository repository, IClock clock, ILoggerService logger)
    {
        _goalService = goalService;
        _repository = repository;
        _clock = clock;
        _logger = logger;

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
        // Cung cấp danh sách màn hình mặc định
        AvailableMonitors.Add("Màn hình 1 (Chính)");
        AvailableMonitors.Add("Màn hình 2");
        AvailableMonitors.Add("Màn hình 3");

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
        var created = await _goalService.AddDailyGoalAsync(text);
        NewDailyText = string.Empty;

        TodayGoals.Add(new GoalItemViewModel(created));
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
        var created = await _goalService.AddMonthlyGoalAsync(text);
        NewMonthlyText = string.Empty;

        MonthGoals.Add(new GoalItemViewModel(created));
        WeakReferenceMessenger.Default.Send(new GoalsChangedMessage());
    }

    [RelayCommand]
    public async Task ToggleGoalAsync(GoalItemViewModel? item)
    {
        if (item == null) return;

        await _goalService.ToggleGoalAsync(item.Model.Id);
        item.IsDone = !item.IsDone;
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

        item.EditText = item.Text;
        item.IsEditing = true;
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
        item.IsEditing = false;

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
            CarryOverUnfinished = CarryOverUnfinished
        };

        _ = _goalService.SaveSettingsAsync(settings);
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(settings));
    }

    [RelayCommand]
    public void SelectColor(string colorHex)
    {
        TextColor = colorHex;
    }
}
