using System.Collections.Generic;
using System.Threading.Tasks;
using GoalOverlay.Models;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ nạp và lưu trữ danh sách mục tiêu
/// </summary>
public interface IDataService
{
    string DataFilePath { get; }
    Task<List<GoalItem>> LoadGoalsAsync();
    Task<bool> SaveGoalsAsync(IEnumerable<GoalItem> goals);
}
