using System;
using System.IO;

namespace GoalOverlay.Services;

/// <summary>
/// Triển khai IAutostartService quản lý tự khởi động cùng Windows qua Registry:
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run với giá trị: "\"<exePath>\" --minimized"
/// Hỗ trợ cơ chế tự động sửa đường dẫn (Self-healing) khi người dùng di chuyển file exe
/// Hỗ trợ Fake Registry qua IRegistryService phục vụ Unit Test tự động.
/// </summary>
public class AutostartService : IAutostartService
{
    public const string RunRegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string AppRegistryName = "GoalOverlay";
    public const string MinimizedFlag = "--minimized";

    private readonly ILoggerService _logger;
    private readonly IRegistryService _registryService;
    private readonly Func<string?> _getExePathFunc;

    public AutostartService(
        ILoggerService logger,
        IRegistryService? registryService = null,
        Func<string?>? getExePathFunc = null)
    {
        _logger = logger;
        _registryService = registryService ?? new WindowsRegistryService();
        _getExePathFunc = getExePathFunc ?? GetExecutablePath;
    }

    public bool IsEnabled()
    {
        try
        {
            object? value = _registryService.GetCurrentUserValue(RunRegistryKeyPath, AppRegistryName);
            return value != null;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi kiểm tra trạng thái Autostart trong Registry", ex);
            return false;
        }
    }

    public bool Enable()
    {
        try
        {
            string? exePath = _getExePathFunc();
            if (string.IsNullOrWhiteSpace(exePath))
            {
                _logger.Error("Không xác định được đường dẫn file thực thi của ứng dụng.");
                return false;
            }

            string command = $"\"{exePath}\" {MinimizedFlag}";
            _registryService.SetCurrentUserValue(RunRegistryKeyPath, AppRegistryName, command);
            _logger.Info($"Đã bật tự khởi động cùng Windows: {command}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi kích hoạt tự khởi động cùng Windows", ex);
            return false;
        }
    }

    public bool Disable()
    {
        try
        {
            _registryService.DeleteCurrentUserValue(RunRegistryKeyPath, AppRegistryName);
            _logger.Info("Đã tắt tự khởi động cùng Windows.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi hủy tự khởi động cùng Windows", ex);
            return false;
        }
    }

    /// <summary>
    /// Tự động kiểm tra và cập nhật lại đường dẫn trong Registry nếu người dùng di chuyển file exe
    /// </summary>
    public void VerifyAndUpdatePath()
    {
        try
        {
            if (!IsEnabled())
            {
                return;
            }

            string? currentVal = _registryService.GetCurrentUserValue(RunRegistryKeyPath, AppRegistryName) as string;
            string? currentExe = _getExePathFunc();

            if (string.IsNullOrWhiteSpace(currentExe)) return;

            string expectedCommand = $"\"{currentExe}\" {MinimizedFlag}";

            if (!string.Equals(currentVal, expectedCommand, StringComparison.OrdinalIgnoreCase))
            {
                _registryService.SetCurrentUserValue(RunRegistryKeyPath, AppRegistryName, expectedCommand);
                _logger.Info($"[Self-healing] Phát hiện đường dẫn exe thay đổi. Đã tự động cập nhật Registry từ '{currentVal}' thành '{expectedCommand}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Lỗi khi kiểm tra và tự sửa đường dẫn autostart trong Registry", ex);
        }
    }

    private static string? GetExecutablePath()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return null;
        }

        if (exePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            string candidateExe = Path.ChangeExtension(exePath, ".exe");
            if (File.Exists(candidateExe))
            {
                return candidateExe;
            }
        }

        return exePath;
    }
}
