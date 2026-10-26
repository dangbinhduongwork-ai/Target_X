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
/// Thông điệp phát đi qua WeakReferenceMessenger khi danh sách mục tiêu có sự thay đổi
/// </summary>
public record GoalsUpdatedMessage(IReadOnlyList<GoalItem> Goals);

/// <summary>
/// ViewModel quản lý dữ liệu và tương tác trực tiếp trên màn hình Overlay HUD
/// </summary>
public partial class OverlayViewModel : ObservableObject, IRecipient<GoalsUpdatedMessage>
{
    private readonly IDataService _dataService;
    private readonly ISettingsService _settingsService;
    private readonly ILoggerService _logger;

    public ObservableCollection<GoalItem> DailyGoals { get; } = new();
    public ObservableCollection<GoalItem> MonthlyGoals { get; } = new();

    [ObservableProperty]
    private double _opacity = 0.95;

    [ObservableProperty]
    private bool _isLocked = true;

    [ObservableProperty]
    private bool _isCollapsed = false;

    public OverlayViewModel(IDataService dataService, ISettingsService settingsService, ILoggerService logger)
    {
        _dataService = dataService;
        _settingsService = settingsService;
        _logger = logger;

        _opacity = _settingsService.CurrentSettings.OverlayOpacity;
        _isLocked = _settingsService.CurrentSettings.IsPositionLocked;

        // Đăng ký nhận thông điệp cập nhật mục tiêu từ các view khác
        WeakReferenceMessenger.Default.Register<GoalsUpdatedMessage>(this);
    }

    /// <summary>
    /// Xử lý thông điệp khi danh sách mục tiêu được cập nhật từ nơi khác
    /// </summary>
    public void Receive(GoalsUpdatedMessage message)
    {
        UpdateLocalCollections(message.Goals);
    }

    /// <summary>
    /// Nạp danh sách mục tiêu từ file dữ liệu và phân loại vào 2 nhóm Daily / Monthly
    /// </summary>
    [RelayCommand]
    public async Task LoadGoalsAsync()
    {
        try
        {
            var goals = await _dataService.LoadGoalsAsync();
            UpdateLocalCollections(goals);
            _logger.Info($"OverlayViewModel đã tải {DailyGoals.Count} mục tiêu ngày và {MonthlyGoals.Count} mục tiêu tháng.");
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi OverlayViewModel nạp danh sách mục tiêu", ex);
        }
    }

    /// <summary>
    /// Tăng tiến độ mục tiêu nhanh trực tiếp trên Overlay
    /// </summary>
    [RelayCommand]
    public async Task IncrementProgressAsync(GoalItem? item)
    {
        if (item == null) return;

        item.IncrementProgress();
        await SaveAllAsync();
    }

    /// <summary>
    /// Giảm tiến độ mục tiêu
    /// </summary>
    [RelayCommand]
    public async Task DecrementProgressAsync(GoalItem? item)
    {
        if (item == null) return;

        item.DecrementProgress();
        await SaveAllAsync();
    }

    /// <summary>
    /// Đánh dấu đảo trạng thái hoàn thành mục tiêu
    /// </summary>
    [RelayCommand]
    public async Task ToggleCompletedAsync(GoalItem? item)
    {
        if (item == null) return;

        item.IsCompleted = !item.IsCompleted;
        if (item.IsCompleted)
        {
            item.CurrentCount = Math.Max(item.CurrentCount, item.TargetCount);
            item.CompletedAt = DateTime.Now;
        }
        else
        {
            item.CompletedAt = null;
        }

        await SaveAllAsync();
    }

    /// <summary>
    /// Chuyển đổi trạng thái khóa vị trí Overlay
    /// </summary>
    [RelayCommand]
    public async Task ToggleLockAsync()
    {
        IsLocked = !IsLocked;
        _settingsService.CurrentSettings.IsPositionLocked = IsLocked;
        await _settingsService.SaveSettingsAsync(_settingsService.CurrentSettings);
    }

    /// <summary>
    /// Thu gọn hoặc mở rộng danh sách mục tiêu trên Overlay
    /// </summary>
    [RelayCommand]
    public void ToggleCollapse()
    {
        IsCollapsed = !IsCollapsed;
    }

    private void UpdateLocalCollections(IEnumerable<GoalItem> allGoals)
    {
        DailyGoals.Clear();
        MonthlyGoals.Clear();

        foreach (var goal in allGoals)
        {
            if (goal.Type == GoalType.Daily)
            {
                DailyGoals.Add(goal);
            }
            else
            {
                MonthlyGoals.Add(goal);
            }
        }
    }

    private async Task SaveAllAsync()
    {
        var all = DailyGoals.Concat(MonthlyGoals).ToList();
        await _dataService.SaveGoalsAsync(all);
        // Thông báo đến các ViewModel khác
        WeakReferenceMessenger.Default.Send(new GoalsUpdatedMessage(all));
    }
}
