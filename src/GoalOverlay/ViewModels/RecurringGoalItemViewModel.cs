using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using GoalOverlay.Models;

namespace GoalOverlay.ViewModels;

/// <summary>
/// ViewModel đại diện cho một mục tiêu lặp lại theo tuần hiển thị trong danh sách quản lý
/// </summary>
public partial class RecurringGoalItemViewModel : ObservableObject
{
    private readonly RecurringGoal _model;

    public RecurringGoal Model => _model;

    public Guid Id => _model.Id;

    public string Text => _model.Text;

    public TimeOnly? ReminderTime => _model.ReminderTime;

    public string ReminderTimeText => _model.ReminderTime.HasValue ? $"⏰ {_model.ReminderTime.Value:HH:mm}" : string.Empty;

    public string DaysDisplay => _model.GetDaysDisplay();

    public IReadOnlyList<DayOfWeek> DaysOfWeek => _model.DaysOfWeek;

    public bool HasMonday => _model.DaysOfWeek.Contains(DayOfWeek.Monday);
    public bool HasTuesday => _model.DaysOfWeek.Contains(DayOfWeek.Tuesday);
    public bool HasWednesday => _model.DaysOfWeek.Contains(DayOfWeek.Wednesday);
    public bool HasThursday => _model.DaysOfWeek.Contains(DayOfWeek.Thursday);
    public bool HasFriday => _model.DaysOfWeek.Contains(DayOfWeek.Friday);
    public bool HasSaturday => _model.DaysOfWeek.Contains(DayOfWeek.Saturday);
    public bool HasSunday => _model.DaysOfWeek.Contains(DayOfWeek.Sunday);

    [ObservableProperty]
    private bool _isEnabled;

    public RecurringGoalItemViewModel(RecurringGoal model)
    {
        _model = model;
        _isEnabled = model.IsEnabled;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _model.IsEnabled = value;
    }
}
