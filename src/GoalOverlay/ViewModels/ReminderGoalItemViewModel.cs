using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoalOverlay.Models;

namespace GoalOverlay.ViewModels;

/// <summary>
/// ViewModel cho từng dòng mục tiêu hiển thị trên cửa sổ nhắc nhở ReminderWindow
/// </summary>
public partial class ReminderGoalItemViewModel : ObservableObject
{
    private readonly Action<ReminderGoalItemViewModel> _onComplete;
    private readonly Action<ReminderGoalItemViewModel, int> _onSnooze;
    private readonly Action<ReminderGoalItemViewModel> _onDismiss;

    public Goal Model { get; }

    public string Text => Model.Text;

    public string DueTimeString
    {
        get
        {
            if (!Model.DueAt.HasValue) return string.Empty;
            DateTime d = Model.DueAt.Value;
            if (Model.Type == GoalType.Monthly)
            {
                return (d.Hour == 0 && d.Minute == 0) ? d.ToString("dd/MM") : d.ToString("dd/MM HH:mm");
            }
            return d.ToString("HH:mm");
        }
    }

    [ObservableProperty]
    private int _overdueMinutes;

    [ObservableProperty]
    private string _overdueText = string.Empty;

    [ObservableProperty]
    private bool _isOverdue;

    public IReadOnlyList<int> SnoozeOptions { get; }

    public ReminderGoalItemViewModel(
        Goal model,
        DateTime now,
        IReadOnlyList<int>? snoozeOptions,
        Action<ReminderGoalItemViewModel> onComplete,
        Action<ReminderGoalItemViewModel, int> onSnooze,
        Action<ReminderGoalItemViewModel> onDismiss)
    {
        Model = model;
        SnoozeOptions = snoozeOptions ?? new List<int> { 5, 10, 30, 60 };
        _onComplete = onComplete;
        _onSnooze = onSnooze;
        _onDismiss = onDismiss;

        CalculateOverdue(now);
    }

    public void CalculateOverdue(DateTime now)
    {
        if (Model.DueAt.HasValue && now > Model.DueAt.Value)
        {
            var diff = now - Model.DueAt.Value;
            OverdueMinutes = (int)Math.Max(1, Math.Round(diff.TotalMinutes));
            OverdueText = $"Đã quá {OverdueMinutes} phút";
            IsOverdue = true;
        }
        else
        {
            OverdueMinutes = 0;
            OverdueText = string.Empty;
            IsOverdue = false;
        }
    }

    [RelayCommand]
    private void Complete() => _onComplete(this);

    [RelayCommand]
    private void Snooze(object? param)
    {
        int minutes = 5;
        if (param is int m) minutes = m;
        else if (param is string s && int.TryParse(s, out int parsed)) minutes = parsed;
        _onSnooze(this, minutes);
    }

    [RelayCommand]
    private void Dismiss() => _onDismiss(this);
}
