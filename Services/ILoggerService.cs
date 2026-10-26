using System;

namespace GoalOverlay.Services;

/// <summary>
/// Giao diện dịch vụ ghi log ứng dụng
/// </summary>
public interface ILoggerService
{
    string LogsDirectory { get; }
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? ex = null);
}
