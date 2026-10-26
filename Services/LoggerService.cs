using System;
using System.Diagnostics;
using System.IO;

namespace GoalOverlay.Services;

/// <summary>
/// Triển khai dịch vụ ghi log ứng dụng vào thư mục %APPDATA%\GoalOverlay\logs\
/// Hỗ trợ thread-safe và tự động xoay file log theo ngày (goaloverlay_yyyyMMdd.log)
/// </summary>
public class LoggerService : ILoggerService
{
    private readonly string _logsDirectory;
    private readonly object _lock = new();

    public string LogsDirectory => _logsDirectory;

    public LoggerService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logsDirectory = Path.Combine(appData, "GoalOverlay", "logs");

        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[CRITICAL] Không thể tạo thư mục logs: {ex.Message}");
        }
    }

    public void Info(string message) => WriteLog("INFO", message, null);

    public void Warn(string message) => WriteLog("WARN", message, null);

    public void Error(string message, Exception? ex = null) => WriteLog("ERROR", message, ex);

    private void WriteLog(string level, string message, Exception? ex)
    {
        try
        {
            DateTime now = DateTime.Now;
            string fileName = $"goaloverlay_{now:yyyyMMdd}.log";
            string filePath = Path.Combine(_logsDirectory, fileName);

            string entry = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
            if (ex != null)
            {
                entry += $"{Environment.NewLine}Chi tiết lỗi: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    entry += $"{Environment.NewLine}Inner Exception: {ex.InnerException.Message}";
                }
            }

            lock (_lock)
            {
                File.AppendAllText(filePath, entry + Environment.NewLine);
            }
        }
        catch (Exception writeEx)
        {
            Trace.WriteLine($"[FALLBACK LOG ERROR] {writeEx.Message} | Message: {message}");
        }
    }
}
