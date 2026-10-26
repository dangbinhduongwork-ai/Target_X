using System;
using GoalOverlay.Services;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

public class AutostartServiceTests
{
    private readonly TestLogger _logger;
    private readonly FakeRegistryService _fakeRegistry;

    public AutostartServiceTests()
    {
        _logger = new TestLogger();
        _fakeRegistry = new FakeRegistryService();
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenRegistryValueDoesNotExist()
    {
        var service = new AutostartService(_logger, _fakeRegistry, () => @"C:\Apps\GoalOverlay\GoalOverlay.exe");
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Enable_WritesPathWithMinimizedFlag_AndSetsEnabled()
    {
        string exePath = @"C:\Apps\GoalOverlay\GoalOverlay.exe";
        var service = new AutostartService(_logger, _fakeRegistry, () => exePath);

        bool success = service.Enable();

        Assert.True(success);
        Assert.True(service.IsEnabled());

        string expectedCommand = $"\"{exePath}\" --minimized";
        var storedVal = _fakeRegistry.GetCurrentUserValue(AutostartService.RunRegistryKeyPath, AutostartService.AppRegistryName);
        Assert.Equal(expectedCommand, storedVal);
    }

    [Fact]
    public void Disable_RemovesRegistryValue_AndSetsDisabled()
    {
        string exePath = @"C:\Apps\GoalOverlay\GoalOverlay.exe";
        var service = new AutostartService(_logger, _fakeRegistry, () => exePath);

        service.Enable();
        Assert.True(service.IsEnabled());

        bool disableSuccess = service.Disable();
        Assert.True(disableSuccess);
        Assert.False(service.IsEnabled());
        Assert.Null(_fakeRegistry.GetCurrentUserValue(AutostartService.RunRegistryKeyPath, AutostartService.AppRegistryName));
    }

    [Fact]
    public void Enable_ReturnsFalse_WhenExePathIsNull()
    {
        var service = new AutostartService(_logger, _fakeRegistry, () => null);

        bool result = service.Enable();

        Assert.False(result);
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void VerifyAndUpdatePath_SelfHeals_WhenExePathMoved()
    {
        string oldExePath = @"C:\OldFolder\GoalOverlay.exe";
        string newExePath = @"D:\NewFolder\GoalOverlay.exe";

        // Ban đầu app ở thư mục cũ
        var serviceOld = new AutostartService(_logger, _fakeRegistry, () => oldExePath);
        serviceOld.Enable();
        Assert.Equal($"\"{oldExePath}\" --minimized", _fakeRegistry.GetCurrentUserValue(AutostartService.RunRegistryKeyPath, AutostartService.AppRegistryName));

        // Người dùng di chuyển file exe sang thư mục mới
        var serviceNew = new AutostartService(_logger, _fakeRegistry, () => newExePath);
        serviceNew.VerifyAndUpdatePath();

        // Registry đã được tự động cập nhật đường dẫn mới
        Assert.Equal($"\"{newExePath}\" --minimized", _fakeRegistry.GetCurrentUserValue(AutostartService.RunRegistryKeyPath, AutostartService.AppRegistryName));
    }

    [Fact]
    public void VerifyAndUpdatePath_DoesNothing_WhenAutostartIsDisabled()
    {
        string exePath = @"C:\Apps\GoalOverlay\GoalOverlay.exe";
        var service = new AutostartService(_logger, _fakeRegistry, () => exePath);

        service.VerifyAndUpdatePath();

        Assert.False(service.IsEnabled());
        Assert.Null(_fakeRegistry.GetCurrentUserValue(AutostartService.RunRegistryKeyPath, AutostartService.AppRegistryName));
    }

    [Fact]
    public void ExceptionSafety_HandlesRegistryErrorsGracefully_WithoutCrash()
    {
        _fakeRegistry.ThrowOnRead = true;
        _fakeRegistry.ThrowOnWrite = true;

        var service = new AutostartService(_logger, _fakeRegistry, () => @"C:\Apps\GoalOverlay.exe");

        // Các hàm phải không ném ngoại lệ ra ngoài (không crash)
        Assert.False(service.IsEnabled());
        Assert.False(service.Enable());
        Assert.False(service.Disable());

        var ex = Record.Exception(() => service.VerifyAndUpdatePath());
        Assert.Null(ex);
    }
}
