using System;
using System.IO;
using Microsoft.Win32;

namespace GoalOverlay.Services;

/// <summary>
/// Quản lý khởi động cùng Windows thông qua Registry:
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run
/// Có try-catch và ghi log qua ILoggerService
/// </summary>
public class WindowsStartupService : IStartupService
{
    private const string RunRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "GoalOverlay";

    private readonly ILoggerService _logger;

    public WindowsStartupService(ILoggerService logger)
    {
        _logger = logger;
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKeyPath, writable: false);
            if (key == null)
            {
                _logger.Warn($"Không thể mở Registry key: {RunRegistryKeyPath}");
                return false;
            }

            object? value = key.GetValue(AppRegistryName);
            bool isEnabled = value != null;
            _logger.Info($"Trạng thái tự khởi động cùng Windows hiện tại: {(isEnabled ? "BẬT" : "TẮT")}");
            return isEnabled;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi kiểm tra Registry tự khởi động cùng Windows", ex);
            return false;
        }
    }

    public bool SetAutoStart(bool enable)
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunRegistryKeyPath, writable: true);
            if (key == null)
            {
                _logger.Error($"Không thể mở Registry key với quyền ghi: {RunRegistryKeyPath}");
                return false;
            }

            if (enable)
            {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    _logger.Error("Không xác định được đường dẫn file thực thi của ứng dụng.");
                    return false;
                }

                if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    string candidateExe = Path.ChangeExtension(exePath, ".exe");
                    if (File.Exists(candidateExe))
                    {
                        exePath = candidateExe;
                    }
                }

                string command = $"\"{exePath}\"";
                key.SetValue(AppRegistryName, command);
                _logger.Info($"Đã đăng ký tự khởi động cùng Windows: {command}");
            }
            else
            {
                if (key.GetValue(AppRegistryName) != null)
                {
                    key.DeleteValue(AppRegistryName, throwOnMissingValue: false);
                    _logger.Info("Đã gỡ đăng ký tự khởi động cùng Windows.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Lỗi khi cập nhật cấu hình tự khởi động cùng Windows (enable={enable})", ex);
            return false;
        }
    }
}
