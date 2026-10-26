namespace GoalOverlay.Models;

/// <summary>
/// Trạng thái thời hạn của mục tiêu để hiển thị màu cảnh báo trên UI và Overlay HUD
/// </summary>
public enum GoalDueStatus
{
    /// <summary>
    /// Bình thường: Chưa đến hạn (> 30 phút) hoặc không hẹn giờ
    /// </summary>
    Normal,

    /// <summary>
    /// Cảnh báo vàng: Còn dưới 30 phút là đến hạn
    /// </summary>
    DueSoon,

    /// <summary>
    /// Cảnh báo đỏ: Đã quá hạn mà chưa hoàn thành
    /// </summary>
    Overdue,

    /// <summary>
    /// Đã hoàn thành: Chữ và giờ mờ đi kèm gạch ngang
    /// </summary>
    Done
}
