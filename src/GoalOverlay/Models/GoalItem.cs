using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GoalOverlay.Models;

/// <summary>
/// Đại diện cho một mục tiêu theo dõi (hàng ngày hoặc hàng tháng)
/// Kế thừa ObservableObject để hỗ trợ binding 2 chiều trong WPF
/// </summary>
public partial class GoalItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRatio))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    private string _title = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRatio))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    private int _targetCount = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRatio))]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    private int _currentCount = 0;

    [ObservableProperty]
    private GoalType _type = GoalType.Daily;

    [ObservableProperty]
    private bool _isCompleted = false;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime? _completedAt;

    /// <summary>
    /// Tỉ lệ tiến độ từ 0.0 đến 1.0 phục vụ cho ProgressBar
    /// </summary>
    [JsonIgnore]
    public double ProgressRatio
    {
        get
        {
            if (TargetCount <= 0) return 0.0;
            return Math.Clamp((double)CurrentCount / TargetCount, 0.0, 1.0);
        }
    }

    /// <summary>
    /// Chuỗi hiển thị % tiến độ (ví dụ "75%")
    /// </summary>
    [JsonIgnore]
    public string ProgressPercentText => $"{Math.Round(ProgressRatio * 100)}%";

    /// <summary>
    /// Tăng tiến độ thêm 1 đơn vị, tự động đánh dấu hoàn thành nếu đạt target
    /// </summary>
    public void IncrementProgress()
    {
        CurrentCount++;
        if (CurrentCount >= TargetCount && !IsCompleted)
        {
            IsCompleted = true;
            CompletedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Giảm tiến độ 1 đơn vị
    /// </summary>
    public void DecrementProgress()
    {
        if (CurrentCount > 0)
        {
            CurrentCount--;
            if (CurrentCount < TargetCount && IsCompleted)
            {
                IsCompleted = false;
                CompletedAt = null;
            }
        }
    }
}
