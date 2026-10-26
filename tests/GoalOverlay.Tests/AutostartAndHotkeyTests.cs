using System;
using System.IO;
using System.Threading.Tasks;
using GoalOverlay.Services;
using GoalOverlay.ViewModels;
using GoalOverlay.Tests.Mocks;
using Xunit;

namespace GoalOverlay.Tests;

public class MockAutostartService : IAutostartService
{
    public bool Enabled { get; set; }
    public bool VerifyCalled { get; set; }

    public bool IsEnabled() => Enabled;
    public bool Enable() { Enabled = true; return true; }
    public bool Disable() { Enabled = false; return true; }
    public void VerifyAndUpdatePath() { VerifyCalled = true; }
}

public class MockHotkeyService : IHotkeyService
{
    public bool IsRegistered { get; set; } = true;
    public string StatusMessage { get; set; } = "Đang kích hoạt: Ctrl + Alt + G";
    public bool Register(IntPtr hWnd, Action callback) => true;
    public bool Register(IntPtr hWnd, Action toggleOverlayCallback, Action? openManagementCallback) => true;
    public void Unregister(IntPtr hWnd) { IsRegistered = false; }
}

public class AutostartAndHotkeyTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestLogger _logger;
    private readonly TestClock _clock;
    private readonly JsonGoalRepository _repository;
    private readonly GoalService _goalService;
    private readonly MockAutostartService _autostartService;
    private readonly MockHotkeyService _hotkeyService;

    public AutostartAndHotkeyTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "GoalOverlay_Phase5Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        _logger = new TestLogger();
        _clock = new TestClock(new DateTime(2026, 10, 26, 10, 0, 0));
        _repository = new JsonGoalRepository(_logger, _testDirectory);
        _goalService = new GoalService(_repository, _clock, _logger);
        _autostartService = new MockAutostartService();
        _hotkeyService = new MockHotkeyService();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task MainViewModel_Initializes_Autostart_And_Hotkey()
    {
        _autostartService.Enabled = true;
        _hotkeyService.StatusMessage = "Đang kích hoạt: Ctrl + Alt + G";

        var vm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostartService, _hotkeyService);
        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.True(vm.AutostartEnabled);
        Assert.Equal("Đang kích hoạt: Ctrl + Alt + G", vm.HotkeyStatusMessage);
    }

    [Fact]
    public async Task MainViewModel_Toggling_Autostart_Updates_Service()
    {
        _autostartService.Enabled = false;

        var vm = new MainViewModel(_goalService, _repository, _clock, _logger, _autostartService, _hotkeyService);
        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.False(vm.AutostartEnabled);
        Assert.False(_autostartService.Enabled);

        // Bật autostart
        vm.AutostartEnabled = true;
        Assert.True(_autostartService.Enabled);

        // Tắt autostart
        vm.AutostartEnabled = false;
        Assert.False(_autostartService.Enabled);
    }

    [Fact]
    public void HotkeyService_Invalid_Hwnd_Returns_False_Gracefully()
    {
        var hotkeyService = new HotkeyService(_logger);
        bool result = hotkeyService.Register(IntPtr.Zero, () => { });

        Assert.False(result);
        Assert.False(hotkeyService.IsRegistered);
        Assert.Contains("không hợp lệ", hotkeyService.StatusMessage);
    }

    [Fact]
    public void HotkeyService_Dual_Register_Invalid_Hwnd_Returns_False()
    {
        var hotkeyService = new HotkeyService(_logger);
        bool result = hotkeyService.Register(IntPtr.Zero, () => { }, () => { });

        Assert.False(result);
        Assert.False(hotkeyService.IsRegistered);
        Assert.Contains("không hợp lệ", hotkeyService.StatusMessage);
    }

    [Fact]
    public void AutostartService_VerifyAndUpdatePath_Does_Not_Throw_When_Disabled()
    {
        var autostartService = new AutostartService(_logger);
        // Khi chưa bật, VerifyAndUpdatePath phải chạy an toàn và không ném ngoại lệ
        var exception = Record.Exception(() => autostartService.VerifyAndUpdatePath());
        Assert.Null(exception);
    }
}
