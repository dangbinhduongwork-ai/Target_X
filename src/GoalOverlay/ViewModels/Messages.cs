using GoalOverlay.Models;

namespace GoalOverlay.ViewModels;

/// <summary>
/// Thông điệp phát đi qua WeakReferenceMessenger khi danh sách mục tiêu có sự thay đổi (thêm, sửa, xóa, toggle, sắp xếp)
/// </summary>
public record GoalsChangedMessage();

/// <summary>
/// Thông điệp phát đi qua WeakReferenceMessenger khi người dùng thay đổi cài đặt (Live Preview)
/// </summary>
public record SettingsChangedMessage(AppSettings Settings);
