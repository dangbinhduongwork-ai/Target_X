using System;
using System.Collections.Generic;
using System.IO;
using GoalOverlay.Services;

namespace GoalOverlay.Tests.Mocks;

/// <summary>
/// Logger phục vụ kiểm thử đơn vị, lưu log trong bộ nhớ để không ảnh hưởng đến file log thật
/// </summary>
public class TestLogger : ILoggerService
{
    public List<string> LogEntries { get; } = new();

    public string LogsDirectory => Path.GetTempPath();

    public void Info(string message) => LogEntries.Add($"[INFO] {message}");

    public void Warn(string message) => LogEntries.Add($"[WARN] {message}");

    public void Error(string message, Exception? ex = null) => LogEntries.Add($"[ERROR] {message} {ex?.Message}");
}
