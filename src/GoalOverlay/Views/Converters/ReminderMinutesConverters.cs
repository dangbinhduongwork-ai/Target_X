using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GoalOverlay.Views.Converters;

/// <summary>
/// Chuyển đổi số phút lặp lại sang văn bản hiển thị thân thiện
/// </summary>
public class RepeatMinutesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int mins)
        {
            return mins == 0 ? "Tắt nhắc lặp lại" : $"Mỗi {mins} phút";
        }
        return "Tắt nhắc lặp lại";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Chuyển đổi số phút nhắc trước sang văn bản hiển thị thân thiện
/// </summary>
public class EarlyMinutesConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int mins)
        {
            return mins == 0 ? "Đúng giờ mới nhắc" : $"Trước {mins} phút";
        }
        return "Đúng giờ mới nhắc";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// Chuyển chuỗi không rỗng thành Visibility.Visible, chuỗi rỗng thành Visibility.Collapsed
/// </summary>
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
