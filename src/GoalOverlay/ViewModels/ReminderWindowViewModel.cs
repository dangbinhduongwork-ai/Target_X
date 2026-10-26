using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GoalOverlay.Models;
using GoalOverlay.Services;

namespace GoalOverlay.ViewModels;

/// <summary>
/// ViewModel quản lý cửa sổ nhắc nhở ReminderWindow gom nhóm các mục tiêu đến hạn
/// Tự động cập nhật danh sách và tự động đóng khi tất cả mục đã được xử lý
/// </summary>
public partial class ReminderWindowViewModel : ObservableObject
{
    private readonly IReminderService _reminderService;
    private readonly IClock _clock;
    private readonly List<int> _snoozeOptions;

    public ObservableCollection<ReminderGoalItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _titleCountText = string.Empty;

    public event Action? RequestClose;

    public ReminderWindowViewModel(
        IReminderService reminderService,
        IClock clock,
        IEnumerable<Goal> initialGoals,
        List<int>? snoozeOptions = null)
    {
        _reminderService = reminderService;
        _clock = clock;
        _snoozeOptions = snoozeOptions ?? new List<int> { 5, 10, 30, 60 };

        AddGoals(initialGoals);
    }

    public void AddGoals(IEnumerable<Goal> goals)
    {
        DateTime now = _clock.Now;
        bool addedAny = false;

        foreach (var goal in goals)
        {
            if (Items.Any(existing => existing.Model.Id == goal.Id))
            {
                continue; // Mục này đã có sẵn trong danh sách
            }

            var itemVm = new ReminderGoalItemViewModel(
                goal,
                now,
                _snoozeOptions,
                HandleComplete,
                HandleSnooze,
                HandleDismiss);

            Items.Add(itemVm);
            addedAny = true;
        }

        if (addedAny || Items.Count > 0)
        {
            UpdateTitleCount();
        }
    }

    private void UpdateTitleCount()
    {
        TitleCountText = Items.Count > 0 ? $"({Items.Count} mục tiêu)" : string.Empty;
    }

    private void HandleComplete(ReminderGoalItemViewModel item)
    {
        Items.Remove(item);
        _ = _reminderService.CompleteGoalFromReminderAsync(item.Model.Id);
        UpdateTitleCount();

        if (Items.Count == 0)
        {
            RequestClose?.Invoke();
        }
    }

    private void HandleSnooze(ReminderGoalItemViewModel item, int minutes)
    {
        Items.Remove(item);
        _ = _reminderService.SnoozeGoalAsync(item.Model.Id, TimeSpan.FromMinutes(minutes));
        UpdateTitleCount();

        if (Items.Count == 0)
        {
            RequestClose?.Invoke();
        }
    }

    private void HandleDismiss(ReminderGoalItemViewModel item)
    {
        Items.Remove(item);
        _ = _reminderService.DismissGoalAsync(item.Model.Id);
        UpdateTitleCount();

        if (Items.Count == 0)
        {
            RequestClose?.Invoke();
        }
    }
}
