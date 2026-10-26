using System;
using CommunityToolkit.Mvvm.ComponentModel;
using GoalOverlay.Models;
using GoalOverlay.Services;

namespace GoalOverlay.ViewModels;

/// <summary>
/// Wrapper quanh Model Goal bổ sung trạng thái hiển thị giờ hẹn, màu sắc và sửa nhanh (In-place editing)
/// </summary>
public partial class GoalItemViewModel : ObservableObject
{
    public Goal Model { get; }

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editText = string.Empty;

    [ObservableProperty]
    private TimeOnly? _editDueTime;

    [ObservableProperty]
    private DateTime? _editDueDate;

    [ObservableProperty]
    private string _statusColor = GoalDueStatusCalculator.ColorNormal;

    public GoalItemViewModel(Goal model)
    {
        Model = model;
        _editText = model.Text;
        RefreshStatus();
    }

    public string Text
    {
        get => Model.Text;
        set
        {
            if (Model.Text != value)
            {
                Model.Text = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDone
    {
        get => Model.IsDone;
        set
        {
            if (Model.IsDone != value)
            {
                Model.IsDone = value;
                OnPropertyChanged();
                RefreshStatus();
            }
        }
    }

    public DateTime? DueAt
    {
        get => Model.DueAt;
        set
        {
            if (Model.DueAt != value)
            {
                Model.DueAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasDueTime));
                OnPropertyChanged(nameof(DueTimeString));
                RefreshStatus();
            }
        }
    }

    public bool HasDueTime => Model.DueAt.HasValue;

    public string DueTimeString
    {
        get
        {
            if (!Model.DueAt.HasValue) return string.Empty;

            var d = Model.DueAt.Value;
            if (Model.Type == GoalType.Monthly)
            {
                // Nếu giờ là 00:00 thì chỉ hiện ngày dd/MM, nếu có giờ thì hiện dd/MM HH:mm
                return (d.Hour == 0 && d.Minute == 0) ? d.ToString("dd/MM") : d.ToString("dd/MM HH:mm");
            }

            return d.ToString("HH:mm");
        }
    }

    public GoalDueStatus DueStatus => GoalDueStatusCalculator.CalculateStatus(Model, DateTime.Now);

    public void RefreshStatus(DateTime? now = null)
    {
        DateTime checkTime = now ?? DateTime.Now;
        var status = GoalDueStatusCalculator.CalculateStatus(Model, checkTime);
        StatusColor = GoalDueStatusCalculator.GetStatusColor(status);
        OnPropertyChanged(nameof(DueStatus));
        OnPropertyChanged(nameof(DueTimeString));
        OnPropertyChanged(nameof(HasDueTime));
    }

    public void BeginEdit()
    {
        EditText = Model.Text;
        EditDueTime = Model.DueAt.HasValue ? TimeOnly.FromDateTime(Model.DueAt.Value) : null;
        EditDueDate = Model.DueAt.HasValue ? Model.DueAt.Value.Date : (Model.Type == GoalType.Monthly ? DateTime.Today : null);
        IsEditing = true;
    }
}
