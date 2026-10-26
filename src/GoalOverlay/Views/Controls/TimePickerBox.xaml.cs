using System;
using System.Windows;
using System.Windows.Controls;

namespace GoalOverlay.Views.Controls;

/// <summary>
/// Control chọn giờ và phút tùy chỉnh (WPF TimePicker)
/// Gồm 2 ComboBox giờ (00-23) và phút (bước 5, cho phép gõ tay 00-59),
/// kèm nút xóa giờ và 3 nút phím tắt nhanh (+15 phút, +1 giờ, 20:00)
/// </summary>
public partial class TimePickerBox : UserControl
{
    private bool _isUpdatingInternal = false;

    public static readonly DependencyProperty SelectedTimeProperty =
        DependencyProperty.Register(
            nameof(SelectedTime),
            typeof(TimeOnly?),
            typeof(TimePickerBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedTimeChanged));

    public TimeOnly? SelectedTime
    {
        get => (TimeOnly?)GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    public TimePickerBox()
    {
        InitializeComponent();
        InitializeItems();
    }

    private void InitializeItems()
    {
        _isUpdatingInternal = true;
        try
        {
            CmbHour.Items.Clear();
            for (int h = 0; h < 24; h++)
            {
                CmbHour.Items.Add(h.ToString("D2"));
            }

            CmbMinute.Items.Clear();
            for (int m = 0; m < 60; m += 5)
            {
                CmbMinute.Items.Add(m.ToString("D2"));
            }
        }
        finally
        {
            _isUpdatingInternal = false;
        }

        UpdateVisualsFromSelectedTime();
    }

    private static void OnSelectedTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TimePickerBox control)
        {
            control.UpdateVisualsFromSelectedTime();
        }
    }

    private void UpdateVisualsFromSelectedTime()
    {
        if (_isUpdatingInternal) return;

        _isUpdatingInternal = true;
        try
        {
            if (SelectedTime.HasValue)
            {
                var time = SelectedTime.Value;
                CmbHour.SelectedItem = time.Hour.ToString("D2");
                CmbMinute.Text = time.Minute.ToString("D2");
            }
            else
            {
                CmbHour.SelectedIndex = -1;
                CmbMinute.Text = string.Empty;
                CmbMinute.SelectedIndex = -1;
            }
        }
        finally
        {
            _isUpdatingInternal = false;
        }
    }

    private void CmbTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncSelectedTimeFromVisuals();
    }

    private void CmbMinute_TextChanged(object sender, TextChangedEventArgs e)
    {
        SyncSelectedTimeFromVisuals();
    }

    private void SyncSelectedTimeFromVisuals()
    {
        if (_isUpdatingInternal) return;

        if (CmbHour.SelectedItem is string hourStr && int.TryParse(hourStr, out int hour))
        {
            int minute = 0;
            string minuteText = CmbMinute.Text?.Trim() ?? string.Empty;
            if (int.TryParse(minuteText, out int parsedMinute) && parsedMinute >= 0 && parsedMinute < 60)
            {
                minute = parsedMinute;
            }

            _isUpdatingInternal = true;
            try
            {
                SelectedTime = new TimeOnly(hour, minute);
            }
            finally
            {
                _isUpdatingInternal = false;
            }
        }
        else
        {
            _isUpdatingInternal = true;
            try
            {
                SelectedTime = null;
            }
            finally
            {
                _isUpdatingInternal = false;
            }
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        SelectedTime = null;
    }

    private void BtnPlus15m_Click(object sender, RoutedEventArgs e)
    {
        var target = DateTime.Now.AddMinutes(15);
        SelectedTime = new TimeOnly(target.Hour, target.Minute);
    }

    private void BtnPlus1h_Click(object sender, RoutedEventArgs e)
    {
        var target = DateTime.Now.AddHours(1);
        SelectedTime = new TimeOnly(target.Hour, target.Minute);
    }

    private void Btn2000_Click(object sender, RoutedEventArgs e)
    {
        SelectedTime = new TimeOnly(20, 0);
    }
}
